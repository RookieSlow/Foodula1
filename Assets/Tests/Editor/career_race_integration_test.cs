using System;
using System.Collections.Generic;
using NUnit.Framework;

public sealed class CareerRaceIntegrationTests
{
    private static readonly TeamId[] Field =
    {
        TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US
    };

    [Test]
    public void LaunchRequest_CopiesAuthoritativeSeasonConfiguration()
    {
        CareerSeasonState state = StartSeason();
        Assert.That(CareerRaceLaunchRequest.TryCreate(state, "launch-1", out CareerRaceLaunchRequest request), Is.True);

        Assert.That(request.ResultId, Is.EqualTo("launch-1"));
        Assert.That(request.RaceIndex, Is.Zero);
        Assert.That(request.TrackId, Is.EqualTo(CareerModeRules.TrackSchedule[0]));
        Assert.That(request.PlayerTeam, Is.EqualTo(TeamId.UK));
        Assert.That(request.Competitors, Is.EqualTo(Field));
        Assert.That(request.TechSnapshot, Is.Not.SameAs(state.ActiveTechSnapshot));
        Assert.That(request.Matches(state), Is.True);
    }

    [Test]
    public void LaunchRequest_RejectsSummerBreakAndCompletedStates()
    {
        CareerSeasonState state = StartSeason();
        for (int race = 0; race < 4; race++)
            Assert.That(CareerModeRules.TryRecordRace(state, BuildCareerResult(state, $"race-{race}")), Is.True);

        Assert.That(state.Phase, Is.EqualTo(CareerPhase.SummerBreak));
        Assert.That(CareerRaceLaunchRequest.TryCreate(state, "summer", out _), Is.False);

        Assert.That(CareerModeRules.ConfirmSummerBreakTechTree(state), Is.True);
        for (int race = 4; race < CareerModeRules.RaceCount; race++)
            Assert.That(CareerModeRules.TryRecordRace(state, BuildCareerResult(state, $"race-{race}")), Is.True);
        Assert.That(state.Phase, Is.EqualTo(CareerPhase.Completed));
        Assert.That(CareerRaceLaunchRequest.TryCreate(state, "complete", out _), Is.False);
    }

