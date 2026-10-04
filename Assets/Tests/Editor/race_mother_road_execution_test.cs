using System;
using System.Collections.Generic;
using NUnit.Framework;

public sealed class RaceMotherRoadExecutionTests
{
    private static PlayerState Player(int engineHeat = 3)
    {
        var player = new PlayerState("US", true, 12, 3)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US)
        };
        player.deck.heatPool = new HeatPool(engineHeat);
        return player;
    }

    [TestCase(0, 1)]
    [TestCase(1, 2)]
    public void ProsperityCoolsTwoAndRecordsActualAmount(int previousPasses, int expectedPasses)
    {
        PlayerState player = Player();
        player.techState.landmark1PassCount = previousPasses;
        var calls = new List<string>();

        RaceMotherRoadExecution.Execute(player, 0, 4,
            amount => { calls.Add("cool:" + amount); return 1; },
            _ => throw new AssertionException("unexpected repair"),
            _ => throw new AssertionException("unexpected movement"), calls.Add);

        Assert.That(player.techState.landmark1PassCount, Is.EqualTo(expectedPasses));
        Assert.That(calls, Is.EqualTo(new[]
        {
            "cool:2", "<color=green>US 母亲之路(繁荣)：自动冷却 1 张热量牌。</color>"
        }));
    }

    [Test]
    public void MissingTechnologyStateHasNoEffect()
    {
        PlayerState player = Player();
        player.techState = null;
        RaceMotherRoadExecution.Execute(player, 0, 4,
            _ => throw new AssertionException("unexpected cooling"),
            _ => throw new AssertionException("unexpected payment"),
            _ => throw new AssertionException("unexpected movement"),
            _ => throw new AssertionException("unexpected log"));
    }

    [Test]
    public void DeclineWithoutEngineHeatSkipsRepairWithoutTryingPayment()
    {
        PlayerState player = Player(0);
        player.techState.landmark1PassCount = 2;
        var logs = new List<string>();
        RaceMotherRoadExecution.Execute(player, 0, 4,
            _ => throw new AssertionException("unexpected cooling"),
            _ => throw new AssertionException("unexpected payment"),
            _ => throw new AssertionException("unexpected movement"), logs.Add);
        Assert.That(player.techState.landmark1PassCount, Is.EqualTo(3));
        Assert.That(player.techState.totalRepairs, Is.Zero);
        Assert.That(logs, Is.EqualTo(new[] { "US 母亲之路(衰退)：引擎无热，跳过修复。" }));
    }

    [TestCase(true, 1)]
    [TestCase(false, 0)]
    public void DeclineRepairsAndLogsOnlyAfterSuccessfulPayment(bool paymentSucceeded, int expectedRepairs)
    {
        PlayerState player = Player(1);
        player.techState.landmark1PassCount = 2;
        var calls = new List<string>();
        RaceMotherRoadExecution.Execute(player, 0, 4,
            _ => throw new AssertionException("unexpected cooling"),
            amount => { calls.Add("pay:" + amount); return paymentSucceeded; },
            _ => throw new AssertionException("unexpected movement"), calls.Add);
        Assert.That(player.techState.totalRepairs, Is.EqualTo(expectedRepairs));
        Assert.That(calls, Is.EqualTo(paymentSucceeded
            ? new[] { "pay:1", "US 母亲之路(衰退)：修复地标，付 1 热。" }
            : new[] { "pay:1" }));
    }

    [Test]
    public void EmptyRevivalDoesNotSpendOncePerRaceOpportunity()
    {
        PlayerState player = Player();
        player.techState.landmark1PassCount = 2;
        player.techState.totalRepairs = 2;
        CardData temporary = CardData.CreateTempHeat();
        player.deck.AddCardsToHand(new[] { temporary });
        RaceMotherRoadExecution.Execute(player, 0, 4,
            _ => throw new AssertionException("unexpected cooling"),
            _ => throw new AssertionException("unexpected payment"),
            _ => throw new AssertionException("unexpected movement"),
            _ => throw new AssertionException("unexpected log"));
        Assert.That(player.techState.landmark1PassCount, Is.EqualTo(3));
        Assert.That(player.techState.landmark1UltUsed, Is.False);
        Assert.That(player.deck.Hand, Does.Contain(temporary));
    }

    [Test]
    public void RevivalReturnsPermanentHeatAndDestroysTemporaryBeforeMovement()
    {
        PlayerState player = Player();
        player.techState.landmark2PassCount = 2;
        player.techState.totalRepairs = 2;
        Assert.That(player.deck.DrawHeatFromPoolToHand(2), Is.EqualTo(2));
        player.deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
        var calls = new List<string>();
        RaceMotherRoadExecution.Execute(player, 1, 4,
            _ => throw new AssertionException("unexpected cooling"),
            _ => throw new AssertionException("unexpected payment"),
            amount =>
            {
                calls.Add("move:" + amount);
                Assert.That(player.deck.CountHeatInHand(), Is.Zero);
                Assert.That(player.deck.heatPool.remaining, Is.EqualTo(3));
                Assert.That(player.techState.landmark2UltUsed, Is.True);
            }, calls.Add);
        Assert.That(calls, Is.EqualTo(new[]
        {
            "move:2", "<color=orange>US 母亲之路(复兴)！2 张热量牌转为移动。</color>"
        }));
        Assert.That(player.techState.landmark2PassCount, Is.EqualTo(3));
    }

    [Test]
    public void RevivalRegionalDebtMovesOnlyForNetEngineReturn()
    {
        PlayerState player = Player(1);
        player.techState.landmark1PassCount = 2;
        player.techState.totalRepairs = 2;
        player.deck.SetRegionalCapacityBonus(2);
        Assert.That(player.deck.DrawHeatFromPoolToHand(3), Is.EqualTo(3));
        player.deck.SetRegionalCapacityBonus(0);
        int movement = -1;
        RaceMotherRoadExecution.Execute(player, 0, 4,
            _ => 0, _ => true, amount => movement = amount, null);
        Assert.That(movement, Is.EqualTo(1));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(1));
        Assert.That(player.deck.RegionalHeatPendingRetirement, Is.Zero);
        Assert.That(player.deck.CountHeatInHand(), Is.Zero);
        Assert.That(player.techState.landmark1UltUsed, Is.True);
    }

    [Test]
    public void OtherLandmarksUsedUltimateReturnsToDeclineInsteadOfMoving()
    {
        PlayerState player = Player(1);
        player.techState.landmark1UltUsed = true;
        player.techState.landmark2PassCount = 2;
        player.techState.totalRepairs = 2;
        int payments = 0;
        RaceMotherRoadExecution.Execute(player, 1, 4,
            _ => throw new AssertionException("unexpected cooling"),
            _ => { payments++; return true; },
            _ => throw new AssertionException("unexpected movement"), null);
        Assert.That(payments, Is.EqualTo(1));
        Assert.That(player.techState.totalRepairs, Is.EqualTo(3));
        Assert.That(player.techState.landmark2UltUsed, Is.False);
    }

    [Test]
    public void CoolingExceptionStopsProsperityBeforeLog()
    {
        PlayerState player = Player();
        Assert.Throws<InvalidOperationException>(() => RaceMotherRoadExecution.Execute(player, 0, 4,
            _ => throw new InvalidOperationException("cooling"),
            _ => throw new AssertionException("unexpected payment"),
            _ => throw new AssertionException("unexpected movement"),
            _ => throw new AssertionException("unexpected log")));
        Assert.That(player.techState.landmark1PassCount, Is.EqualTo(1));
    }

    [Test]
    public void PaymentExceptionStopsDeclineBeforeRepair()
    {
        PlayerState player = Player(1);
        player.techState.landmark1PassCount = 2;
        Assert.Throws<InvalidOperationException>(() => RaceMotherRoadExecution.Execute(player, 0, 4,
            _ => 0,
            _ => throw new InvalidOperationException("payment"),
            _ => throw new AssertionException("unexpected movement"),
            _ => throw new AssertionException("unexpected log")));
        Assert.That(player.techState.landmark1PassCount, Is.EqualTo(3));
        Assert.That(player.techState.totalRepairs, Is.Zero);
    }
}
