using NUnit.Framework;

/// <summary>
/// I09 focused regression coverage: CN L2 温杯 and Zhou Guanyu's old-heat
/// preparation/recovery stay owner-input scoped and do not fabricate heat.
/// </summary>
public class ChinaWarmCupTests
{
    private static TechTreeState WarmCupState()
    {
        var state = new TechTreeState(TeamId.CN, TechTreeRules.DEMO_BUDGET);
        state.activeNodeIds.Add("cn-l2-dim-sum-combo");
        return state;
    }

    [Test]
    public void WarmCupResolvesOneFixedGoPaymentDuringOrderedCleanup()
    {
        var session = new RaceSession(new SystemRandomSource(1))
        {
            TechDb = TechTreeDatabaseFactory.CreateDefault()
        };
        var player = new PlayerState("周冠宇", false, 0, ChinaGearShiftRules.GoGear)
        {
            teamId = TeamId.CN,
            usesChinaGearSystem = true,
            techState = WarmCupState()
        };
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 1) }, new HeatPool(3));
        session.Players.Add(player);

        session.BeginTurn(player, 7);
        Assert.That(session.TryDeclareChinaWarmCup(player, true, 7), Is.True);
        int handBefore = player.deck.HandCount;
        ChinaWarmCupResult result = session.ResolveChinaWarmCup(player);

        Assert.That(result.triggered, Is.True);
        Assert.That(result.paidHeat, Is.EqualTo(1));
        Assert.That(result.extraMovement, Is.EqualTo(1));
        Assert.That(player.deck.HandCount, Is.EqualTo(handBefore + 1));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(2));
        Assert.That(session.TryDeclareChinaWarmCup(player, true, 7), Is.False,
            "A Warm Cup declaration cannot be repeated in the same owner input.");
    }

    [Test]
    public void WarmCupRuntimeReopensOnlyOnTheNextGlobalTurn()
    {
        var runtime = new WarmCupRuntime();
        runtime.BeginTurn(3);
        Assert.That(runtime.TryDeclare(3), Is.True);
        Assert.That(runtime.TryConsume(3), Is.True);
        Assert.That(runtime.TryDeclare(3), Is.False);

        runtime.BeginTurn(4);
        Assert.That(runtime.TryDeclare(4), Is.True);
    }

    [Test]
    public void ZhouPreparationIsAtomicAndRecoversOnlyOldPermanentHandHeat()
    {
        var deck = new CardDeck();
        deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 2) }, new HeatPool(3));
        Assert.That(deck.DrawToHand(1), Is.True);
        CardData speed = deck.Hand[0];
        Assert.That(deck.DrawHeatFromPoolToHand(1), Is.EqualTo(1));

        Assert.That(deck.TryCommitZhouPreparationCost(speed, 1, out int restored), Is.True);
        Assert.That(restored, Is.EqualTo(1));
        Assert.That(deck.ContainsInHand(speed), Is.False);
        Assert.That(deck.HandCount, Is.EqualTo(0));
        Assert.That(deck.heatPool.remaining, Is.EqualTo(3));

        var noHeat = new CardDeck();
        noHeat.InitializeExactOrder(new[] { new CardData(CardType.Speed, 2) }, new HeatPool(3));
        Assert.That(noHeat.DrawToHand(1), Is.True);
        CardData untouched = noHeat.Hand[0];
        Assert.That(noHeat.TryCommitZhouPreparationCost(untouched, 1, out _), Is.False);
        Assert.That(noHeat.ContainsInHand(untouched), Is.True,
            "A failed old-heat validation must not discard the ordinary speed card.");
    }

    [Test]
    public void ZhouSteadyHeartRecoversOldHeatAfterARealGoPayment()
    {
        DriverCatalog.TryGet("cn_zhou_guanyu", out DriverProfile profile);
        var session = new RaceSession(new SystemRandomSource(2));
        var player = new PlayerState("周冠宇", false, 0, ChinaGearShiftRules.GoGear)
        {
            teamId = TeamId.CN,
            usesChinaGearSystem = true
        };
        player.driverSkill.Initialize(profile, 7, true);
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 1) }, new HeatPool(4));
        Assert.That(player.deck.DrawHeatFromPoolToHand(2), Is.EqualTo(2));
        session.Players.Add(player);

        session.BeginTurn(player, 1);
        Assert.That(session.BeginDriverSkillInput(player), Is.True);
        session.TrackHeatPaid(player, 1);

        Assert.That(session.ResolveZhouSteadyHeartAfterCooling(player), Is.EqualTo(2));
        Assert.That(player.deck.CountHandHeatRestorableToEngine(), Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
    }
}
