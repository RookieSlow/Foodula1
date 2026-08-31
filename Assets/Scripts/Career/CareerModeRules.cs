using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>High-level lifecycle for one eight-race career season.</summary>
public enum CareerPhase
{
    NotStarted,
    Racing,
    SummerBreak,
    Completed
}

/// <summary>One competitor's classified result for a career race.</summary>
public sealed class CareerCompetitorResult
{
    public TeamId TeamId { get; }
    public int FinishPosition { get; }
    public bool DidNotFinish { get; }

    public CareerCompetitorResult(TeamId teamId, int finishPosition, bool didNotFinish = false)
    {
        TeamId = teamId;
        FinishPosition = finishPosition;
        DidNotFinish = didNotFinish;
    }
}

/// <summary>Immutable classified result for one race in a career season.</summary>
public sealed class CareerRaceResult
{
    private readonly ReadOnlyCollection<CareerCompetitorResult> standings;

    public string ResultId { get; }
    public string TrackId { get; }
    public IReadOnlyList<CareerCompetitorResult> Standings => standings;

    public CareerRaceResult(
        string resultId,
        string trackId,
        IReadOnlyList<CareerCompetitorResult> standings)
    {
        ResultId = resultId ?? string.Empty;
        TrackId = trackId ?? string.Empty;

        var copiedStandings = new List<CareerCompetitorResult>();
        if (standings != null)
        {
            for (int i = 0; i < standings.Count; i++)
            {
                CareerCompetitorResult entry = standings[i];
                if (entry != null)
                {
                    copiedStandings.Add(new CareerCompetitorResult(
                        entry.TeamId,
                        entry.FinishPosition,
                        entry.DidNotFinish));
                }
            }
        }

        this.standings = copiedStandings.AsReadOnly();
    }
}

/// <summary>Computed championship table entry.</summary>
public sealed class CareerStanding
{
    public TeamId TeamId { get; internal set; }
    public int Rank { get; internal set; }
    public int Points { get; internal set; }
    public int Wins { get; internal set; }
    public int Podiums { get; internal set; }
    public int BestFinish { get; internal set; } = int.MaxValue;
    public int MostRecentFinish { get; internal set; } = int.MaxValue;
}

/// <summary>
/// Immutable career-owned technology configuration. It deliberately stores
/// only configuration data, never per-race effect flags from TechTreeState.
/// </summary>
public sealed class CareerTechSnapshot
{
    private readonly ReadOnlyCollection<string> unlockedNodeIds;
    private readonly ReadOnlyCollection<string> activeNodeIds;

    public TeamId TeamId { get; }
    public int RpBalance { get; }
    public IReadOnlyList<string> UnlockedNodeIds => unlockedNodeIds;
    public IReadOnlyList<string> ActiveNodeIds => activeNodeIds;
    public TeamId? SunNeverSetsTarget { get; }

    private CareerTechSnapshot(
        TeamId teamId,
        int rpBalance,
        List<string> unlocked,
        List<string> active,
        TeamId? sunNeverSetsTarget)
    {
        TeamId = teamId;
        RpBalance = rpBalance;
        unlockedNodeIds = unlocked.AsReadOnly();
        activeNodeIds = active.AsReadOnly();
        SunNeverSetsTarget = sunNeverSetsTarget;
    }

    public static CareerTechSnapshot CreateEmpty(TeamId teamId)
    {
        if (!Enum.IsDefined(typeof(TeamId), teamId))
            throw new ArgumentOutOfRangeException(nameof(teamId));

        return new CareerTechSnapshot(
            teamId,
            0,
            new List<string>(),
            new List<string>(),
            null);
    }

    public static bool TryCreate(
        TeamId teamId,
        int rpBalance,
        IEnumerable<string> unlockedNodeIds,
        IEnumerable<string> activeNodeIds,
        TeamId? sunNeverSetsTarget,
        out CareerTechSnapshot snapshot)
    {
        snapshot = null;
        if (!Enum.IsDefined(typeof(TeamId), teamId) || rpBalance < 0 ||
            (sunNeverSetsTarget.HasValue &&
             (!Enum.IsDefined(typeof(TeamId), sunNeverSetsTarget.Value) || teamId != TeamId.UK)))
        {
            return false;
        }

        if (!TryNormalizeNodeIds(unlockedNodeIds, out List<string> unlocked) ||
            !TryNormalizeNodeIds(activeNodeIds, out List<string> active))
        {
            return false;
        }

        var unlockedSet = new HashSet<string>(unlocked, StringComparer.Ordinal);
        for (int i = 0; i < active.Count; i++)
        {
            if (!unlockedSet.Contains(active[i]))
                return false;
        }

        snapshot = new CareerTechSnapshot(
            teamId,
            rpBalance,
            unlocked,
            active,
            sunNeverSetsTarget);
        return true;
    }

