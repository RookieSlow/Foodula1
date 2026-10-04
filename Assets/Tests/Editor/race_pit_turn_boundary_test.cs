using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Composes the real entry/exit adapters and session reset on inactive objects.
/// This protects synchronous turn boundaries, not the GameLoop coroutine or UI.
/// </summary>
public class RacePitTurnBoundaryTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private GameConfigSO config;
    private MVPGameManager manager;
    private RaceSession session;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Pit turn boundary regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        manager.trackManager = host.AddComponent<TrackManager>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.minGear = 2;
        config.pitExitMoveBonus = 1;
        config.enableTechTree = false;
        manager.config = config;
        session = new RaceSession(new SystemRandomSource(1));
        SetField("session", session);
        SetField("tutorialPitRuleNodes", RacePitStopExecutionTests.Track());
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(config);
    }

    private void SetField(string name, object value) =>
        typeof(MVPGameManager).GetField(name, Private).SetValue(manager, value);

    private void Invoke(string name, params object[] args) =>
        typeof(MVPGameManager).GetMethod(name, Private).Invoke(manager, args);

    [TestCase(TeamId.US)] [TestCase(TeamId.UK)]
    public void RegionalBBQCapacityFollowsPositionAndReentryWithoutStacking(TeamId team)
    {
        config.enableTechTree = true;
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 60; i++) nodes.Add(new TrackNode(i, 99));
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);
        var player = new PlayerState("Regional BBQ", false, 5, 1)
        { teamId = team, techState = new TechTreeState(team) };
        player.techState.activeNodeIds.Add(team == TeamId.UK ? "uk-l3-sun-never-sets" : "us-l2-smoked-bbq");
        player.techState.sunNeverSetsTarget = TeamId.US;
        player.deck.heatPool = new HeatPool(6);
        Assert.AreEqual(6, session.EffectiveHeatPoolSize(player, 6));
        Invoke("SyncRegionalHeatCapacity", player);
        Assert.AreEqual(8, player.deck.heatPool.remaining);
        Invoke("SyncRegionalHeatCapacity", player);
        Assert.AreEqual(8, player.deck.heatPool.remaining); // same zone does not stack
        Assert.AreEqual(8, player.deck.DrawHeatFromPoolToHand(8));
        player.position = 6;
        Invoke("SyncRegionalHeatCapacity", player);
        Assert.AreEqual(0, player.deck.heatPool.remaining);
        Assert.AreEqual(2, player.deck.RegionalHeatPendingRetirement);
        Assert.AreEqual(6, HeatGaugeRules.Evaluate(player.deck).Capacity);
        player.position = 25;
        Invoke("SyncRegionalHeatCapacity", player);
        Assert.AreEqual(0, player.deck.RegionalHeatPendingRetirement);
        Assert.AreEqual(8, HeatGaugeRules.Evaluate(player.deck).Capacity);
        Assert.AreEqual(2, player.deck.CoolHeat(2));
        Assert.AreEqual(2, player.deck.heatPool.remaining);
        player.position = 36;
        Invoke("SyncRegionalHeatCapacity", player);
        Assert.AreEqual(0, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.RegionalHeatPendingRetirement);
        Assert.AreEqual(6, HeatGaugeRules.Evaluate(player.deck).Capacity);
        Assert.AreEqual(6, player.deck.RecoverAllHeatWithSources().Total);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
    }

    [Test]
    public void RegionalBBQCapacityIsDisabledForOrdinaryRaceAndOtherBorrowTarget()
    {
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 60; i++) nodes.Add(new TrackNode(i, 99));
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);
        var us = new PlayerState("US disabled", false, 0, 1)
        { teamId = TeamId.US, techState = new TechTreeState(TeamId.US) };
        us.techState.activeNodeIds.Add("us-l2-smoked-bbq");
        us.deck.heatPool = new HeatPool(6);
        Invoke("SyncRegionalHeatCapacity", us);
        Assert.AreEqual(6, us.deck.heatPool.remaining);
        config.enableTechTree = true;
        var uk = new PlayerState("UK target other", false, 0, 1)
        { teamId = TeamId.UK, techState = new TechTreeState(TeamId.UK) };
        uk.techState.activeNodeIds.Add("uk-l3-sun-never-sets");
        uk.techState.sunNeverSetsTarget = TeamId.IT;
        uk.deck.heatPool = new HeatPool(6);
        Invoke("SyncRegionalHeatCapacity", uk);
        Assert.AreEqual(6, uk.deck.heatPool.remaining);
    }

    [Test]
    public void HeadlessMovementAndSpinRewindSynchronizeRegionalCapacity()
    {
        config.enableTechTree = true;
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 60; i++) nodes.Add(new TrackNode(i, 99));
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);
        var player = new PlayerState("BBQ movement", true, 5, 1)
        { teamId = TeamId.US, techState = new TechTreeState(TeamId.US) };
        player.techState.activeNodeIds.Add("us-l2-smoked-bbq");
        player.deck.heatPool = new HeatPool(6);
        Invoke("SyncRegionalHeatCapacity", player);
        Assert.AreEqual(8, player.deck.heatPool.remaining);
        var move = (System.Collections.IEnumerator)typeof(MVPGameManager)
            .GetMethod("AnimateMovementByAmount", Private)
            .Invoke(manager, new object[] { player, -1, 1, false });
        DrainMovement(move);
        Assert.AreEqual(6, player.position);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        manager.HandleSpin(player, 5, "regional regression");
        Assert.AreEqual(5, player.position);
        Assert.AreEqual(8, player.deck.heatPool.remaining);
    }

    private static void DrainMovement(System.Collections.IEnumerator routine)
    {
        int steps = 0;
        while (routine.MoveNext())
        {
            Assert.Less(++steps, 100, "Unexpected unbounded movement wait");
            if (routine.Current is System.Collections.IEnumerator nested) DrainMovement(nested);
        }
    }

    [TestCase(TeamId.US, false)] [TestCase(TeamId.US, true)]
    [TestCase(TeamId.UK, false)] [TestCase(TeamId.UK, true)]
    public void BBQRuntimeUsesActualHeatSpeedAndFullRecoveryCannotRefundTwice(TeamId team, bool temporary)
    {
        config.enableTechTree = true;
        session.TeamVehicleBonusesEnabled = false;
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 60; i++) nodes.Add(new TrackNode(i, 99));
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);
        var player = new PlayerState("BBQ runtime", false, 5, 1)
        { teamId = team, techState = new TechTreeState(team) };
        player.techState.activeNodeIds.Add(team == TeamId.UK ? "uk-l3-sun-never-sets" : "us-l2-smoked-bbq");
        player.techState.sunNeverSetsTarget = TeamId.US;
        player.deck.heatPool = new HeatPool(6);
        player.deck.DrawHeatFromPoolToHand(1);
        CardData heat = temporary ? CardData.CreateTempHeat() : player.deck.Hand[0];
        if (temporary) player.deck.AddCardsToHand(new[] { heat });
        session.Players.Add(player);
        Assert.IsTrue(manager.CanUseBBQHeatCards(player));
        Assert.AreEqual(temporary ? 2 : 1, manager.CountPlayableSpeedCardsInHand(player));
        Assert.AreEqual(SpeedCardCommitResult.Success,
            CardPlayRules.CommitSpeedCards(player, new[] { heat }, 1, session, 60));
        Invoke("ComputeMovements", new List<PlayerState> { player }, new HashSet<PlayerState>());
        Assert.AreEqual(2, player.cornerTotalThisTurn);
        Assert.AreEqual(2, player.totalMovementThisTurn);
        StringAssert.Contains("values=[2]", RaceStateLogFormatter.BuildPlayedCards(player, "runtime"));
        StringAssert.Contains("bbq_heat=1", RaceStateLogFormatter.BuildPlayedCards(player, "runtime"));
        Assert.AreEqual(0, heat.value);
        Invoke("RecoverAllHeatWithPresentation", player);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountPermanentHeatOutsideEngine());
        RaceTurnCleanup.Execute(player, null, null, null);
        Invoke("RecoverAllHeatWithPresentation", player);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountTemporaryHeatOutsideEngine());
        Assert.AreEqual(0, player.deck.DiscardPileCount);
    }

    [TestCase(5)] [TestCase(54)]
    public void BBQDoesNotGrantFreeMovementForSpeedOnlyOrEnteringZone(int start)
    {
        config.enableTechTree = true;
        session.TeamVehicleBonusesEnabled = false;
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 60; i++) nodes.Add(new TrackNode(i, 99));
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);
        var player = new PlayerState("No free BBQ", false, start, 1)
        { teamId = TeamId.US, techState = new TechTreeState(TeamId.US) };
        player.techState.activeNodeIds.Add("us-l2-smoked-bbq");
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 1));
        Invoke("ComputeMovements", new List<PlayerState> { player }, new HashSet<PlayerState>());
        Assert.AreEqual(1, player.totalMovementThisTurn);
        config.enableTechTree = false;
        Assert.IsFalse(manager.CanUseBBQHeatCards(player));
        Assert.IsFalse(manager.IsPlayableSpeedCard(player, new CardData(CardType.Heat, 0)));
    }

    [TestCase(true, 5, 4, 0)]
    [TestCase(true, 5, 5, 1)]
    [TestCase(true, 18, 2, 1)]
    [TestCase(true, 5, 15, 2)]
    [TestCase(false, 5, 15, 0)]
    public void DriveThruMovementUsesEachBaseMoveLandmarkOnce(
        bool techEnabled, int start, int cardSpeed, int expectedBonus)
    {
        config.enableTechTree = techEnabled;
        session.TeamVehicleBonusesEnabled = false;
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 20; i++) nodes.Add(new TrackNode(i, 99));
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);
        var player = new PlayerState("Drive-Thru movement", false, start, 1)
        { teamId = TeamId.US, techState = new TechTreeState(TeamId.US) };
        player.techState.activeNodeIds.Add("us-l1-drive-thru");
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, cardSpeed));

        Invoke("ComputeMovements", new List<PlayerState> { player }, new HashSet<PlayerState>());

        Assert.AreEqual(cardSpeed, player.cornerTotalThisTurn);
        Assert.AreEqual(cardSpeed + expectedBonus, player.totalMovementThisTurn);
    }

    [TestCase(TeamId.US)] [TestCase(TeamId.UK)]
    public void BBQAIAndHandSelectionUseTheSameLiveGate(TeamId team)
    {
        config.enableTechTree = true;
        config.aiCardVariationChance = 0f;
        session.TeamVehicleBonusesEnabled = false;
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 60; i++) nodes.Add(new TrackNode(i, 99));
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);
        var player = new PlayerState("BBQ selection", false, 5, 1)
        { teamId = team, techState = new TechTreeState(team) };
        player.techState.activeNodeIds.Add(team == TeamId.UK ? "uk-l3-sun-never-sets" : "us-l2-smoked-bbq");
        player.techState.sunNeverSetsTarget = TeamId.US;
        player.deck.heatPool = new HeatPool(6);
        player.deck.DrawHeatFromPoolToHand(1);
        var heat = player.deck.Hand[0];
        session.Players.Add(player);
        ((RacePhaseState)typeof(MVPGameManager).GetField("phaseState", Private).GetValue(manager)).BeginCardSelection();
        var uiRoot = new GameObject("BBQ selection test", typeof(RectTransform));
        try
        {
            var hand = uiRoot.AddComponent<CardHandUI>();
            typeof(CardHandUI).GetField("gameManager", Private).SetValue(hand, manager);
            var cardObject = new GameObject("Heat", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button), typeof(CardUI));
            cardObject.transform.SetParent(uiRoot.transform, false);
            var card = cardObject.GetComponent<CardUI>();
            card.selectionDuration = 0f;
            card.SetupCard(heat, null);
            typeof(CardHandUI).GetField("cardUIs", Private).SetValue(hand, new List<CardUI> { card });
            hand.RefreshRequirementFeedback(player);
            Assert.IsTrue(card.GetComponent<UnityEngine.UI.Button>().interactable);
            Assert.IsTrue(hand.MoveKeyboardHighlight(1));
            Assert.IsTrue(hand.ToggleKeyboardHighlightedCard());
            CollectionAssert.AreEqual(new[] { heat }, hand.GetSelectedPlayCards());
            hand.SetDiscardMode(true);
            Assert.IsFalse(card.isSelected);
            Assert.IsFalse(hand.MoveKeyboardHighlight(1));
            hand.SetGearSelectionMode(true);
            Assert.IsFalse(card.GetComponent<UnityEngine.UI.Button>().interactable);
            hand.SetGearSelectionMode(false);
            Assert.IsTrue(hand.ToggleKeyboardHighlightedCard());
            player.position = 15;
            hand.RefreshRequirementFeedback(player);
            Assert.IsFalse(card.isSelected);
            Assert.IsEmpty(hand.GetSelectedPlayCards());
            Assert.IsTrue(player.deck.ContainsInHand(heat));
        }
        finally { Object.DestroyImmediate(uiRoot); }
        player.position = 5;
        player.isAI = true;
        var ai = host.AddComponent<AIController>();
        ai.Initialize(manager, player, new SystemRandomSource(3));
        ai.SelectCards();
        CollectionAssert.AreEqual(new[] { heat }, player.playedSpeedCardsThisTurn);
        Assert.IsFalse(player.deck.ContainsInHand(heat));
        Assert.AreEqual(5, player.deck.heatPool.remaining);
        Invoke("RecoverAllHeatWithPresentation", player);
        player.isAI = false;
        player.deck.DrawHeatFromPoolToHand(1);
        heat = player.deck.Hand[0];
        manager.cardHandUI = host.AddComponent<CardHandUI>();
        var prefab = new GameObject("BBQ inactive confirmation prefab", typeof(RectTransform),
            typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(CardUI));
        prefab.transform.SetParent(host.transform, false);
        manager.cardHandUI.cardPrefab = prefab;
        manager.cardHandUI.handContainer = host.transform;
        Assert.AreEqual(true, typeof(MVPGameManager).GetMethod("ConfirmPlayerCard", Private)
            .Invoke(manager, new object[] { player, heat }));
        CollectionAssert.AreEqual(new[] { heat }, player.playedSpeedCardsThisTurn);
        Assert.IsEmpty(player.deck.Hand);
        Assert.AreEqual(5, player.deck.heatPool.remaining);
    }

    [TestCase(TeamId.DE)]
    [TestCase(TeamId.UK)]
    public void WurstplatteDoesNotExemptOriginalSpeedFromCornerPayment(TeamId team)
    {
        session.TeamVehicleBonusesEnabled = false;
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 20; i++) nodes.Add(new TrackNode(i, i == 1 ? 3 : 99, "Test", i == 1 ? 1 : 0, false, i == 1));
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);
        typeof(TrackManager).GetField("cornerSpeedLimits", Private).SetValue(manager.trackManager,
            new Dictionary<int, int> { { 1, 3 } });
        var player = new PlayerState("Suspension", false, 0, 1) { teamId = team };
        player.deck.heatPool = new HeatPool(6);
        player.techState = new TechTreeState(team);
        player.techState.activeNodeIds.Add(team == TeamId.UK ? "uk-l3-sun-never-sets" : "de-l2-wurstplatte");
        player.techState.sunNeverSetsTarget = TeamId.DE;
        player.cornerTotalThisTurn = 5;
        var result = typeof(MVPGameManager).GetMethod("ResolveCorners", Private).Invoke(manager, new object[] { player, 0, 6 });
        Assert.AreEqual(true, result);
        Assert.AreEqual(4, player.deck.heatPool.remaining);
        Assert.AreEqual(2, player.deck.CountPermanentHeatOutsideEngine());
    }

    private void ConfigureSingleApex(int limit)
    {
        session.TeamVehicleBonusesEnabled = false;
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 12; i++)
            nodes.Add(new TrackNode(i, i == 5 ? limit : 99, "Apex",
                i == 5 ? 1 : 0, false, i == 5));
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);
        typeof(TrackManager).GetField("cornerSpeedLimits", Private).SetValue(manager.trackManager,
            new Dictionary<int, int> { { 1, limit } });
    }

    private bool ResolveSingleApex(PlayerState player) =>
        (bool)typeof(MVPGameManager).GetMethod("ResolveCorners", Private)
            .Invoke(manager, new object[] { player, 4, 6 });

    [Test]
    public void SafeApexDoesNotDrawHeatOrSpin()
    {
        ConfigureSingleApex(5);
        var player = new PlayerState("Safe", true, 4, 1)
        { cornerTotalThisTurn = 5 };
        player.deck.heatPool = new HeatPool(2);

        Assert.That(ResolveSingleApex(player), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(2));
        Assert.That(player.deck.CountPermanentHeatOutsideEngine(), Is.Zero);
        Assert.That(player.spinCounter, Is.Zero);
    }

    [Test]
    public void OverspeedApexPaysRealHeatIntoHand()
    {
        ConfigureSingleApex(3);
        var player = new PlayerState("Overspeed", true, 4, 1)
        { cornerTotalThisTurn = 5 };
        player.deck.heatPool = new HeatPool(3);

        Assert.That(ResolveSingleApex(player), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(1));
        Assert.That(player.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(2));
        Assert.That(player.deck.Hand.Count, Is.EqualTo(2));
        Assert.That(player.spinCounter, Is.Zero);
    }

    [Test]
    public void InsufficientHeatAtApexSpinsAndRestoresPaidHeat()
    {
        ConfigureSingleApex(3);
        var player = new PlayerState("Spin", true, 4, 1)
        { cornerTotalThisTurn = 5 };
        player.deck.heatPool = new HeatPool(1);

        Assert.That(ResolveSingleApex(player), Is.False);
        Assert.That(player.position, Is.EqualTo(4));
        Assert.That(player.spinCounter, Is.EqualTo(1));
        Assert.That(player.skipNextTurn, Is.True);
        Assert.That(player.gear, Is.EqualTo(config.minGear));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(1));
        Assert.That(player.deck.CountPermanentHeatOutsideEngine(), Is.Zero);
    }

    [Test]
    public void GutterRunIgnorePreventsPaymentAndSpinAtApex()
    {
        ConfigureSingleApex(2);
        DriverCatalog.TryGet("jp_takumi_fujiwara", out DriverProfile driver);
        var player = new PlayerState("Ignore", true, 4, 1)
        { teamId = TeamId.JP, driverId = "jp_takumi_fujiwara", cornerTotalThisTurn = 5 };
        player.deck.heatPool = new HeatPool(0);
        player.driverSkill.Initialize(driver, 7, true);
        Assert.That(player.driverSkill.TryActivate(driver,
            new DriverSkillActivationContext(true, 0, 3, 10, 10, 0), out _), Is.True);

        Assert.That(ResolveSingleApex(player), Is.True);
        Assert.That(player.spinCounter, Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.Zero);
        Assert.That(player.driverSkill.TryConsumeCornerIgnore(), Is.True,
            "One of the two granted ignores should remain after the first apex.");
        Assert.That(player.driverSkill.TryConsumeCornerIgnore(), Is.False);
    }

    [TestCase(false, 0)]
    [TestCase(true, 5)]
    public void IneligiblePlayerDoesNotResolveApex(bool blown, int speed)
    {
        ConfigureSingleApex(2);
        var player = new PlayerState("Ineligible", true, 4, 1)
        { isBlown = blown, cornerTotalThisTurn = speed };
        player.deck.heatPool = new HeatPool(1);

        Assert.That(ResolveSingleApex(player), Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(1));
        Assert.That(player.spinCounter, Is.Zero);
    }

    [Test]
    public void RealChinaOverclockFailureRecordsSpinRecoveryAsSelectedGear()
    {
        var player = new PlayerState("Overclock", true, 4, ChinaGearShiftRules.GoGear)
        { teamId = TeamId.CN, chinaConsecutiveGearCount = 1 };
        player.deck.heatPool = new HeatPool(0);

        Invoke("ApplyGearShift", player, ChinaGearShiftRules.GoGear);

        Assert.That(player.spinCounter, Is.EqualTo(1));
        Assert.That(player.skipNextTurn, Is.True);
        Assert.That(player.gear, Is.EqualTo(ChinaGearShiftRules.RecoverGear));
        Assert.That(player.selectedGearThisTurn, Is.EqualTo(player.gear));
        Assert.That(player.chinaConsecutiveGearCount, Is.Zero);
    }

    [TestCase(1)] [TestCase(2)]
    public void SchwarzbierUsesLastAvailableHeatAndPaysToDiscardOncePerLap(int initialHeat)
    {
        config.enableTechTree = true;
        session.TeamVehicleBonusesEnabled = false;
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 20; i++) nodes.Add(new TrackNode(i, 99));
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);
        var player = new PlayerState("Fuel", false, 0, 1) { teamId = TeamId.DE };
        player.deck.heatPool = new HeatPool(initialHeat);
        player.techState = new TechTreeState(TeamId.DE);
        player.techState.activeNodeIds.Add("de-l1-schwarzbier-fuel");
        var order = new List<PlayerState> { player };
        Invoke("ComputeMovements", order, new HashSet<PlayerState>());
        Assert.AreEqual(2, player.totalMovementThisTurn);
        Assert.AreEqual(initialHeat - 1, player.deck.heatPool.remaining);
        Assert.AreEqual(1, player.deck.CountHeatInDiscardPile());
        Assert.AreEqual(0, player.deck.Hand.Count);
        Invoke("ComputeMovements", order, new HashSet<PlayerState>());
        Assert.AreEqual(0, player.totalMovementThisTurn);
        Assert.AreEqual(initialHeat - 1, player.deck.heatPool.remaining);
    }

    [TestCase("IT", true, TeamId.IT)] [TestCase("DE", false, TeamId.DE)]
    [TestCase("GB", false, null)] [TestCase("FR", false, null)]
    public void TrackBindingIgnoresDemoTargetAndPrecedesCapacityCalculation(string country, bool demo, TeamId? expected)
    {
        config.enableUkSunNeverSetsDemo = demo;
        config.ukSunNeverSetsTargetTeam = TeamId.CN;
        typeof(TrackManager).GetProperty("LoadedTrackConfig").SetValue(manager.trackManager, new TrackConfig { country = country });
        var player = new PlayerState("UK", false, 0, 1) { teamId = TeamId.UK, techState = new TechTreeState(TeamId.UK) };
        player.techState.activeNodeIds.Add("uk-l3-sun-never-sets");
        var cache = (Dictionary<TeamId, TechTreeState>)typeof(TechTreeProfileStore)
            .GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        bool hadProfile = cache.TryGetValue(TeamId.UK, out var previousProfile);
        var previousRandom = Random.state;
        cache[TeamId.UK] = player.techState;
        config.enableTechTree = true;
        config.enableTrickCards = false;
        config.jpDemoBroth = BrothType.None;
        config.heatPoolPerPlayer = 6;
        try
        {
            // The old BindTrackTechnology helper was absorbed into participant setup.
            // Keep this a real human adapter test without touching PlayerPrefs.
            Invoke("SetupPlayerForRace", player, TeamId.UK, null);
            Assert.AreEqual(expected, player.techState.sunNeverSetsTarget);
            int capacity = country == "IT" ? 7 : 6;
            Assert.AreEqual(capacity, session.EffectiveHeatPoolSize(player, 6));
            int baseCapacity = TeamVehicleRules.GetBaseHeatPoolSize(TeamId.UK, config.heatPoolPerPlayer);
            Assert.AreEqual(baseCapacity + (country == "IT" ? 1 : 0), HeatGaugeRules.Evaluate(player.deck).Capacity,
                "The actual deck must be initialized after loaded-track technology binding.");
        }
        finally
        {
            if (hadProfile) cache[TeamId.UK] = previousProfile;
            else cache.Remove(TeamId.UK);
            Random.state = previousRandom;
        }
    }

    [TestCase(TeamId.CN, false)]
    [TestCase(TeamId.CN, true)]
    [TestCase(TeamId.US, false)]
    [TestCase(TeamId.US, true)]
    [TestCase(TeamId.UK, false)]
    [TestCase(TeamId.UK, true)]
    [TestCase(TeamId.DE, false)]
    [TestCase(TeamId.DE, true)]
    [TestCase(TeamId.IT, false)]
    [TestCase(TeamId.IT, true)]
    [TestCase(TeamId.JP, false)]
    [TestCase(TeamId.JP, true)]
    public void ReservationCrossingStopAndNextPlayableTurnKeepTheirBoundaries(TeamId team, bool ai)
    {
        var player = RacePitStopExecutionTests.Player(team);
        player.isAI = ai;
        player.position = 12; // Already settled movement: entry must not teleport it.
        player.pitStopScheduled = false;
        player.skipNextTurn = false;
        session.Players.Add(player);
        var handBefore = new List<CardData>(player.deck.Hand);
        Assert.AreEqual(RaceTurnStartAction.SelectGear, RaceTurnRules.GetStartAction(player));

        Invoke("RegisterPitEntryCrossing", player, 8, 9);
        Assert.IsTrue(player.pitStopRequested);
        Assert.IsFalse(player.pitStopScheduled);
        Invoke("RegisterPitEntryCrossing", player, 8, 12);
        Assert.IsFalse(player.pitStopRequested);
        Assert.IsTrue(player.pitStopScheduled);
        Assert.IsFalse(player.skipNextTurn);
        Assert.AreEqual(12, player.position);
        Assert.AreEqual(8, player.totalMovementThisTurn);
        CollectionAssert.AreEqual(handBefore, player.deck.Hand);
        Assert.AreEqual(2, player.deck.heatPool.remaining);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);

        session.BeginTurn(player);
        Assert.IsTrue(player.pitStopScheduled);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(0, player.heatPaidCardsThisTurn.Count);
        Assert.AreEqual(RaceTurnStartAction.ExecuteScheduledPitStop, RaceTurnRules.GetStartAction(player));
        Invoke("ExecuteScheduledPitStop", player);
        Assert.AreEqual(0, player.position);
        Assert.AreEqual(team == TeamId.CN ? 1 : 2, player.gear);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.IsFalse(player.skipNextTurn);
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(0, player.chinaConsecutiveGearCount);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountPermanentHeatOutsideEngine());
        Assert.AreEqual(2, player.lap);

        // A1 retains exclusion after the stop has consumed its scheduling flag.
        var skipped = new HashSet<PlayerState> { player };
        var order = new List<PlayerState> { player };
        Assert.IsTrue(RaceTurnRules.IsInactive(player, skipped));
        player.totalMovementThisTurn = 7;
        player.cornerTotalThisTurn = 7;
        Invoke("ComputeMovements", order, skipped);
        Invoke("ResolveSlipstreamsAtTurnEnd", order, skipped);
        Assert.AreEqual(0, player.totalMovementThisTurn);
        Assert.AreEqual(0, player.cornerTotalThisTurn);
        Assert.AreEqual(0, player.position);

        session.BeginTurn(player);
        Assert.AreEqual(RaceTurnStartAction.SelectGear, RaceTurnRules.GetStartAction(player));
        Assert.IsFalse(RaceTurnRules.IsInactive(player, new HashSet<PlayerState>()));
        Assert.AreEqual(0, player.positionAtTurnStart);
        Assert.AreEqual(player.gear, player.selectedGearThisTurn);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
    }

    [TestCase(false, false, false)]
    [TestCase(true, true, false)]
    [TestCase(true, false, true)]
    public void CrossingWithoutEligibleReservationOnlyReopensChoice(bool requested, bool finished, bool blown)
    {
        var player = RacePitStopExecutionTests.Player();
        player.pitStopScheduled = false;
        player.skipNextTurn = false;
        player.pitStopRequested = requested;
        player.hasFinished = finished;
        player.isBlown = blown;
        Invoke("RegisterPitEntryCrossing", player, 8, 12);
        Assert.IsFalse(player.pitStopRequested);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(10, player.position);
        Assert.AreEqual(2, player.deck.heatPool.remaining);
        Assert.AreEqual(4, player.deck.CountPermanentHeatOutsideEngine());
    }

    [Test]
    public void RepeatedEntryCrossingDoesNotCancelAnAlreadyScheduledStop()
    {
        var player = RacePitStopExecutionTests.Player();
        player.pitStopScheduled = false;
        player.pitStopRequested = true;
        player.pitChoiceResolvedThisLap = true;

        Invoke("RegisterPitEntryCrossing", player, 8, 12);
        Assert.IsTrue(player.pitStopScheduled);
        Invoke("RegisterPitEntryCrossing", player, 8, 12);

        Assert.IsTrue(player.pitStopScheduled);
        Assert.IsFalse(player.pitStopRequested);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(10, player.position);
    }

    [TestCase("missing-track")]
    [TestCase("missing-pit")]
    [TestCase("disabled")]
    [TestCase("before-entry")]
    [TestCase("reverse")]
    [TestCase("standing-on-entry")]
    public void EntryGuardKeepsReservationAndPaidHeatUntouched(string reason)
    {
        var player = RacePitStopExecutionTests.Player();
        player.pitStopScheduled = false;
        if (reason == "missing-track") manager.trackManager = null;
        if (reason == "missing-pit") SetField("tutorialPitRuleNodes", RacePitStopExecutionTests.Track(false));
        if (reason == "disabled")
        {
            SetField("tutorialPitRuleNodes", null);
            config.enablePitLane = false;
        }
        int start = reason == "standing-on-entry" ? 10 : 8;
        int end = reason == "before-entry" ? 9 : reason == "reverse" ? 7 : 12;
        Invoke("RegisterPitEntryCrossing", player, start, end);
        Assert.IsTrue(player.pitStopRequested);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.IsTrue(player.skipNextTurn);
        Assert.AreEqual(10, player.position);
        Assert.AreEqual(4, player.heatPaidCardsThisTurn.Count);
        Assert.AreEqual(2, player.deck.heatPool.remaining);
    }

    [TestCase(false, 7)]
    [TestCase(true, 5)]
    public void SettledSlipstreamAdapterAppliesOnlyEligibleFollowerBonus(bool skipFollower, int expectedMovement)
    {
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 60; i++) nodes.Add(new TrackNode(i, 99));
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);
        session.TeamVehicleBonusesEnabled = false;
        var follower = new PlayerState("Follower", false, 10, 1) { teamId = TeamId.CN };
        var leader = new PlayerState("Leader", true, 11, 1) { teamId = TeamId.UK };
        follower.totalMovementThisTurn = 5;
        leader.totalMovementThisTurn = 6;
        session.Players.Add(follower);
        session.Players.Add(leader);
        var order = new List<PlayerState> { follower, leader };
        var skipped = skipFollower
            ? new HashSet<PlayerState> { follower }
            : new HashSet<PlayerState>();

        Invoke("ResolveSlipstreamsAtTurnEnd", order, skipped);

        Assert.That(follower.totalMovementThisTurn, Is.EqualTo(expectedMovement));
        Assert.That(leader.totalMovementThisTurn, Is.EqualTo(6));
        var results = (Dictionary<PlayerState, SlipstreamChainResult>)
            typeof(MVPGameManager).GetField("slipstreamsThisTurn", Private).GetValue(manager);
        Assert.That(results[follower].Triggered, Is.EqualTo(!skipFollower));
    }
}

