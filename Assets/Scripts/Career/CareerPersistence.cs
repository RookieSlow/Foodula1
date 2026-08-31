using System;
using System.Collections.Generic;

[Serializable]
public sealed class CareerTechSnapshotData
{
    public int teamId;
    public int rpBalance;
    public string[] unlockedNodeIds = Array.Empty<string>();
    public string[] activeNodeIds = Array.Empty<string>();
    public bool hasSunNeverSetsTarget;
    public int sunNeverSetsTarget;
}

[Serializable]
public sealed class CareerCompetitorResultData
{
    public int teamId;
    public int finishPosition;
    public bool didNotFinish;
}

[Serializable]
public sealed class CareerRaceResultData
{
    public string resultId = string.Empty;
    public string trackId = string.Empty;
    public CareerCompetitorResultData[] standings = Array.Empty<CareerCompetitorResultData>();
}

[Serializable]
public sealed class CareerStandingData
{
    public int teamId;
    public int rank;
    public int points;
    public int wins;
    public int podiums;
    public int bestFinish;
    public int mostRecentFinish;
}

/// <summary>Versioned, JsonUtility-compatible career save envelope.</summary>
[Serializable]
public sealed class CareerSaveData
{
    public int schemaVersion;
    public int scheduleVersion;
    public string[] trackIds = Array.Empty<string>();
    public int phase;
    public bool hasLockedTeam;
    public int lockedTeam;
    public int nextTrackIndex;
    public bool summerBreakUsed;
    public int[] competitors = Array.Empty<int>();
    public CareerRaceResultData[] raceResults = Array.Empty<CareerRaceResultData>();
    public CareerStandingData[] standings = Array.Empty<CareerStandingData>();
    public CareerTechSnapshotData initialTech;
    public CareerTechSnapshotData summerBreakTech;
}

/// <summary>
/// Converts between the immutable rules state and the persistence DTO. Loading
/// rebuilds the season through CareerModeRules instead of trusting saved totals.
/// </summary>
public static class CareerSaveCodec
{
    public const int SchemaVersion = 1;
    public const int ScheduleVersion = 1;

    public static bool TryToData(CareerSeasonState state, out CareerSaveData data)
    {
        data = null;
        if (state == null || !state.HasLockedTeam || state.Phase == CareerPhase.NotStarted ||
            state.InitialTechSnapshot == null || state.ActiveTechSnapshot == null)
        {
            return false;
        }

        var result = new CareerSaveData
        {
            schemaVersion = SchemaVersion,
            scheduleVersion = ScheduleVersion,
            trackIds = CopyTrackSchedule(),
            phase = (int)state.Phase,
            hasLockedTeam = state.HasLockedTeam,
            lockedTeam = (int)state.LockedTeam,
            nextTrackIndex = state.NextTrackIndex,
            summerBreakUsed = state.SummerBreakUsed,
            competitors = new int[state.Competitors.Count],
            raceResults = new CareerRaceResultData[state.RaceResults.Count],
            initialTech = ToData(state.InitialTechSnapshot),
            summerBreakTech = state.SummerBreakTechSnapshot == null
                ? null
                : ToData(state.SummerBreakTechSnapshot)
        };

        for (int i = 0; i < state.Competitors.Count; i++)
            result.competitors[i] = (int)state.Competitors[i];

        for (int raceIndex = 0; raceIndex < state.RaceResults.Count; raceIndex++)
        {
            CareerRaceResult race = state.RaceResults[raceIndex];
            var raceData = new CareerRaceResultData
            {
                resultId = race.ResultId,
                trackId = race.TrackId,
                standings = new CareerCompetitorResultData[race.Standings.Count]
            };
            for (int entryIndex = 0; entryIndex < race.Standings.Count; entryIndex++)
            {
                CareerCompetitorResult entry = race.Standings[entryIndex];
                raceData.standings[entryIndex] = new CareerCompetitorResultData
                {
                    teamId = (int)entry.TeamId,
                    finishPosition = entry.FinishPosition,
                    didNotFinish = entry.DidNotFinish
                };
            }
            result.raceResults[raceIndex] = raceData;
        }

        List<CareerStanding> standings = CareerModeRules.GetStandings(state);
        result.standings = new CareerStandingData[standings.Count];
        for (int i = 0; i < standings.Count; i++)
        {
            CareerStanding entry = standings[i];
            result.standings[i] = new CareerStandingData
            {
                teamId = (int)entry.TeamId,
                rank = entry.Rank,
                points = entry.Points,
                wins = entry.Wins,
                podiums = entry.Podiums,
                bestFinish = entry.BestFinish,
                mostRecentFinish = entry.MostRecentFinish
            };
        }

        data = result;
        return true;
    }

