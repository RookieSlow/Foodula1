using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.JP)]
    [TestCase(TeamId.CN)]
    public void JsonPipeline_EightRacesReloadSummerDraftAndReplacementRemainCareerOwned(TeamId team)
    {
        var store = new CareerOnlyStore();
        TechTreeDatabase database = TechTreeDatabaseFactory.CreateDefault();
        CareerRepository repository = CreateJsonRepository(store, database);
        CareerModeService service = CreateJsonSeason(repository, team);
        TeamId[] field = BuildField(team);
        var expectedPoints = new Dictionary<TeamId, int>();
        foreach (TeamId competitor in field) expectedPoints.Add(competitor, 0);

        for (int race = 0; race < 8; race++)
        {
            service = new CareerModeService(repository);
            Assert.That(service.LoadStatus, Is.EqualTo(CareerLoadStatus.Loaded));
            CareerSeasonState before = service.CurrentState;
            Assert.That(before.Competitors, Is.EqualTo(field));
            Assert.That(before.LockedTeam, Is.EqualTo(team));
            CareerRaceLaunchRequest launch = CreateRequest(before, $"json-{team}-{race}");
            Assert.That(launch.TrackId, Is.EqualTo(CareerModeRules.TrackSchedule[race]));
            List<PlayerState> players = BuildPlannedFinishers(launch, race);
            int writes = store.SuccessfulWrites;
            Assert.That(CareerRaceSettlement.TryRecord(launch, launch.TrackId, players, repository,
                out CareerSeasonState updated, out _), Is.True);
            Assert.That(store.SuccessfulWrites, Is.EqualTo(writes + 1));
            Assert.That(before.NextTrackIndex, Is.EqualTo(race), "Launch source must stay immutable.");
            Assert.That(updated.RaceResults.Count, Is.EqualTo(race + 1));
            Assert.That(updated.NextTrackIndex, Is.EqualTo(race + 1));
            foreach (PlayerState player in players)
                expectedPoints[player.teamId] += player.isBlown ? 0 : new[] { 10, 6, 4, 2 }[player.finishOrder - 1];
            foreach (CareerStanding standing in CareerModeRules.GetStandings(updated))
                Assert.That(standing.Points, Is.EqualTo(expectedPoints[standing.TeamId]));
            string savedJson = store.Json;
            Assert.That(savedJson, Does.StartWith("{"), "Exercise the production JSON serializer, not token storage.");
            Assert.That(CareerRaceSettlement.TryRecord(launch, launch.TrackId, players, repository,
                out _, out _), Is.False);
            Assert.That(store.SuccessfulWrites, Is.EqualTo(writes + 1));
            Assert.That(store.Json, Is.EqualTo(savedJson));
            CareerSeasonState reloaded = new CareerModeService(repository).CurrentState;
            Assert.That(reloaded, Is.Not.SameAs(updated));
            Assert.That(reloaded.RaceResults[race].ResultId, Is.EqualTo(launch.ResultId));
            Assert.That(CareerRaceLogFormatter.BuildSaved(launch, reloaded),
                Does.Contain($"race={race + 1}/8").And.Contain($"phase={reloaded.Phase}"));

            if (race == 3)
            {
                service = new CareerModeService(repository);
                Assert.That(service.CurrentState.Phase, Is.EqualTo(CareerPhase.SummerBreak));
                Assert.That(CareerRaceLaunchRequest.TryCreate(service.CurrentState, "blocked", out _), Is.False);
                CareerTechSnapshot summer = BuildSummerDraft(service.CurrentState, database);
                Assert.That(store.Json, Is.EqualTo(savedJson), "Unconfirmed draft must not save.");
                Assert.That(service.TryConfirmSummerBreak(summer), Is.True);
                Assert.That(service.TryConfirmSummerBreak(summer), Is.False);
                Assert.That(store.SuccessfulWrites, Is.EqualTo(writes + 2));
            }
            if (race >= 4)
            {
                Assert.That(reloaded.SummerBreakUsed, Is.True);
                Assert.That(launch.TechSnapshot.ActiveNodeIds.Count, Is.EqualTo(1));
                Assert.That(reloaded.InitialTechSnapshot.ActiveNodeIds, Is.Empty);
                Assert.That(reloaded.ActiveTechSnapshot.ActiveNodeIds, Is.EqualTo(launch.TechSnapshot.ActiveNodeIds));
            }
        }

        service = new CareerModeService(repository);
        CareerSeasonState complete = service.CurrentState;
        Assert.That(complete.Phase, Is.EqualTo(CareerPhase.Completed));
        Assert.That(CareerRaceLaunchRequest.TryCreate(complete, "ninth", out _), Is.False);
        Assert.That(CareerModeRules.GetStandings(repository.Load().State)[0].TeamId,
            Is.EqualTo(CareerModeRules.GetStandings(complete)[0].TeamId));
        Assert.That(service.TryCreateNew(team, field, complete.InitialTechSnapshot, false), Is.False);
        Assert.That(service.TryCreateNew(team, field, complete.InitialTechSnapshot, true), Is.True);
        Assert.That(service.CurrentState.RaceResults, Is.Empty);
        Assert.That(service.CurrentState.SummerBreakUsed, Is.False);
        Assert.That(complete.RaceResults.Count, Is.EqualTo(8));
        foreach (CareerStanding standing in CareerModeRules.GetStandings(repository.Load().State))
            Assert.That(standing.Points, Is.Zero);
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.JP)]
    [TestCase(TeamId.CN)]
    public void JsonPipeline_FailedRaceWriteCanRetrySameLaunchExactlyOnce(TeamId team)
    {
        var store = new CareerOnlyStore();
        CareerRepository repository = CreateJsonRepository(store, TechTreeDatabaseFactory.CreateDefault());
        CareerModeService service = CreateJsonSeason(repository, team);
        CareerRaceLaunchRequest launch = CreateRequest(service.CurrentState, $"retry-{team}");
        List<PlayerState> players = BuildPlannedFinishers(launch, 0);
        string before = store.Json;
        store.FailWrites = true;
        Assert.That(CareerRaceSettlement.TryRecord(launch, launch.TrackId, players, repository,
            out _, out string reason), Is.False);
        Assert.That(reason, Does.Contain("保存失败"));
        Assert.That(store.Json, Is.EqualTo(before));
        Assert.That(repository.Load().State.RaceResults, Is.Empty);
        Assert.That(launch.Matches(repository.Load().State), Is.True);
        store.FailWrites = false;
        Assert.That(CareerRaceSettlement.TryRecord(launch, launch.TrackId, players, repository,
            out _, out _), Is.True);
        Assert.That(CareerRaceSettlement.TryRecord(launch, launch.TrackId, players, repository,
            out _, out _), Is.False);
        Assert.That(store.SuccessfulWrites, Is.EqualTo(2));
        Assert.That(repository.Load().State.RaceResults.Count, Is.EqualTo(1));
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.JP)]
    [TestCase(TeamId.CN)]
    public void JsonPipeline_FailedSummerWritePreservesDraftGateAndRetriesAtomically(TeamId team)
    {
        var store = new CareerOnlyStore();
        TechTreeDatabase database = TechTreeDatabaseFactory.CreateDefault();
        CareerRepository repository = CreateJsonRepository(store, database);
        CreateJsonSeason(repository, team);
        for (int race = 0; race < 4; race++)
        {
            CareerRaceLaunchRequest launch = CreateRequest(repository.Load().State, $"summer-{team}-{race}");
            Assert.That(CareerRaceSettlement.TryRecord(launch, launch.TrackId,
                BuildPlannedFinishers(launch, race), repository, out _, out _), Is.True);
        }
        var service = new CareerModeService(repository);
        CareerSeasonState before = service.CurrentState;
        string originalJson = store.Json;
        CareerTechSnapshot draft = BuildSummerDraft(before, database);
        Assert.That(service.TryConfirmSummerBreak(CareerTechSnapshot.CreateEmpty(BuildField(team)[0])), Is.False);
        store.FailWrites = true;
        Assert.That(service.TryConfirmSummerBreak(draft), Is.False);
        Assert.That(service.CurrentState, Is.SameAs(before));
        Assert.That(before.SummerBreakUsed, Is.False);
        Assert.That(before.ActiveTechSnapshot.ActiveNodeIds, Is.Empty);
        Assert.That(store.Json, Is.EqualTo(originalJson));
        Assert.That(repository.Load().State.Phase, Is.EqualTo(CareerPhase.SummerBreak));
        store.FailWrites = false;
        Assert.That(service.TryConfirmSummerBreak(draft), Is.True);
        Assert.That(service.TryConfirmSummerBreak(draft), Is.False);
        Assert.That(store.SuccessfulWrites, Is.EqualTo(6));
        CareerRaceLaunchRequest fifth = CreateRequest(repository.Load().State, "fifth");
        Assert.That(fifth.RaceIndex, Is.EqualTo(4));
        Assert.That(fifth.TechSnapshot.ActiveNodeIds, Is.EqualTo(draft.ActiveNodeIds));
        Assert.That(fifth.TechSnapshot.RpBalance, Is.EqualTo(draft.RpBalance));
    }

    private static TeamId[] BuildField(TeamId human)
    {
        var opponents = new List<TeamId>();
        foreach (TeamId team in new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.JP, TeamId.CN })
            if (team != human && opponents.Count < 3) opponents.Add(team);
        return new[] { opponents[0], human, opponents[1], opponents[2] };
    }

    private static CareerRepository CreateJsonRepository(CareerOnlyStore store, TechTreeDatabase database)
    {
        return new CareerRepository(store, new JsonUtilityCareerSerializer(),
            state => CareerTechSnapshotMapper.IsSeasonValidForDatabase(state, database));
    }

    private static CareerModeService CreateJsonSeason(CareerRepository repository, TeamId team)
    {
        Assert.That(CareerTechSnapshotMapper.TryCapture(new TechTreeState(team, 25000), out CareerTechSnapshot snapshot), Is.True);
        var service = new CareerModeService(repository);
        Assert.That(service.TryCreateNew(team, BuildField(team), snapshot, false), Is.True);
        return service;
    }

    private static CareerTechSnapshot BuildSummerDraft(CareerSeasonState state, TechTreeDatabase database)
    {
        Assert.That(CareerTechTreeDraft.TryCreate(state.ActiveTechSnapshot, database, out CareerTechTreeDraft draft), Is.True);
        TechNodeDef node = draft.GetNodes(TechTreeTier.L1)[0];
        Assert.That(draft.TryToggleOrUnlock(node.id), Is.True);
        Assert.That(draft.TryBuildSnapshot(out CareerTechSnapshot snapshot), Is.True);
        Assert.That(state.ActiveTechSnapshot.ActiveNodeIds, Is.Empty);
        Assert.That(state.ActiveTechSnapshot.RpBalance, Is.EqualTo(25000));
        Assert.That(snapshot.ActiveNodeIds, Is.EqualTo(new[] { node.id }));
        Assert.That(snapshot.RpBalance, Is.EqualTo(25000 - node.rpCost));
        return snapshot;
    }

    // Terminal result fixtures exercise real plan/mapping/storage boundaries, not GameLoop or movement.
    private static List<PlayerState> BuildPlannedFinishers(CareerRaceLaunchRequest launch, int race)
    {
        int xpReads = 0;
        List<RaceParticipantPlan> plans = RaceParticipantPlanBuilder.Build(null, launch,
            Array.Empty<FreeRaceRosterEntry>(), DriverCatalog.GetDefaultForTeam(BuildField(launch.PlayerTeam)[0]),
            new[] { TeamId.CN }, 11, id => { xpReads++; return 137; });
        Assert.That(plans.Count, Is.EqualTo(4));
        Assert.That(xpReads, Is.EqualTo(1));
        Assert.That(plans[0].Team, Is.EqualTo(launch.PlayerTeam));
        Assert.That(plans[0].InitialXp, Is.EqualTo(137));
        var players = new List<PlayerState>();
        int opponent = 1;
        foreach (TeamId team in launch.Competitors)
            if (team != launch.PlayerTeam) Assert.That(plans[opponent++].Team, Is.EqualTo(team));
        for (int index = 0; index < plans.Count; index++)
        {
            RaceParticipantPlan plan = plans[index];
            Assert.That(plan.Driver.Team, Is.EqualTo(plan.Team));
            Assert.That(plan.IsHuman, Is.EqualTo(index == 0));
            Assert.That(plan.CareerTechSnapshot, index == 0 ? Is.SameAs(launch.TechSnapshot) : Is.Null);
            int order = (index + race) % 4 + 1;
            players.Add(new PlayerState(plan.Name, !plan.IsHuman, 0, 1)
            {
                teamId = plan.Team, hasFinished = order != 4 || race % 2 != 0,
                isBlown = order == 4 && race % 2 == 0, finishOrder = order
            });
        }
        return players;
    }

    private sealed class CareerOnlyStore : ICareerKeyValueStore
    {
        public string Json { get; private set; }
        public bool FailWrites { get; set; }
        public int SuccessfulWrites { get; private set; }
        public bool HasKey(string key) { AssertCareerKey(key); return Json != null; }
        public string GetString(string key) { AssertCareerKey(key); return Json ?? string.Empty; }
        public bool TrySetAndSave(string key, string value)
        {
            AssertCareerKey(key);
            if (FailWrites) return false;
            Json = value;
            SuccessfulWrites++;
            return true;
        }
        public bool TryDeleteAndSave(string key)
        {
            Assert.Fail("These pipelines must not delete stored progress.");
            return false;
        }
        private static void AssertCareerKey(string key) => Assert.That(key, Is.EqualTo(CareerRepository.SaveKey));
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

/// <summary>
/// Exercises the real manager entry/re-entry adapter without Awake, scene loads,
/// GameLoop or profile storage. Static launch references are restored even on failure.
/// </summary>
public sealed class RaceLaunchLifecycleAdapterTests
{
    private readonly Dictionary<FieldInfo, object> savedLaunchFields =
        new Dictionary<FieldInfo, object>();
    private UnityEngine.GameObject root;
    private MVPGameManager manager;
    private UnityEngine.Random.State randomState;
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;

    private static IEnumerable<TestCaseData> TeamActivationCases()
    {
        foreach (TeamId team in FreeRaceRosterRules.AvailableTeams)
            foreach (bool alreadyActive in new[] { false, true })
                yield return new TestCaseData(team, alreadyActive);
    }

    [SetUp]
    public void SetUp()
    {
        foreach (Type type in new[] { typeof(TutorialLaunchState), typeof(CareerRaceLaunchState) })
            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                if (!field.IsInitOnly) savedLaunchFields.Add(field, field.GetValue(null));
        TutorialLaunchState.Clear();
        CareerRaceLaunchState.Clear();
        randomState = UnityEngine.Random.state;
        root = new UnityEngine.GameObject("RaceLaunchLifecycleTest");
        root.SetActive(false);
        manager = root.AddComponent<MVPGameManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        foreach (var entry in savedLaunchFields) entry.Key.SetValue(null, entry.Value);
        savedLaunchFields.Clear();
        UnityEngine.Random.state = randomState;
    }

    [TestCaseSource(nameof(TeamActivationCases))]
    public void TutorialEntryAndReentry_WinOverCareerAndKeepSelectedConfiguration(
        TeamId team, bool alreadyActive)
    {
        var scenario = TutorialScenarioDefinition.CreateTeamSpecialty(team);
        TutorialLaunchState.Request(scenario);
        var career = RequestCareer(team);
        if (alreadyActive)
        {
            TutorialLaunchState.ActivateRequested();
            CareerRaceLaunchState.ActivateRequested();
        }
        string trackSelection = TrackSelectionState.SelectedTrackId;
        TeamId[] rosterSelection = FreeRaceRosterState.SelectedTeams.ToArray();
        string seasonBefore = SerializeSeason(career);

        for (int entry = 0; entry < 2; entry++)
        {
            SeedStaleManagerState();
            Activate();
            Assert.That(manager.IsTutorialMode, Is.True);
            Assert.That(manager.IsCareerMode, Is.False);
            Assert.That(manager.TutorialScenario, Is.SameAs(scenario));
            Assert.That(TutorialLaunchState.IsRequested, Is.False);
            Assert.That(TutorialLaunchState.IsActive, Is.True);
            Assert.That(CareerRaceLaunchState.Current, Is.Null);
            Assert.That(CareerRaceLaunchState.IsCareerMode, Is.False);
            Assert.That(RaceModeLaunchResolver.ResolveTrackId(""), Is.EqualTo(scenario.trackId));
            AssertTransientFlagsReset();
        }

        Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo(trackSelection));
        Assert.That(FreeRaceRosterState.SelectedTeams, Is.EqualTo(rosterSelection));
        Assert.That(SerializeSeason(career),
            Is.EqualTo(seasonBefore), "Mode activation must not settle or rewrite the career.");
    }

    [TestCaseSource(nameof(TeamActivationCases))]
    public void CareerEntryAndReentry_KeepTheSameRequestAndResetPriorResultFlags(
        TeamId team, bool alreadyActive)
    {
        CareerSeasonState season = RequestCareer(team);
        CareerRaceLaunchRequest request = CareerRaceLaunchState.Current;
        if (alreadyActive) CareerRaceLaunchState.ActivateRequested();
        string selectedTrack = TrackSelectionState.SelectedTrackId;

        for (int entry = 0; entry < 2; entry++)
        {
            SeedStaleManagerState();
            Activate();
            Assert.That(manager.IsTutorialMode, Is.False);
            Assert.That(manager.TutorialScenario, Is.Null);
            Assert.That(manager.IsCareerMode, Is.True);
            Assert.That(Get("careerRaceLaunch"), Is.SameAs(request));
            Assert.That(CareerRaceLaunchState.Current, Is.SameAs(request));
            Assert.That(CareerRaceLaunchState.IsRequested, Is.False);
            Assert.That(CareerRaceLaunchState.IsActive, Is.True);
            Assert.That(request.Matches(season), Is.True);
            Assert.That(RaceModeLaunchResolver.ResolveTrackId(""), Is.EqualTo(request.TrackId));
            Assert.That(season.NextTrackIndex, Is.Zero, "Restart does not advance the season.");
            AssertTransientFlagsReset();
        }
        Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo(selectedTrack));
    }

    [Test]
    public void ClearedLaunches_ReplaceStaleManagerModesWithoutChangingFreeRaceSelection()
    {
        var scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.US);
        RequestCareer(TeamId.CN);
        Set("tutorialScenario", scenario);
        Set("careerRaceLaunch", CareerRaceLaunchState.Current);
        TutorialLaunchState.Clear();
        CareerRaceLaunchState.Clear();
        string selectedTrack = TrackSelectionState.SelectedTrackId;
        TeamId[] selectedTeams = FreeRaceRosterState.SelectedTeams.ToArray();
        string[] selectedDrivers = selectedTeams.Select(FreeRaceRosterState.GetDriverId).ToArray();
        bool configured = FreeRaceRosterState.IsConfigured;
        bool thunderstorm = FreeRaceRosterState.IsThunderstorm;
        SeedStaleManagerState();

        Activate();

        Assert.That(manager.IsTutorialMode, Is.False);
        Assert.That(manager.IsCareerMode, Is.False);
        AssertTransientFlagsReset();
        Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo(selectedTrack));
        Assert.That(FreeRaceRosterState.SelectedTeams, Is.EqualTo(selectedTeams));
        Assert.That(selectedTeams.Select(FreeRaceRosterState.GetDriverId), Is.EqualTo(selectedDrivers));
        Assert.That(FreeRaceRosterState.IsConfigured, Is.EqualTo(configured));
        Assert.That(FreeRaceRosterState.IsThunderstorm, Is.EqualTo(thunderstorm));
    }

    private static CareerSeasonState RequestCareer(TeamId team)
    {
        var season = new CareerSeasonState();
        TeamId[] field = new[] { team }.Concat(FreeRaceRosterRules.AvailableTeams
            .Where(candidate => candidate != team).Take(3)).ToArray();
        Assert.That(CareerModeRules.TryStartSeason(season, team, field), Is.True);
        Assert.That(CareerRaceLaunchState.Request(season, "lifecycle-" + team), Is.True);
        return season;
    }

    private static string SerializeSeason(CareerSeasonState season)
    {
        Assert.That(CareerSaveCodec.TryToData(season, out CareerSaveData data), Is.True);
        return new JsonUtilityCareerSerializer().Serialize(data);
    }

    private void SeedStaleManagerState()
    {
        Set("careerResultRecorded", true);
        Set("careerInitializationFailure", "previous launch failure");
        Set("freeRaceRoster", new[] { new FreeRaceRosterEntry(TeamId.JP, "stale") });
    }

    private void AssertTransientFlagsReset()
    {
        Assert.That(Get("careerResultRecorded"), Is.False);
        Assert.That(Get("careerInitializationFailure"), Is.EqualTo(string.Empty));
        Assert.That(Get("freeRaceRoster"), Is.Null);
    }

    private void Activate() => typeof(MVPGameManager).GetMethod("ActivateRaceLaunchState", InstanceFields)
        .Invoke(manager, null);
    private object Get(string field) => typeof(MVPGameManager).GetField(field, InstanceFields).GetValue(manager);
    private void Set(string field, object value) => typeof(MVPGameManager).GetField(field, InstanceFields)
        .SetValue(manager, value);
}