public sealed class RaceSettledSlipstreamResolutionTests
{
    private static (RaceSession session, PlayerState follower, PlayerState leader)
        Pair(int followerPosition = 10, int leaderPosition = 11)
    {
        var session = new RaceSession(new SystemRandomSource(1))
        { TeamVehicleBonusesEnabled = false };
        var follower = new PlayerState("Follower", false, followerPosition, 1)
        { teamId = TeamId.CN, totalMovementThisTurn = 5 };
        var leader = new PlayerState("Leader", true, leaderPosition, 1)
        { teamId = TeamId.UK, totalMovementThisTurn = 6 };
        session.Players.Add(follower);
        session.Players.Add(leader);
        return (session, follower, leader);
    }

    [Test]
    public void NormalRaceUsesSettledPositionsAndLogsFinalTotals()
    {
        var (session, follower, leader) = Pair();
        var results = new Dictionary<PlayerState, SlipstreamChainResult>();
        var logs = new List<string>();

        RaceSettledSlipstreamResolution.Execute(session,
            new[] { follower, leader }, new HashSet<PlayerState>(), 60,
            follower, null, results, logs.Add);

        Assert.That(results[follower].Triggered, Is.True);
        Assert.That(results[follower].TotalBonus, Is.EqualTo(2));
        Assert.That(follower.totalMovementThisTurn, Is.EqualTo(7));
        Assert.That(leader.totalMovementThisTurn, Is.EqualTo(6));
        Assert.That(logs, Does.Contain(
            "[MOVE_PLAN_FINAL] Follower position=10 base_total=5 tailwind_bonus=2 total=7"));
    }

