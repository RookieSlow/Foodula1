using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Real participant/result/HUD adapters, with JSON career persistence in memory.
/// Terminal race states are fixtures: this is not GameLoop, PlayMode or disk evidence.
/// The host stays inactive; profile cache references and Unity randomness are restored.
/// </summary>
public sealed partial class RaceResultSettlementAdapterTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly TeamId[] Teams =
        { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP };
    private GameObject root;
    private MVPGameManager manager;
    private GameConfigSO config;
    private HUDUI hud;
    private CardHandUI hand;
    private RaceSession session;
    private UnityEngine.Random.State randomState;
    private Dictionary<TeamId, TechTreeState> cache;
    private Dictionary<TeamId, TechTreeState> savedCache;

    [SetUp]
    public void SetUp()
    {
        randomState = UnityEngine.Random.state;
        cache = (Dictionary<TeamId, TechTreeState>)typeof(TechTreeProfileStore)
            .GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        savedCache = new Dictionary<TeamId, TechTreeState>(cache);
        foreach (TeamId team in Teams) cache[team] = new TechTreeState(team, 77);

        root = new GameObject("Result settlement adapter", typeof(RectTransform));
        root.SetActive(false);
        manager = root.AddComponent<MVPGameManager>();
        manager.trackManager = root.AddComponent<TrackManager>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.enableTechTree = true;
        config.ensurePlayerAttackTrickInOpeningHand = false;
        manager.config = config;
        hud = Child("HUD", root.transform, typeof(HUDUI)).GetComponent<HUDUI>();
        hud.gameOverPanel = Child("GameOverPanel", hud.transform, typeof(Image));
        hud.gameOverPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 600f);
        hud.gameOverText = Child("GameOverText", hud.gameOverPanel.transform,
            typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        manager.hudUI = hud;
        hand = Child("Hand", root.transform, typeof(CardHandUI)).GetComponent<CardHandUI>();
        hand.gearSelectionPanel = Child("GearSelection", hand.transform);
        hand.playCardsButton = Child("PlayCards", hand.transform, typeof(Button)).GetComponent<Button>();
        manager.cardHandUI = hand;
        ResetPresentation();
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        if (config != null) Object.DestroyImmediate(config);
        cache.Clear();
        foreach (var entry in savedCache) cache.Add(entry.Key, entry.Value);
        UnityEngine.Random.state = randomState;
    }

    [TestCase(TeamId.IT, "IT", 11250)]
    [TestCase(TeamId.IT, "US", 7500)]
    [TestCase(TeamId.UK, "UK", 5000)]
    public void NormalResultFillsRemainingPlacesBeforeRewardsAndHud(
        TeamId team, string country, int humanReward)
    {
        if (team == TeamId.IT) cache[team].activeNodeIds.Add("it-l3-cavallino-rampante");
        CreateParticipants(team);
        SetTrack(new TrackConfig { country = country });
        PlayerState human = session.Players[0];
        human.hasFinished = true;
        Assert.That(session.AssignFinish(human), Is.EqualTo(1));
        session.Players[1].lap = 2;
        session.Players[1].position = 5;
        session.Players[2].isBlown = true;
        session.Players[3].lap = 2;
        session.Players[3].position = 4;
        int[] rpBefore = session.Players.Select(p => p.techState.rpBalance).ToArray();
        int[] xpBefore = session.Players.Select(p => p.driverXp).ToArray();
        var calls = new List<string>();

        Complete(
            state =>
            {
                Assert.That(state, Is.SameAs(human.techState));
                Assert.That(state.rpBalance, Is.EqualTo(77 + humanReward));
                Assert.That(session.Players[1].finishOrder, Is.EqualTo(2));
                Assert.That(session.Players[3].finishOrder, Is.EqualTo(3));
                Assert.That(session.Players[2].finishOrder, Is.Zero);
                Assert.That(session.Players.Select(p => p.driverXp), Is.EqualTo(xpBefore));
                Assert.That(hud.gameOverPanel.activeSelf, Is.False);
                calls.Add("RP");
            },
            (id, xp) =>
            {
                Assert.That(id, Is.EqualTo(human.DriverProfile.Id));
                Assert.That(xp, Is.EqualTo(human.driverXp));
                Assert.That(session.Players[1].techState.rpBalance,
                    Is.EqualTo(rpBefore[1] + TechTreeRules.CalculateRaceRP(2)));
                calls.Add("XP");
            },
            () => throw new InvalidOperationException("Normal result must not open career storage."));

        Assert.That(calls, Is.EqualTo(new[] { "RP", "XP" }));
        Assert.That(human.driverXp, Is.EqualTo(xpBefore[0] + DriverProgression.CalculateRaceXp(
            1, human.DriverProfile.TalentMultiplier, human.DriverProfile.Team)));
        Assert.That(session.Players[2].driverXp, Is.EqualTo(xpBefore[2]), "DNF grants no XP.");
        Assert.That(hud.gameOverText.text, Does.Contain("【冠军】"));
        Assert.That(hud.gameOverText.text, Does.Contain("+" + humanReward + " RP"));
        Assert.That(GetField<bool>("careerResultRecorded"), Is.False);
        AssertPresented();
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void ActualGameOverEntryUsesInjectedStorageAndNeverOpensCareerForNormalRace(TeamId team)
    {
        CreateParticipants(team);
        SetTrack(new TrackConfig { country = "UK" });
        PlayerState human = session.Human;
        human.hasFinished = true;
        session.AssignFinish(human);
        int rp = human.techState.rpBalance;
        int xp = human.driverXp;
        var calls = new List<string>();
        SetField("saveRaceTechState", (Action<TechTreeState>)(state =>
        {
            Assert.That(state, Is.SameAs(human.techState));
            Assert.That(state.rpBalance, Is.EqualTo(rp + TechTreeRules.CalculateRaceRP(1)));
            Assert.That(human.driverXp, Is.EqualTo(xp));
            calls.Add("RP");
        }));
        SetField("saveRaceDriverXp", (Action<string, int>)((id, value) =>
        {
            Assert.That(id, Is.EqualTo(human.DriverProfile.Id));
            Assert.That(value, Is.GreaterThan(xp));
            calls.Add("XP");
        }));
        SetField("createRaceCareerRepository", (Func<CareerRepository>)(() =>
            throw new InvalidOperationException("Normal game-over must not open career storage.")));

        typeof(MVPGameManager).GetMethod("ShowGameOver", PrivateInstance).Invoke(manager, null);

        Assert.That(calls, Is.EqualTo(new[] { "RP", "XP" }));
        AssertPresented();
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void CareerEightResultsUseRealParticipantsHudAndCareerOnlyJson(TeamId team)
    {
        MemoryCareerStore store;
        CareerRepository repository = CreateSeason(team, out store);
        for (int race = 0; race < CareerModeRules.RaceCount; race++)
        {
            CareerSeasonState before = repository.Load().State;
            if (before.Phase == CareerPhase.SummerBreak)
            {
                var summer = new CareerModeService(repository);
                Assert.That(summer.TryConfirmSummerBreak(before.ActiveTechSnapshot), Is.True);
                before = repository.Load().State;
            }
            CareerRaceLaunchRequest launch = CreateLaunch(before, "adapter-" + race);
            CreateParticipants(team, launch);
            SetTerminalCareerResults(race % 2 == 1);
            string snapshotBefore = SnapshotFingerprint(launch.TechSnapshot);
            int[] rp = session.Players.Select(p => p.techState.rpBalance).ToArray();
            int[] xp = session.Players.Select(p => p.driverXp).ToArray();
            int writes = store.SuccessfulWrites;

            CompleteCareer(repository);

            CareerSeasonState after = repository.Load().State;
            Assert.That(after.NextTrackIndex, Is.EqualTo(race + 1));
            Assert.That(after.RaceResults[race].ResultId, Is.EqualTo(launch.ResultId));
            Assert.That(after.RaceResults[race].TrackId, Is.EqualTo(launch.TrackId));
            CareerCompetitorResult humanResult = after.RaceResults[race].Standings
                .Single(entry => entry.TeamId == team);
            Assert.That(humanResult.DidNotFinish, Is.EqualTo(race % 2 == 1));
            Assert.That(humanResult.FinishPosition, Is.EqualTo(race % 2 == 1 ? 0 : 1));
            Assert.That(CareerModeRules.GetStandings(after).Single(entry => entry.TeamId == team).Points,
                Is.EqualTo(10 * ((race + 2) / 2)));
            Assert.That(store.SuccessfulWrites, Is.EqualTo(writes + 1));
            Assert.That(GetField<bool>("careerResultRecorded"), Is.True);
            Assert.That(session.Players.Select(p => p.techState.rpBalance), Is.EqualTo(rp));
            Assert.That(session.Players.Select(p => p.driverXp), Is.EqualTo(xp));
            Assert.That(SnapshotFingerprint(launch.TechSnapshot),
                Is.EqualTo(snapshotBefore), "Launch technology is not a writable free-race profile.");
            Assert.That(hud.gameOverText.text, Does.Contain("生涯赛果已保存："));
            Assert.That(hud.gameOverText.text, Does.Not.Contain("RP 奖励:"));
            if (race == 3) Assert.That(hud.gameOverText.text, Does.Contain("已进入夏休"));
            if (race == 7) Assert.That(hud.gameOverText.text, Does.Contain("八站生涯已完成"));
            AssertPresented();

            string savedJson = store.Json;
            Complete(ForbiddenTechSave, ForbiddenXpSave,
                () => throw new InvalidOperationException("Recorded result must not reopen storage."));
            Assert.That(store.Json, Is.EqualTo(savedJson));
            Assert.That(store.SuccessfulWrites, Is.EqualTo(writes + 1));
            Assert.That(hud.gameOverText.text, Does.Contain("生涯赛果已保存。返回主菜单"));
        }
        Assert.That(repository.Load().State.Phase, Is.EqualTo(CareerPhase.Completed));
        Assert.That(store.SuccessfulWrites, Is.EqualTo(10), "Initial save, eight results and one summer confirmation.");
        Assert.That(store.KeysWritten, Has.All.EqualTo(CareerRepository.SaveKey));
        Assert.That(cache[team].rpBalance, Is.EqualTo(77), "Career must not touch free-race cache.");
    }

    [Test]
    public void FailedCareerWriteShowsRejectionThenRetriesSameResultExactlyOnce()
    {
        MemoryCareerStore store;
        CareerRepository repository = CreateSeason(TeamId.UK, out store);
        CareerRaceLaunchRequest launch = CreateLaunch(repository.Load().State, "adapter-retry");
        CreateParticipants(TeamId.UK, launch);
        SetTerminalCareerResults(false);
        string originalJson = store.Json;
        store.FailWrites = true;

        CompleteCareer(repository);

        Assert.That(GetField<bool>("careerResultRecorded"), Is.False);
        Assert.That(store.Json, Is.EqualTo(originalJson));
        Assert.That(repository.Load().State.NextTrackIndex, Is.Zero);
        Assert.That(hud.gameOverText.text, Does.Contain("保存失败，进度未推进"));
        AssertPresented();
        store.FailWrites = false;
        CompleteCareer(repository);
        Assert.That(GetField<bool>("careerResultRecorded"), Is.True);
        Assert.That(repository.Load().State.NextTrackIndex, Is.EqualTo(1));
        Assert.That(store.SuccessfulWrites, Is.EqualTo(2));
        CompleteCareer(repository);
        Assert.That(store.SuccessfulWrites, Is.EqualTo(2));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void WrongTrackOrStaleCareerCannotAdvanceOrFallThroughToNormalRewards(bool wrongTrack)
    {
        MemoryCareerStore store;
        CareerRepository repository = CreateSeason(TeamId.DE, out store);
        CareerRaceLaunchRequest launch = CreateLaunch(repository.Load().State, "adapter-rejected");
        CreateParticipants(TeamId.DE, launch);
        SetTerminalCareerResults(false);
        if (wrongTrack) SetTrack(new TrackConfig { trackId = "fallback_42" });
        else Assert.That(CareerRaceSettlement.TryRecord(launch, launch.TrackId, session.Players,
            repository, out _, out _), Is.True);
        string json = store.Json;
        int writes = store.SuccessfulWrites;
        int[] rp = session.Players.Select(p => p.techState.rpBalance).ToArray();
        int[] xp = session.Players.Select(p => p.driverXp).ToArray();

        CompleteCareer(repository);

        Assert.That(store.Json, Is.EqualTo(json));
        Assert.That(store.SuccessfulWrites, Is.EqualTo(writes));
        Assert.That(GetField<bool>("careerResultRecorded"), Is.False);
        Assert.That(session.Players.Select(p => p.techState.rpBalance), Is.EqualTo(rp));
        Assert.That(session.Players.Select(p => p.driverXp), Is.EqualTo(xp));
        Assert.That(hud.gameOverText.text, Does.Contain(wrongTrack ? "阵容或完赛状态无效" : "存档已变化"));
        AssertPresented();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TutorialResultTakesPrecedenceWithoutOpeningAnyStorage(bool dormantCareer)
    {
        CareerRaceLaunchRequest launch = null;
        if (dormantCareer)
        {
            MemoryCareerStore store;
            launch = CreateLaunch(CreateSeason(TeamId.UK, out store).Load().State, "dormant");
        }
        CreateParticipants(TeamId.UK, launch, TutorialScenarioDefinition.CreateLeMansUk());
        SetTerminalCareerResults(false);

        Complete(ForbiddenTechSave, ForbiddenXpSave,
            () => throw new InvalidOperationException("Tutorial must not open career storage."));

        Assert.That(GetField<bool>("careerResultRecorded"), Is.False);
        Assert.That(hud.gameOverText.text, Does.Contain("本次练习未完成"));
        Assert.That(hud.gameOverText.text, Does.Contain("不发放 RP、车手 XP"));
        Assert.That(session.Players.Select(p => p.driverXp), Is.All.EqualTo(0));
        AssertPresented();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NormalStorageExceptionStopsBeforePresentationWithoutRollback(bool failXp)
    {
        CreateParticipants(TeamId.UK);
        SetTerminalCareerResults(false);
        PlayerState human = session.Players[0];
        var failure = new InvalidOperationException("injected result save failure");

        TargetInvocationException raised = Assert.Throws<TargetInvocationException>(() => Complete(
            state => { if (!failXp) throw failure; },
            (id, xp) => throw failure,
            () => throw new InvalidOperationException("Wrong storage branch.")));

        Assert.That(raised.InnerException, Is.SameAs(failure));
        Assert.That(human.techState.rpBalance, Is.EqualTo(5077));
        Assert.That(human.driverXp, failXp ? Is.GreaterThan(137) : Is.EqualTo(137));
        Assert.That(hud.gameOverPanel.activeSelf, Is.False);
        Assert.That(hand.gearSelectionPanel.activeSelf, Is.True);
        Assert.That(hand.playCardsButton.gameObject.activeSelf, Is.True);
    }

    private void CreateParticipants(TeamId team, CareerRaceLaunchRequest launch = null,
        TutorialScenarioDefinition tutorial = null, DriverProfile selectedDriver = null)
    {
        session = new RaceSession(new SystemRandomSource(103));
        SetField("session", session);
        SetField("tutorialScenario", tutorial);
        SetField("careerRaceLaunch", launch);
        SetField("careerResultRecorded", false);
        SetTrack(TrackDataLoader.LoadConfig(launch?.TrackId ?? "silverstone_afternoon_tea"));
        List<RaceParticipantPlan> plans = RaceParticipantPlanBuilder.Build(tutorial, launch, null,
            selectedDriver ?? DriverCatalog.GetDefaultForTeam(team), Teams.Where(t => t != team).Take(3).ToArray(),
            3, id => 137);
        foreach (RaceParticipantPlan plan in plans)
        {
            PlayerState player = (PlayerState)typeof(MVPGameManager)
                .GetMethod("CreateParticipantForRace", PrivateInstance)
                .Invoke(manager, new object[] { plan, 0 });
            Assert.That(player.teamId, Is.EqualTo(plan.Team));
            Assert.That(player.driverId, Is.EqualTo(plan.Driver.Id));
            Assert.That(player.deck, Is.Not.Null);
            Assert.That(player.deck.Hand, Is.Not.Empty);
        }
        Assert.That(session.Players.Count, Is.EqualTo(plans.Count));
        ResetPresentation();
    }

    private void SetTerminalCareerResults(bool humanDnf)
    {
        for (int i = 0; i < session.Players.Count; i++)
        {
            PlayerState player = session.Players[i];
            player.isBlown = humanDnf && i == 0;
            player.hasFinished = !player.isBlown;
            if (!player.isBlown) session.AssignFinish(player);
        }
    }

    private CareerRepository CreateSeason(TeamId team, out MemoryCareerStore store)
    {
        store = new MemoryCareerStore();
        var repository = new CareerRepository(store, new JsonUtilityCareerSerializer(),
            state => CareerTechSnapshotMapper.IsSeasonValidForDatabase(
                state, TechTreeDatabaseFactory.CreateDefault()));
        var state = new CareerSeasonState();
        Assert.That(CareerModeRules.TryStartSeason(state, team,
            new[] { team }.Concat(Teams.Where(t => t != team).Take(3)).ToArray()), Is.True);
        Assert.That(repository.Save(state), Is.True);
        return repository;
    }

    private static CareerRaceLaunchRequest CreateLaunch(CareerSeasonState state, string id)
    {
        Assert.That(CareerRaceLaunchRequest.TryCreate(state, id, out CareerRaceLaunchRequest launch), Is.True);
        return launch;
    }

    private static string SnapshotFingerprint(CareerTechSnapshot snapshot)
        => $"{snapshot.TeamId}|{snapshot.RpBalance}|{snapshot.SunNeverSetsTarget}|" +
           string.Join(",", snapshot.UnlockedNodeIds) + "|" + string.Join(",", snapshot.ActiveNodeIds);

    private void CompleteCareer(CareerRepository repository)
        => Complete(ForbiddenTechSave, ForbiddenXpSave, () => repository);

    private void Complete(Action<TechTreeState> saveTech, Action<string, int> saveXp,
        Func<CareerRepository> createRepository)
        => typeof(MVPGameManager).GetMethod("CompleteRaceWithPersistence", PrivateInstance)
            .Invoke(manager, new object[] { saveTech, saveXp, createRepository });

    private void AssertPresented()
    {
        Assert.That(hud.gameOverPanel.activeSelf, Is.True);
        Assert.That(hud.gameOverScrollRect, Is.Not.Null);
        Assert.That(hand.gearSelectionPanel.activeSelf, Is.False);
        Assert.That(hand.playCardsButton.gameObject.activeSelf, Is.False);
    }

    private void ResetPresentation()
    {
        hud.gameOverPanel.SetActive(false);
        hand.gearSelectionPanel.SetActive(true);
        hand.playCardsButton.gameObject.SetActive(true);
    }

    private void SetTrack(TrackConfig track)
        => typeof(TrackManager).GetProperty("LoadedTrackConfig").SetValue(manager.trackManager, track);

    private void SetField(string name, object value)
        => typeof(MVPGameManager).GetField(name, PrivateInstance).SetValue(manager, value);

    private T GetField<T>(string name)
        => (T)typeof(MVPGameManager).GetField(name, PrivateInstance).GetValue(manager);

    private static void ForbiddenTechSave(TechTreeState state) => Assert.Fail("No normal RP write in this mode.");
    private static void ForbiddenXpSave(string id, int xp) => Assert.Fail("No normal XP write in this mode.");

    private static GameObject Child(string name, Transform parent, params Type[] components)
    {
        var child = new GameObject(name, new[] { typeof(RectTransform) }.Concat(components).ToArray());
        child.transform.SetParent(parent, false);
        return child;
    }

    private sealed class MemoryCareerStore : ICareerKeyValueStore
    {
        public string Json;
        public bool FailWrites;
        public int SuccessfulWrites;
        public readonly List<string> KeysWritten = new List<string>();
        public bool HasKey(string key) => key == CareerRepository.SaveKey && Json != null;
        public string GetString(string key)
        {
            Assert.That(key, Is.EqualTo(CareerRepository.SaveKey));
            return Json;
        }
        public bool TrySetAndSave(string key, string value)
        {
            KeysWritten.Add(key);
            Assert.That(key, Is.EqualTo(CareerRepository.SaveKey));
            if (FailWrites) return false;
            Json = value;
            SuccessfulWrites++;
            return true;
        }
        public bool TryDeleteAndSave(string key)
        {
            Assert.Fail("Result settlement must never delete a save.");
            return false;
        }
    }
}
