using System.Collections.Generic;
using NUnit.Framework;

public class UkPrepMealTests
{
    private RaceSession race;
    private PlayerState player;
    private CardData first, second;

    [SetUp] public void SetUp()
    {
        race = new RaceSession();
        player = new PlayerState("UK", false, 0, 4) { teamId = TeamId.UK, techState = new TechTreeState(TeamId.UK) };
        race.Players.Add(player);
        player.techState.activeNodeIds.Add("uk-l1-fish-and-chips");
        first = new CardData(CardType.Speed, 2); second = new CardData(CardType.Speed, 2);
        player.deck.InitializeExactOrder(new[] { first, second }, new HeatPool(8));
        player.deck.DrawToHand(2);
        player.deck.DrawHeatFromPool(2, HeatPaymentDestination.Hand);
        player.deck.DrawHeatFromPool(1, HeatPaymentDestination.Discard);
        race.BeginPrepMealInput(player, 1);
    }

    private UkPrepMealActivationPlan Plan(string country = "FR")
    {
        Assert.IsTrue(UkPrepMealActivationPlan.TryCreate(race, player, 1, 5, country, true, true, out var plan, out var reason), reason);
        return plan;
    }
    private bool Commit(UkPrepMealActivationPlan plan, CardData cost, string country = "FR", int turn = 1, int version = 5)
        => plan.TryCommit(race, player, turn, version, country, true, true, cost, out _, out _, out _, out _);

    [TestCase("FR", false, 1)] [TestCase("UK", false, 1)]
    [TestCase("UK", true, 2)] [TestCase("GB", true, 2)] [TestCase("DE", true, 1)]
    public void CorrectLimitExactSecondCostAndConservation(string country, bool l3, int count)
    {
        if (l3) player.techState.activeNodeIds.Add("uk-l3-sun-never-sets");
        var plan = Plan(country);
        Assert.AreEqual(count, plan.PreviewRestored);
        Assert.IsTrue(Commit(plan, second, country));
        Assert.IsTrue(player.deck.ContainsInHand(first)); Assert.IsFalse(player.deck.ContainsInHand(second));
        Assert.That(player.deck.DiscardPile, Has.Member(second));
        Assert.AreEqual(5 + count, player.deck.heatPool.remaining);
        Assert.AreEqual(8, player.deck.heatPool.remaining + player.deck.CountPermanentHeatOutsideEngine());
        Assert.AreEqual(0, player.techState.prepMeal.LastUsedLap);
        Assert.IsFalse(Commit(plan, first, country));
    }

