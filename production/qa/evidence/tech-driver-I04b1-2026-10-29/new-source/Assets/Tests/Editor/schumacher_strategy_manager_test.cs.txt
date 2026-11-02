using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Integration; Assets/Tests/Editor; BLOCKING. D03 Word paragraphs 195/199/203/207.
/// Real transaction, gear, formal cards, movement, cleanup and forecast adapters.
/// Owned inactive objects; API-only I04b1 evidence, not player-facing UI or mode acceptance.
/// </summary>
public class SchumacherStrategyManagerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Audio = typeof(AudioService).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
    private GameObject host;
    private MVPGameManager manager;
    private GameConfigSO config;
    private RaceSession race;
    private PlayerState player;
    private object previousAudio;
    private RaceInputState Input => Get<RaceInputState>(manager, "inputState");
    private RacePhaseState Phase => Get<RacePhaseState>(manager, "phaseState");

    [SetUp]
    public void SetUp()
    {
        previousAudio = Audio.GetValue(null); Audio.SetValue(null, null);
        host = new GameObject("Owned Schumacher strategy adapters", typeof(RectTransform)); host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.enableTechTree = true; config.enableWeather = false;
        config.totalLaps = 99; config.minGear = 1; config.maxGear = 4;
        config.gearOneCooldown = config.gearTwoCooldown = 0; config.twoGearShiftHeatCost = 1;
        manager.config = config;
        manager.trackManager = host.AddComponent<TrackManager>(); manager.trackManager.config = config;
        Set(manager.trackManager, "nodes", Enumerable.Range(0, 60).Select(i => new TrackNode(i, 99)).ToList());
        Set(manager.trackManager, "cornerSpeedLimits", new Dictionary<int, int> { { 1, 2 } });
        Set(manager.trackManager, "cornerNames", new Dictionary<int, string> { { 1, "Owned paid corner" } });
        manager.cardHandUI = Child("Inactive hand adapter").AddComponent<CardHandUI>();
        manager.cardHandUI.enabled = false; manager.cardHandUI.handContainer = manager.cardHandUI.transform;
        var template = Child("Inactive card template"); template.SetActive(false); template.AddComponent<CardUI>();
        manager.cardHandUI.cardPrefab = template;
        race = new RaceSession(new SystemRandomSource(73)) { TeamVehicleBonusesEnabled = false };
        Set(manager, "session", race);
    }

    [TearDown]
    public void TearDown()
    {
        try { if (host != null) Object.DestroyImmediate(host); if (config != null) Object.DestroyImmediate(config); }
        finally { Audio.SetValue(null, previousAudio); }
    }

    private void Create(int level = 5)
    {
        Assert.That(DriverCatalog.TryGet("de_michael_schumacher", out DriverProfile profile), Is.True);
        player = new PlayerState("Owned Schumacher", false, 8, 1)
        { driverId = profile.Id, teamId = TeamId.DE, lap = 1, techState = new TechTreeState(TeamId.DE) };
        player.deck.InitializeExactOrder(Enumerable.Range(0, 40).Select(_ => new CardData(CardType.Speed, 1)).ToArray(), new HeatPool(12));
        player.deck.DrawToHand(8); player.driverSkill.Initialize(profile, level, true); race.Players.Add(player); Begin(1, 1);
    }

    private void Begin(int turn, int gear)
    {
        race.BeginTurn(player, turn); race.BeginDriverSkillInput(player, 60);
        Set(manager, "raceTurnNumber", turn); Phase.BeginGearSelection(); Input.BeginGearSelection(player.gear); Input.SelectGear(gear);
        player.deck.DrawToHand(8);
    }
    private SchumacherStrategyActivationPlan Prepare()
    {
        Assert.That(manager.TryPrepareSchumacherStrategyRequest(out var plan, out string reason), Is.True, reason);
        return plan;
    }
    private SchumacherStrategyActivationPlan Commit()
    {
        var plan = Prepare();
        Assert.That(manager.TryConfirmSchumacherStrategyRequest(plan, plan.IsExecution ? null : plan.Choices[0], out string reason), Is.True, reason);
        return plan;
    }
    private void FormalCards(int count)
    {
        var cards = player.deck.Hand.Where(c => c.type == CardType.Speed && !c.isTemp).Take(count).ToList();
        Assert.That(cards.Count, Is.EqualTo(count));
        Assert.That(CardPlayRules.CommitSpeedCards(player, cards, player.gear, race, 60), Is.EqualTo(SpeedCardCommitResult.Success));
    }
    private void Compute() => Invoke("ComputeMovements", new List<PlayerState> { player }, new HashSet<PlayerState>());
    private void PreparationThenExecution(int level = 5)
    {
        Create(level); Commit(); Invoke("ApplyGearShift", player, 1); FormalCards(1); Compute();
        Assert.That(player.totalMovementThisTurn, Is.EqualTo(1));
        Invoke("CleanupTurn", player); Begin(2, 3);
        Assert.That(player.driverSkill.Strategy.IsExecutionAvailable, Is.True);
    }

    [Test]
    public void PreparationPreviewAndRepeatedCancelLeaveAllResourcesAndUsesUnchanged()
    {
        Create(); var hand = player.deck.Hand.ToArray(); int uses = player.driverSkill.UsesRemaining;
        var plan = Prepare(); Assert.That(plan.IsExecution, Is.False);
        manager.CancelSchumacherStrategyRequest(); manager.CancelSchumacherStrategyRequest(); plan.Cancel();
        Assert.That(manager.TryConfirmSchumacherStrategyRequest(plan, hand[0], out _), Is.False);
        CollectionAssert.AreEqual(hand, player.deck.Hand); Assert.That(player.deck.DiscardPileCount, Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(12)); Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
        Assert.That(player.driverSkill.Strategy.ManualPath, Is.False);
    }

    [Test]
    public void PreparationDiscardsExactOriginalRefreshesHandAndConsumesOnlyOneUse()
    {
        Create(); int uses = player.driverSkill.UsesRemaining;
        var plan = Prepare(); var chosen = plan.Choices[0]; var equalValueOther = plan.Choices[1];
        Assert.That(manager.TryConfirmSchumacherStrategyRequest(plan, chosen, out string reason), Is.True, reason);
        Assert.That(player.deck.DiscardPile.Single(), Is.SameAs(chosen));
        Assert.That(player.deck.Hand.Contains(chosen), Is.False); Assert.That(player.deck.Hand.Contains(equalValueOther), Is.True);
        Assert.That(Get<List<CardUI>>(manager.cardHandUI, "cardUIs").Count, Is.EqualTo(player.deck.Hand.Count));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(12)); Assert.That(player.heatPaidCardsThisTurn, Is.Empty);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
        Assert.That(player.driverSkill.Strategy.MovementBonusThisInput, Is.Zero);
        Assert.That(manager.TryConfirmSchumacherStrategyRequest(plan, chosen, out _), Is.False);
        Assert.That(player.deck.DiscardPileCount, Is.EqualTo(1));
    }

    [Test]
    public void ReplacedPreparationIgnoresStaleConfirmAndStaleCancel()
    {
        Create(); var old = Prepare(); var current = Prepare();
        Assert.That(manager.TryConfirmSchumacherStrategyRequest(old, old.Choices[0], out _), Is.False); old.Cancel();
        Assert.That(manager.TryConfirmSchumacherStrategyRequest(current, current.Choices[0], out string reason), Is.True, reason);
        Assert.That(player.deck.DiscardPileCount, Is.EqualTo(1));
    }

    [TestCase("phase")] [TestCase("input")] [TestCase("tutorial")] [TestCase("disabled")]
    [TestCase("ai")] [TestCase("wrong-driver")] [TestCase("locked")] [TestCase("high-gear")] [TestCase("no-config")]
    public void PreparationRejectsNonEligibleManagerOrParticipantScopeWithoutCharge(string boundary)
    {
        Create();
        switch (boundary)
        {
            case "phase": Phase.BeginCardSelection(); break;
            case "input": Input.ConfirmGear(); break;
            case "tutorial": Set(manager, "tutorialScenario", TutorialScenarioDefinition.CreateLeMansUk()); break;
            case "disabled": player.driverSkill.Initialize(player.DriverProfile, 5, false); break;
            case "ai": player.isAI = true; break;
            case "wrong-driver": player.driverId = "de_sebastian_vettel"; player.driverSkill.Initialize(player.DriverProfile, 5, true); break;
            case "locked": player.driverSkill.Initialize(player.DriverProfile, 1, true); break;
            case "high-gear": Input.SelectGear(3); break;
            case "no-config": manager.config = null; break;
        }
        int uses = player.driverSkill.UsesRemaining;
        Assert.That(manager.TryPrepareSchumacherStrategyRequest(out _, out _), Is.False, boundary);
        Assert.That(player.deck.Hand.Count, Is.EqualTo(8)); Assert.That(player.deck.DiscardPileCount, Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(12)); Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [TestCase("value")] [TestCase("engine")] [TestCase("turn")] [TestCase("pending-gear")]
    public void ChangedSnapshotRejectsLateConfirmationWithoutDiscardOrUse(string mutation)
    {
        Create(); var plan = Prepare(); int uses = player.driverSkill.UsesRemaining;
        if (mutation == "value") plan.Choices[0].value++;
        if (mutation == "engine") player.deck.DrawHeatFromPoolToHand(1);
        if (mutation == "turn") Set(manager, "raceTurnNumber", 2);
        if (mutation == "pending-gear") manager.OnGearButtonClicked(2);
        int engine = player.deck.heatPool.remaining; int hand = player.deck.Hand.Count;
        Assert.That(manager.TryConfirmSchumacherStrategyRequest(plan, plan.Choices[0], out _), Is.False);
        Assert.That(player.deck.DiscardPileCount, Is.Zero); Assert.That(player.deck.Hand.Count, Is.EqualTo(hand));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine)); Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [Test]
    public void CommittedPreparationBlocksRealHighGearClickShiftAndInvalidConfirmation()
    {
        Create(); Commit(); manager.OnGearButtonClicked(4); Assert.That(Input.PendingGear, Is.EqualTo(1));
        Invoke("ApplyGearShift", player, 4); Assert.That(player.gear, Is.EqualTo(1));
        Input.SelectGear(4); manager.OnConfirmGearClicked(); Assert.That(Input.WaitingForGear, Is.True);
        Assert.That(player.deck.DiscardPileCount, Is.EqualTo(1)); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(12));
        Assert.That(player.driverSkill.Strategy.MovementBonusThisInput, Is.Zero);
    }

    [TestCase(3, 2)] [TestCase(5, 3)] [TestCase(7, 3)]
    public void TwoExecutionInputsPaySeparateFixedReceiptsAndMovementWithoutExtraUse(int level, int bonus)
    {
        PreparationThenExecution(level); int uses = player.driverSkill.UsesRemaining;
        for (int turn = 2; turn <= 3; turn++)
        {
            var plan = Commit(); Assert.That(plan.IsExecution, Is.True); Assert.That(plan.ExecutionNumber, Is.EqualTo(turn - 1));
            Assert.That(plan.PaidHeat, Is.Not.Null); Assert.That(plan.PaidHeat.IsHeat && !plan.PaidHeat.isTemp, Is.True);
            Assert.That(player.deck.Hand.Contains(plan.PaidHeat), Is.True); Assert.That(player.heatPaidCardsThisTurn.Contains(plan.PaidHeat), Is.True);
            Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
            Invoke("ApplyGearShift", player, 3); FormalCards(3); Compute();
            Assert.That(player.cornerTotalThisTurn, Is.EqualTo(3)); Assert.That(player.totalMovementThisTurn, Is.EqualTo(3 + bonus));
            var tail = RaceMovementRules.GetHunterActiveCornerTail(player, player.position);
            Assert.That(tail.start, Is.EqualTo(player.position + 3)); Assert.That(tail.end, Is.EqualTo(player.position + 3 + bonus));
            Assert.That(DriverSkillRules.GetSlipstreamBonus(player.driverSkill), Is.Zero);
            Assert.That(DriverSkillRules.ReduceCornerHeat(player.driverSkill, 2), Is.EqualTo(2));
            Invoke("CleanupTurn", player); Begin(turn + 1, 3);
        }
        Assert.That(player.driverSkill.Strategy.IsExecutionAvailable, Is.False);
        Assert.That(player.driverSkill.Strategy.MovementBonusThisInput, Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(9), "Two fixed execution fees plus ordinary gear 1 to 3 fee.");
        manager.OnDriverSkillButtonClicked(); Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses), "Legacy free activation must stay closed after manual path.");
    }

    [Test]
    public void ExecutionSupersedeThenStaleCancelCannotDeclineReplacementAndFixedCostBypassesBread()
    {
        PreparationThenExecution(); player.trickState.schwarzbrotActive = true;
        var old = Prepare(); var current = Prepare(); old.Cancel();
        Assert.That(manager.TryConfirmSchumacherStrategyRequest(old, null, out _), Is.False);
        Assert.That(manager.TryConfirmSchumacherStrategyRequest(current, null, out string reason), Is.True, reason);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11));
        Assert.That(player.trickState.schwarzbrotActive, Is.True, "Fixed fee must neither apply nor consume ordinary fee discount.");
        Assert.That(player.heatPaidCardsThisTurn.Single(), Is.SameAs(current.PaidHeat));
    }

    [Test]
    public void UnpaidExecutionCancelDeclinesRemainingInputsForFree()
    {
        PreparationThenExecution(); var plan = Prepare(); int uses = player.driverSkill.UsesRemaining;
        manager.CancelSchumacherStrategyRequest(); Assert.That(manager.TryConfirmSchumacherStrategyRequest(plan, null, out _), Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(12)); Assert.That(player.heatPaidCardsThisTurn, Is.Empty);
        Assert.That(player.driverSkill.Strategy.IsExecutionAvailable, Is.False);
        Assert.That(manager.TryPrepareSchumacherStrategyRequest(out _, out _), Is.False);
        Invoke("CleanupTurn", player); Begin(3, 3);
        Assert.That(player.driverSkill.Strategy.IsExecutionAvailable, Is.False); Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [Test]
    public void MissingFormalCardPaysOrdinaryPenaltyWithoutSpinButCancelsPaidMovementAndContinuation()
    {
        PreparationThenExecution(); var plan = Commit(); Invoke("ApplyGearShift", player, 3); FormalCards(2);
        Phase.BeginCardSelection(); Input.BeginCardSelection(); Invoke("FinishPlayerCardPhase", player); Compute();
        Assert.That(player.isBlown, Is.False); Assert.That(player.spinCounter, Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(9), "Fixed one, ordinary shift one and missing card one remain paid.");
        Assert.That(player.deck.Hand.Contains(plan.PaidHeat), Is.True); Assert.That(player.totalMovementThisTurn, Is.EqualTo(2));
        Assert.That(player.driverSkill.Strategy.MovementBonusThisInput, Is.Zero);
        Invoke("CleanupTurn", player); Begin(3, 3); Assert.That(player.driverSkill.Strategy.IsExecutionAvailable, Is.False);
    }

    [TestCase("skip")] [TestCase("finished")] [TestCase("blown")] [TestCase("disabled")]
    public void InactiveResolutionOrManagerExitClearsPaidSequenceWithoutRefund(string boundary)
    {
        PreparationThenExecution(); var plan = Commit(); int uses = player.driverSkill.UsesRemaining;
        if (boundary == "disabled") Invoke("OnDisable");
        else
        {
            var skipped = new HashSet<PlayerState>();
            if (boundary == "skip") skipped.Add(player);
            if (boundary == "finished") player.hasFinished = true;
            if (boundary == "blown") player.isBlown = true;
            Invoke("ComputeMovements", new List<PlayerState> { player }, skipped);
        }
        Assert.That(player.driverSkill.Strategy.MovementBonusThisInput, Is.Zero);
        Assert.That(player.driverSkill.Strategy.IsExecutionAvailable, Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11)); Assert.That(player.deck.Hand.Contains(plan.PaidHeat), Is.True);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [TestCase(3, 2)] [TestCase(5, 3)]
    public void ForecastDeepCopyRetainsPaidBonusOnlyWithEnoughSelectedOrdinaryCards(int level, int bonus)
    {
        PreparationThenExecution(level); Commit(); Invoke("ApplyGearShift", player, 3);
        var selected = player.deck.Hand.Where(c => c.IsSpeed).Take(3).ToArray();
        var hand = player.deck.Hand.ToArray(); var discard = player.deck.DiscardPile.ToArray(); var receipts = player.heatPaidCardsThisTurn.ToArray();
        int engine = player.deck.heatPool.remaining, version = player.driverSkill.Strategy.Version;
        for (int i = 0; i < 2; i++)
        {
            var forecast = RaceTurnPreviewRules.Evaluate(player, race, config, selected, false, 3, totalCells: 60);
            Assert.That(forecast.Movement.TotalCells, Is.EqualTo(3 + bonus));
            var missing = RaceTurnPreviewRules.Evaluate(player, race, config, selected.Take(2).ToArray(), false, 3, totalCells: 60);
            Assert.That(missing.MissingCards, Is.EqualTo(1)); Assert.That(missing.Movement.TotalCells, Is.EqualTo(2));
            CollectionAssert.AreEqual(hand, player.deck.Hand); CollectionAssert.AreEqual(discard, player.deck.DiscardPile);
            CollectionAssert.AreEqual(receipts, player.heatPaidCardsThisTurn); Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine)); Assert.That(player.driverSkill.Strategy.Version, Is.EqualTo(version));
            Assert.That(player.driverSkill.Strategy.MovementBonusThisInput, Is.EqualTo(bonus));
            Assert.That(selected.All(c => c.value == 1 && c.type == CardType.Speed && !c.isTemp), Is.True);
        }
    }

    [Test]
    public void StrategyTailCrossingNewApexActuallyPaysOrdinaryCornerHeatAndRecordsReceipt()
    {
        PreparationThenExecution(); Commit(); Invoke("ApplyGearShift", player, 3); FormalCards(3); Compute();
        var nodes = Get<List<TrackNode>>(manager.trackManager, "nodes");
        nodes[12] = new TrackNode(12, 2, "Owned tail apex", 1, false, true);
        int engine = player.deck.heatPool.remaining;
        int receiptCount = player.heatPaidCardsThisTurn.Count;
        Assert.That((bool)Invoke("ResolveBaseMovementCorners", player, 8, 11, 1), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine - 1), "Apex 12 is outside base endpoint 11 but inside paid strategy tail.");
        Assert.That(player.heatPaidCardsThisTurn.Count, Is.EqualTo(receiptCount + 1));
        var ordinaryFee = player.heatPaidCardsThisTurn.Last();
        Assert.That(player.deck.Hand.Contains(ordinaryFee), Is.True);
        Assert.That(ordinaryFee.IsHeat && !ordinaryFee.isTemp, Is.True);
        Assert.That(player.spinCounter, Is.Zero);
    }

    [Test]
    public void ScheduledPitCleanupEndsRemainingSequenceButKeepsFixedFeeAndSpentUse()
    {
        PreparationThenExecution(); var paid = Commit().PaidHeat; int uses = player.driverSkill.UsesRemaining;
        player.pitStopScheduled = true; Invoke("CleanupTurn", player);
        Assert.That(player.driverSkill.Strategy.MovementBonusThisInput, Is.Zero);
        player.pitStopScheduled = false; Begin(3, 3);
        Assert.That(player.driverSkill.Strategy.IsExecutionAvailable, Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11));
        Assert.That(player.deck.Hand.Contains(paid), Is.True); Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [Test]
    public void CleanupAdapterExceptionClosesPaidSequenceAndRethrowsWithoutRefund()
    {
        PreparationThenExecution(); var paid = Commit().PaidHeat; int uses = player.driverSkill.UsesRemaining;
        var failure = new InvalidOperationException("Owned cleanup callback failure");
        var actual = Assert.Throws<InvalidOperationException>(() => RaceTurnCleanup.Execute(player,
            p => Invoke("ResolveCleanupSkills", p), null, _ => throw failure));
        Assert.That(actual, Is.SameAs(failure));
        Assert.That(player.driverSkill.Strategy.IsInputOpen, Is.False);
        Assert.That(player.driverSkill.Strategy.MovementBonusThisInput, Is.Zero);
        Begin(3, 3); Assert.That(player.driverSkill.Strategy.IsExecutionAvailable, Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11));
        Assert.That(player.deck.Hand.Contains(paid), Is.True); Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [Test]
    public void DeclaredGermanBurstReturnsExactFixedHandReceiptAndOrdinaryShiftButKeepsPreDeclarationHeat()
    {
        PreparationThenExecution();
        player.techState.activeNodeIds.UnionWith(new[] { "de-l1-schwarzbier-fuel", "de-l3-grill-spezial" });
        player.deck.DrawHeatFromPoolToHand(1); var oldHeat = player.deck.Hand.Last();
        Assert.That(race.TryDeclareGermanBurst(player, true, 2), Is.True);
        int uses = player.driverSkill.UsesRemaining;
        var fixedHeat = Commit().PaidHeat;
        Assert.That(player.techState.germanBurst.Receipts.Contains(fixedHeat), Is.True);
        Invoke("ApplyGearShift", player, 3);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(9));
        Invoke("CleanupTurn", player);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11), "Declared fixed fee plus ordinary shift return, old heat remains outside.");
        Assert.That(player.deck.Hand.Contains(oldHeat), Is.True);
        Assert.That(player.deck.Hand.Contains(fixedHeat), Is.False);
        Assert.That(player.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(1));
        Assert.That(player.techState.grillSpezialUsed, Is.True);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }
    private GameObject Child(string name) { var child = new GameObject(name, typeof(RectTransform)); child.transform.SetParent(host.transform, false); return child; }
    private object Invoke(string method, params object[] args) => typeof(MVPGameManager).GetMethod(method, Private).Invoke(manager, args);
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
}