    public static bool TryFromData(CareerSaveData data, out CareerSeasonState state)
    {
        state = new CareerSeasonState();
        if (!HasValidEnvelope(data) || !TryReadTeam(data.lockedTeam, out TeamId lockedTeam) ||
            !TryReadCompetitors(data.competitors, out List<TeamId> competitors) ||
            !TryReadTechSnapshot(data.initialTech, lockedTeam, out CareerTechSnapshot initialTech))
        {
            return false;
        }

        CareerTechSnapshot summerTech = null;
        if (data.summerBreakUsed)
        {
            if (!TryReadTechSnapshot(data.summerBreakTech, lockedTeam, out summerTech))
                return false;
        }
        else if (data.summerBreakTech != null)
        {
            return false;
        }

        var rebuilt = new CareerSeasonState();
        if (!CareerModeRules.TryStartSeason(rebuilt, lockedTeam, competitors, initialTech))
            return false;

        for (int raceIndex = 0; raceIndex < data.raceResults.Length; raceIndex++)
        {
            if (rebuilt.Phase == CareerPhase.SummerBreak)
            {
                if (!data.summerBreakUsed ||
                    !CareerModeRules.ConfirmSummerBreakTechTree(rebuilt, summerTech))
                {
                    return false;
                }
            }

            if (!TryReadRaceResult(data.raceResults[raceIndex], out CareerRaceResult raceResult) ||
                !CareerModeRules.TryRecordRace(rebuilt, raceResult))
            {
                return false;
            }
        }

        if (rebuilt.Phase == CareerPhase.SummerBreak && data.summerBreakUsed)
        {
            if (!CareerModeRules.ConfirmSummerBreakTechTree(rebuilt, summerTech))
                return false;
        }

        if (rebuilt.NextTrackIndex != data.nextTrackIndex ||
            rebuilt.Phase != (CareerPhase)data.phase ||
            rebuilt.SummerBreakUsed != data.summerBreakUsed ||
            !StandingsMatch(rebuilt, data.standings))
        {
            return false;
        }

        state = rebuilt;
        return true;
    }

    public static bool TryClone(CareerSeasonState source, out CareerSeasonState clone)
    {
        clone = null;
        return TryToData(source, out CareerSaveData data) && TryFromData(data, out clone);
    }