    public CareerTechSnapshot Clone()
    {
        return new CareerTechSnapshot(
            TeamId,
            RpBalance,
            new List<string>(unlockedNodeIds),
            new List<string>(activeNodeIds),
            SunNeverSetsTarget);
    }

    private static bool TryNormalizeNodeIds(
        IEnumerable<string> source,
        out List<string> normalized)
    {
        normalized = new List<string>();
        if (source == null)
            return true;

        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string nodeId in source)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || !unique.Add(nodeId))
                return false;
            normalized.Add(nodeId);
        }

        normalized.Sort(StringComparer.Ordinal);
        return true;
    }
}

/// <summary>
/// Runtime state for one career season. Mutation is restricted to
/// <see cref="CareerModeRules"/> so team locking and phase gates cannot be
/// bypassed by UI code.
/// </summary>
public sealed class CareerSeasonState
{
    private readonly List<TeamId> competitors = new List<TeamId>();
    private readonly List<CareerRaceResult> raceResults = new List<CareerRaceResult>();

    public int Version { get; private set; } = CareerModeRules.CurrentVersion;
    public CareerPhase Phase { get; private set; } = CareerPhase.NotStarted;
    public bool HasLockedTeam { get; private set; }
    public TeamId LockedTeam { get; private set; }
    public int NextTrackIndex { get; private set; }
    public bool SummerBreakUsed { get; private set; }
    public CareerTechSnapshot InitialTechSnapshot { get; private set; }
    public CareerTechSnapshot ActiveTechSnapshot { get; private set; }
    public CareerTechSnapshot SummerBreakTechSnapshot { get; private set; }
    public IReadOnlyList<TeamId> Competitors => competitors;
    public IReadOnlyList<CareerRaceResult> RaceResults => raceResults;

    internal void Initialize(
        TeamId lockedTeam,
        IReadOnlyList<TeamId> initialCompetitors,
        CareerTechSnapshot initialTechSnapshot)
    {
        competitors.Clear();
        for (int i = 0; i < initialCompetitors.Count; i++)
            competitors.Add(initialCompetitors[i]);

        raceResults.Clear();
        Version = CareerModeRules.CurrentVersion;
        HasLockedTeam = true;
        LockedTeam = lockedTeam;
        NextTrackIndex = 0;
        SummerBreakUsed = false;
        InitialTechSnapshot = initialTechSnapshot.Clone();
        ActiveTechSnapshot = initialTechSnapshot.Clone();
        SummerBreakTechSnapshot = null;
        Phase = CareerPhase.Racing;
    }

    internal void SetPreselectedTeam(TeamId teamId)
    {
        LockedTeam = teamId;
    }

    internal void AddRaceResult(CareerRaceResult result, int raceCount, int summerBreakAfterRaceCount)
    {
        raceResults.Add(result);
        NextTrackIndex++;
        if (NextTrackIndex >= raceCount)
            Phase = CareerPhase.Completed;
        else if (NextTrackIndex == summerBreakAfterRaceCount && !SummerBreakUsed)
            Phase = CareerPhase.SummerBreak;
    }

    internal void ConsumeSummerBreak(CareerTechSnapshot techSnapshot)
    {
        SummerBreakUsed = true;
        SummerBreakTechSnapshot = techSnapshot.Clone();
        ActiveTechSnapshot = techSnapshot.Clone();
        Phase = CareerPhase.Racing;
    }
}

/// <summary>
/// Pure career rules: calendar, team lock, championship points, stable ranking,
/// race advancement and the four-race summer-break technology gate.
/// </summary>
public static class CareerModeRules
{
    public const int CurrentVersion = 1;
    public const int RaceCount = 8;
    public const int SummerBreakAfterRaceCount = 4;

    private static readonly int[] PointsByPosition = { 10, 6, 4, 2 };
    private static readonly ReadOnlyCollection<string> Schedule = BuildSchedule();

    /// <summary>
    /// The career calendar reuses the explicit main-menu track catalog order,
    /// keeping one authoritative list of the eight official tracks.
    /// </summary>
    public static IReadOnlyList<string> TrackSchedule => Schedule;

