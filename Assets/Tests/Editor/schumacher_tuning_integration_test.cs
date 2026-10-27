using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Integration; Assets/Tests/Editor; BLOCKING. D03 draft paragraphs 194/198/202/206.
/// Real coordinator corner/payment/spin/cleanup adapters, with owned inactive objects.
/// No scene startup, physical input, progression writes or complete-mode acceptance.
/// </summary>
public class SchumacherTuningIntegrationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private MVPGameManager manager;
    private GameConfigSO config;
    private RaceSession race;
    private PlayerState player;
    private object previousAudio;
    private static readonly FieldInfo Audio = typeof(AudioService).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);

    [SetUp]
    public void SetUp()
    {
        previousAudio = Audio.GetValue(null);
        Audio.SetValue(null, null); // Do not play sounds through an editor/user-owned service.
        host = new GameObject("Owned Schumacher tuning adapters");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.enableTechTree = true;
        config.enableWeather = false;
        config.gearOneCooldown = config.gearTwoCooldown = 0;
        config.twoGearShiftHeatCost = 1;
        config.minGear = 1; config.maxGear = 4;
        config.trackId = "owned-schumacher-straight";
        manager.config = config;
        manager.trackManager = host.AddComponent<TrackManager>();
        manager.trackManager.config = config;
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 60; i++) nodes.Add(new TrackNode(i, 99));
        Set(manager.trackManager, "nodes", nodes);
        Set(manager.trackManager, "cornerSpeedLimits", new Dictionary<int, int> { { 1, 2 }, { 2, 2 } });
        Set(manager.trackManager, "cornerNames", new Dictionary<int, string> { { 1, "Owned first corner" }, { 2, "Owned second corner" } });
        race = new RaceSession(new SystemRandomSource(31)) { TeamVehicleBonusesEnabled = false };
        Set(manager, "session", race);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (host != null) Object.DestroyImmediate(host);
            if (config != null) Object.DestroyImmediate(config);
        }
        finally { Audio.SetValue(null, previousAudio); }
    }

    private void CreateDriver(int level, int engine = 20)
    {
        Assert.That(DriverCatalog.TryGet("de_michael_schumacher", out DriverProfile profile), Is.True);
        player = new PlayerState("Owned Schumacher", false, 8, 1)
        {
            driverId = profile.Id, teamId = TeamId.DE, lap = 1,
            techState = new TechTreeState(TeamId.DE)
        };
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 4), new CardData(CardType.Speed, 2) }, new HeatPool(engine));
        player.deck.DrawToHand(2);
        player.driverSkill.Initialize(profile, level, true);
        race.Players.Add(player);
        BeginInput(1);
        Assert.That(player.driverSkill.Tuning.IsInputOpen, Is.True);
    }

    private void BeginInput(int turn)
    {
        race.BeginTurn(player, turn);
        Assert.That(race.BeginDriverSkillInput(player, 60), Is.True);
    }

    private bool Corner(int overspeed, params int[] corners)
    {
        // Real TrackManager lookup supplies limit 2; use actual coordinator typed payment.
        player.cornerTotalThisTurn = 2 + overspeed;
        return (bool)Invoke("ResolveCornerSet", player, player.position, corners);
    }

    private void Arm(int level)
    {
        CreateDriver(level);
        Assert.That(Corner(level == 2 ? 2 : 1, 1), Is.True);
        Assert.That(player.driverSkill.Tuning.HasPending, Is.True);
        Assert.That(player.driverSkill.Tuning.HasGrantedInLap(1), Is.True);
    }

    private void NextInput(int turn = 2)
    {
        Invoke("CleanupTurn", player);
        Assert.That(player.driverSkill.Tuning.IsInputOpen, Is.False);
        BeginInput(turn);
    }

    [TestCase(2, 2)] [TestCase(4, 1)] [TestCase(6, 1)]
    public void ActualCornerReceiptsAccumulateThresholdThenReduceOnlyFirstPositiveNextInputCost(int level, int threshold)
    {
        CreateDriver(level);
        for (int i = 1; i <= threshold; i++)
        {
            Assert.That(Corner(1, 1), Is.True);
            Assert.That(player.driverSkill.Tuning.PaidThisInput, Is.EqualTo(i));
            Assert.That(player.driverSkill.Tuning.HasPending, Is.EqualTo(i == threshold));
        }
        // A prepared mark cannot reduce a second fee in the very input that granted it.
        int beforeSameInput = player.deck.heatPool.remaining;
        Assert.That(Corner(1, 2), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(beforeSameInput - 1));
        Assert.That(player.driverSkill.Tuning.HasPending, Is.True);
        NextInput();
        int before = player.deck.heatPool.remaining;
        Assert.That(Corner(2, 1, 2), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(before - 3), "Costs 2-1, then full 2; only one reduction.");
        Assert.That(player.heatPaidCardsThisTurn.Count, Is.EqualTo(3));
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False, "Already-granted lap cannot stack or re-arm.");
        Assert.That(player.driverSkill.Tuning.InputsStarted, Is.EqualTo(2));
    }

    [Test]
    public void FullyReducedCornerCreatesNoReceiptAndCannotGrantAnotherMark()
    {
        Arm(4);
        NextInput();
        int engine = player.deck.heatPool.remaining;
        Assert.That(Corner(1, 1), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine));
        Assert.That(player.heatPaidCardsThisTurn, Is.Empty);
        Assert.That(player.driverSkill.Tuning.PaidThisInput, Is.Zero);
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False);
        Assert.That(Corner(1, 2), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine - 1));
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False);
    }

    [TestCase("generic")] [TestCase("gear")] [TestCase("fuel")]
    public void GenericReasonTextRealGearAndFixedFuelCannotEarnOrdinaryCornerCredit(string source)
    {
        CreateDriver(4);
        int engine = player.deck.heatPool.remaining;
        switch (source)
        {
            case "generic":
                Assert.That(manager.TryPayHeat(player, 1, player.position, "overspeed at spoofed corner"), Is.True);
                break;
            case "gear":
                Invoke("ApplyGearShift", player, 3);
                Assert.That(player.gear, Is.EqualTo(3));
                break;
            case "fuel":
                player.techState.activeNodeIds.Add("de-l1-schwarzbier-fuel");
                Assert.That(race.TryCommitSchwarzbierFuel(player, true, out CardData receipt), Is.True);
                Assert.That(player.deck.IsUniquePermanentHeatInHandOrDiscard(receipt), Is.True);
                break;
        }
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine - 1));
        Assert.That(player.heatPaidCardsThisTurn.Count, Is.EqualTo(1));
        Assert.That(player.driverSkill.Tuning.PaidThisInput, Is.Zero);
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False);
        Assert.That(player.driverSkill.Tuning.HasGrantedInLap(1), Is.False);
        Assert.That(Corner(1, 1), Is.True, "The input remains eligible for a later real corner.");
        Assert.That(player.driverSkill.Tuning.PaidThisInput, Is.EqualTo(1));
        Assert.That(player.driverSkill.Tuning.HasPending, Is.True);
    }

    [Test]
    public void GenericNonCornerPaymentCannotConsumePendingOrdinaryCornerReduction()
    {
        Arm(4);
        NextInput();
        int before = player.deck.heatPool.remaining;
        Assert.That(manager.TryPayHeat(player, 1, player.position, "generic engine failure"), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(before - 1));
        Assert.That(player.driverSkill.Tuning.HasPending, Is.True);
        Assert.That(Corner(1, 1), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(before - 1));
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False);
    }

    [TestCase(2)] [TestCase(4)]
    public void PartialInsufficientActualCornerTriggersSpinAndCannotRetainNewOrFutureMark(int level)
    {
        CreateDriver(level, 1);
        Assert.That(Corner(2, 1), Is.False);
        Assert.That(player.spinCounter, Is.EqualTo(1));
        Assert.That(player.skipNextTurn, Is.True);
        Assert.That(player.gear, Is.EqualTo(1));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(1), "HandleSpin recovers the actual partial payment.");
        Assert.That(player.deck.CountPermanentHeatOutsideEngine(), Is.Zero);
        Assert.That(player.driverSkill.Tuning.IsInputOpen, Is.False);
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False);
        Assert.That(player.driverSkill.Tuning.PaidThisInput, Is.Zero);
        Assert.That(player.driverSkill.Tuning.HasGrantedInLap(1), Is.EqualTo(level >= 4),
            "Actual partial receipt can reach tier2 threshold, but spin loses the mark without restoring the lap grant.");
    }

    [TestCase(2)] [TestCase(4)] [TestCase(6)]
    public void ProductionForecastRebindReducesCopiedCornerWithoutChangingRealMarkCollectionsCardsOrHeat(int level)
    {
        Arm(level);
        NextInput();
        player.lap = 2; // Projected payment may grant this new lap only in its isolated copy.
        CardData shared = player.deck.Hand[0];
        Assert.That(shared.IsSpeed && shared.value == 4, Is.True);
        int engine = player.deck.heatPool.remaining;
        var hand = new List<CardData>(player.deck.Hand);
        var discard = new List<CardData>(player.deck.DiscardPile);
        var paid = new List<CardData>(player.heatPaidCardsThisTurn);
        var granted = Get<HashSet<int>>(player.driverSkill.Tuning, "grantedLaps");
        var seen = Get<HashSet<CardData>>(player.driverSkill.Tuning, "seenReceipts");
        var old = Get<HashSet<CardData>>(player.driverSkill.Tuning, "oldReceipts");
        var grantedBefore = new HashSet<int>(granted);
        var seenBefore = new HashSet<CardData>(seen);
        var oldBefore = new HashSet<CardData>(old);
        for (int i = 0; i < 2; i++)
        {
            RaceTurnPreview preview = RaceTurnPreviewRules.Evaluate(player, race, config,
                new[] { shared }, false, 1, (_, __) => true, totalCells: 60,
                crossedCorners: (_, __) => new[] { 1 }, cornerBaseLimit: _ => 2);
            Assert.That(preview.CanPay, Is.True);
            Assert.That(preview.CornerHeat, Is.EqualTo(1), "Real pending mark must rebind into the isolated forecast.");
            Assert.That(preview.HeatPaid, Is.EqualTo(1));
            Assert.That(preview.ProjectedHeat.EngineRemaining, Is.EqualTo(engine - 1));
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine));
            CollectionAssert.AreEqual(hand, player.deck.Hand);
            CollectionAssert.AreEqual(discard, player.deck.DiscardPile);
            CollectionAssert.AreEqual(paid, player.heatPaidCardsThisTurn);
            CollectionAssert.AreEquivalent(grantedBefore, granted);
            CollectionAssert.AreEquivalent(seenBefore, seen);
            CollectionAssert.AreEquivalent(oldBefore, old);
            Assert.That(player.driverSkill.Tuning.HasPending, Is.True);
            Assert.That(player.driverSkill.Tuning.HasGrantedInLap(2), Is.False);
            Assert.That(player.driverSkill.Tuning.InputsStarted, Is.EqualTo(2));
            Assert.That(shared.value, Is.EqualTo(4));
        }
        Assert.That(Corner(2, 1), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine - 1));
        Assert.That(player.driverSkill.Tuning.HasGrantedInLap(2), Is.EqualTo(level >= 4),
            "Only a real residual payment meeting this tier's threshold may grant the new lap.");
    }

    [TestCase(false)] [TestCase(true)]
    public void ForecastScheduledPitRejectsOldMarkWithoutClearingRealReadinessWhileRequestedOnlyRemainsEligible(bool scheduled)
    {
        Arm(4);
        NextInput();
        player.pitStopRequested = true;
        player.pitStopScheduled = scheduled;
        CardData speed = player.deck.Hand[0];
        int engine = player.deck.heatPool.remaining;
        var paid = new List<CardData>(player.heatPaidCardsThisTurn);
        RaceTurnPreview preview = RaceTurnPreviewRules.Evaluate(player, race, config,
            new[] { speed }, false, 1, (_, __) => true, totalCells: 60,
            crossedCorners: (_, __) => new[] { 1 }, cornerBaseLimit: _ => 2);
        Assert.That(preview.CornerHeat, Is.EqualTo(scheduled ? 2 : 1),
            "A copied scheduled-pit participant cannot consume an old input mark; a request alone is not a scheduled stop.");
        Assert.That(preview.HeatPaid, Is.EqualTo(scheduled ? 2 : 1));
        Assert.That(preview.CanPay, Is.True);
        Assert.That(player.driverSkill.Tuning.HasPending, Is.True, "Prediction cannot interrupt the real owner's pending mark.");
        Assert.That(player.driverSkill.Tuning.IsInputOpen, Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine));
        CollectionAssert.AreEqual(paid, player.heatPaidCardsThisTurn);
        Assert.That(speed.value, Is.EqualTo(4));
        Assert.That(race.ResolveOverspeedHeatCost(player, 2), Is.EqualTo(scheduled ? 2 : 1));
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False,
            "Actual resolution interrupts a scheduled stop or consumes the eligible request-only mark.");
        Assert.That(player.driverSkill.Tuning.IsInputOpen, Is.EqualTo(!scheduled));
        Assert.That(player.driverSkill.Tuning.HasGrantedInLap(1), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine), "Resolving a cost does not itself pay it.");
    }

    [TestCase("pit")] [TestCase("skip")] [TestCase("finished")] [TestCase("blown")]
    public void CleanupAndActualBlockedNextInputLoseFutureReadinessWithoutRestoringLapGrant(string blocked)
    {
        Arm(4);
        switch (blocked)
        {
            case "pit": player.pitStopScheduled = true; break;
            case "skip": player.skipNextTurn = true; break;
            case "finished": player.hasFinished = true; break;
            case "blown": player.isBlown = true; break;
        }
        int engine = player.deck.heatPool.remaining;
        Invoke("CleanupTurn", player);
        Assert.That(player.driverSkill.Tuning.IsInputOpen, Is.False);
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False, "Scheduled pit/skip/terminal cleanup clears future readiness immediately.");
        race.BeginTurn(player, 2);
        Assert.That(race.BeginDriverSkillInput(player, 60), Is.False);
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False);
        Assert.That(player.driverSkill.Tuning.InputsStarted, Is.EqualTo(1));
        Assert.That(player.driverSkill.Tuning.HasGrantedInLap(1), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine));
        player.pitStopScheduled = player.skipNextTurn = player.hasFinished = player.isBlown = false;
        BeginInput(3);
        Assert.That(Corner(1, 1), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine - 1));
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False);
    }

    [Test]
    public void PitRequestBeforeSchedulingDoesNotInterruptAnOtherwiseEligibleFutureInput()
    {
        Arm(4);
        player.pitStopRequested = true;
        Assert.That(player.pitStopScheduled, Is.False);
        Invoke("CleanupTurn", player);
        Assert.That(player.driverSkill.Tuning.HasPending, Is.True);
        BeginInput(2);
        int engine = player.deck.heatPool.remaining;
        Assert.That(Corner(1, 1), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine));
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False);
        Assert.That(player.driverSkill.Tuning.HasGrantedInLap(1), Is.True);
    }

    [TestCase(2, false)] [TestCase(4, false)] [TestCase(6, true)]
    public void OrdinaryCleanupPreservesMarkUntilSpecifiedOwnerInputLifetime(int level, bool survivesSecondFutureInput)
    {
        Arm(level);
        NextInput();
        Assert.That(player.driverSkill.Tuning.HasPending, Is.True);
        NextInput(3);
        Assert.That(player.driverSkill.Tuning.HasPending, Is.EqualTo(survivesSecondFutureInput));
        int engine = player.deck.heatPool.remaining;
        Assert.That(Corner(1, 1), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine - (survivesSecondFutureInput ? 0 : 1)));
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False);
        Assert.That(player.driverSkill.Tuning.HasGrantedInLap(1), Is.True);
    }

    [Test]
    public void ActualManagerDisableClearsPendingWindowWithoutSpendingHeatOrResettingLapGrant()
    {
        Arm(4);
        int engine = player.deck.heatPool.remaining;
        Invoke("OnDisable");
        Assert.That(player.driverSkill.Tuning.IsInputOpen, Is.False);
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False);
        Assert.That(player.driverSkill.Tuning.HasGrantedInLap(1), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine));
        Assert.That(player.driverSkill.Tuning.InputsStarted, Is.EqualTo(1));
    }

    private object Invoke(string name, params object[] arguments)
    {
        MethodInfo method = typeof(MVPGameManager).GetMethod(name, Private);
        Assert.That(method, Is.Not.Null, "Actual manager adapter changed: " + name);
        return method.Invoke(manager, arguments);
    }

    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, Private).SetValue(target, value);

    private static T Get<T>(object target, string field)
        => (T)target.GetType().GetField(field, Private).GetValue(target);
}