    [Test] public void OneSpeedAndHighGearRiskRemainAllowed()
    {
        player.deck.DiscardPlayableCardsFromHand(new[] { second });
        Assert.IsTrue(Commit(Plan(), first));
        Assert.AreEqual(0, player.deck.CountSpeedInHand()); Assert.AreEqual(4, player.gear);
    }
    [Test] public void CancelReopenDoesNotSpendOrRefill()
    {
        var plan = Plan(); plan.Cancel(); Assert.IsFalse(Commit(plan, first));
        Assert.AreEqual(5, player.deck.heatPool.remaining); Assert.AreEqual(4, player.deck.HandCount);
        Assert.IsTrue(Commit(Plan(), second));
    }
    [TestCase(2, 5)] [TestCase(1, 6)]
    public void StaleTurnOrRequestDoesNotPay(int turn, int version)
    {
        Assert.IsFalse(Commit(Plan(), first, turn:turn, version:version));
        Assert.IsTrue(player.deck.ContainsInHand(first)); Assert.AreEqual(-1, player.techState.prepMeal.LastUsedLap);
    }
    [TestCase("gear")] [TestCase("source")] [TestCase("deck")] [TestCase("tech")]
    [TestCase("finish")] [TestCase("skip")] [TestCase("pit")] [TestCase("race")]
    public void ChangedBindingRejectsWholeCommit(string change)
    {
        var plan = Plan();
        if(change=="gear") player.gear=1;
        if(change=="source") player.deck.ReturnHeatCardsToPool(new[] { player.deck.Hand[2] });
        if(change=="deck") player.deck=new CardDeck();
        if(change=="tech") player.techState=new TechTreeState(TeamId.UK);
        if(change=="finish") player.hasFinished=true;
        if(change=="skip") player.skipNextTurn=true;
        if(change=="pit") player.pitStopScheduled=true;
        if(change=="race") race=new RaceSession();
        Assert.IsFalse(Commit(plan, first));
        Assert.IsTrue(plan.Choices.ContainsReference(first));
    }
    [Test] public void NewPaymentsAndTemporaryHeatNeverBecomeOldSources()
    {
        player.deck.ReturnHeatCardsToPool(new[] { player.deck.Hand[2], player.deck.Hand[3] });
        player.deck.RecoverOnePermanentHeatFromHandOrDiscard();
        player.deck.DrawHeatFromPool(1, HeatPaymentDestination.Hand);
        player.deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
        Assert.IsFalse(race.BeginPrepMealInput(player, 1));
        Assert.IsFalse(UkPrepMealRules.CanPrepare(race, player, 1, "FR", true, true, out _));
    }
    [Test] public void CopyAndTurnResetDoNotMutateOriginalSources()
    {
        var copy=player.techState.CreatePresentationCopy();
        copy.prepMeal.CloseInput(); Assert.IsTrue(player.techState.prepMeal.IsOpen);
        Assert.AreEqual(2, player.techState.prepMeal.OldHandHeat.Count);
        Assert.IsTrue(Commit(Plan(), first));
        race.BeginTurn(player); Assert.IsFalse(player.techState.prepMeal.IsOpen);
        race.BeginPrepMealInput(player, 2);
        Assert.IsFalse(UkPrepMealRules.CanPrepare(race, player, 2, "FR", true, true, out _));
        player.lap++; race.BeginTurn(player); race.BeginPrepMealInput(player, 3);
        Assert.IsTrue(UkPrepMealRules.CanPrepare(race, player, 3, "FR", true, true, out _));
    }
    [Test] public void FormalSpeedClosesInputAndDoesNotRecaptureSameTurn()
    {
        Assert.AreEqual(SpeedCardCommitResult.Success, CardPlayRules.CommitSpeedCard(player, first, 4));
        Assert.IsFalse(player.techState.prepMeal.IsOpen);
        Assert.IsFalse(race.BeginPrepMealInput(player, 1));
    }
    [Test] public void RetirementIsSeparateAndZeroNetCannotChargeCost()
    {
        player.deck.SetRegionalCapacityBonus(2); player.deck.DrawHeatFromPool(7);
        player.deck.SetRegionalCapacityBonus(0);
        Assert.AreEqual(2,player.deck.RegionalHeatPendingRetirement);
        Assert.IsFalse(UkPrepMealRules.CanPrepare(race, player, 1, "UK", true, true, out _));
        Assert.IsTrue(player.deck.ContainsInHand(first));
    }

    [Test] public void HomeTwoOldHeatRetiresOneAndActuallyRestoresOne()
    {
        player.techState.activeNodeIds.Add("uk-l3-sun-never-sets");
        player.deck.SetRegionalCapacityBonus(1);
        player.deck.DrawHeatFromPool(6, HeatPaymentDestination.Discard);
        player.deck.SetRegionalCapacityBonus(0);
        Assert.AreEqual(1,player.deck.RegionalHeatPendingRetirement);
        var plan=Plan("UK");
        Assert.AreEqual(2,plan.PreviewSources.FromHand);
        Assert.AreEqual(1,plan.PreviewRetired);
        Assert.AreEqual(1,plan.PreviewRestored);
        Assert.IsTrue(plan.TryCommit(race,player,1,5,"UK",true,true,first,
            out var removed,out int retired,out int restored,out string reason),reason);
        Assert.AreEqual(2,removed.Total); Assert.AreEqual(1,retired); Assert.AreEqual(1,restored);
        Assert.AreEqual(0,player.deck.RegionalHeatPendingRetirement);
        Assert.AreEqual(1,player.deck.heatPool.remaining);
        Assert.AreEqual(8,player.deck.heatPool.remaining+player.deck.CountPermanentHeatOutsideEngine());
    }

    [Test] public void DiscardSourceIsUsedOnlyAfterOldHandSourcesHaveGone()
    {
        var handHeat=new List<CardData>{player.deck.Hand[2],player.deck.Hand[3]};
        player.deck.ReturnHeatCardsToPool(handHeat);
        var plan=Plan();
        Assert.AreEqual(0,plan.PreviewSources.FromHand);
        Assert.AreEqual(1,plan.PreviewSources.FromDiscard);
        Assert.IsTrue(Commit(plan,first)); Assert.AreEqual(8,player.deck.heatPool.remaining);
    }