    [Test]
    public void SkippedFollowerGetsDefaultWithoutChangingItsMovement()
    {
        var (session, follower, leader) = Pair();
        var results = new Dictionary<PlayerState, SlipstreamChainResult>();
        RaceSettledSlipstreamResolution.Execute(session,
            new[] { follower, leader }, new HashSet<PlayerState> { follower }, 60,
            follower, null, results, _ => { });

        Assert.That(results[follower].Triggered, Is.False);
        Assert.That(follower.totalMovementThisTurn, Is.EqualTo(5));
    }

    [TestCase(TutorialStepId.Slipstream)]
    [TestCase(TutorialStepId.UsSlipstream)]
    [TestCase(TutorialStepId.JpTorpedo)]
    public void TeachingCueBlocksAiFollowerButRetainsItsBaseMovement(TutorialStepId step)
    {
        var (session, follower, leader) = Pair();
        var cue = new TutorialOpponentCue(step, 11, 10, 1);
        var results = new Dictionary<PlayerState, SlipstreamChainResult>();
        var logs = new List<string>();
        RaceSettledSlipstreamResolution.Execute(session,
            new[] { follower, leader }, new HashSet<PlayerState>(), 60,
            leader, cue, results, logs.Add);

        Assert.That(results[follower].Triggered, Is.False);
        Assert.That(follower.totalMovementThisTurn, Is.EqualTo(5));
        Assert.That(logs, Does.Contain(
            $"[TUTORIAL_CUE] type=opponent step={step} follower=Follower tailwind=blocked reason=player_only"));
    }

