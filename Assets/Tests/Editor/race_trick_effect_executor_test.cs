using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class RaceTrickEffectExecutorTests
{
    private static PlayerState Player(params CardData[] drawOrder)
    {
        var player = new PlayerState("Racer", false, 3, 2);
        player.deck.InitializeExactOrder(drawOrder, new HeatPool(3));
        return player;
    }

    [Test]
    public void FailedHeatPaymentInterruptsEveryLaterEffect()
    {
        var next = new CardData(CardType.Speed, 4);
        PlayerState player = Player(next);
        player.trickState.tempHeatAvailable = true;
        var calls = new List<string>();
        var result = new TrickPlayResult
        {
            heatToPay = 1, heatToCool = 1, extraMovement = 2,
            cardsToDraw = 1, requiresSpeedDiscard = true
        };

        RaceTrickEffectExecutor.Execute(player, result,
            amount => { calls.Add("pay:" + amount); return false; },
            amount => { calls.Add("cool"); return amount; },
            () => { calls.Add("lookup"); return TrickEffectType.KantoOden; },
            calls.Add);

        Assert.That(calls, Is.EqualTo(new[] { "pay:1" }));
        Assert.That(player.trickMoveBonusThisTurn, Is.Zero);
        Assert.That(player.deck.HandCount, Is.Zero);
        Assert.That(player.trickState.tempHeatAvailable, Is.True);
        Assert.That(player.kantoOdenSkipThisTurn, Is.False);
    }

    [Test]
    public void PaymentCoolingAndLaterEffectsRetainOriginalSequence()
    {
        var next = new CardData(CardType.Speed, 4);
        PlayerState player = Player(next);
        player.deck.AddCardsToHand(new[] { new CardData(CardType.Speed, 1) });
        player.trickState.tempHeatAvailable = true;
        player.trickState.kantoOdenActive = true;
        var calls = new List<string>();
        var result = new TrickPlayResult
        {
            heatToPay = 1, heatToCool = 2, extraMovement = 3,
            cardsToDraw = 1, requiresSpeedDiscard = true
        };

        RaceTrickEffectExecutor.Execute(player, result,
            amount => { calls.Add("pay:" + amount); return true; },
            amount => { calls.Add("cool:" + amount); return 1; },
            () =>
            {
                calls.Add("lookup");
                Assert.That(player.deck.Hand, Does.Contain(next));
                Assert.That(player.deck.DiscardPile.Single().value, Is.EqualTo(1));
                Assert.That(player.deck.CountHeatInHand(), Is.EqualTo(1));
                return TrickEffectType.KantoOden;
            },
            calls.Add);

        Assert.That(calls, Is.EqualTo(new[]
        {
            "pay:1", "cool:2", "Racer 特技冷却 1 张热量牌。",
            "Racer 弃掉 1 速度牌。", "Racer 获得 1 张限时热量牌（回合结束销毁）。",
            "lookup", "Racer 关东慢煮生效：本回合跳过，下回合可多出 2 张牌。"
        }));
        Assert.That(player.trickMoveBonusThisTurn, Is.EqualTo(3));
        Assert.That(player.trickState.tempHeatAvailable, Is.False);
        Assert.That(player.kantoOdenSkipThisTurn, Is.True);
        Assert.That(player.trickState.kantoOdenAccumulatedCards, Is.EqualTo(2));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonPositiveHeatRequestDoesNotCallPaymentOrCooling(int amount)
    {
        PlayerState player = Player();
        RaceTrickEffectExecutor.Execute(player,
            new TrickPlayResult { heatToPay = amount, heatToCool = amount, extraMovement = 2 },
            _ => throw new AssertionException("unexpected payment"),
            _ => throw new AssertionException("unexpected cooling"),
            () => null, null);
        Assert.That(player.trickMoveBonusThisTurn, Is.EqualTo(2));
    }

    [Test]
    public void ZeroActualCoolingProducesNoMessage()
    {
        PlayerState player = Player();
        var messages = new List<string>();
        RaceTrickEffectExecutor.Execute(player,
            new TrickPlayResult { heatToCool = 3 },
            _ => true, _ => 0, () => null, messages.Add);
        Assert.That(messages, Is.Empty);
    }

    [Test]
    public void SpeedDiscardChoosesLowestRealHandCardAndKeepsOtherCards()
    {
        PlayerState player = Player();
        var high = new CardData(CardType.Speed, 4);
        var low = new CardData(CardType.Speed, 1);
        var trick = CardData.CreateTrick("us-cola");
        player.deck.AddCardsToHand(new[] { high, trick, low });
        var messages = new List<string>();
        RaceTrickEffectExecutor.Execute(player,
            new TrickPlayResult { requiresSpeedDiscard = true },
            _ => true, _ => 0, () => null, messages.Add);
        Assert.That(player.deck.Hand, Is.EquivalentTo(new[] { high, trick }));
        Assert.That(player.deck.DiscardPile, Is.EqualTo(new[] { low }));
        Assert.That(messages, Is.EqualTo(new[] { "Racer 弃掉 1 速度牌。" }));
    }

    [Test]
    public void NoSpeedInHandDoesNotDiscardOrAnnounce()
    {
        PlayerState player = Player();
        var trick = CardData.CreateTrick("us-cola");
        player.deck.AddCardsToHand(new[] { trick });
        var messages = new List<string>();
        RaceTrickEffectExecutor.Execute(player,
            new TrickPlayResult { requiresSpeedDiscard = true },
            _ => true, _ => 0, () => null, messages.Add);
        Assert.That(player.deck.Hand, Is.EqualTo(new[] { trick }));
        Assert.That(player.deck.DiscardPileCount, Is.Zero);
        Assert.That(messages, Is.Empty);
    }

    [Test]
    public void AvailableTempHeatIsGrantedAndConsumedOnlyOnce()
    {
        PlayerState player = Player();
        player.trickState.tempHeatAvailable = true;
        var messages = new List<string>();
        var empty = new TrickPlayResult();
        RaceTrickEffectExecutor.Execute(player, empty, _ => true, _ => 0, () => null, messages.Add);
        RaceTrickEffectExecutor.Execute(player, empty, _ => true, _ => 0, () => null, messages.Add);
        Assert.That(player.deck.HandCount, Is.EqualTo(1));
        Assert.That(player.deck.Hand.Single().IsHeat, Is.True);
        Assert.That(player.deck.Hand.Single().isTemp, Is.True);
        Assert.That(player.trickState.tempHeatAvailable, Is.False);
        Assert.That(messages.Count, Is.EqualTo(1));
    }

    [Test]
    public void NonKantoEffectDoesNotSetSkipOrAccumulateCards()
    {
        PlayerState player = Player();
        RaceTrickEffectExecutor.Execute(player, new TrickPlayResult(),
            _ => true, _ => 0, () => TrickEffectType.Scone, null);
        Assert.That(player.kantoOdenSkipThisTurn, Is.False);
        Assert.That(player.trickState.kantoOdenAccumulatedCards, Is.Zero);
    }

    [Test]
    public void PaymentExceptionStillStopsAllLaterEffects()
    {
        PlayerState player = Player();
        Assert.Throws<InvalidOperationException>(() => RaceTrickEffectExecutor.Execute(player,
            new TrickPlayResult { heatToPay = 1, extraMovement = 2 },
            _ => throw new InvalidOperationException("payment"),
            _ => throw new AssertionException("unexpected cooling"),
            () => throw new AssertionException("unexpected lookup"), null));
        Assert.That(player.trickMoveBonusThisTurn, Is.Zero);
    }

    [Test]
    public void CoolingExceptionStillStopsAllLaterEffects()
    {
        PlayerState player = Player();
        Assert.Throws<InvalidOperationException>(() => RaceTrickEffectExecutor.Execute(player,
            new TrickPlayResult { heatToCool = 1, extraMovement = 2 },
            _ => true, _ => throw new InvalidOperationException("cooling"),
            () => throw new AssertionException("unexpected lookup"), null));
        Assert.That(player.trickMoveBonusThisTurn, Is.Zero);
    }
}
