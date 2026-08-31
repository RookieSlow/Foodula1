using System;
using System.Collections.Generic;

/// <summary>
/// Immutable, session-only configuration for one career race. It is copied
/// from a validated season before the scene transition, so Race never reads
/// Quick Race selections for its track, team or technology state.
/// </summary>
public sealed class CareerRaceLaunchRequest
{
    private readonly TeamId[] competitors;

    public string ResultId { get; }
    public int RaceIndex { get; }
    public string TrackId { get; }
    public TeamId PlayerTeam { get; }
    public IReadOnlyList<TeamId> Competitors => competitors;
    public CareerTechSnapshot TechSnapshot { get; }

    private CareerRaceLaunchRequest(
        string resultId,
        int raceIndex,
        string trackId,
        TeamId playerTeam,
        TeamId[] competitors,
        CareerTechSnapshot techSnapshot)
    {
        ResultId = resultId;
        RaceIndex = raceIndex;
        TrackId = trackId;
        PlayerTeam = playerTeam;
        this.competitors = competitors;
        TechSnapshot = techSnapshot.Clone();
    }

    public static bool TryCreate(
        CareerSeasonState state,
        string resultId,
        out CareerRaceLaunchRequest request)
    {
        request = null;
        if (!CareerModeRules.CanStartNextRace(state) || string.IsNullOrWhiteSpace(resultId) ||
            state.ActiveTechSnapshot == null || state.ActiveTechSnapshot.TeamId != state.LockedTeam)
        {
            return false;
        }

        string trackId = CareerModeRules.GetNextTrackId(state);
        if (string.IsNullOrEmpty(trackId))
            return false;

        var copiedCompetitors = new TeamId[state.Competitors.Count];
        bool containsPlayer = false;
        var unique = new HashSet<TeamId>();
        for (int i = 0; i < copiedCompetitors.Length; i++)
        {
            TeamId team = state.Competitors[i];
            if (!Enum.IsDefined(typeof(TeamId), team) || !unique.Add(team))
                return false;
            copiedCompetitors[i] = team;
            containsPlayer |= team == state.LockedTeam;
        }
        if (!containsPlayer)
            return false;

        request = new CareerRaceLaunchRequest(
            resultId.Trim(),
            state.NextTrackIndex,
            trackId,
            state.LockedTeam,
            copiedCompetitors,
            state.ActiveTechSnapshot);
        return true;
    }

    public bool Matches(CareerSeasonState state)
    {
        if (!CareerModeRules.CanStartNextRace(state) || state.NextTrackIndex != RaceIndex ||
            state.LockedTeam != PlayerTeam ||
            !string.Equals(CareerModeRules.GetNextTrackId(state), TrackId, StringComparison.Ordinal) ||
            state.Competitors.Count != competitors.Length ||
            !TechMatches(state.ActiveTechSnapshot, TechSnapshot))
        {
            return false;
        }

        for (int i = 0; i < competitors.Length; i++)
        {
            if (state.Competitors[i] != competitors[i])
                return false;
        }
        return true;
    }

    private static bool TechMatches(CareerTechSnapshot left, CareerTechSnapshot right)
    {
        if (left == null || right == null || left.TeamId != right.TeamId ||
            left.RpBalance != right.RpBalance || left.SunNeverSetsTarget != right.SunNeverSetsTarget ||
            left.UnlockedNodeIds.Count != right.UnlockedNodeIds.Count ||
            left.ActiveNodeIds.Count != right.ActiveNodeIds.Count)
        {
            return false;
        }

        for (int i = 0; i < left.UnlockedNodeIds.Count; i++)
            if (!string.Equals(left.UnlockedNodeIds[i], right.UnlockedNodeIds[i], StringComparison.Ordinal))
                return false;
        for (int i = 0; i < left.ActiveNodeIds.Count; i++)
            if (!string.Equals(left.ActiveNodeIds[i], right.ActiveNodeIds[i], StringComparison.Ordinal))
                return false;
        return true;
    }
}

/// <summary>Session-only handoff between MainMenu and Race scenes.</summary>
public static class CareerRaceLaunchState
{
    private static CareerRaceLaunchRequest requested;
    private static CareerRaceLaunchRequest active;

    public static bool IsRequested => requested != null;
    public static bool IsActive => active != null;
    public static bool IsCareerMode => IsRequested || IsActive;
    public static CareerRaceLaunchRequest Current => active ?? requested;

    public static bool Request(CareerSeasonState state, string resultId)
    {
        if (!CareerRaceLaunchRequest.TryCreate(state, resultId, out CareerRaceLaunchRequest launch))
            return false;
        requested = launch;
        active = null;
        return true;
    }

    public static CareerRaceLaunchRequest ActivateRequested()
    {
        if (requested != null)
        {
            active = requested;
            requested = null;
        }
        return active;
    }

