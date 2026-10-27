using System;
using NUnit.Framework;
using UnityEngine;

/// <summary>DE A declared-window production APIs; manual HUD entry and complete modes are not this fixture.</summary>
public class GermanBurstRuntimeTests
{
    private RaceSession race;
    private PlayerState player;
    private GermanBurstRuntime Burst => player.techState.germanBurst;

    [SetUp]
    public void SetUp()
    {
        race = new RaceSession(new SystemRandomSource(11)) { TeamVehicleBonusesEnabled = false };
        player = new PlayerState("burst", false, 0, 1) { teamId = TeamId.DE, techState = new TechTreeState(TeamId.DE) };
        player.techState.activeNodeIds.UnionWith(new[] { "de-l1-schwarzbier-fuel", "de-l3-grill-spezial" });
        player.deck.heatPool = new HeatPool(10); race.Players.Add(player); race.BeginTurn(player, 1);
    }

    private void Declare() => Assert.That(race.TryDeclareGermanBurst(player, true, 1), Is.True);
    private CardData Pay(int count = 1)
    {
        int before = player.heatPaidCardsThisTurn.Count;
        var result = RaceHeatPaymentExecution.Pay(player, race, count, HeatPaymentDestination.Hand);
        Assert.That(result.Drawn, Is.EqualTo(count)); return player.heatPaidCardsThisTurn[before];
    }