    [Test]
    public void TrackResolver_UsesTutorialThenCareerThenQuickRaceWithoutMutation()
    {
        CareerRaceLaunchState.Clear();
        TutorialLaunchState.Clear();
        TrackSelectionState.Reset();
        Assert.That(TrackSelectionState.TrySelect(CareerModeRules.TrackSchedule[2]), Is.True);

        CareerSeasonState state = StartSeason();
        Assert.That(CareerRaceLaunchState.Request(state, "launch-resolver"), Is.True);
        Assert.That(RaceModeLaunchResolver.ResolveTrackId(""), Is.EqualTo(CareerModeRules.TrackSchedule[0]));

        TutorialLaunchState.Request(TutorialScenarioDefinition.CreateLeMansUk());
        Assert.That(RaceModeLaunchResolver.ResolveTrackId(""), Is.EqualTo("le_mans_old_mulsanne"));

        TutorialLaunchState.Clear();
        CareerRaceLaunchState.Clear();
        Assert.That(RaceModeLaunchResolver.ResolveTrackId(""), Is.EqualTo(CareerModeRules.TrackSchedule[2]));
        Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo(CareerModeRules.TrackSchedule[2]));
        TrackSelectionState.Reset();
    }

    [Test]
    public void ResultMapper_MapsBlownCarsToDnfAndKeepsClassifiedPositionsContiguous()
    {
        CareerRaceLaunchRequest request = CreateRequest(StartSeason(), "launch-result");
        List<PlayerState> players = BuildRuntimePlayers(oneDnf: true);

        Assert.That(CareerRaceResultMapper.TryBuild(request, request.TrackId, players, out CareerRaceResult result), Is.True);
        Assert.That(result.Standings.Count, Is.EqualTo(4));
        Assert.That(result.Standings[0].TeamId, Is.EqualTo(TeamId.DE));
        Assert.That(result.Standings[0].FinishPosition, Is.EqualTo(1));
        Assert.That(result.Standings[2].FinishPosition, Is.EqualTo(3));
        Assert.That(result.Standings[3].TeamId, Is.EqualTo(TeamId.US));
        Assert.That(result.Standings[3].DidNotFinish, Is.True);
        Assert.That(result.Standings[3].FinishPosition, Is.Zero);
    }

    [Test]
    public void ResultMapper_RejectsWrongTrackIncompleteOrChangedRoster()
    {
        CareerRaceLaunchRequest request = CreateRequest(StartSeason(), "launch-invalid-result");
        List<PlayerState> players = BuildRuntimePlayers(oneDnf: false);
        Assert.That(CareerRaceResultMapper.TryBuild(request, "fallback_42", players, out _), Is.False);

        players[0].hasFinished = false;
        Assert.That(CareerRaceResultMapper.TryBuild(request, request.TrackId, players, out _), Is.False);
        players[0].hasFinished = true;
        players[3].teamId = TeamId.JP;
        Assert.That(CareerRaceResultMapper.TryBuild(request, request.TrackId, players, out _), Is.False);
    }

    [Test]
    public void Settlement_RecordsExactlyOnceAndRejectsStaleCallback()
    {
        var store = new MemoryStore();
        var repository = new CareerRepository(store, new MemorySerializer());
        CareerSeasonState state = StartSeason();
        Assert.That(repository.Save(state), Is.True);
        CareerRaceLaunchRequest request = CreateRequest(state, "launch-settle");
        List<PlayerState> players = BuildRuntimePlayers(oneDnf: false);

        Assert.That(CareerRaceSettlement.TryRecord(
            request, request.TrackId, players, repository, out CareerSeasonState updated, out _), Is.True);
        Assert.That(updated.NextTrackIndex, Is.EqualTo(1));
        Assert.That(CareerRaceSettlement.TryRecord(
            request, request.TrackId, players, repository, out _, out string staleReason), Is.False);
        Assert.That(staleReason, Does.Contain("已变化"));
        Assert.That(repository.Load().State.NextTrackIndex, Is.EqualTo(1));
    }

    [Test]
    public void Settlement_SaveFailureLeavesCareerAtCurrentRace()
    {
        var store = new MemoryStore();
        var repository = new CareerRepository(store, new MemorySerializer());
        CareerSeasonState state = StartSeason();
        Assert.That(repository.Save(state), Is.True);
        CareerRaceLaunchRequest request = CreateRequest(state, "launch-save-fail");
        store.FailWrites = true;

        Assert.That(CareerRaceSettlement.TryRecord(
            request, request.TrackId, BuildRuntimePlayers(false), repository,
            out _, out string failureReason), Is.False);
        Assert.That(failureReason, Does.Contain("保存失败"));
        Assert.That(repository.Load().State.NextTrackIndex, Is.Zero);
    }

    [Test]
    public void LogFormatter_RecordsCompletedChampionAndPlayerRank()
    {
        CareerSeasonState state = StartSeason();
        CareerRaceLaunchRequest finalLaunch = null;
        for (int race = 0; race < CareerModeRules.RaceCount; race++)
        {
            if (state.Phase == CareerPhase.SummerBreak)
                Assert.That(CareerModeRules.ConfirmSummerBreakTechTree(state), Is.True);
            finalLaunch = CreateRequest(state, $"log-{race}");
            Assert.That(CareerModeRules.TryRecordRace(state, BuildCareerResult(state, $"log-{race}")), Is.True);
        }

        string line = CareerRaceLogFormatter.BuildSaved(finalLaunch, state);
        Assert.That(line, Does.Contain("status=saved"));
        Assert.That(line, Does.Contain("race=8/8"));
        Assert.That(line, Does.Contain("phase=Completed"));
        Assert.That(line, Does.Contain("player_rank=1"));
        Assert.That(line, Does.Contain("champion=UK"));
    }

    [Test]
    public void LogFormatter_RecordsRejectedResultWithoutMultilineReason()
    {
        CareerRaceLaunchRequest request = CreateRequest(StartSeason(), "log-rejected");
        string line = CareerRaceLogFormatter.BuildRejected(request, "保存失败\r\n进度未推进");
        Assert.That(line, Does.Contain("status=rejected"));
        Assert.That(line, Does.Contain("result_id=log-rejected"));
        Assert.That(line, Does.Not.Contain("\r"));
        Assert.That(line, Does.Not.Contain("\n"));
    }

    private static CareerSeasonState StartSeason()
    {
        var state = new CareerSeasonState();
        Assert.That(CareerModeRules.TryStartSeason(state, TeamId.UK, Field), Is.True);
        return state;
    }

    private static CareerRaceLaunchRequest CreateRequest(CareerSeasonState state, string id)
    {
        Assert.That(CareerRaceLaunchRequest.TryCreate(state, id, out CareerRaceLaunchRequest request), Is.True);
        return request;
    }

    private static CareerRaceResult BuildCareerResult(CareerSeasonState state, string id)
    {
        return new CareerRaceResult(id, CareerModeRules.GetNextTrackId(state), new[]
        {
            new CareerCompetitorResult(TeamId.UK, 1),
            new CareerCompetitorResult(TeamId.DE, 2),
            new CareerCompetitorResult(TeamId.IT, 3),
            new CareerCompetitorResult(TeamId.US, 4)
        });
    }

    private static List<PlayerState> BuildRuntimePlayers(bool oneDnf)
    {
        return new List<PlayerState>
        {
            Finished(TeamId.UK, 2),
            Finished(TeamId.DE, 1),
            Finished(TeamId.IT, 3),
            oneDnf ? Blown(TeamId.US) : Finished(TeamId.US, 4)
        };
    }

    private static PlayerState Finished(TeamId team, int order)
    {
        var player = new PlayerState(team.ToString(), team != TeamId.UK, 0, 1)
        {
            teamId = team,
            hasFinished = true,
            finishOrder = order
        };
        return player;
    }

    private static PlayerState Blown(TeamId team)
    {
        var player = new PlayerState(team.ToString(), true, 0, 1)
        {
            teamId = team,
            isBlown = true
        };
        return player;
    }

    private sealed class MemoryStore : ICareerKeyValueStore
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();
        public bool FailWrites { get; set; }
        public bool HasKey(string key) => values.ContainsKey(key);
        public string GetString(string key) => values.TryGetValue(key, out string value) ? value : string.Empty;
        public bool TrySetAndSave(string key, string value)
        {
            if (FailWrites) return false;
            values[key] = value;
            return true;
        }
        public bool TryDeleteAndSave(string key) => values.Remove(key) || !values.ContainsKey(key);
    }

    private sealed class MemorySerializer : ICareerSerializer
    {
        private readonly Dictionary<string, CareerSaveData> values = new Dictionary<string, CareerSaveData>();
        private int nextId;
        public string Serialize(CareerSaveData data)
        {
            string id = (++nextId).ToString();
            values[id] = data;
            return id;
        }
        public CareerSaveData Deserialize(string serialized) => values[serialized];
    }
}
