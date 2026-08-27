using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Pure analyzer for the timestamped race log written by
/// <see cref="RaceTestLogWriter"/>. It turns manual-play evidence into
/// repeatable assertions without changing gameplay state or file output.
/// </summary>
public static class RaceLogAnalyzer
{
    private static readonly Regex DiscardCounts = new Regex(
        @"selected=(\d+)\s+discarded=(\d+)",
        RegexOptions.Compiled);

    /// <summary>Analyzes phase order and event-level invariants in one log.</summary>
    public static RaceLogAnalysisResult Analyze(string contents)
    {
        var result = new RaceLogAnalysisResult();
        TurnState currentTurn = null;

        if (string.IsNullOrWhiteSpace(contents))
        {
            result.AddError("Log is empty.");
            return result;
        }

        string[] lines = contents.Replace("\r", string.Empty).Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            if (line.Contains("[TURN_START]"))
            {
                FinalizeTurn(result, currentTurn);
                currentTurn = new TurnState(++result.TurnCount);
                continue;
            }

            if (line.Contains("[CARD_PHASE] end"))
            {
                SetStage(result, currentTurn, TurnStage.CardEnd,
                    "[CARD_PHASE] end", "turn start");
                continue;
            }

            if (line.Contains("[MOVE_PHASE] begin"))
            {
                SetStage(result, currentTurn, TurnStage.MoveBegin,
                    "[MOVE_PHASE] begin", "[CARD_PHASE] end");
                continue;
            }

            if (line.Contains("[MOVE_PHASE] end"))
            {
                SetStage(result, currentTurn, TurnStage.MoveEnd,
                    "[MOVE_PHASE] end", "[MOVE_PHASE] begin");
                continue;
            }

            if (line.Contains("[SLIPSTREAM_PHASE] begin"))
            {
                SetStage(result, currentTurn, TurnStage.SlipstreamBegin,
                    "[SLIPSTREAM_PHASE] begin", "[MOVE_PHASE] end");
                result.SlipstreamPhaseCount++;
                continue;
            }

            if (line.Contains("[SLIPSTREAM_PHASE] end"))
            {
                SetStage(result, currentTurn, TurnStage.SlipstreamEnd,
                    "[SLIPSTREAM_PHASE] end", "[SLIPSTREAM_PHASE] begin");
                continue;
            }

            if (line.Contains("[DISCARD]"))
            {
                AnalyzeDiscard(result, currentTurn, line);
                continue;
            }

            if (line.Contains("[RACE_END]"))
                result.SawRaceEnd = true;
        }

        FinalizeTurn(result, currentTurn);
        result.IsComplete = result.SawRaceEnd && result.IncompleteTurnCount == 0 && result.TurnCount > 0;
        return result;
    }

    private static void SetStage(
        RaceLogAnalysisResult result,
        TurnState turn,
        TurnStage expectedStage,
        string marker,
        string predecessor)
    {
        if (turn == null)
        {
            result.AddError($"{marker} appeared before [TURN_START].");
            return;
        }

        if (turn.Stage != expectedStage - 1)
        {
            result.AddError(
                $"Turn {turn.Number}: {marker} must follow {predecessor}.");
            return;
        }

        turn.Stage = expectedStage;
    }

    private static void AnalyzeDiscard(
        RaceLogAnalysisResult result,
        TurnState turn,
        string line)
    {
        if (turn == null)
        {
            result.AddError("[DISCARD] appeared before [TURN_START].");
            return;
        }

        if (turn.Stage < TurnStage.MoveEnd)
        {
            result.AddError(
                $"Turn {turn.Number}: [DISCARD] must follow [MOVE_PHASE] end.");
        }

        Match match = DiscardCounts.Match(line);
        if (!match.Success)
        {
            result.AddError(
                $"Turn {turn.Number}: [DISCARD] is missing selected/discarded counts.");
            return;
        }

        int selected = int.Parse(match.Groups[1].Value);
        int discarded = int.Parse(match.Groups[2].Value);
        if (discarded > selected)
        {
            result.AddError(
                $"Turn {turn.Number}: discarded count {discarded} exceeds selected count {selected}.");
        }

        result.DiscardEventCount++;
    }

    private static void FinalizeTurn(RaceLogAnalysisResult result, TurnState turn)
    {
        if (turn == null)
            return;

        bool baseMovementComplete = turn.Stage >= TurnStage.MoveEnd;
        bool slipstreamComplete = turn.Stage != TurnStage.SlipstreamBegin;
        if (baseMovementComplete && slipstreamComplete)
            result.CompletedTurnCount++;
        else
            result.IncompleteTurnCount++;
    }

    private sealed class TurnState
    {
        public TurnState(int number)
        {
            Number = number;
        }

        public int Number { get; }
        public TurnStage Stage { get; set; }
    }

    private enum TurnStage
    {
        TurnStart = 0,
        CardEnd = 1,
        MoveBegin = 2,
        MoveEnd = 3,
        SlipstreamBegin = 4,
        SlipstreamEnd = 5
    }
}

/// <summary>Immutable-style result object returned by <see cref="RaceLogAnalyzer"/>.</summary>
public sealed class RaceLogAnalysisResult
{
    private readonly List<string> errors = new List<string>();

    public bool IsValid => errors.Count == 0;
    public bool IsComplete { get; internal set; }
    public bool SawRaceEnd { get; internal set; }
    public int TurnCount { get; internal set; }
    public int CompletedTurnCount { get; internal set; }
    public int IncompleteTurnCount { get; internal set; }
    public int DiscardEventCount { get; internal set; }
    public int SlipstreamPhaseCount { get; internal set; }
    public IReadOnlyList<string> Errors => errors;

    internal void AddError(string message)
    {
        errors.Add(message);
    }
}
