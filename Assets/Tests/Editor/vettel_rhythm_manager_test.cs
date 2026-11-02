using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Integration; Assets/Tests/Editor; BLOCKING. D04 active Word243/247/251/254-258.
/// Real Manager transaction, formal cards, source-observation and cleanup adapters.
/// Controlled movement seams advance the actual-cell ledger explicitly; native animation,
/// reward tail and ordinary partial-payment/spin ordering have separate GameLoop evidence.
/// </summary>
public class VettelRhythmManagerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Audio = typeof(AudioService).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
    private object previousAudio;
    private GameObject host;
    private MVPGameManager manager;
    private GameConfigSO config;
    private RaceSession race;
    private PlayerState player;
    private RaceInputState Input => Get<RaceInputState>(manager, "inputState");
    private RacePhaseState Phase => Get<RacePhaseState>(manager, "phaseState");

    [SetUp]
    public void SetUp()
    {
        previousAudio = Audio.GetValue(null); Audio.SetValue(null, null);
        host = new GameObject("Owned Vettel rhythm API adapters"); host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>(); config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.enableTechTree = true; config.enableWeather = false; config.totalLaps = 99;
        config.minGear = 1; config.maxGear = 4; config.gearOneCooldown = config.gearTwoCooldown = 0; config.twoGearShiftHeatCost = 1;
        manager.config = config; manager.trackManager = host.AddComponent<TrackManager>(); manager.trackManager.config = config;
        Set(manager.trackManager, "nodes", Enumerable.Range(0, 60).Select(i => new TrackNode(i, 99)).ToList());
        Set(manager.trackManager, "cornerSpeedLimits", new Dictionary<int, int> { { 1, 99 } });
        Set(manager.trackManager, "cornerNames", new Dictionary<int, string> { { 1, "Owned safe native corner" } });
        race = new RaceSession(new SystemRandomSource(93)) { TeamVehicleBonusesEnabled = true }; Set(manager, "session", race);
    }

    [TearDown]
    public void TearDown()
    {
        try { if (host != null) Object.DestroyImmediate(host); if (config != null) Object.DestroyImmediate(config); }
        finally { Audio.SetValue(null, previousAudio); }
    }

    private void Create(int level = 5, int value = 1)
    {
        player = new PlayerState("Owned Vettel", false, 8, 3)
        { driverId = "de_sebastian_vettel", teamId = TeamId.DE, lap = 0, techState = new TechTreeState(TeamId.DE) };
        player.deck.InitializeExactOrder(Enumerable.Range(0, 40).Select(_ => new CardData(CardType.Speed, value)).ToArray(), new HeatPool(12));
        player.deck.DrawToHand(8); player.driverSkill.Initialize(player.DriverProfile, level, true); race.Players.Add(player); Begin(1);
    }
    private void Begin(int turn)
    {
        race.BeginTurn(player, turn); race.BeginDriverSkillInput(player, 60);
        Set(manager, "raceTurnNumber", turn); Phase.BeginGearSelection(); Input.BeginGearSelection(player.gear); player.deck.DrawToHand(8);
    }
    private VettelRhythmActivationPlan Prepare()
    {
        Assert.That(manager.TryPrepareVettelRhythmRequest(out var plan, out string reason), Is.True, reason); return plan;
    }
    private VettelRhythmActivationPlan Commit(VettelNativeSource source = VettelNativeSource.BasicCruise)
    {
        var plan = Prepare(); Assert.That(manager.TryConfirmVettelRhythmRequest(plan, source, out string reason), Is.True, reason); return plan;
    }
    private void Play(int count = 3)
    {
        var cards = player.deck.Hand.Where(c => c.IsSpeed).Take(count).ToArray(); Assert.That(cards.Length, Is.EqualTo(count));
        Assert.That(CardPlayRules.CommitSpeedCards(player, cards, count, race, 60), Is.EqualTo(SpeedCardCommitResult.Success));
    }
    private void Compute() => Invoke("ComputeMovements", new List<PlayerState> { player }, new HashSet<PlayerState>());
    private int Settle(bool corner = false, int missingCells = 0)
    {
        int old = player.position, lap = player.lap, moved = player.totalMovementThisTurn - missingCells;
        player.turnMovement.Advance(moved); // Controlled adapter seam; never presented as native animation.
        player.position = (old + moved) % 60; player.lap += (old + moved) / 60;
        return (int)Invoke("ResolveVettelFeedbackAfterMovement", player, old, lap, corner);
    }
    private void Next(int turn) { Invoke("CleanupTurn", player); Begin(turn); }

    [Test]
    public void FreePreviewAndCancelNeverReserveHeatOrSpendRaceUses()
    {
        Create(); int uses = player.driverSkill.UsesRemaining; var hand = player.deck.Hand.ToArray(); var plan = Prepare();
        CollectionAssert.AreEqual(new[] { VettelNativeSource.BasicCruise }, plan.Choices);
        manager.CancelVettelRhythmRequest(); manager.CancelVettelRhythmRequest();
        Assert.That(manager.TryConfirmVettelRhythmRequest(plan, VettelNativeSource.BasicCruise, out _), Is.False);
        CollectionAssert.AreEqual(hand, player.deck.Hand); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(12));
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses)); Assert.That(player.driverSkill.Rhythm.ManualPath, Is.False);
    }

    [Test]
    public void FixedPaymentMovesOnePermanentOriginalToHandRegistersReceiptAndConsumesOneUseOnce()
    {
        Create(); int uses = player.driverSkill.UsesRemaining; player.trickState.schwarzbrotActive = true;
        var plan = Commit(); var paid = plan.PaidHeat;
        Assert.That(paid, Is.Not.Null); Assert.That(paid.IsHeat && !paid.isTemp, Is.True);
        Assert.That(player.deck.Hand.Contains(paid), Is.True); Assert.That(player.deck.DiscardPile.Contains(paid), Is.False);
        Assert.That(player.heatPaidCardsThisTurn, Is.EquivalentTo(new[] { paid })); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11));
        Assert.That(player.trickState.schwarzbrotActive, Is.True, "Fixed fee neither applies nor consumes ordinary bread discount.");
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses - 1)); Assert.That(player.driverSkill.Rhythm.ManualPath, Is.True);
        Assert.That(player.driverSkill.Rhythm.LockedSource, Is.EqualTo(VettelNativeSource.BasicCruise));
        Assert.That(manager.TryConfirmVettelRhythmRequest(plan, VettelNativeSource.BasicCruise, out _), Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11));
    }

    [Test]
    public void ReplacedRequestIgnoresStaleConfirmAndCancelButCurrentRequestCanCommit()
    {
        Create(); var old = Prepare(); var current = Prepare(); old.Cancel();
        Assert.That(manager.TryConfirmVettelRhythmRequest(old, VettelNativeSource.BasicCruise, out _), Is.False);
        Assert.That(manager.TryConfirmVettelRhythmRequest(current, VettelNativeSource.BasicCruise, out string reason), Is.True, reason);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11)); Assert.That(player.heatPaidCardsThisTurn.Count, Is.EqualTo(1));
    }

    [TestCase("phase")] [TestCase("input")] [TestCase("tutorial")] [TestCase("disabled")]
    [TestCase("ai")] [TestCase("wrong-driver")] [TestCase("locked")] [TestCase("no-config")] [TestCase("empty-engine")]
    public void ManagerAndOwnerInputBoundsRejectWithoutAdditionalFee(string boundary)
    {
        Create();
        if (boundary == "phase") Phase.BeginCardSelection();
        if (boundary == "input") Input.ConfirmGear();
        if (boundary == "tutorial") Set(manager, "tutorialScenario", TutorialScenarioDefinition.CreateLeMansUk());
        if (boundary == "disabled") player.driverSkill.Initialize(player.DriverProfile, 5, false);
        if (boundary == "ai") player.isAI = true;
        if (boundary == "wrong-driver") { player.driverId = "de_michael_schumacher"; player.driverSkill.Initialize(player.DriverProfile, 5, true); }
        if (boundary == "locked") player.driverSkill.Initialize(player.DriverProfile, 2, true);
        if (boundary == "no-config") manager.config = null;
        if (boundary == "empty-engine") player.deck.DrawHeatFromPoolToHand(12);
        int engine = player.deck.heatPool.remaining, uses = player.driverSkill.UsesRemaining;
        Assert.That(manager.TryPrepareVettelRhythmRequest(out _, out _), Is.False, boundary);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine)); Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [TestCase("gear")] [TestCase("engine")] [TestCase("card-value")] [TestCase("turn")] [TestCase("nodes")]
    public void LateConfirmationRejectsChangedExactSnapshotBeforeAnyFeeOrUse(string change)
    {
        Create(); var plan = Prepare(); int uses = player.driverSkill.UsesRemaining;
        if (change == "gear") manager.OnGearButtonClicked(4);
        if (change == "engine") player.deck.DrawHeatFromPoolToHand(1);
        if (change == "card-value") player.deck.Hand[0].value = 2;
        if (change == "turn") Set(manager, "raceTurnNumber", 2);
        if (change == "nodes") player.techState.activeNodeIds.Add("de-l1-schwarzbier-fuel");
        int engine = player.deck.heatPool.remaining;
        Assert.That(manager.TryConfirmVettelRhythmRequest(plan, VettelNativeSource.BasicCruise, out _), Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine)); Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [TestCase(VettelNativeSource.PaidBeer)] [TestCase(VettelNativeSource.GermanCornerExit)]
    public void LockedTechnologySourceCannotBeSelectedWithoutItsActualNode(VettelNativeSource source)
    {
        Create(); var plan = Prepare(); Assert.That(plan.Choices.Contains(source), Is.False);
        Assert.That(manager.TryConfirmVettelRhythmRequest(plan, source, out _), Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(12)); Assert.That(player.driverSkill.Rhythm.HasWindow, Is.False);
    }

    [Test]
    public void OwnedTechnologySourcesAppearAndEntireWindowKeepsSingleCommittedSource()
    {
        Create(); player.techState.activeNodeIds.UnionWith(new[] { "de-l1-schwarzbier-fuel", "de-l2-wurstplatte" });
        var plan = Prepare(); CollectionAssert.AreEquivalent(new[] { VettelNativeSource.BasicCruise, VettelNativeSource.PaidBeer, VettelNativeSource.GermanCornerExit }, plan.Choices);
        Assert.That(manager.TryConfirmVettelRhythmRequest(plan, VettelNativeSource.PaidBeer, out string reason), Is.True, reason);
        Assert.That(manager.TryPrepareVettelRhythmRequest(out _, out _), Is.False);
        Next(2); Assert.That(player.driverSkill.Rhythm.LockedSource, Is.EqualTo(VettelNativeSource.PaidBeer));
        Assert.That(manager.TryPrepareVettelRhythmRequest(out _, out _), Is.False);
    }

    [TestCase(3, 1)] [TestCase(5, 2)] [TestCase(7, 2)]
    public void RealCruiseAmplifiesOncePerEligibleInputAndNeverChargesContinuationOrEachCard(int level, int inputs)
    {
        Create(level); int uses = player.driverSkill.UsesRemaining; Commit();
        for (int turn = 1; turn <= inputs; turn++)
        {
            if (turn > 1) Next(turn);
            Assert.That(player.driverSkill.Rhythm.InputsStarted, Is.EqualTo(turn)); Play(); Compute();
            Assert.That(player.totalMovementThisTurn, Is.EqualTo(7), "Three formal value-one cards keep native base7 before driver tail.");
            Assert.That(player.driverSkill.Rhythm.BonusThisInput, Is.Zero, "Pure plan is not a settled source.");
            int old = player.position, lap = player.lap;
            Assert.That(Settle(), Is.EqualTo(2), "One native source reward for the whole input, not +2 per value-one card.");
            Assert.That((int)Invoke("ResolveVettelFeedbackAfterMovement", player, old, lap, false), Is.Zero);
            Assert.That(player.driverSkill.Rhythm.BonusThisInput, Is.EqualTo(2));
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11)); Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
        }
        Next(inputs + 1); Assert.That(player.driverSkill.Rhythm.HasWindow, Is.False);
        Assert.That(player.driverSkill.Rhythm.BonusThisInput, Is.Zero); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11));
    }

    [TestCase("no-value-one")] [TestCase("partial-base")]
    public void MissingNativeSourceOrIncompleteRealMovementLosesOpportunityWithoutRefund(string boundary)
    {
        Create(value: boundary == "no-value-one" ? 2 : 1); Commit(); Play(); Compute();
        Assert.That(Settle(missingCells: boundary == "partial-base" ? 1 : 0), Is.Zero);
        Assert.That(player.driverSkill.Rhythm.BonusThisInput, Is.Zero); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11));
        Next(2); Next(3); Assert.That(player.driverSkill.Rhythm.HasWindow, Is.False);
    }

    [Test]
    public void RealGermanCornerExitIsRequiredForLockedExitSourceWhileOtherCruiseCannotSubstitute()
    {
        Create(value: 2); player.techState.activeNodeIds.Add("de-l2-wurstplatte"); Commit(VettelNativeSource.GermanCornerExit);
        Get<List<TrackNode>>(manager.trackManager, "nodes")[9] = new TrackNode(9, 99, "Owned native exit", 1, false, true);
        Play(); Compute(); Assert.That(Settle(true), Is.EqualTo(2));
        Assert.That(player.driverSkill.Rhythm.BonusThisInput, Is.EqualTo(2));
    }

    [Test]
    public void RealThreeBeerPaymentsGiveSixNativePlusOnlyTwoDriverCellsAndRefundKeepsUsesAndQuota()
    {
        Create(7, 2); player.techState.activeNodeIds.UnionWith(new[] { "de-l1-schwarzbier-fuel", "de-l3-grill-spezial" });
        Assert.That(race.TryDeclareGermanBurst(player, true, 1), Is.True); int uses = player.driverSkill.UsesRemaining;
        var paid = Commit(VettelNativeSource.PaidBeer).PaidHeat;
        Assert.That(player.techState.germanBurst.Receipts.Contains(paid), Is.True);
        Phase.BeginCardSelection(); Input.BeginCardSelection();
        for (int i = 0; i < 3; i++)
        {
            Assert.That(manager.TryPrepareGermanFuelRequest(out var plan, out string reason), Is.True, reason);
            Assert.That(manager.TryConfirmGermanFuelRequest(plan, out reason), Is.True, reason);
        }
        Assert.That(player.techState.germanBurst.FuelMovement, Is.EqualTo(6)); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(8));
        Assert.That(manager.TryPrepareGermanFuelRequest(out _, out _), Is.False);
        Play(); Compute(); int driver = Settle(); Assert.That(driver, Is.EqualTo(2));
        Assert.That(player.techState.germanBurst.FuelMovement + driver, Is.EqualTo(8));
        Assert.That(player.techState.germanBurst.FuelUsesThisTurn, Is.EqualTo(3));
        Next(2); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(12), "Declared exact4 receipts include active fee and three beer payments.");
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses - 1)); Assert.That(player.driverSkill.Rhythm.HasWindow, Is.True);
        Phase.BeginCardSelection(); Input.BeginCardSelection(); Assert.That(manager.TryPrepareGermanFuelRequest(out _, out _), Is.False);
        Play(); Compute(); Assert.That(Settle(), Is.Zero, "Second paid-beer input may have no same-lap fuel allowance.");
    }

    [Test]
    public void ManualPathSuppressesLegacyGearWaiverAndGenericFreeReactivation()
    {
        Create(); player.gear = 1; Input.BeginGearSelection(1); Commit(); int uses = player.driverSkill.UsesRemaining;
        Invoke("ApplyGearShift", player, 3); Assert.That(player.gear, Is.EqualTo(3)); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(10));
        manager.OnDriverSkillButtonClicked(); Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(10));
    }

    [TestCase("pit")] [TestCase("skip")] [TestCase("finished")] [TestCase("blown")] [TestCase("exit")]
    public void ProductionLifecycleEndsPaidWindowWithoutRefundingFixedFeeOrUses(string boundary)
    {
        Create(); var paid = Commit().PaidHeat; int uses = player.driverSkill.UsesRemaining;
        if (boundary == "exit") Invoke("OnDisable");
        else
        {
            if (boundary == "pit") player.pitStopScheduled = true;
            if (boundary == "skip") player.skipNextTurn = true;
            if (boundary == "finished") player.hasFinished = true;
            if (boundary == "blown") player.isBlown = true;
            Invoke("CleanupTurn", player);
        }
        Assert.That(player.driverSkill.Rhythm.HasWindow, Is.False); Assert.That(player.driverSkill.Rhythm.ManualPath, Is.True);
        Assert.That(player.deck.ContainsInHand(paid), Is.True); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11));
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [Test]
    public void ForecastDeepCopyShowsSingleSourceRewardWithoutSpendingRealHeatOrRewardOpportunity()
    {
        Create(); Commit(); var selected = player.deck.Hand.Where(c => c.IsSpeed).Take(3).ToArray();
        var hand = player.deck.Hand.ToArray(); var receipts = player.heatPaidCardsThisTurn.ToArray(); int uses = player.driverSkill.UsesRemaining;
        for (int i = 0; i < 2; i++)
        {
            var preview = RaceTurnPreviewRules.Evaluate(player, race, config, selected, false, 3, totalCells: 60);
            Assert.That(preview.Movement.TotalCells, Is.EqualTo(9)); Assert.That(player.driverSkill.Rhythm.BonusThisInput, Is.Zero);
            Assert.That(preview.Movement.Entries.Where(e => e.Kind == TurnMovementKind.Driver && e.Label.Contains("人车合拍")).Sum(e => e.EffectiveCells), Is.EqualTo(2));
            CollectionAssert.AreEqual(hand, player.deck.Hand); CollectionAssert.AreEqual(receipts, player.heatPaidCardsThisTurn);
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11)); Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
            Assert.That(selected.All(c => c.type == CardType.Speed && c.value == 1), Is.True);
        }
        Play(); Compute(); Assert.That(Settle(), Is.EqualTo(2));
    }

    private object Invoke(string name, params object[] args)
    {
        var method = typeof(MVPGameManager).GetMethod(name, Private); Assert.That(method, Is.Not.Null, name); return method.Invoke(manager, args);
    }
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
}
