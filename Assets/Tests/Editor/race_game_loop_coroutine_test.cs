using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Native Unity coroutine scheduler, actual GameLoop/input/AI/cleanup, synthetic
/// straight track and headless cars, plus authored-track Start/roster input gates.
/// Bounded turn/startup and controlled full-race fixtures share safe native setup.
/// Not visual/real-storage evidence; display, progression and telemetry are isolated.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string StateKey = "Foodula1.Tests.GameLoop.";
    private GameObject host;
    private MVPGameManager manager;
    private GameConfigSO config;
    private RaceSession session;
    private readonly List<string> messages = new List<string>();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // The runner reconstructs this iterator after EnterPlayMode reloads the
        // domain. Do not repeat pre-entry scene checks or re-arm evidence then.
        if (!SessionState.GetBool(StateKey + "prepared", false))
        {
            Assert.That(Application.isPlaying, Is.False, "Never interrupt an existing play session.");
            Assert.That(EditorApplication.isCompiling, Is.False);
            Scene original = SceneManager.GetActiveScene();
            // Unity Test Framework 1.1.33 EditModeLauncher adds an empty active scene
            // beside the saved workspace. Accept only that exact runner-owned shape.
            if (SceneManager.sceneCount == 2)
            {
                Assert.That(original.path, Is.Empty, "Only the runner's untitled scene may be replaced.");
                Assert.That(original.rootCount, Is.Zero, "Never discard objects in an untitled scene.");
                original = SceneManager.GetSceneAt(0);
                Assert.That(original.path, Is.Not.Empty, "The workspace scene must be saved.");
            }
            else Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "Do not replace a multi-scene workspace.");
            Assert.That(original.isDirty, Is.False, "Never replace unsaved scene changes.");
            SessionState.SetString(StateKey + "scene", original.path);
            SessionState.SetString(StateKey + "savedLogRoot", SessionState.GetString(
                PlaytestTelemetryTestIsolationTests.OverrideKey, string.Empty));
            SessionState.SetString(StateKey + "logRoot", Path.Combine(
                PlaytestTelemetryTestIsolationTests.AllowedRoot, "game-loop-" + Guid.NewGuid().ToString("N")));
            SessionState.SetBool(StateKey + "prepared", true);
            SessionState.SetString(PlaytestTelemetryTestIsolationTests.OverrideKey,
                SessionState.GetString(StateKey + "logRoot", string.Empty));
            RaceCoroutineTestEvidence.Arm();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        yield return new EnterPlayMode();
        Assert.That(Application.isPlaying, Is.True);
        string root = SessionState.GetString(StateKey + "logRoot", string.Empty);
        Assert.That(PlaytestTelemetryService.CurrentSessionDirectory,
            Does.StartWith(root + Path.DirectorySeparatorChar), "Bootstrap logging must already be isolated.");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        DestroyRace();
        try
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        finally
        {
            if (SessionState.GetBool(StateKey + "prepared", false))
            {
                SessionState.SetString(PlaytestTelemetryTestIsolationTests.OverrideKey,
                    SessionState.GetString(StateKey + "savedLogRoot", string.Empty));
                string original = SessionState.GetString(StateKey + "scene", string.Empty);
                SessionState.EraseBool(StateKey + "prepared");
                SessionState.EraseString(StateKey + "scene");
                SessionState.EraseString(StateKey + "savedLogRoot");
                SessionState.EraseString(StateKey + "logRoot");
                if (!EditorApplication.isPlaying)
                {
                    if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }
    }

    [UnityTest]
    public IEnumerator RealGameLoopSchedulesTwoTurnsAcrossTeamsAndFinalSprintRecoveryOrRetirement()
    {
        foreach (TeamId team in new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP })
        {
            CreateRace(team);
            yield return DriveFirstTurn(false);
            yield return WaitFor(() => Turn == 2 && manager.TutorialInputPhase == "gear", team + " second gear");
            Assert.That(session.Human.playedSpeedCardsThisTurn, Is.Empty);
            Assert.That(session.Human.deck.CountTemporaryHeatOutsideEngine(), Is.Zero);
            Assert.That(session.Human.position, Is.GreaterThan(0));
            Assert.That(session.Human.positionAtTurnStart, Is.EqualTo(session.Human.position));
            yield return ConfirmTurnCards();
            yield return WaitFor(() => Turn == 3 && manager.TutorialInputPhase == "gear", team + " third gear");
            Assert.That(session.Players[1].position, Is.GreaterThan(101), "AI must actually move in both turns.");
            Assert.That(session.Human.deck.DiscardPileCount, Is.EqualTo(2));
            AssertActiveAndHeatConserved();
            TestContext.WriteLine("Native GameLoop completed two turns: " + team);
            DestroyRace();
            yield return null;
        }

        // Four independent races: Tier 1 recovery, Tier 3 uninterrupted next input,
        // and both tiers retiring at the real cleanup cost's configured spin cap.
        foreach (int level in new[] { 3, 7 })
        foreach (int spins in new[] { 0, 2 })
        {
            CreateRace(TeamId.IT, level, spins);
            yield return DriveFirstTurn(true);
            bool retired = spins == 2;
            if (retired || level == 3)
            {
                yield return WaitFor(() => Turn == 2 && session.Players[1].playedSpeedCardsThisTurn.Count > 0,
                    "AI-only recovery/retirement turn");
                Assert.That(manager.TutorialInputPhase, Is.EqualTo("none"), "Inactive human must not reopen input.");
                Assert.That(session.Human.totalMovementThisTurn, Is.Zero);
                Assert.That(session.Human.playedSpeedCardsThisTurn, Is.Empty);
                Assert.That(messages.Count(m => m.Contains("sits out this turn")), Is.EqualTo(retired ? 0 : 1));
            }
            else
            {
                yield return WaitFor(() => Turn == 2 && manager.TutorialInputPhase == "gear", "Tier 3 second input");
            }
            Assert.That(session.Human.spinCounter, Is.EqualTo(spins + 1));
            Assert.That(session.Human.isBlown, Is.EqualTo(retired));
            Assert.That(session.Human.skipNextTurn, Is.False); // Recovery consumed; retirement creates no new skip.
            Assert.That(session.Human.driverSkill.ActivatedThisTurn, Is.False, "GameLoop must reset activation next turn.");
            Assert.That(messages.Count(m => m.Contains("最后冲刺代价")), Is.EqualTo(1));
            Assert.That(session.Human.deck.CountTemporaryHeatOutsideEngine(), Is.Zero);
            AssertActiveAndHeatConserved();
            TestContext.WriteLine($"Native GameLoop cleanup/next-turn exclusion: level={level}; spins={spins}; retired={retired}");
            DestroyRace();
            yield return null;
        }

        // Genuine Unity Start lifecycle, not reflection/manual IEnumerator driving.
        // The regular and 2/4/6/12-car roster routes use authored Silverstone nodes.
        foreach (int rosterSize in new[] { 0, 2, 4, 6, 12 })
        {
            using (var launch = new RaceStartupTestScope())
            {
                CreateStartupRace(rosterSize, out List<string> reads);
                LogAssert.Expect(LogType.Warning, Camera.main == null
                    ? "[MVPGameManager] Main camera not found; race camera setup skipped."
                    : "[RaceCameraController] Race canvas not found; minimap UI skipped.");
                manager.enabled = true;
                yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "actual Start gear");
                session = Get<RaceSession>(manager, "session");
                int count = rosterSize == 0 ? 4 : rosterSize;
                Assert.That(session.Players.Count, Is.EqualTo(count));
                Assert.That(session.Human.driverXp, Is.EqualTo(137));
                Assert.That(session.Human.teamId, Is.EqualTo(TeamId.UK));
                Assert.That(reads, Is.EqualTo(new[] { "DISPLAY", "XP:" + session.Human.driverId, "TECH:UK" }));
                Assert.That(Get<Dictionary<PlayerState, AIController>>(manager, "aiControllers").Keys,
                    Is.EquivalentTo(session.Players.Skip(1)), "Every real startup opponent must be bound.");
                Assert.That(session.Players.Skip(1).All(p => p.isAI && p.driverXp == 0), Is.True);
                Assert.That(session.Players.Select(p => p.driverId).Distinct().Count(), Is.EqualTo(count));
                Assert.That(session.Players.All(p => p.deck.Hand.Count > 0 && !p.hasFinished && !p.isBlown), Is.True);
                Assert.That(manager.trackManager.Nodes.Count, Is.EqualTo(77), "Use current authored Silverstone track data.");
                string logPath = manager.LastRaceLogPath;
                Assert.That(logPath, Does.StartWith(SessionState.GetString(StateKey + "logRoot", string.Empty)));
                // Opening the real selected-card path proves startup hand/UI/input wiring.
                manager.OnGearButtonClicked(1);
                manager.OnConfirmGearClicked();
                yield return WaitFor(() => manager.TutorialInputPhase == "cards", "startup card gate");
                CardUI speed = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(c => c.cardData.IsSpeed);
                speed.SetSelectedWithoutNotify(true);
                manager.OnPlayCardsButtonClicked();
                Assert.That(session.Human.playedSpeedCardsThisTurn.Count, Is.EqualTo(1));
                manager.OnPlayCardsButtonClicked();
                Assert.That(manager.TutorialInputPhase, Is.EqualTo("none"), "Card confirmation must close its input gate.");
                DestroyRace(); // Stop before movement/settlement: this is bounded startup acceptance only.
                string contents = File.ReadAllText(logPath);
                Assert.That(contents, Does.Contain("track_id=silverstone_afternoon_tea").And.Contain("[TURN_START] turn=1"));
                Assert.That(contents, Does.Not.Contain("[CAREER_SETUP]").And.Not.Contain("[TUTORIAL_SETUP]"));
                if (rosterSize > 0) Assert.That(contents, Does.Contain("[FREE_RACE_SETUP] field=" + rosterSize));
                else Assert.That(contents, Does.Not.Contain("[FREE_RACE_SETUP]"));
                TestContext.WriteLine($"Native Unity Start/roster/card input: field={count}; configuredRoster={rosterSize}; xp=137; authoredNodes=77");
                yield return null;
            }
        }
    }

    private void CreateStartupRace(int rosterSize, out List<string> reads)
    {
        CreateRace(TeamId.UK);
        foreach (AIController controller in host.GetComponents<AIController>()) Object.DestroyImmediate(controller);
        Get<Dictionary<PlayerState, AIController>>(manager, "aiControllers").Clear();
        Set(manager, "session", null);
        config.playerTeam = TeamId.UK;
        config.playerDriverId = DriverCatalog.GetDefaultForTeam(TeamId.UK).Id;
        config.aiTeams = new[] { TeamId.DE, TeamId.IT, TeamId.US };
        config.aiOpponentCount = 3;
        config.enableTechTree = true;
        config.ensurePlayerAttackTrickInOpeningHand = false;
        config.nodeDelay = 0;
        TrackConfig track = TrackDataLoader.LoadConfig("silverstone_afternoon_tea");
        typeof(TrackManager).GetProperty("LoadedTrackConfig").SetValue(manager.trackManager, track);
        Set(manager.trackManager, "nodes", TrackDataLoader.ConfigToNodes(track));
        if (rosterSize == 12) FreeRaceRosterState.InitializeThunderstorm(DriverCatalog.GetDefaultForTeam(TeamId.UK));
        else if (rosterSize > 0)
            foreach (TeamId team in FreeRaceRosterRules.AvailableTeams.Take(rosterSize))
                Assert.That(FreeRaceRosterState.TrySetTeamSelected(team, true), Is.True);
        var observed = new List<string>();
        reads = observed;
        Set(manager, "prepareRaceDisplay", (Action)(() => observed.Add("DISPLAY")));
        Set(manager, "loadRaceDriverXp", (Func<string, int>)(id => { observed.Add("XP:" + id); return 137; }));
        Set(manager, "loadRaceTechProfile", (Func<TeamId, TechTreeDatabase, TechTreeState>)((team, db) =>
        {
            observed.Add("TECH:" + team);
            return new TechTreeState(team, 77);
        }));
        Set(manager, "raceLogWriter", new RaceTestLogWriter(Path.Combine(
            SessionState.GetString(StateKey + "logRoot", string.Empty), "startup-" + rosterSize)));
    }

    private IEnumerator DriveFirstTurn(bool finalSprint)
    {
        manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance)
            .Invoke(manager, null));
        yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "first gear");
        session.Human.deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
        if (finalSprint)
        {
            session.Human.deck.DrawHeatFromPoolToHand(6);
            manager.OnDriverSkillButtonClicked();
            Assert.That(session.Human.driverSkill.ActivatedThisTurn, Is.True, "Actual skill input must activate.");
        }
        yield return ConfirmTurnCards();
    }

    private IEnumerator ConfirmTurnCards()
    {
        manager.OnGearButtonClicked(1);
        manager.OnConfirmGearClicked();
        yield return WaitFor(() => manager.TutorialInputPhase == "cards", "speed cards");
        List<CardUI> cards = Get<List<CardUI>>(manager.cardHandUI, "cardUIs");
        CardUI speed = cards.First(card => card.cardData.IsSpeed);
        speed.SetSelectedWithoutNotify(true);
        manager.OnPlayCardsButtonClicked(); // Real selected-card commit path.
        Assert.That(session.Human.playedSpeedCardsThisTurn.Count, Is.EqualTo(1));
        manager.OnPlayCardsButtonClicked(); // Refreshed selection is empty: finish cards.
        yield return WaitFor(() => manager.TutorialInputPhase == "discard", "discard confirmation");
        manager.OnPlayCardsButtonClicked();
    }

    private void CreateRace(TeamId team, int level = 0, int spins = 0)
    {
        messages.Clear();
        host = new GameObject("Isolated native GameLoop", typeof(RectTransform));
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        manager.enabled = false; // Awake is empty; do not execute real Start or Update.
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.totalLaps = 99;
        config.minGear = config.maxGear = 1;
        config.gearOneCooldown = 0;
        config.enableWeather = config.enablePitLane = config.enableTrickCards = false;
        config.enableTechTree = false;
        manager.config = config;
        var trackHost = Child("Inactive synthetic track");
        trackHost.SetActive(false); // TrackManager.Awake would load/render authored assets.
        manager.trackManager = trackHost.AddComponent<TrackManager>();
        manager.trackManager.config = config;
        Set(manager.trackManager, "nodes", Enumerable.Range(0, 200)
            .Select(i => new TrackNode(i, 0, "straight", 0, i == 0)).ToList());
        session = new RaceSession(new SystemRandomSource(11));
        Set(manager, "session", session);
        var human = Participant(team, false, 0);
        var ai = Participant(TeamId.DE, true, 100);
        if (level > 0)
        {
            human.driverId = "it_tazio_nuvolari";
            human.driverSkill.Initialize(human.DriverProfile, level, true);
            human.spinCounter = spins; // Starting counter, never fabricate a terminal result.
        }
        var controller = host.AddComponent<AIController>();
        controller.Initialize(manager, ai, new SystemRandomSource(12));
        Get<Dictionary<PlayerState, AIController>>(manager, "aiControllers").Add(ai, controller);
        manager.hudUI = Child("Headless HUD").AddComponent<HUDUI>();
        manager.hudUI.SetLogSink(messages.Add);
        manager.cardHandUI = Child("Headless hand").AddComponent<CardHandUI>();
        manager.cardHandUI.enabled = false; // No physical input polling or presentation coroutines.
        manager.cardHandUI.handContainer = manager.cardHandUI.transform;
        var card = Child("Inactive card template");
        card.SetActive(false);
        card.AddComponent<CardUI>();
        manager.cardHandUI.cardPrefab = card;
        Set(manager, "saveRaceTechState", (Action<TechTreeState>)(_ => Assert.Fail("Unexpected real-loop RP save.")));
        Set(manager, "saveRaceDriverXp", (Action<string, int>)((_, __) => Assert.Fail("Unexpected real-loop XP save.")));
        Set(manager, "createRaceCareerRepository", (Func<CareerRepository>)(() =>
            throw new InvalidOperationException("Unexpected real-loop career storage.")));
        Get<RacePhaseState>(manager, "phaseState").ResetForRace();
        host.SetActive(true);
        Assert.That(manager.LastRaceLogPath, Is.Null, "No production race initialization/log writer.");
    }

    private PlayerState Participant(TeamId team, bool ai, int position)
    {
        var player = new PlayerState(ai ? "AI" : "human", ai, position, 1) { teamId = team };
        player.deck.InitializeExactOrder(Enumerable.Range(0, 40)
            .Select(_ => new CardData(CardType.Speed, 1)).ToArray(), new HeatPool(6));
        player.deck.DrawToHand(3);
        session.Players.Add(player);
        return player;
    }

    private void AssertActiveAndHeatConserved()
    {
        Assert.That(manager.CurrentPhase, Is.Not.EqualTo(GamePhase.GameOver));
        Assert.That(session.IsRaceOver(), Is.False);
        foreach (PlayerState player in session.Players)
        {
            Assert.That(player.hasFinished, Is.False);
            Assert.That(player.finishOrder, Is.Zero);
            Assert.That(player.driverXp, Is.Zero);
            Assert.That(player.deck.heatPool.remaining + player.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(6));
        }
    }

    private int Turn => Get<int>(manager, "raceTurnNumber");

    private IEnumerator WaitFor(Func<bool> predicate, string checkpoint)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!predicate())
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Bounded GameLoop watchdog: " + checkpoint);
            yield return null;
        }
    }

    private GameObject Child(string name)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(host.transform, false);
        return child;
    }

    private void DestroyRace()
    {
        if (manager != null) manager.StopAllCoroutines();
        if (host != null) Object.DestroyImmediate(host);
        if (config != null) Object.DestroyImmediate(config);
        manager = null;
        host = null;
        config = null;
    }

    private static T Get<T>(object target, string field)
        => (T)target.GetType().GetField(field, PrivateInstance).GetValue(target);
    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
}
