using System;
using System.Collections.Generic;
using NUnit.Framework;

public class RaceTurnTechnologyCleanupTests
{
    private RaceSession session;
    private PlayerState player;

    [SetUp]
    public void SetUp()
    {
        session = new RaceSession(new SystemRandomSource(1)) { TechDb = TechTreeDatabaseFactory.CreateDefault() };
        player = new PlayerState("CN", false, 0, 3) { teamId = TeamId.CN, techState = new TechTreeState(TeamId.CN, 0) };
        // Explicit runtime snapshot: unlock economy is covered by TechTreeRulesTests.
        player.techState.activeNodeIds.UnionWith(new[] { "cn-l1-yin-yang-tea", "cn-l2-dim-sum-combo" });
        player.techState.dimSumPlayedTrick = true;
        player.techState.dimSumPlayedSpeed = true;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ComboIsReadAfterFirstEffectAndSecondEffectReadsCurrentMode(bool finishOnFirst)
    {
        var events = new List<string>();
        RaceTurnTechnologyCleanup.Execute(player, session, (p, result) =>
        {
            events.Add(!result.triggered ? "none" : result.isYin ? "yin" : "yang");
            p.techState.dimSumPaidHeat = true;
            p.gear = 1;
            p.hasFinished = finishOnFirst;
        }, p => events.Add("combo"), (p, count) => Assert.Fail("CN must not cool through Grill."));
        CollectionAssert.AreEqual(new[] { "yin", "combo", finishOnFirst ? "none" : "yang" }, events);
    }

    [Test]
    public void IncompleteComboOnlyRunsFirstEffect()
    {
        int applications = 0;
        RaceTurnTechnologyCleanup.Execute(player, session, (p, r) => applications++,
            p => Assert.Fail("Incomplete combo"), (p, n) => Assert.Fail("No Grill"));
        Assert.AreEqual(1, applications);
        Assert.IsFalse(player.techState.dimSumPaidHeat);
    }

    [Test]
    public void FirstEffectFailureAbortsRemainingEffects()
    {
        player.techState.dimSumPaidHeat = true;
        Assert.Throws<InvalidOperationException>(() => RaceTurnTechnologyCleanup.Execute(player, session,
            (p, r) => throw new InvalidOperationException("effect failed"),
            p => Assert.Fail("Must abort"), (p, n) => Assert.Fail("Must abort")));
    }

    [Test]
    public void ComboReportFailureStopsSecondEffect()
    {
        player.techState.dimSumPaidHeat = true;
        int applications = 0;
        Assert.Throws<InvalidOperationException>(() => RaceTurnTechnologyCleanup.Execute(player, session,
            (p, r) => applications++, p => throw new InvalidOperationException("report failed"),
            (p, n) => Assert.Fail("Must abort")));
        Assert.AreEqual(1, applications);
    }

    [Test]
    public void NoActiveNodesReturnsNoTriggerWithoutMutatingCombo()
    {
        player.techState.activeNodeIds.Clear();
        player.techState.dimSumPaidHeat = true;
        RaceTurnTechnologyCleanup.Execute(player, session, (p, r) => Assert.IsFalse(r.triggered),
            p => Assert.Fail("No active combo"), (p, n) => Assert.Fail("No Grill"));
        Assert.IsTrue(player.techState.dimSumPaidHeat);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GrillUsesRecordedPaymentsOnlyOnce(bool used)
    {
        player.teamId = TeamId.DE;
        player.techState = new TechTreeState(TeamId.DE, 0);
        player.techState.activeNodeIds.Add("de-l3-grill-spezial");
        player.techState.grillSpezialUsed = used;
        player.techState.grillSpezialHeatPaidThisTurn = 3;
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 1) }, new HeatPool(6));
        player.deck.DrawHeatFromPool(3, HeatPaymentDestination.Hand, player.heatPaidCardsThisTurn);
        int calls = 0;
        RaceTurnTechnologyCleanup.Execute(player, session, (p, r) => Assert.IsFalse(r.triggered),
            p => Assert.Fail("No CN combo"), (p, n) =>
            {
                calls++;
                Assert.IsTrue(p.techState.grillSpezialUsed, "Once flag precedes cooling adapter.");
                Assert.AreEqual(3, p.deck.CoolRecordedHeatThroughDiscard(p.heatPaidCardsThisTurn, n));
            });
        Assert.IsTrue(player.techState.grillSpezialUsed);
        Assert.AreEqual(used ? 0 : 1, calls);
        Assert.AreEqual(used ? 3 : 6, player.deck.heatPool.remaining);
    }
}