    [Test] public void TwoUKRacersHaveIndependentLapUsageAndExactSources()
    {
        var other=new PlayerState("second UK",true,1,1){teamId=TeamId.UK,techState=new TechTreeState(TeamId.UK)};
        other.techState.activeNodeIds.Add("uk-l1-fish-and-chips");
        var cost=new CardData(CardType.Speed,1);
        other.deck.InitializeExactOrder(new[]{cost},new HeatPool(8));other.deck.DrawToHand(1);
        other.deck.DrawHeatFromPool(1,HeatPaymentDestination.Hand);
        race.Players.Add(other);race.BeginPrepMealInput(other,1);
        Assert.IsTrue(Commit(Plan(),first));
        Assert.AreEqual(-1,other.techState.prepMeal.LastUsedLap);
        Assert.IsTrue(UkPrepMealActivationPlan.TryCreate(race,other,1,9,"FR",true,true,out var plan,out var reason),reason);
        Assert.IsFalse(plan.TryCommit(race,other,1,9,"FR",true,true,second,out _,out _,out _,out _));
        Assert.IsTrue(plan.TryCommit(race,other,1,9,"FR",true,true,cost,out _,out _,out _,out reason),reason);
        Assert.AreEqual(8,other.deck.heatPool.remaining);
        Assert.AreEqual(0,other.techState.prepMeal.LastUsedLap);
        Assert.AreEqual(6,player.deck.heatPool.remaining);
    }

    [TestCase(false,false,0)] [TestCase(true,false,1)]
    [TestCase(false,true,1)] [TestCase(true,true,2)]
    public void BreakfastOriginalSnapshotSurvivesPrepMealWithoutRechecking(bool l2,bool l3,int triggers)
    {
        if(l2)player.techState.activeNodeIds.Add("uk-l2-full-english");
        if(l3)player.techState.activeNodeIds.Add("uk-l3-sun-never-sets");
        player.deck.AddCardsToHand(race.CreateInitialTrickCards(TeamId.UK));
        // A new real input freezes only after breakfast; temporary breakfast heat is never a source.
        race.BeginTurn(player);
        Assert.AreEqual(triggers,race.ResolveFullEnglishOnDraw(player,"UK"));
        race.BeginPrepMealInput(player,2);
        Assert.AreEqual(2,player.techState.prepMeal.OldHandHeat.Count);
        Assert.IsTrue(UkPrepMealActivationPlan.TryCreate(race,player,2,5,"UK",true,true,out var plan,out var reason),reason);
        Assert.IsTrue(plan.TryCommit(race,player,2,5,"UK",true,true,first,out _,out _,out _,out reason),reason);
        Assert.AreEqual(triggers,player.slipstreamRangeBonusThisTurn);
        Assert.AreEqual(triggers,player.deck.CountTemporaryHeatOutsideEngine());
    }

    [Test] public void HartThenPrepMealUseDistinctCostInstancesInProductionPhaseOrder()
    {
        player.driverId="uk_hunter_hart";
        player.driverSkill.Initialize(player.DriverProfile,3,true);
        race.Players.Add(new PlayerState("target",true,3,1){teamId=TeamId.DE});
        race.BeginTurn(player); race.BeginDriverSkillInput(player);
        var context=DriverSkillRaceContextRules.Build(player,true,3,race.Players,60);
        Assert.IsTrue(HunterSkillActivationPlan.TryCreate(race,player,60,2,1,7,context,out var hart,out var reason),reason);
        int uses=player.driverSkill.UsesRemaining;
        Assert.IsTrue(hart.TryCommit(race,player,2,1,7,context,first,out reason),reason);
        player.gear=1;
        race.BeginPrepMealInput(player,2);
        Assert.IsTrue(UkPrepMealActivationPlan.TryCreate(race,player,2,8,"FR",true,true,out var meal,out reason),reason);
        Assert.IsFalse(meal.TryCommit(race,player,2,8,"FR",true,true,first,out _,out _,out _,out _));
        Assert.IsTrue(meal.TryCommit(race,player,2,8,"FR",true,true,second,out _,out _,out _,out reason),reason);
        Assert.That(player.deck.DiscardPile,Has.Member(first));Assert.That(player.deck.DiscardPile,Has.Member(second));
        Assert.AreEqual(uses-1,player.driverSkill.UsesRemaining);
        Assert.AreEqual(0,player.techState.prepMeal.LastUsedLap);
        Assert.AreEqual(0,player.deck.CountSpeedInHand());
    }
}

internal static class PrepMealTestReferences
{
    internal static bool ContainsReference(this IReadOnlyList<CardData> cards, CardData target)
    { foreach(var card in cards) if(ReferenceEquals(card,target)) return true; return false; }
}