    public static int GetPointsForFinish(int finishPosition, bool didNotFinish = false)
    {
        if (didNotFinish || finishPosition < 1 || finishPosition > PointsByPosition.Length)
            return 0;

        return PointsByPosition[finishPosition - 1];
    }

    /// <summary>Starts a season and permanently locks its player team.</summary>
    public static bool TryStartSeason(
        CareerSeasonState state,
        TeamId playerTeam,
        IReadOnlyList<TeamId> competitors,
        CareerTechSnapshot initialTechSnapshot = null)
    {
        if (!Enum.IsDefined(typeof(TeamId), playerTeam))
            return false;

        CareerTechSnapshot snapshot = initialTechSnapshot ?? CareerTechSnapshot.CreateEmpty(playerTeam);
        if (state == null || state.Phase != CareerPhase.NotStarted ||
            !IsValidCompetitorField(playerTeam, competitors) ||
            snapshot.TeamId != playerTeam)
        {
            return false;
        }

        state.Initialize(playerTeam, competitors, snapshot);
        return true;
    }

    /// <summary>
    /// Team changes are only legal before a season starts. A started season
    /// always returns false, even if the caller bypasses the menu.
    /// </summary>
    public static bool TrySetPlayerTeam(CareerSeasonState state, TeamId playerTeam)
    {
        if (state == null || state.Phase != CareerPhase.NotStarted || state.HasLockedTeam ||
            !Enum.IsDefined(typeof(TeamId), playerTeam))
        {
            return false;
        }

        state.SetPreselectedTeam(playerTeam);
        return true;
    }

    public static string GetNextTrackId(CareerSeasonState state)
    {
        if (state == null || state.NextTrackIndex < 0 || state.NextTrackIndex >= Schedule.Count)
            return string.Empty;

        return Schedule[state.NextTrackIndex];
    }

    public static bool CanStartNextRace(CareerSeasonState state)
    {
        return state != null && state.HasLockedTeam && state.Phase == CareerPhase.Racing &&
               state.NextTrackIndex >= 0 && state.NextTrackIndex < Schedule.Count;
    }

    public static bool CanAdjustTechTree(CareerSeasonState state)
    {
        return state != null && state.HasLockedTeam &&
               state.Phase == CareerPhase.SummerBreak &&
               state.NextTrackIndex == SummerBreakAfterRaceCount &&
               !state.SummerBreakUsed;
    }

    /// <summary>Consumes the sole mid-season technology adjustment window.</summary>
    public static bool ConfirmSummerBreakTechTree(CareerSeasonState state)
    {
        return state != null &&
               ConfirmSummerBreakTechTree(state, state.ActiveTechSnapshot);
    }

    public static bool ConfirmSummerBreakTechTree(
        CareerSeasonState state,
        CareerTechSnapshot techSnapshot)
    {
        if (!CanAdjustTechTree(state) || techSnapshot == null ||
            techSnapshot.TeamId != state.LockedTeam)
            return false;

        state.ConsumeSummerBreak(techSnapshot);
        return true;
    }

