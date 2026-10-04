using System;
using System.Collections.Generic;
using System.Globalization;
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
        bool sawTermination = false;
        bool sawHeader = false;

        if (string.IsNullOrWhiteSpace(contents))
        {
            result.AddError("Log is empty.");
            return result;
        }

        string[] lines = contents.Replace("\r", string.Empty).Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line == "# Foodula1 Race Test Log")
            {
                if (sawHeader || result.TurnCount > 0 || result.SawRaceEnd)
                    result.AddError("Multiple race logs must be analyzed separately.");
                sawHeader = true;
            }
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            // Writer timestamps precede the event with a tab. Metadata and
            // result prose containing marker names must not become events.
            int timestampSeparator = line.IndexOf('\t');
            if (timestampSeparator >= 0)
                line = line.Substring(timestampSeparator + 1).TrimStart();
            if (!line.StartsWith("[", StringComparison.Ordinal))
                continue;
            if (result.SawRaceEnd)
            {
                result.AddError("Event appeared after [RACE_END]; analyze restarted races separately.");
                continue;
            }
            if (HasMarker(line, "[TUTORIAL_SETUP]") ||
                HasMarker(line, "[CAREER_SETUP]") ||
                HasMarker(line, "[FREE_RACE_SETUP]"))
            {
                RaceLogMode mode = HasMarker(line, "[TUTORIAL_SETUP]") ? RaceLogMode.Tutorial :
                    HasMarker(line, "[CAREER_SETUP]") ? RaceLogMode.Career : RaceLogMode.FreeRace;
                if (result.TurnCount > 0 || sawTermination)
                    result.AddError("Race setup appeared after the first turn or termination.");
                else if (result.Mode != RaceLogMode.Unknown && result.Mode != mode)
                    result.AddError("Conflicting race setup modes.");
                else
                    result.Mode = mode;
                continue;
            }
            if (HasMarker(line, "[RACE_TERMINATION]"))
            {
                const string prefix = "[RACE_TERMINATION] outcome=";
                string value = line.StartsWith(prefix, StringComparison.Ordinal)
                    ? line.Substring(prefix.Length) : string.Empty;
                if (sawTermination)
                    result.AddError("Duplicate [RACE_TERMINATION].");
                sawTermination = true;
                if (!Enum.TryParse(value, out RaceLogTermination termination) ||
                    Enum.GetName(typeof(RaceLogTermination), termination) != value)
                    result.AddError("Invalid [RACE_TERMINATION] outcome.");
                else
                    result.Termination = termination;
                continue;
            }
            if (sawTermination && !HasMarker(line, "[RACE_END]"))
            {
                result.AddError("Event appeared between [RACE_TERMINATION] and [RACE_END].");
                continue;
            }

            if (HasMarker(line, "[TURN_START]"))
            {
                FinalizeTurn(result, currentTurn);
                currentTurn = new TurnState(++result.TurnCount);
                continue;
            }

            if (HasMarker(line, "[CARD_PHASE] end"))
            {
                SetStage(result, currentTurn, TurnStage.CardEnd,
                    "[CARD_PHASE] end", "turn start");
                continue;
            }

            if (HasMarker(line, "[MOVE_PHASE] begin"))
            {
                SetStage(result, currentTurn, TurnStage.MoveBegin,
                    "[MOVE_PHASE] begin", "[CARD_PHASE] end");
                continue;
            }

            if (HasMarker(line, "[MOVE_PHASE] end"))
            {
                SetStage(result, currentTurn, TurnStage.MoveEnd,
                    "[MOVE_PHASE] end", "[MOVE_PHASE] begin");
                continue;
            }

            if (HasMarker(line, "[SLIPSTREAM_PHASE] begin"))
            {
                SetStage(result, currentTurn, TurnStage.SlipstreamBegin,
                    "[SLIPSTREAM_PHASE] begin", "[MOVE_PHASE] end");
                result.SlipstreamPhaseCount++;
                continue;
            }

            if (HasMarker(line, "[SLIPSTREAM_PHASE] end"))
            {
                SetStage(result, currentTurn, TurnStage.SlipstreamEnd,
                    "[SLIPSTREAM_PHASE] end", "[SLIPSTREAM_PHASE] begin");
                continue;
            }

            if (HasMarker(line, "[DISCARD]"))
            {
                AnalyzeDiscard(result, currentTurn, line);
                continue;
            }

            if (HasMarker(line, "[RACE_END]"))
                result.SawRaceEnd = true;
        }

        FinalizeTurn(result, currentTurn);
        result.IsComplete = result.SawRaceEnd && result.IncompleteTurnCount == 0 && result.TurnCount > 0;
        return result;
    }

    private static bool HasMarker(string line, string marker)
    {
        return line.StartsWith(marker, StringComparison.Ordinal) &&
            (line.Length == marker.Length || char.IsWhiteSpace(line[marker.Length]));
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

        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int selected) ||
            !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int discarded))
        {
            result.AddError(
                $"Turn {turn.Number}: [DISCARD] contains invalid selected/discarded counts.");
            return;
        }

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
    /// <summary>Mode observed in setup events; old logs without one remain Unknown.</summary>
    public RaceLogMode Mode { get; internal set; }
    /// <summary>Legacy logs remain Unknown; end prose is never guessed.</summary>
    public RaceLogTermination Termination { get; internal set; }
    /// <summary>Finished race trace only, not visual/storage/Play Mode sign-off.</summary>
    public bool HasCompletedRaceEvidence => IsValid && IsComplete &&
        Termination == RaceLogTermination.Completed;
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

/// <summary>Explicit race setup mode, independent of the termination outcome.</summary>
public enum RaceLogMode
{
    Unknown = 0,
    Tutorial,
    FreeRace,
    Career
}

/// <summary>Explicit log closure cause, independent of player-facing result text.</summary>
public enum RaceLogTermination
{
    Unknown = 0,
    Completed,
    Restarted,
    Exited,
    SceneDestroyed,
    TutorialIncomplete
}