    [Test]
    public void DeclarationPreviewAndZeroCostDoNotConsumeButFirstActualPaymentLocks()
    {
        Declare(); int version = Burst.Version;
        Assert.That(player.techState.grillSpezialUsed, Is.False);
        RaceHeatPaymentExecution.Pay(player, race, 0, HeatPaymentDestination.Hand);
        Assert.That(Burst.Version, Is.EqualTo(version));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(10));
        var receipt = Pay();
        Assert.That(Burst.Locked && player.techState.grillSpezialUsed, Is.True);
        Assert.That(Burst.Receipts, Is.EquivalentTo(new[] { receipt }));
        Assert.That(race.TryCancelGermanBurst(player, 1, Burst.Version), Is.False);
    }

    [TestCase(0)] [TestCase(1)]
    public void PartialOrdinaryPaymentLocksOnlyWhenAPermanentInstanceWasReallyPaid(int available)
    {
        player.deck.heatPool = new HeatPool(available); Declare();
        var payment = RaceHeatPaymentExecution.Pay(player, race, 2, HeatPaymentDestination.Hand);
        Assert.That(payment.Sufficient, Is.False);
        Assert.That(payment.Drawn, Is.EqualTo(available));
        Assert.That(Burst.Receipts.Count, Is.EqualTo(available));
        Assert.That(player.techState.grillSpezialUsed, Is.EqualTo(available > 0));
    }

    [Test]
    public void CopyClosureCannotEraseTheLiveExcludedOldSourceSnapshot()
    {
        player.deck.DrawHeatFromPoolToHand(1); CardData old = player.deck.Hand[0]; Declare();
        var copy = player.techState.CreatePresentationCopy(); copy.germanBurst.CloseWindow();
        player.heatPaidCardsThisTurn.Add(old); race.TrackHeatPaid(player, 1); // A forged old-source counter is not payment.
        Assert.That(Burst.Receipts, Is.Empty);
        Assert.That(Burst.Locked, Is.False);
        Assert.That(player.techState.grillSpezialUsed, Is.False);
    }

    [Test]
    public void RefundSeparatesCapacityRetirementFromNetRestoration()
    {
        player.deck.heatPool = new HeatPool(0); player.deck.SetRegionalCapacityBonus(1);
        Declare(); Pay(); player.deck.SetRegionalCapacityBonus(0);
        Assert.That(player.deck.RegionalHeatPendingRetirement, Is.EqualTo(1));
        var returned = race.ResolveGermanBurstRefund(player, 1);
        Assert.That(returned.Removed.Total, Is.EqualTo(1));
        Assert.That(returned.Restored, Is.Zero);
        Assert.That(returned.Retired, Is.EqualTo(1));
        Assert.That(player.deck.heatPool.remaining, Is.Zero);
        Assert.That(race.ResolveGermanBurstRefund(player, 1).Removed.Total, Is.Zero);
        Assert.That(player.techState.grillSpezialUsed, Is.True);
    }

    [Test]
    public void UnpaidCancelCanReopenAndStaleCancelCannotTouchNewWindow()
    {
        Declare(); int oldVersion = Burst.Version;
        Assert.That(race.TryCancelGermanBurst(player, 1, oldVersion - 1), Is.False);
        Assert.That(race.TryCancelGermanBurst(player, 1, oldVersion), Is.True);
        Declare();
        Assert.That(race.TryCancelGermanBurst(player, 1, oldVersion), Is.False);
        Assert.That(Burst.Declared, Is.True);
        Assert.That(player.techState.grillSpezialUsed, Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(10));
    }

    [TestCase(false)] [TestCase(true)]
    public void ThreeTotalFuelUsesIncludeCurrentOrdinaryUseButNotPriorTurnUse(bool alreadyThisTurn)
    {
        if (alreadyThisTurn) Assert.That(race.TryCommitSchwarzbierFuel(player, true, out _), Is.True);
        else player.techState.schwarzbierFuelLastLap = player.lap; // Last turn's normal use is not this turn's budget.
        Declare();
        for (int i = alreadyThisTurn ? 1 : 0; i < 3; i++)
            Assert.That(race.TryCommitSchwarzbierFuel(player, true, out _), Is.True);
        Assert.That(Burst.FuelUsesThisTurn, Is.EqualTo(3));
        Assert.That(Burst.FuelMovement, Is.EqualTo(6));
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out var fourth), Is.False);
        Assert.That(fourth, Is.Null);
    }

    [Test]
    public void CoolingCrossingAndDuplicateTurnCannotRefreshTheBudget()
    {
        Declare();
        for (int i = 0; i < 3; i++) Assert.That(race.TryCommitSchwarzbierFuel(player, true, out _), Is.True);
        player.deck.CoolRecordedHeatThroughDiscard(Burst.Receipts, 1);
        player.lap++;
        race.BeginTurn(player, 1);
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out _), Is.False);
        Assert.That(Burst.FuelUsesThisTurn, Is.EqualTo(3));
        Assert.That(player.techState.schwarzbierFuelLastLap, Is.EqualTo(Burst.OpeningLap));
        race.BeginTurn(player, 2);
        Assert.That(race.TryDeclareGermanBurst(player, true, 2), Is.False);
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out _), Is.True, "New lap next turn still has its ordinary use.");
    }

    [Test]
    public void OpeningLapIsFrozenEvenWhenFuelIsPaidAfterCrossing()
    {
        Declare(); int opening = player.lap; player.lap++;
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out _), Is.True);
        Assert.That(player.techState.schwarzbierFuelLastLap, Is.EqualTo(opening));
    }

    [Test]
    public void CleanupReturnsOnlyPostDeclarationOwnedReceiptsAfterNormalCooling()
    {
        player.deck.DrawHeatFromPoolToHand(1); player.deck.DrawHeatFromPool(1);
        var preWindowPayment = Pay(); Declare();
        var alreadyCooled = Pay();
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out var fuel), Is.True);
        player.deck.CoolRecordedHeatThroughDiscard(new[] { alreadyCooled }, 1);
        Assert.That(race.GetGrillSpezialCooldown(player), Is.EqualTo(1));
        RaceTurnTechnologyCleanup.Execute(player, race, (_, __) => { }, _ => { },
            (p, count) => p.deck.CoolRecordedHeatThroughDiscard(p.techState.germanBurst.Receipts, count));
        Assert.That(player.deck.CountRecordedHeat(new[] { preWindowPayment }), Is.EqualTo(1));
        Assert.That(player.deck.CountRecordedHeat(new[] { fuel, alreadyCooled }), Is.Zero);
        Assert.That(player.deck.CountHeatInHand(), Is.EqualTo(2)); // Old hand plus pre-window payment.
        Assert.That(player.deck.CountHeatInDiscardPile(), Is.EqualTo(1)); // Old discard stays.
        Assert.That(player.techState.grillSpezialUsed, Is.True);
        Assert.That(Burst.Declared, Is.False);
        Assert.That(race.GetGrillSpezialCooldown(player), Is.Zero);
    }

    [TestCase("foreign-session")] [TestCase("changed-deck")] [TestCase("changed-pool")]
    public void ChangedSourceScopeCannotSpendOrRefund(string change)
    {
        Declare();
        var other = new RaceSession(new SystemRandomSource(2));
        if (change == "changed-deck") player.deck = new CardDeck { heatPool = new HeatPool(10) };
        if (change == "changed-pool") player.deck.heatPool = new HeatPool(10);
        var usedRace = change == "foreign-session" ? other : race;
        Assert.That(usedRace.TryCommitSchwarzbierFuel(player, true, out _), Is.False);
        Assert.That(usedRace.GetGrillSpezialCooldown(player), Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(10));
    }

    [Test]
    public void ReplacingPoolAfterLockCannotCreditTheNewPoolWithOldReceipts()
    {
        Declare(); var receipt = Pay(); player.deck.heatPool = new HeatPool(10);
        Assert.That(race.ResolveGermanBurstRefund(player, 9).Removed.Total, Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(10));
        Assert.That(player.deck.CountRecordedHeat(new[] { receipt }), Is.EqualTo(1));
        Assert.That(player.techState.grillSpezialUsed, Is.True);
    }

    [TestCase("late")] [TestCase("wrong-turn")] [TestCase("disabled")]
    [TestCase("no-l3")] [TestCase("finished")] [TestCase("pit")]
    public void InvalidDeclarationNeverConsumesResourcesOrRaceUse(string reason)
    {
        if (reason == "late") race.SealGermanBurstDeclaration(player);
        if (reason == "no-l3") player.techState.activeNodeIds.Remove("de-l3-grill-spezial");
        player.hasFinished = reason == "finished"; player.pitStopScheduled = reason == "pit";
        Assert.That(race.TryDeclareGermanBurst(player, reason != "disabled", reason == "wrong-turn" ? 2 : 1), Is.False);
        Assert.That(player.techState.grillSpezialUsed, Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(10));
    }

    [TestCase(false)] [TestCase(true)]
    public void TerminalOrExitClosesReceiptsWithoutRefundingTheLockedUse(bool terminal)
    {
        Declare(); Pay(); player.hasFinished = terminal;
        if (terminal) RaceTurnCleanup.Execute(player, null, null, _ => { });
        else Burst.CloseWindow();
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(9));
        Assert.That(Burst.Receipts, Is.Empty);
        Assert.That(player.techState.grillSpezialUsed, Is.True);
    }

    [Test]
    public void CleanupExceptionStillClosesTheUnpaidDeclaration()
    {
        Declare();
        Assert.Throws<InvalidOperationException>(() => RaceTurnTechnologyCleanup.Execute(player, race,
            (_, __) => throw new InvalidOperationException("fixture"), _ => { }, (_, __) => { }));
        Assert.That(Burst.Declared, Is.False);
        Assert.That(player.techState.grillSpezialUsed, Is.False);
    }

    [Test]
    public void SkillCleanupExceptionAlsoClosesTheWindowWithoutRefund()
    {
        Declare(); Pay();
        Assert.Throws<InvalidOperationException>(() => RaceTurnCleanup.Execute(player,
            _ => throw new InvalidOperationException("fixture"), null, null));
        Assert.That(Burst.Declared, Is.False);
        Assert.That(player.techState.grillSpezialUsed, Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(9));
    }

    [Test]
    public void BorrowedBurstTracksMansellFixedPaymentAsTheSameExactReceipt()
    {
        player.teamId = TeamId.UK; player.driverId = "uk_nigel_mansell";
        player.techState.teamId = TeamId.UK; player.techState.activeNodeIds.Clear();
        player.techState.activeNodeIds.Add("uk-l3-sun-never-sets"); player.techState.sunNeverSetsTarget = TeamId.DE;
        player.driverSkill.Initialize(player.DriverProfile, 7, true);
        player.deck.AddCardsToHand(new[] { new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 2) });
        race.Players.Add(new PlayerState("target", true, 3, 1) { driverId = "de_michael_schumacher", teamId = TeamId.DE });
        race.BeginDriverSkillInput(player, 60); Declare();
        var context = new DriverSkillActivationContext(true, 1, 3, 10, 10, 0);
        Assert.That(race.TryPrepareMansellCharge(player, 60, 0, 1, 1, 1, context, out var plan, out var reason), Is.True, reason);
        Assert.That(plan.TryCommit(race, player, 60, 0, 1, 1, 1, context, plan.Choices[0], out reason), Is.True, reason);
        Assert.That(Burst.Receipts, Is.EquivalentTo(new[] { plan.PaidHeat }));
        Assert.That(player.heatPaidCardsThisTurn, Is.EquivalentTo(new[] { plan.PaidHeat }));
        Assert.That(player.techState.grillSpezialUsed, Is.True);
        Assert.That(race.GetGrillSpezialCooldown(player), Is.EqualTo(1));
    }

    [Test]
    public void BorrowedL3WithoutL1CanRefundOrdinaryPaymentWithoutGrantingFuel()
    {
        player.teamId = TeamId.UK; player.techState.activeNodeIds.Clear();
        player.techState.teamId = TeamId.UK; player.techState.activeNodeIds.Add("uk-l3-sun-never-sets");
        player.techState.sunNeverSetsTarget = TeamId.DE; Declare(); Pay();
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out _), Is.False);
        Assert.That(race.GetGrillSpezialCooldown(player), Is.EqualTo(1));
    }

    [Test]
    public void ForecastCopiesReceiptCollectionsAndCannotSpendTheLiveWindow()
    {
        Declare(); var card = new CardData(CardType.Speed, 2); player.deck.AddCardsToHand(new[] { card });
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        try
        {
            config.enableTechTree = true; config.gearOneCooldown = 0;
            int version = Burst.Version;
            for (int i = 0; i < 3; i++)
            {
                var forecast = RaceTurnPreviewRules.Evaluate(player, race, config, new[] { card }, false, 1);
                Assert.That(forecast.Movement.TotalCells, Is.EqualTo(2));
                Assert.That(forecast.HeatPaid, Is.Zero);
            }
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(10));
            Assert.That(Burst.Version, Is.EqualTo(version));
            Assert.That(Burst.Receipts, Is.Empty);
            Assert.That(Burst.FuelUsesThisTurn, Is.Zero);
            Assert.That(player.techState.grillSpezialUsed, Is.False);
            var copy = player.techState.CreatePresentationCopy(); copy.germanBurst.CloseWindow();
            Assert.That(Burst.Declared, Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }

    [Test]
    public void ForecastWithThreeAlreadyPaidUsesNeitherChargesNorAddsAnExtraOrdinaryFuel()
    {
        Declare();
        for (int i = 0; i < 3; i++) Assert.That(race.TryCommitSchwarzbierFuel(player, true, out _), Is.True);
        var card = new CardData(CardType.Speed, 2); player.deck.AddCardsToHand(new[] { card });
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        try
        {
            config.enableTechTree = true; config.gearOneCooldown = 0;
            for (int i = 0; i < 3; i++)
            {
                var forecast = RaceTurnPreviewRules.Evaluate(player, race, config, new[] { card }, false, 1);
                Assert.That(forecast.Movement.TotalCells, Is.EqualTo(8));
                Assert.That(forecast.HeatPaid, Is.Zero);
            }
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(7));
            Assert.That(Burst.FuelUsesThisTurn, Is.EqualTo(3));
            Assert.That(Burst.Receipts.Count, Is.EqualTo(3));
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }
}