    /// <summary>
    /// Records the expected next race exactly once and advances the season.
    /// Result IDs make repeated result callbacks idempotent.
    /// </summary>
    public static bool TryRecordRace(CareerSeasonState state, CareerRaceResult result)
    {
        if (!CanStartNextRace(state) || !IsValidRaceResult(state, result))
            return false;

        for (int i = 0; i < state.RaceResults.Count; i++)
        {
            if (string.Equals(
                    state.RaceResults[i].ResultId,
                    result.ResultId,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        state.AddRaceResult(new CareerRaceResult(
            result.ResultId,
            result.TrackId,
            result.Standings),
            Schedule.Count,
            SummerBreakAfterRaceCount);

        return true;
    }

    /// <summary>
    /// Builds the current table. Ties use wins, podiums, best finish, most recent
    /// finish, then original competitor order for deterministic presentation.
    /// </summary>
    public static List<CareerStanding> GetStandings(CareerSeasonState state)
    {
        var result = new List<CareerStanding>();
        if (state == null)
            return result;

        var originalOrder = new Dictionary<TeamId, int>();
        for (int i = 0; i < state.Competitors.Count; i++)
        {
            TeamId team = state.Competitors[i];
            originalOrder[team] = i;
            result.Add(new CareerStanding { TeamId = team });
        }

        var byTeam = new Dictionary<TeamId, CareerStanding>();
        for (int i = 0; i < result.Count; i++)
            byTeam[result[i].TeamId] = result[i];

        for (int raceIndex = 0; raceIndex < state.RaceResults.Count; raceIndex++)
        {
            IReadOnlyList<CareerCompetitorResult> race = state.RaceResults[raceIndex].Standings;
            for (int entryIndex = 0; entryIndex < race.Count; entryIndex++)
            {
                CareerCompetitorResult entry = race[entryIndex];
                CareerStanding standing = byTeam[entry.TeamId];
                standing.Points += GetPointsForFinish(entry.FinishPosition, entry.DidNotFinish);
                if (!entry.DidNotFinish)
                {
                    if (entry.FinishPosition == 1) standing.Wins++;
                    if (entry.FinishPosition <= 3) standing.Podiums++;
                    standing.BestFinish = Math.Min(standing.BestFinish, entry.FinishPosition);
                    standing.MostRecentFinish = entry.FinishPosition;
                }
            }
        }

        result.Sort((left, right) =>
        {
            int comparison = right.Points.CompareTo(left.Points);
            if (comparison != 0) return comparison;
            comparison = right.Wins.CompareTo(left.Wins);
            if (comparison != 0) return comparison;
            comparison = right.Podiums.CompareTo(left.Podiums);
            if (comparison != 0) return comparison;
            comparison = left.BestFinish.CompareTo(right.BestFinish);
            if (comparison != 0) return comparison;
            comparison = left.MostRecentFinish.CompareTo(right.MostRecentFinish);
            if (comparison != 0) return comparison;
            return originalOrder[left.TeamId].CompareTo(originalOrder[right.TeamId]);
        });

        for (int i = 0; i < result.Count; i++)
            result[i].Rank = i + 1;

        return result;
    }

    private static ReadOnlyCollection<string> BuildSchedule()
    {
        IReadOnlyList<TrackSelectionOption> tracks = TrackSelectionState.AvailableTracks;
        if (tracks.Count != RaceCount)
            throw new InvalidOperationException($"Career requires exactly {RaceCount} official tracks.");

        var ids = new List<string>(tracks.Count);
        var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < tracks.Count; i++)
        {
            string trackId = tracks[i].TrackId;
            if (string.IsNullOrEmpty(trackId) || !uniqueIds.Add(trackId))
                throw new InvalidOperationException("Career track IDs must be non-empty and unique.");
            ids.Add(trackId);
        }

        return ids.AsReadOnly();
    }

    private static bool IsValidCompetitorField(
        TeamId playerTeam,
        IReadOnlyList<TeamId> competitors)
    {
        if (competitors == null || competitors.Count < 2 ||
            competitors.Count > PointsByPosition.Length)
        {
            return false;
        }

        bool containsPlayer = false;
        var uniqueTeams = new HashSet<TeamId>();
        for (int i = 0; i < competitors.Count; i++)
        {
            TeamId team = competitors[i];
            if (!Enum.IsDefined(typeof(TeamId), team) || !uniqueTeams.Add(team))
                return false;
            containsPlayer |= team == playerTeam;
        }

        return containsPlayer;
    }

    private static bool IsValidRaceResult(CareerSeasonState state, CareerRaceResult result)
    {
        if (result == null || string.IsNullOrEmpty(result.ResultId) ||
            !string.Equals(result.TrackId, GetNextTrackId(state), StringComparison.Ordinal) ||
            result.Standings.Count != state.Competitors.Count)
        {
            return false;
        }

        var expectedTeams = new HashSet<TeamId>(state.Competitors);
        var seenTeams = new HashSet<TeamId>();
        var seenPositions = new HashSet<int>();
        for (int i = 0; i < result.Standings.Count; i++)
        {
            CareerCompetitorResult entry = result.Standings[i];
            if (entry == null || !expectedTeams.Contains(entry.TeamId) || !seenTeams.Add(entry.TeamId))
                return false;

            if (entry.DidNotFinish)
            {
                if (entry.FinishPosition != 0)
                    return false;
            }
            else if (entry.FinishPosition < 1 ||
                     entry.FinishPosition > state.Competitors.Count ||
                     !seenPositions.Add(entry.FinishPosition))
            {
                return false;
            }
        }

        for (int position = 1; position <= seenPositions.Count; position++)
        {
            if (!seenPositions.Contains(position))
                return false;
        }

        return seenTeams.Count == expectedTeams.Count;
    }
}