    [Test]
    public void NonSlipstreamCueDoesNotBlockAiFollower()
    {
        var (session, follower, leader) = Pair();
        var cue = new TutorialOpponentCue(TutorialStepId.Weather, 11, 10, 1);
        var results = new Dictionary<PlayerState, SlipstreamChainResult>();
        RaceSettledSlipstreamResolution.Execute(session,
            new[] { follower, leader }, new HashSet<PlayerState>(), 60,
            leader, cue, results, _ => { });

        Assert.That(results[follower].Triggered, Is.True);
        Assert.That(follower.totalMovementThisTurn, Is.EqualTo(7));
    }

    [Test]
    public void AllChainsAreComputedBeforeAnyBonusIsWritten()
    {
        var (session, follower, leader) = Pair(10, 12);
        var second = new PlayerState("Second", true, 11, 1)
        { teamId = TeamId.DE, totalMovementThisTurn = 4 };
        session.Players.Add(second);
        var order = new[] { follower, second, leader };
        var expected = session.ComputeSettledSlipstreamChains(order, order, 60);
        var results = new Dictionary<PlayerState, SlipstreamChainResult>();

        RaceSettledSlipstreamResolution.Execute(session, order,
            new HashSet<PlayerState>(), 60, follower, null, results, null);

        foreach (PlayerState racer in order)
            Assert.That(results[racer].TotalBonus, Is.EqualTo(expected[racer].TotalBonus));
        Assert.That(follower.totalMovementThisTurn,
            Is.EqualTo(5 + expected[follower].TotalBonus));
        Assert.That(second.totalMovementThisTurn,
            Is.EqualTo(4 + expected[second].TotalBonus));
    }
}