    private static bool HasValidEnvelope(CareerSaveData data)
    {
        if (data == null || data.schemaVersion != SchemaVersion ||
            data.scheduleVersion != ScheduleVersion || !data.hasLockedTeam ||
            data.trackIds == null || data.trackIds.Length != CareerModeRules.TrackSchedule.Count ||
            data.raceResults == null || data.standings == null || data.competitors == null ||
            data.nextTrackIndex < 0 || data.nextTrackIndex > CareerModeRules.RaceCount ||
            data.raceResults.Length != data.nextTrackIndex ||
            !Enum.IsDefined(typeof(CareerPhase), data.phase) ||
            (CareerPhase)data.phase == CareerPhase.NotStarted)
        {
            return false;
        }

        for (int i = 0; i < data.trackIds.Length; i++)
        {
            if (!string.Equals(
                    data.trackIds[i],
                    CareerModeRules.TrackSchedule[i],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadCompetitors(int[] values, out List<TeamId> competitors)
    {
        competitors = new List<TeamId>();
        if (values == null)
            return false;
        for (int i = 0; i < values.Length; i++)
        {
            if (!TryReadTeam(values[i], out TeamId team))
                return false;
            competitors.Add(team);
        }
        return true;
    }

    private static bool TryReadRaceResult(CareerRaceResultData data, out CareerRaceResult result)
    {
        result = null;
        if (data == null || data.standings == null)
            return false;

        var standings = new List<CareerCompetitorResult>();
        for (int i = 0; i < data.standings.Length; i++)
        {
            CareerCompetitorResultData entry = data.standings[i];
            if (entry == null || !TryReadTeam(entry.teamId, out TeamId team))
                return false;
            standings.Add(new CareerCompetitorResult(team, entry.finishPosition, entry.didNotFinish));
        }

        result = new CareerRaceResult(data.resultId, data.trackId, standings);
        return true;
    }

    private static bool TryReadTechSnapshot(
        CareerTechSnapshotData data,
        TeamId expectedTeam,
        out CareerTechSnapshot snapshot)
    {
        snapshot = null;
        if (data == null || !TryReadTeam(data.teamId, out TeamId team) || team != expectedTeam)
            return false;

        TeamId? target = null;
        if (data.hasSunNeverSetsTarget)
        {
            if (!TryReadTeam(data.sunNeverSetsTarget, out TeamId parsedTarget))
                return false;
            target = parsedTarget;
        }

        return CareerTechSnapshot.TryCreate(
            team,
            data.rpBalance,
            data.unlockedNodeIds,
            data.activeNodeIds,
            target,
            out snapshot);
    }

    private static bool TryReadTeam(int value, out TeamId team)
    {
        team = (TeamId)value;
        return Enum.IsDefined(typeof(TeamId), team);
    }

    private static CareerTechSnapshotData ToData(CareerTechSnapshot snapshot)
    {
        return new CareerTechSnapshotData
        {
            teamId = (int)snapshot.TeamId,
            rpBalance = snapshot.RpBalance,
            unlockedNodeIds = CopyStrings(snapshot.UnlockedNodeIds),
            activeNodeIds = CopyStrings(snapshot.ActiveNodeIds),
            hasSunNeverSetsTarget = snapshot.SunNeverSetsTarget.HasValue,
            sunNeverSetsTarget = snapshot.SunNeverSetsTarget.HasValue
                ? (int)snapshot.SunNeverSetsTarget.Value
                : 0
        };
    }

    private static bool StandingsMatch(CareerSeasonState state, CareerStandingData[] saved)
    {
        List<CareerStanding> computed = CareerModeRules.GetStandings(state);
        if (saved == null || saved.Length != computed.Count)
            return false;

        for (int i = 0; i < computed.Count; i++)
        {
            CareerStanding actual = computed[i];
            CareerStandingData expected = saved[i];
            if (expected == null || expected.teamId != (int)actual.TeamId ||
                expected.rank != actual.Rank || expected.points != actual.Points ||
                expected.wins != actual.Wins || expected.podiums != actual.Podiums ||
                expected.bestFinish != actual.BestFinish ||
                expected.mostRecentFinish != actual.MostRecentFinish)
            {
                return false;
            }
        }

        return true;
    }

    private static string[] CopyTrackSchedule()
    {
        var result = new string[CareerModeRules.TrackSchedule.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = CareerModeRules.TrackSchedule[i];
        return result;
    }

    private static string[] CopyStrings(IReadOnlyList<string> values)
    {
        var result = new string[values.Count];
        for (int i = 0; i < values.Count; i++)
            result[i] = values[i];
        return result;
    }
}

public enum CareerLoadStatus
{
    Missing,
    Loaded,
    Invalid
}

public sealed class CareerLoadResult
{
    public CareerLoadStatus Status { get; }
    public CareerSeasonState State { get; }

    public CareerLoadResult(CareerLoadStatus status, CareerSeasonState state)
    {
        Status = status;
        State = state ?? new CareerSeasonState();
    }
}

public interface ICareerKeyValueStore
{
    bool HasKey(string key);
    string GetString(string key);
    bool TrySetAndSave(string key, string value);
    bool TryDeleteAndSave(string key);
}

public interface ICareerSerializer
{
    string Serialize(CareerSaveData data);
    CareerSaveData Deserialize(string serialized);
}

/// <summary>Isolated repository using a career-only key.</summary>
public sealed class CareerRepository
{
    public const string SaveKey = "Foodula1.Career.V1";

    private readonly ICareerKeyValueStore store;
    private readonly ICareerSerializer serializer;
    private readonly Func<CareerSeasonState, bool> stateValidator;

    public CareerRepository(
        ICareerKeyValueStore store,
        ICareerSerializer serializer,
        Func<CareerSeasonState, bool> stateValidator = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.stateValidator = stateValidator ?? (_ => true);
    }

    public bool HasStoredSave => store.HasKey(SaveKey);

    public CareerLoadResult Load()
    {
        if (!store.HasKey(SaveKey))
            return new CareerLoadResult(CareerLoadStatus.Missing, new CareerSeasonState());

        try
        {
            CareerSaveData data = serializer.Deserialize(store.GetString(SaveKey));
            return CareerSaveCodec.TryFromData(data, out CareerSeasonState state) && stateValidator(state)
                ? new CareerLoadResult(CareerLoadStatus.Loaded, state)
                : new CareerLoadResult(CareerLoadStatus.Invalid, new CareerSeasonState());
        }
        catch (Exception)
        {
            return new CareerLoadResult(CareerLoadStatus.Invalid, new CareerSeasonState());
        }
    }

    public bool Save(CareerSeasonState state)
    {
        try
        {
            if (!stateValidator(state) || !CareerSaveCodec.TryToData(state, out CareerSaveData data))
                return false;
            string serialized = serializer.Serialize(data);
            return !string.IsNullOrEmpty(serialized) && store.TrySetAndSave(SaveKey, serialized);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool Abandon()
    {
        return !store.HasKey(SaveKey) || store.TryDeleteAndSave(SaveKey);
    }
}

/// <summary>
/// Career application service. Mutations happen on a validated clone and only
/// replace CurrentState after persistence succeeds.
/// </summary>
public sealed class CareerModeService
{
    private readonly CareerRepository repository;

    public CareerSeasonState CurrentState { get; private set; }
    public CareerLoadStatus LoadStatus { get; private set; }
    public bool HasStoredCareer => repository.HasStoredSave;

    public CareerModeService(CareerRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        CareerLoadResult loaded = repository.Load();
        CurrentState = loaded.State;
        LoadStatus = loaded.Status;
    }

    public bool TryCreateNew(
        TeamId playerTeam,
        IReadOnlyList<TeamId> competitors,
        CareerTechSnapshot initialTech,
        bool confirmReplaceExisting)
    {
        if (repository.HasStoredSave && !confirmReplaceExisting)
            return false;

        var candidate = new CareerSeasonState();
        if (!CareerModeRules.TryStartSeason(candidate, playerTeam, competitors, initialTech) ||
            !repository.Save(candidate))
        {
            return false;
        }

        CurrentState = candidate;
        LoadStatus = CareerLoadStatus.Loaded;
        return true;
    }

    public bool TryRecordRace(CareerRaceResult result)
    {
        if (!CareerSaveCodec.TryClone(CurrentState, out CareerSeasonState candidate) ||
            !CareerModeRules.TryRecordRace(candidate, result) ||
            !repository.Save(candidate))
        {
            return false;
        }

        CurrentState = candidate;
        return true;
    }

    public bool TryConfirmSummerBreak(CareerTechSnapshot snapshot)
    {
        if (!CareerSaveCodec.TryClone(CurrentState, out CareerSeasonState candidate) ||
            !CareerModeRules.ConfirmSummerBreakTechTree(candidate, snapshot) ||
            !repository.Save(candidate))
        {
            return false;
        }

        CurrentState = candidate;
        return true;
    }

    public bool TryAbandon(bool confirmed)
    {
        if (!confirmed || !repository.Abandon())
            return false;

        CurrentState = new CareerSeasonState();
        LoadStatus = CareerLoadStatus.Missing;
        return true;
    }
}