/// <summary>Restart selection and ordered teardown with fixture-owned I/O only.</summary>
public sealed class RaceRestartAdapterTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly Dictionary<FieldInfo, object> savedLaunchFields = new Dictionary<FieldInfo, object>();
    private UnityEngine.GameObject root;
    private MVPGameManager manager;
    private UnityEngine.Random.State savedRandom;

    private static IEnumerable<TestCaseData> TutorialCases()
    {
        foreach (TeamId team in FreeRaceRosterRules.AvailableTeams)
            foreach (TutorialRunPhase phase in new[] { TutorialRunPhase.Guided,
                         TutorialRunPhase.Practice, TutorialRunPhase.Completed })
                yield return new TestCaseData(team, phase);
    }

    private static IEnumerable<TestCaseData> CareerCases()
    {
        foreach (TeamId team in FreeRaceRosterRules.AvailableTeams)
            foreach (bool recorded in new[] { false, true })
                yield return new TestCaseData(team, recorded);
    }

    [SetUp]
    public void SetUp()
    {
        savedRandom = UnityEngine.Random.state;
        foreach (Type type in new[] { typeof(TutorialLaunchState), typeof(CareerRaceLaunchState) })
            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                if (!field.IsInitOnly) savedLaunchFields.Add(field, field.GetValue(null));
        TutorialLaunchState.Clear();
        CareerRaceLaunchState.Clear();
        root = new UnityEngine.GameObject("RaceRestartAdapterTest");
        root.SetActive(false);
        manager = root.AddComponent<MVPGameManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        foreach (var item in savedLaunchFields) item.Key.SetValue(null, item.Value);
        savedLaunchFields.Clear();
        UnityEngine.Random.state = savedRandom;
    }

    [TestCaseSource(nameof(TutorialCases))]
    public void TutorialRestartTakesPrecedenceOverRecordedCareerWithoutChangingState(
        TeamId team, TutorialRunPhase phase)
    {
        var director = new TutorialRuntimeDirector(TutorialScenarioDefinition.CreateTeamSpecialty(team),
            phase != TutorialRunPhase.Guided);
        if (phase == TutorialRunPhase.Completed)
            Assert.That(director.CompletePracticeLap(out _), Is.True);
        Set("tutorialDirector", director);
        var season = SeedCareer(team, true);
        string json = Serialize(season);
        var request = CareerRaceLaunchState.Current;
        var trace = new List<string>();

        Dispatch(trace);

        Assert.That(trace, Is.EqualTo(new[] { phase == TutorialRunPhase.Guided ? "guided" : "practice" }));
        Assert.That(director.Phase, Is.EqualTo(phase), "Selection itself must not mutate the director.");
        Assert.That(CareerRaceLaunchState.Current, Is.SameAs(request));
        Assert.That(Serialize(season), Is.EqualTo(json));
        Assert.That(Get("careerResultRecorded"), Is.True);
    }

    [TestCaseSource(nameof(CareerCases))]
    public void CareerRestartReturnsOnlyAfterRecordedSettlementAndPreservesRequest(TeamId team, bool recorded)
    {
        var season = SeedCareer(team, recorded);
        string before = Serialize(season);
        var request = CareerRaceLaunchState.Current;
        string track = TrackSelectionState.SelectedTrackId;
        var roster = FreeRaceRosterState.SelectedTeams.ToArray();
        var trace = new List<string>();

        Dispatch(trace);

        Assert.That(trace, Is.EqualTo(new[] { recorded ? "menu" : "runtime" }));
        Assert.That(Serialize(season), Is.EqualTo(before));
        Assert.That(CareerRaceLaunchState.Current, Is.SameAs(request));
        Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo(track));
        Assert.That(FreeRaceRosterState.SelectedTeams, Is.EqualTo(roster));
        Assert.That(Get("careerResultRecorded"), Is.EqualTo(recorded));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OrdinaryRestartIgnoresStaleResultFlag(bool recorded)
    {
        Set("careerResultRecorded", recorded);
        var trace = new List<string>();
        Dispatch(trace);
        Assert.That(trace, Is.EqualTo(new[] { "runtime" }));
    }

    [TestCase(0)]
    [TestCase(11)]
    public void RebuildRetiresOwnedControllersBeforeClearInitializeGuideAndLoop(int count)
    {
        HUDUI hud = root.AddComponent<HUDUI>();
        var panel = new UnityEngine.GameObject("FixtureGameOver");
        panel.transform.SetParent(root.transform);
        hud.gameOverPanel = panel;
        manager.hudUI = hud;
        var bindings = (Dictionary<PlayerState, AIController>)Get("aiControllers");
        for (int i = 0; i < count; i++)
            bindings.Add(new PlayerState("old-" + i, true, 0, 1), root.AddComponent<AIController>());
        var oldControllers = root.GetComponents<AIController>();
        var trace = new List<string>();
        var next = new PlayerState("next", true, 0, 1);
        Action<AIController> retire = controller =>
        {
            Assert.That(oldControllers, Does.Contain(controller));
            Assert.That(bindings.Count, Is.EqualTo(count), "Clear follows all retirement requests.");
            Assert.That(panel.activeSelf, Is.True);
            trace.Add("retire");
            UnityEngine.Object.DestroyImmediate(controller);
        };
        Action initialize = () =>
        {
            Assert.That(bindings, Is.Empty);
            Assert.That(root.GetComponents<AIController>(), Is.Empty);
            Assert.That(panel.activeSelf, Is.False);
            trace.Add("initialize");
            bindings.Add(next, root.AddComponent<AIController>());
        };
        Action guide = () => { Assert.That(bindings.ContainsKey(next), Is.True); trace.Add("guide"); };
        Action loop = () => trace.Add("loop");

        Rebuild(retire, initialize, guide, loop);

        Assert.That(trace, Is.EqualTo(Enumerable.Repeat("retire", count)
            .Concat(new[] { "initialize", "guide", "loop" })));
        Assert.That(bindings.Count, Is.EqualTo(1));
        Assert.That(root.GetComponents<AIController>().Length, Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void RebuildFailureStopsRemainingAdaptersWithoutInventingRollback(int failureStage)
    {
        var old = root.AddComponent<AIController>();
        var bindings = (Dictionary<PlayerState, AIController>)Get("aiControllers");
        bindings.Add(new PlayerState("old", true, 0, 1), old);
        var trace = new List<int>();
        Action<int> step = index =>
        {
            trace.Add(index);
            if (index == failureStage) throw new InvalidOperationException("fixture restart failure");
        };

        var exception = Assert.Throws<TargetInvocationException>(() => Rebuild(
            controller => step(0), () => step(1), () => step(2), () => step(3)));

        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        Assert.That(trace, Is.EqualTo(Enumerable.Range(0, failureStage + 1)));
        Assert.That(bindings.Count, Is.EqualTo(failureStage == 0 ? 1 : 0));
        Assert.That(old, Is.Not.Null, "Recording retirement does not destroy any live editor object.");
    }

    private CareerSeasonState SeedCareer(TeamId team, bool recorded)
    {
        var season = new CareerSeasonState();
        var field = new[] { team }.Concat(FreeRaceRosterRules.AvailableTeams
            .Where(candidate => candidate != team).Take(3)).ToArray();
        Assert.That(CareerModeRules.TryStartSeason(season, team, field), Is.True);
        Assert.That(CareerRaceLaunchState.Request(season, "restart-" + team), Is.True);
        Set("careerRaceLaunch", CareerRaceLaunchState.ActivateRequested());
        Set("careerResultRecorded", recorded);
        return season;
    }
    private void Dispatch(List<string> trace) => Invoke("DispatchRaceRestart",
        (Action)(() => trace.Add("guided")), (Action)(() => trace.Add("practice")),
        (Action)(() => trace.Add("menu")), (Action)(() => trace.Add("runtime")));
    private void Rebuild(Action<AIController> retire, Action initialize, Action guide, Action loop) =>
        Invoke("RebuildRaceRuntime", retire, initialize, guide, loop);
    private void Invoke(string name, params object[] args) => typeof(MVPGameManager)
        .GetMethod(name, PrivateInstance).Invoke(manager, args);
    private object Get(string name) => typeof(MVPGameManager).GetField(name, PrivateInstance).GetValue(manager);
    private void Set(string name, object value) => typeof(MVPGameManager).GetField(name, PrivateInstance)
        .SetValue(manager, value);
    private static string Serialize(CareerSeasonState season)
    {
        Assert.That(CareerSaveCodec.TryToData(season, out CareerSaveData data), Is.True);
        return new JsonUtilityCareerSerializer().Serialize(data);
    }
}
