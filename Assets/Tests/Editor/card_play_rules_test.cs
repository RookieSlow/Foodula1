using System.Collections.Generic;
using NUnit.Framework;

public class CardPlayRulesTest
{
    private PlayerState CreatePlayerWithHand(params CardData[] cards)
    {
        var player = new PlayerState("P", false, 0, 1);
        player.deck.heatPool = new HeatPool(5);
        player.deck.AddCardsToHand(new List<CardData>(cards));
        return player;
    }

    [Test]
    public void test_commit_speed_card_removes_from_hand_and_accumulates_turn_play()
    {
        var speed = new CardData(CardType.Speed, 3);
        var player = CreatePlayerWithHand(speed);

        var result = CardPlayRules.CommitSpeedCard(player, speed, 2);

        Assert.AreEqual(SpeedCardCommitResult.Success, result);
        Assert.IsFalse(player.deck.ContainsInHand(speed));
        Assert.AreEqual(1, player.playedSpeedCardsThisTurn.Count);
        Assert.AreSame(speed, player.playedSpeedCardsThisTurn[0]);
    }

    [TestCase(CardType.Heat)]
    [TestCase(CardType.Trick)]
    public void test_commit_speed_card_rejects_non_speed_without_mutation(CardType type)
    {
        CardData card = type == CardType.Trick
            ? CardData.CreateTrick("uk-scone")
            : new CardData(type, 0);
        var player = type == CardType.Heat
            ? CreatePlayerWithHand()
            : CreatePlayerWithHand(card);
        if (type == CardType.Heat)
        {
            Assert.AreEqual(1, player.deck.DrawHeatFromPoolToHand(1));
            card = player.deck.Hand[0];
        }

        var result = CardPlayRules.CommitSpeedCard(player, card, 2);

        Assert.AreEqual(SpeedCardCommitResult.InvalidCard, result);
        Assert.IsTrue(player.deck.ContainsInHand(card));
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
    }

    [Test]
    public void test_commit_speed_card_rejects_card_not_in_hand()
    {
        var held = new CardData(CardType.Speed, 2);
        var player = CreatePlayerWithHand(held);
        var speed = new CardData(CardType.Speed, 2);

        var result = CardPlayRules.CommitSpeedCard(player, speed, 2);

        Assert.AreEqual(SpeedCardCommitResult.CardNotInHand, result);
        Assert.IsTrue(player.deck.ContainsInHand(held));
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
    }

    [Test]
    public void test_commit_speed_card_rejects_when_limit_reached_without_losing_card()
    {
        var first = new CardData(CardType.Speed, 1);
        var second = new CardData(CardType.Speed, 2);
        var player = CreatePlayerWithHand(first, second);
        Assert.AreEqual(SpeedCardCommitResult.Success,
            CardPlayRules.CommitSpeedCard(player, first, 1));

        var result = CardPlayRules.CommitSpeedCard(player, second, 1);

        Assert.AreEqual(SpeedCardCommitResult.SpeedLimitReached, result);
        Assert.IsTrue(player.deck.ContainsInHand(second));
        Assert.AreEqual(1, player.playedSpeedCardsThisTurn.Count);
    }

    [Test]
    public void test_commit_speed_cards_commits_selected_group_atomically()
    {
        var first = new CardData(CardType.Speed, 1);
        var second = new CardData(CardType.Speed, 3);
        var third = new CardData(CardType.Speed, 2);
        var player = CreatePlayerWithHand(first, second, third);

        var result = CardPlayRules.CommitSpeedCards(
            player, new List<CardData> { first, second }, 3);

        Assert.AreEqual(SpeedCardCommitResult.Success, result);
        Assert.IsFalse(player.deck.ContainsInHand(first));
        Assert.IsFalse(player.deck.ContainsInHand(second));
        Assert.IsTrue(player.deck.ContainsInHand(third));
        Assert.AreEqual(2, player.playedSpeedCardsThisTurn.Count);
    }

    [Test]
    public void test_commit_speed_cards_limit_failure_does_not_partially_consume_selection()
    {
        var first = new CardData(CardType.Speed, 1);
        var second = new CardData(CardType.Speed, 3);
        var player = CreatePlayerWithHand(first, second);
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 2));

        var result = CardPlayRules.CommitSpeedCards(
            player, new List<CardData> { first, second }, 2);

        Assert.AreEqual(SpeedCardCommitResult.SpeedLimitReached, result);
        Assert.IsTrue(player.deck.ContainsInHand(first));
        Assert.IsTrue(player.deck.ContainsInHand(second));
        Assert.AreEqual(1, player.playedSpeedCardsThisTurn.Count);
    }

    [Test]
    public void test_hotpot_bonus_requires_using_the_extra_attack_slot()
    {
        var player = CreatePlayerWithHand();
        player.gear = 3;
        player.trickState.hotpotBaseActive = true;
        player.playedSpeedCardsThisTurn.AddRange(new[]
        {
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 3)
        });

        Assert.AreEqual(0, CardPlayRules.GetHotpotMovementBonus(player));

        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 4));

        Assert.AreEqual(1, CardPlayRules.GetHotpotMovementBonus(player));
    }

    [Test]
    public void test_hotpot_bonus_starts_after_kanto_slots_are_filled()
    {
        var player = CreatePlayerWithHand();
        player.gear = 2;
        player.extraCardSlotsThisTurn = 2;
        player.trickState.hotpotBaseActive = true;
        for (int i = 0; i < 4; i++)
            player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 1));

        Assert.AreEqual(0, CardPlayRules.GetHotpotMovementBonus(player));

        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 1));

        Assert.AreEqual(1, CardPlayRules.GetHotpotMovementBonus(player));
    }
}
