using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RaceTurnPreviewTests
{
    private GameConfigSO config;
    private RaceSession session;
    private PlayerState player;
    private CardData four, two, one;
    [SetUp]
    public void SetUp()
    {
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.enableTechTree = false;
        session = new RaceSession(new SystemRandomSource(1)) { TeamVehicleBonusesEnabled = false,
            TrickDb = TrickCardDatabaseFactory.CreateDefault() };
        player = new PlayerState("forecast", false, 0, 2) { teamId = TeamId.UK };
        player.deck.heatPool = new HeatPool(6);
        four = new CardData(CardType.Speed, 4); two = new CardData(CardType.Speed, 2); one = new CardData(CardType.Speed, 1);
        player.deck.AddCardsToHand(new[] { four, two, one });
        player.deck.DrawHeatFromPoolToHand(2);
    }
    [TearDown] public void TearDown() => Object.DestroyImmediate(config);

    [Test]
    public void SelectedCardsAddAndCancelImmediatelyWithKnownCoolingAndNoCommit()
    {
        var first = RaceTurnPreviewRules.Evaluate(player, session, config, new[] { four }, false, 2);
        var both = RaceTurnPreviewRules.Evaluate(player, session, config, new[] { four, two }, false, 2);
        var cancel = RaceTurnPreviewRules.Evaluate(player, session, config, new[] { two }, false, 2);
        Assert.That(first.Movement.TotalCells, Is.EqualTo(4));
        Assert.That(both.Movement.TotalCells, Is.EqualTo(6));
        Assert.That(cancel.Movement.TotalCells, Is.EqualTo(2));
        Assert.That(both.ProjectedHeat.TotalHeat, Is.EqualTo(1));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
        Assert.That(player.deck.HandCount, Is.EqualTo(5));
        Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
        Assert.That(player.turnMovement.Entries, Is.Empty);
    }

    [TestCase(1, 4, 0)]
    [TestCase(2, 6, 1)]
    [TestCase(3, 7, 3)]
    public void GearTrialUsesCurrentHandCapacityAndShiftFeesWithoutChangingActualGear(int gear, int movement, int heat)
    {
        player.gear = 1;
        var result = RaceTurnPreviewRules.Evaluate(player, session, config, null, true, gear);
        Assert.That(result.GearTrial, Is.True);
        Assert.That(result.Movement.TotalCells, Is.EqualTo(movement));
        Assert.That(result.ProjectedHeat.TotalHeat, Is.EqualTo(heat));
        Assert.That(player.gear, Is.EqualTo(1));
        Assert.That(player.chinaConsecutiveGearCount, Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
    }

    [TestCase(1, 0)]
    [TestCase(2, 4)]
    public void ChinaModesPreviewCoolingAndNextGoChainOnlyOnCopies(int gear, int heat)
    {
        player.teamId = TeamId.CN; player.usesChinaGearSystem = true;
        player.chinaConsecutiveGearCount = 1;
        var result = RaceTurnPreviewRules.Evaluate(player, session, config, null, true, gear);
        Assert.That(result.ProjectedHeat.TotalHeat, Is.EqualTo(heat));
        Assert.That(player.chinaConsecutiveGearCount, Is.EqualTo(1));
        Assert.That(player.gear, Is.EqualTo(2));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
    }

    [Test]
    public void RepeatedPreviewDoesNotConsumeHotpotOrItalyCornerExitReward()
    {
        player.teamId = TeamId.CN; player.usesChinaGearSystem = true;
        player.trickState.hotpotBaseActive = true;
        for (int i = 0; i < 5; i++)
            Assert.That(RaceTurnPreviewRules.Evaluate(player, session, config, new[] { four }, false, 2)
                .Movement.TotalCells, Is.EqualTo(5));
        Assert.That(player.trickState.hotpotBaseActive, Is.True);
        Assert.That(player.hotpotAttackAppliedThisTurn, Is.False);
        player.teamId = TeamId.IT; player.trickState.hotpotBaseActive = false;
        player.italyCornerExitBoostReady = true; session.TeamVehicleBonusesEnabled = true;
        for (int i = 0; i < 3; i++)
            Assert.That(RaceTurnPreviewRules.Evaluate(player, session, config, new[] { four }, false, 2,
                (_, __) => true).Movement.TotalCells, Is.EqualTo(5));
        Assert.That(player.italyCornerExitBoostReady, Is.True);
    }

    [Test]
    public void SelectedSconeAndTeaPreviewTheirHeatAndMovementWithoutSpendingTrickSlot()
    {
        player.gear = 3;
        player.deck.RemoveFromHand(new List<CardData> { four, two, one });
        player.playedSpeedCardsThisTurn.AddRange(new[] { four, two, one });
        var scone = CardData.CreateTrick("uk-scone");
        var tea = CardData.CreateTrick("uk-english-breakfast-tea");
        player.deck.AddCardsToHand(new[] { scone, tea });
        var hot = RaceTurnPreviewRules.Evaluate(player, session, config, new[] { scone }, false, 3);
        Assert.That(hot.Movement.TotalCells, Is.EqualTo(9));
        Assert.That(hot.ProjectedHeat.TotalHeat, Is.EqualTo(3));
        var cool = RaceTurnPreviewRules.Evaluate(player, session, config, new[] { tea }, false, 3);
        Assert.That(cool.ProjectedHeat.TotalHeat, Is.EqualTo(1));
        Assert.That(player.trickState.trickPlayedThisTurn, Is.False);
        Assert.That(player.deck.ContainsInHand(scone), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
    }

    [Test]
    public void TemporaryCoolingAndRegionalRetirementKeepCapacityAndOwnershipIndependent()
    {
        player.gear = 1;
        player.deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
        var result = RaceTurnPreviewRules.Evaluate(player, session, config, new[] { four }, false, 1);
        Assert.That(result.ProjectedHeat.Capacity, Is.EqualTo(6));
        Assert.That(result.ProjectedHeat.TemporaryHeat, Is.Zero);
        Assert.That(player.deck.CountTemporaryHeatOutsideEngine(), Is.EqualTo(1));
        var deck = player.deck.CreatePresentationCopy();
        deck.SetRegionalCapacityBonus(2);
        deck.DrawHeatFromPool(5);
        deck.SetRegionalCapacityBonus(0);
        var isolated = deck.CreatePresentationCopy();
        int retirement = deck.RegionalHeatPendingRetirement;
        isolated.CoolHeat(3);
        Assert.That(deck.RegionalHeatPendingRetirement, Is.EqualTo(retirement));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
    }

    [Test]
    public void StaleAndDuplicateSelectionsCannotInventCardsAndInsufficientFeesStayExplicit()
    {
        var result = RaceTurnPreviewRules.Evaluate(player, session, config,
            new[] { four, four, new CardData(CardType.Speed, 4) }, false, 2);
        Assert.That(result.CardCount, Is.EqualTo(1));
        Assert.That(result.Movement.TotalCells, Is.EqualTo(4));
        player.gear = 1; player.deck.DrawHeatFromPool(4);
        result = RaceTurnPreviewRules.Evaluate(player, session, config, null, true, 3);
        Assert.That(result.CanPay, Is.False);
        Assert.That(result.Gear, Is.EqualTo(3));
        Assert.That(result.ProjectedHeat.TotalHeat, Is.EqualTo(6));
        Assert.That(player.gear, Is.EqualTo(1));
        Assert.That(player.spinCounter, Is.Zero);
    }

    [Test]
    public void SelectedSpeedChangesMissingCardFeesAndCornerHeatWithoutUsingRealDiscounts()
    {
        var lower = RaceTurnPreviewRules.Evaluate(player, session, config, new[] { two, one }, false, 2,
            (_, __) => true, null, 0, (_, __) => new[] { 1 }, _ => 2);
        var higher = RaceTurnPreviewRules.Evaluate(player, session, config, new[] { four, two }, false, 2,
            (_, __) => true, null, 0, (_, __) => new[] { 1 }, _ => 2);
        Assert.That(lower.CornerHeat, Is.EqualTo(1));
        Assert.That(lower.ProjectedHeat.TotalHeat, Is.EqualTo(2));
        Assert.That(higher.CornerHeat, Is.EqualTo(4));
        Assert.That(higher.ProjectedHeat.TotalHeat, Is.EqualTo(5));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
        player.techState = session.CreateDemoTechState(TeamId.DE);
        bool before = player.techState.heatReductionUsedThisLap;
        for (int i = 0; i < 3; i++)
            RaceTurnPreviewRules.Evaluate(player, session, config, new[] { four, two }, false, 2,
                (_, __) => true, null, 0, (_, __) => new[] { 1, 2 }, _ => 2);
        Assert.That(player.techState.heatReductionUsedThisLap, Is.EqualTo(before));
        Assert.That(player.techState.fishAndChipsUsed, Is.False);
        var clone = player.techState.CreatePresentationCopy();
        clone.activeNodeIds.Clear();
        Assert.That(player.techState.activeNodeIds, Is.Not.Empty);
    }

    [Test]
    public void ClonePaymentsConsumeOnlyCopiedBreadAndDriverFlagsAndNeverTouchRandomState()
    {
        player.trickState.schwarzbrotActive = true; player.trickState.schwarzbrotRemaining = 1;
        player.driverSkill = new DriverSkillRuntimeState();
        typeof(DriverSkillRuntimeState).GetField("passiveHeatDiscountReady",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(player.driverSkill, true);
        Random.State rng = Random.state;
        for (int i = 0; i < 8; i++)
            RaceTurnPreviewRules.Evaluate(player, session, config, null, true, 4);
        Assert.That(player.trickState.schwarzbrotActive, Is.True);
        Assert.That(player.trickState.schwarzbrotRemaining, Is.EqualTo(1));
        Assert.That(typeof(DriverSkillRuntimeState).GetField("passiveHeatDiscountReady",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(player.driverSkill), Is.True);
        Assert.That(Random.state, Is.EqualTo(rng));
    }
}
