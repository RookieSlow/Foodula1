using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Real fixed-fuel request/commit boundaries. HUD button and complete races are separate acceptance.</summary>
public class GermanFuelActivationPlanTests
{
    private RaceSession race;
    private PlayerState player;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        race = new RaceSession(new SystemRandomSource(8));
        player = new PlayerState("DE", false, 0, 1) { teamId = TeamId.DE, techState = new TechTreeState(TeamId.DE) };
        player.techState.activeNodeIds.UnionWith(new[] { "de-l1-schwarzbier-fuel", "de-l3-grill-spezial" });
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 3) }, new HeatPool(5));
        player.deck.DrawToHand(2); race.Players.Add(player); race.BeginTurn(player, 1);
    }

    private GermanFuelActivationPlan Prepare()
    {
        Assert.That(GermanFuelActivationPlan.TryCreate(race, player, 1, 7, true, true, out var plan, out _), Is.True);
        return plan;
    }

    [TestCase(false)] [TestCase(true)]
    public void RealOneHeatReceiptCommitsExactlyOnceWithoutDiscardingSpeed(bool declared)
    {
        if (declared) Assert.That(race.TryDeclareGermanBurst(player, true, 1), Is.True);
        var plan = Prepare(); int version = player.techState.germanBurst.Version;
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(5));
        Assert.That(player.techState.germanBurst.Version, Is.EqualTo(version));
        Assert.That(player.techState.grillSpezialUsed, Is.False);
        Assert.That(plan.TryCommit(race, player, 1, 7, true, true, out _), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
        Assert.That(player.deck.Hand.Count, Is.EqualTo(2));
        Assert.That(player.deck.DiscardPile, Is.EquivalentTo(new[] { plan.PaidHeat }));
        Assert.That(player.heatPaidCardsThisTurn, Is.EquivalentTo(new[] { plan.PaidHeat }));
        Assert.That(player.techState.germanBurst.FuelMovement, Is.EqualTo(2));
        Assert.That(player.techState.grillSpezialUsed, Is.EqualTo(declared));
        Assert.That(plan.TryCommit(race, player, 1, 7, true, true, out _), Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
    }

    [Test]
    public void CancelledFuelRequestLeavesUnpaidDeclarationOpenAndNoSpeedCardIsRequired()
    {
        player.deck.RemoveFromHand(new System.Collections.Generic.List<CardData>(player.deck.Hand));
        Assert.That(race.TryDeclareGermanBurst(player, true, 1), Is.True);
        var plan = Prepare(); plan.Cancel();
        Assert.That(plan.TryCommit(race, player, 1, 7, true, true, out _), Is.False);
        Assert.That(player.techState.germanBurst.Declared, Is.True);
        Assert.That(player.techState.grillSpezialUsed, Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(5));
        Assert.That(Prepare().TryCommit(race, player, 1, 7, true, true, out _), Is.True);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void ForeignSessionOwnerTurnRequestOrClosedGateCannotCharge(int change)
    {
        var plan = Prepare(); var currentRace = race; var currentPlayer = player;
        if (change == 0) currentRace = new RaceSession(new SystemRandomSource(9));
        if (change == 1) currentPlayer = new PlayerState("other DE", false, 0, 1)
            { teamId = TeamId.DE, deck = player.deck, techState = player.techState };
        Assert.That(plan.TryCommit(currentRace, currentPlayer, change == 2 ? 2 : 1, change == 3 ? 8 : 7,
            change != 4, change != 5, out _), Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(5));
        Assert.That(player.heatPaidCardsThisTurn, Is.Empty);
        Assert.That(player.techState.germanBurst.FuelUsesThisTurn, Is.Zero);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
    [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)] [TestCase(16)] [TestCase(17)]
    [TestCase(18)] [TestCase(19)] [TestCase(20)] [TestCase(21)] [TestCase(22)] [TestCase(23)]
    [TestCase(24)] [TestCase(25)] [TestCase(26)] [TestCase(27)] [TestCase(28)] [TestCase(29)] [TestCase(30)] [TestCase(31)]
    public void ChangedLiveSnapshotRejectsBeforeAnyAdditionalPaymentOrBudgetMutation(int change)
    {
        var plan = Prepare();
        switch (change)
        {
            case 0: player.deck.heatPool.remaining--; break;
            case 1: player.deck = player.deck.CreatePresentationCopy(); break;
            case 2: player.deck.heatPool = new HeatPool(5); break;
            case 3: player.techState = player.techState.CreatePresentationCopy(); break;
            case 4: player.techState.germanBurst = player.techState.germanBurst.CreatePresentationCopy(); break;
            case 5: player.lap++; break;
            case 6: player.position++; break;
            case 7: player.gear++; break;
            case 8: player.teamId = TeamId.UK; break;
            case 9: player.techState.teamId = TeamId.UK; break;
            case 10: player.techState.sunNeverSetsTarget = TeamId.DE; break;
            case 11: player.techState.activeNodeIds.Add("de-l2-wurstplatte"); break;
            case 12: player.techState.activeNodeIds.Remove("de-l1-schwarzbier-fuel"); break;
            case 13: player.techState.germanBurst.SealDeclaration(); break;
            case 14: player.deck.Hand[0].value++; break;
            case 15: player.deck.Hand[0].type = CardType.Heat; break;
            case 16: player.deck.Hand[0].isTemp = true; break;
            case 17: player.deck.Hand[0].trickId = "changed"; break;
            case 18:
                player.deck.RemoveFromHand(new System.Collections.Generic.List<CardData> { player.deck.Hand[0] });
                player.deck.AddCardsToHand(new[] { new CardData(CardType.Speed, 2) }); break;
            case 19: player.deck.DrawToHand(3); break;
            case 20: player.deck.DiscardPlayableCardsFromHand(new[] { player.deck.Hand[0] }); break;
            case 21: player.deck.DrawPile[0].value++; break;
            case 22: player.hasFinished = true; break;
            case 23: player.isBlown = true; break;
            case 24: player.skipNextTurn = true; break;
            case 25: player.pitStopScheduled = true; break;
            case 26: player.kantoOdenSkipThisTurn = true; break;
            case 27: player.isAI = true; break;
            case 28: player.heatPaidCardsThisTurn.Add(new CardData(CardType.Heat, 0)); break;
            case 29: player.techState.schwarzbierFuelLastLap = player.lap; break;
            case 30: player.techState.germanBurst.BeginTurn(2); break;
            case 31: player.techState.activeNodeIds = null; break;
        }
        var pool = player.deck.heatPool; int engine = pool.remaining, receipts = player.heatPaidCardsThisTurn.Count;
        int hand = player.deck.HandCount, draw = player.deck.DrawPileCount, discard = player.deck.DiscardPileCount;
        int version = player.techState.germanBurst.Version, uses = player.techState.germanBurst.FuelUsesThisTurn;
        bool used = player.techState.grillSpezialUsed;
        Assert.That(plan.TryCommit(race, player, 1, 7, true, true, out _), Is.False);
        Assert.That(pool.remaining, Is.EqualTo(engine));
        Assert.That(player.heatPaidCardsThisTurn.Count, Is.EqualTo(receipts));
        Assert.That(player.deck.HandCount, Is.EqualTo(hand)); Assert.That(player.deck.DrawPileCount, Is.EqualTo(draw));
        Assert.That(player.deck.DiscardPileCount, Is.EqualTo(discard));
        Assert.That(player.techState.germanBurst.Version, Is.EqualTo(version));
        Assert.That(player.techState.germanBurst.FuelUsesThisTurn, Is.EqualTo(uses));
        Assert.That(player.techState.grillSpezialUsed, Is.EqualTo(used)); Assert.That(plan.PaidHeat, Is.Null);
    }

    [Test]
    public void NullNodeCollectionRejectsRequestCreationWithoutRepairingOrSpending()
    {
        player.techState.activeNodeIds = null;
        Assert.That(GermanFuelActivationPlan.TryCreate(race, player, 1, 7, true, true, out _, out _), Is.False);
        Assert.That(player.techState.activeNodeIds, Is.Null);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(5));
    }

    [Test]
    public void OtherRealPaymentLocksDeclarationAndExpiresFuelRequestWithoutUndoingPayment()
    {
        Assert.That(race.TryDeclareGermanBurst(player, true, 1), Is.True);
        var plan = Prepare();
        Assert.That(RaceHeatPaymentExecution.Pay(player, race, 1, HeatPaymentDestination.Hand).Drawn, Is.EqualTo(1));
        var receipt = player.heatPaidCardsThisTurn[0];
        Assert.That(plan.TryCommit(race, player, 1, 7, true, true, out _), Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
        Assert.That(player.techState.grillSpezialUsed, Is.True);
        Assert.That(player.techState.germanBurst.Receipts, Is.EquivalentTo(new[] { receipt }));
        Assert.That(player.techState.germanBurst.FuelUsesThisTurn, Is.Zero);
    }

    [Test]
    public void ConcurrentPureRequestsCannotPurchaseTwoOrdinaryUses()
    {
        var first = Prepare(); var stale = Prepare();
        Assert.That(first.TryCommit(race, player, 1, 7, true, true, out _), Is.True);
        Assert.That(stale.TryCommit(race, player, 1, 7, true, true, out _), Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
        Assert.That(player.techState.germanBurst.FuelUsesThisTurn, Is.EqualTo(1));
    }

    [Test]
    public void OrdinaryPlusBurstRequestsStopAtThreeAndNewRequestsNeverTouchAnotherRacer()
    {
        Assert.That(Prepare().TryCommit(race, player, 1, 7, true, true, out _), Is.True);
        Assert.That(race.TryDeclareGermanBurst(player, true, 1), Is.True);
        for (int i = 0; i < 2; i++) Assert.That(Prepare().TryCommit(race, player, 1, 7, true, true, out _), Is.True);
        Assert.That(GermanFuelActivationPlan.TryCreate(race, player, 1, 7, true, true, out _, out _), Is.False);
        Assert.That(player.techState.germanBurst.FuelUsesThisTurn, Is.EqualTo(3));
        var other = new PlayerState("other DE", false, 0, 1) { teamId = TeamId.DE, techState = new TechTreeState(TeamId.DE) };
        other.techState.activeNodeIds.Add("de-l1-schwarzbier-fuel"); other.deck.heatPool = new HeatPool(1);
        race.Players.Add(other); race.BeginTurn(other, 1);
        Assert.That(GermanFuelActivationPlan.TryCreate(race, other, 1, 7, true, true, out var plan, out _), Is.True);
        Assert.That(plan.TryCommit(race, other, 1, 7, true, true, out _), Is.True);
        Assert.That(other.deck.heatPool.remaining, Is.Zero); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(2));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void ChangedDiscardMetadataAloneInvalidatesWithoutAnotherPayment(int change)
    {
        Assert.That(player.deck.TryPayFixedPermanentHeatToDiscard(out var old), Is.True);
        var plan = Prepare(); int engine = player.deck.heatPool.remaining;
        if (change == 0) old.value = 1;
        if (change == 1) old.type = CardType.Speed;
        if (change == 2) old.isTemp = true;
        if (change == 3) old.trickId = "changed";
        Assert.That(plan.TryCommit(race, player, 1, 7, true, true, out _), Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine));
        Assert.That(player.techState.germanBurst.FuelUsesThisTurn, Is.Zero);
        Assert.That(player.heatPaidCardsThisTurn, Is.Empty);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
    public void RealManagerReissueCancellationGearConfirmationAndDisableRejectOldCallbacks(int action)
    {
        var host = new GameObject("German fuel request adapter"); host.SetActive(false);
        var config = ScriptableObject.CreateInstance<GameConfigSO>(); config.enableTechTree = true;
        try
        {
            var manager = host.AddComponent<MVPGameManager>(); manager.config = config;
            typeof(MVPGameManager).GetField("session", Private).SetValue(manager, race);
            typeof(MVPGameManager).GetField("raceTurnNumber", Private).SetValue(manager, 1);
            var phase = (RacePhaseState)typeof(MVPGameManager).GetField("phaseState", Private).GetValue(manager);
            var input = (RaceInputState)typeof(MVPGameManager).GetField("inputState", Private).GetValue(manager);
            phase.BeginCardSelection(); input.BeginCardSelection();
            Assert.That(manager.TryPrepareGermanFuelRequest(out var old, out _), Is.True);
            if (action == 0)
            {
                Assert.That(manager.TryPrepareGermanFuelRequest(out var newer, out _), Is.True);
                manager.CancelGermanFuelRequest(old);
                Assert.That(manager.TryConfirmGermanFuelRequest(old, out _), Is.False);
                Assert.That(manager.TryConfirmGermanFuelRequest(newer, out _), Is.True);
                Assert.That(manager.TryConfirmGermanFuelRequest(newer, out _), Is.False);
                Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4)); return;
            }
            if (action == 1) manager.CancelGermanFuelRequest(old);
            if (action == 2) { phase.BeginGearSelection(); input.BeginGearSelection(1); manager.OnGearButtonClicked(2); }
            if (action == 3) { phase.BeginGearSelection(); input.BeginGearSelection(1); manager.OnConfirmGearClicked(); }
            if (action == 4) typeof(MVPGameManager).GetMethod("OnDisable", Private).Invoke(manager, null);
            if (action == 5) config.enableTechTree = false;
            if (action == 6) typeof(MVPGameManager).GetField("tutorialScenario", Private)
                .SetValue(manager, TutorialScenarioDefinition.CreateLeMansUk());
            Assert.That(manager.TryConfirmGermanFuelRequest(old, out _), Is.False);
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(5)); Assert.That(old.IsClosed, Is.True);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(config); }
    }
}