    public static void Clear()
    {
        requested = null;
        active = null;
    }
}

/// <summary>Single precedence rule for Tutorial, Career and Quick Race tracks.</summary>
public static class RaceModeLaunchResolver
{
    public static string ResolveTrackId(string configuredFallback)
    {
        if (TutorialLaunchState.IsTutorialMode)
            return TutorialLaunchState.ResolveTrackId(configuredFallback);
        CareerRaceLaunchRequest career = CareerRaceLaunchState.Current;
        return career != null
            ? career.TrackId
            : TrackSelectionState.ResolveTrackId(configuredFallback);
    }
}

/// <summary>Maps a completed four-car runtime race to the career result model.</summary>
public static class CareerRaceResultMapper
{
    public static bool TryBuild(
        CareerRaceLaunchRequest launch,
        string actualTrackId,
        IReadOnlyList<PlayerState> players,
        out CareerRaceResult result)
    {
        result = null;
        if (launch == null || !string.Equals(actualTrackId, launch.TrackId, StringComparison.Ordinal) ||
            players == null || players.Count != launch.Competitors.Count)
            return false;

        var expected = new HashSet<TeamId>(launch.Competitors);
        var seen = new HashSet<TeamId>();
        var copiedPlayers = new List<PlayerState>(players.Count);
        for (int i = 0; i < players.Count; i++)
        {
            PlayerState player = players[i];
            if (player == null || !expected.Contains(player.teamId) || !seen.Add(player.teamId) ||
                (!player.isBlown && !player.hasFinished))
            {
                return false;
            }
            copiedPlayers.Add(player);
        }

        List<RaceRanking.RankEntry> rankings = RaceRanking.GetRankings(copiedPlayers);
        var standings = new List<CareerCompetitorResult>(rankings.Count);
        int classifiedPosition = 0;
        for (int i = 0; i < rankings.Count; i++)
        {
            PlayerState player = rankings[i].player;
            if (player.isBlown)
                standings.Add(new CareerCompetitorResult(player.teamId, 0, true));
            else
                standings.Add(new CareerCompetitorResult(player.teamId, ++classifiedPosition));
        }

        result = new CareerRaceResult(launch.ResultId, launch.TrackId, standings);
        return true;
    }
}

/// <summary>
/// Reloads the authoritative save and records the result only when it still
/// matches the launch request. This rejects stale scene callbacks safely.
/// </summary>
public static class CareerRaceSettlement
{
    public static bool TryRecord(
        CareerRaceLaunchRequest launch,
        string actualTrackId,
        IReadOnlyList<PlayerState> players,
        CareerRepository repository,
        out CareerSeasonState updatedState,
        out string failureReason)
    {
        updatedState = null;
        failureReason = string.Empty;
        if (launch == null || repository == null)
        {
            failureReason = "生涯比赛配置不可用";
            return false;
        }

        var service = new CareerModeService(repository);
        if (service.LoadStatus != CareerLoadStatus.Loaded || !launch.Matches(service.CurrentState))
        {
            failureReason = "生涯存档已变化，未写入本场结果";
            return false;
        }
        if (!CareerRaceResultMapper.TryBuild(launch, actualTrackId, players, out CareerRaceResult result))
        {
            failureReason = "比赛结果阵容或完赛状态无效";
            return false;
        }
        if (!service.TryRecordRace(result))
        {
            failureReason = "生涯赛果保存失败，进度未推进";
            return false;
        }

        updatedState = service.CurrentState;
        return true;
    }
}

/// <summary>Stable structured entries written to RaceTestLog for career settlement.</summary>
public static class CareerRaceLogFormatter
{
    public static string BuildSaved(CareerRaceLaunchRequest launch, CareerSeasonState state)
    {
        if (launch == null || state == null)
            return "[CAREER_RESULT] status=invalid_context";

        List<CareerStanding> standings = CareerModeRules.GetStandings(state);
        CareerStanding player = standings.Find(entry => entry.TeamId == state.LockedTeam);
        CareerStanding champion = standings.Count > 0 ? standings[0] : null;
        return $"[CAREER_RESULT] status=saved result_id={launch.ResultId} " +
               $"race={launch.RaceIndex + 1}/{CareerModeRules.RaceCount} track={launch.TrackId} " +
               $"phase={state.Phase} player_points={player?.Points ?? 0} " +
               $"player_rank={player?.Rank ?? 0} champion={champion?.TeamId.ToString() ?? "none"}";
    }

    public static string BuildRejected(CareerRaceLaunchRequest launch, string reason)
    {
        string resultId = launch != null ? launch.ResultId : "none";
        string safeReason = string.IsNullOrWhiteSpace(reason)
            ? "unknown"
            : reason.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return $"[CAREER_RESULT] status=rejected result_id={resultId} reason={safeReason}";
    }
}
