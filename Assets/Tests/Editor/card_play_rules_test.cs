using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>Explicit BBQ commit and original heat-instance retirement, without live UI/storage.</summary>
public class BBQHeatCardLifecycleTests
{
    private static PlayerState CreatePlayer(TeamId team = TeamId.US, bool ai = false)
    {
        var player = new PlayerState("BBQ", ai, 5, 2)
        {
            teamId = team, techState = new TechTreeState(team, 77)
        };
        player.techState.activeNodeIds.Add(team == TeamId.UK ? "uk-l3-sun-never-sets" : "us-l2-smoked-bbq");
        if (team == TeamId.UK) player.techState.sunNeverSetsTarget = TeamId.US;
        player.deck.heatPool = new HeatPool(6);
        return player;
    }

    [TestCase(TeamId.US, false, false)]
    [TestCase(TeamId.US, true, false)]
    [TestCase(TeamId.US, false, true)]
    [TestCase(TeamId.US, true, true)]
    [TestCase(TeamId.UK, false, false)]
    [TestCase(TeamId.UK, true, false)]
    [TestCase(TeamId.UK, false, true)]
    [TestCase(TeamId.UK, true, true)]
    public void MixedCommitLeavesZoneThenRetiresAndCoolsWithoutCreatingHeat(TeamId team, bool ai, bool temporary)
    {
        var session = new RaceSession(new SystemRandomSource(7));
        var player = CreatePlayer(team, ai);
        var speed = new CardData(CardType.Speed, 3);
        player.deck.DrawHeatFromPoolToHand(1);
        CardData permanent = player.deck.Hand[0];
        CardData heat = temporary ? CardData.CreateTempHeat() : permanent;
        var unplayedTemp = CardData.CreateTempHeat();
        player.deck.AddCardsToHand(new[] { speed, unplayedTemp });
        if (temporary) player.deck.AddCardsToHand(new[] { heat });
        string[] active = new List<string>(player.techState.activeNodeIds).ToArray();
        var selection = new[] { heat, speed };

        Assert.AreEqual(SpeedCardCommitResult.Success,
            CardPlayRules.CommitSpeedCards(player, selection, 2, session, 60));
        CollectionAssert.AreEqual(selection, player.playedSpeedCardsThisTurn);
        Assert.IsFalse(player.deck.ContainsInHand(heat));
        Assert.IsTrue(heat.IsHeat);
        Assert.AreEqual(0, heat.value);
        Assert.AreEqual(temporary, heat.isTemp);
        Assert.AreEqual(5, player.deck.heatPool.remaining);
        Assert.AreEqual(6, player.deck.heatPool.remaining + player.deck.CountPermanentHeatOutsideEngine() +
            (temporary ? 0 : 1)); // played heat is outside the deck's three zones
        Assert.AreEqual(0, player.deck.DiscardPileCount);
        Assert.AreEqual(5, CardPlayRules.SumCommittedSpeedCardValues(player.playedSpeedCardsThisTurn));
        Assert.AreEqual(0, RaceRules.SumCardValues(new[] { heat })); // ordinary heat value stays zero
        CollectionAssert.AreEqual(active, player.techState.activeNodeIds);
        Assert.AreEqual(77, player.techState.rpBalance);
        Assert.IsEmpty(player.techState.unlockedNodeIds);

        // Eligibility is checked on commit, not retroactively on movement/cleanup.
        player.position = 15;
        player.techState.activeNodeIds.Clear();
        Assert.AreEqual(5, CardPlayRules.SumCommittedSpeedCardValues(player.playedSpeedCardsThisTurn));
        var order = new List<string>();
        RaceTurnCleanup.Execute(player, p =>
        {
            order.Add("skills");
            Assert.AreSame(heat, p.playedSpeedCardsThisTurn[0]);
            Assert.AreEqual(!temporary, p.deck.DiscardPile.Contains(heat));
            Assert.AreSame(speed, p.deck.DiscardPile[0]);
            Assert.AreEqual(5, p.deck.heatPool.remaining);
        }, (p, count) =>
        {
            order.Add("temporary");
            Assert.AreEqual(temporary ? 2 : 1, count);
            Assert.AreEqual(0, p.deck.CountTemporaryHeatOutsideEngine());
        }, p =>
        {
            order.Add("technology");
            CollectionAssert.AreEqual(selection, p.playedSpeedCardsThisTurn);
            Assert.AreEqual(1, p.deck.CoolHeat(1));
        });
        CollectionAssert.AreEqual(new[] { "skills", "temporary", "technology" }, order);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountPermanentHeatOutsideEngine());
        Assert.AreEqual(0, player.deck.CountTemporaryHeatOutsideEngine());
        Assert.AreEqual(1, player.deck.DiscardPileCount);
        Assert.AreSame(speed, player.deck.DiscardPile[0]);
        RaceTurnCleanup.Execute(player, null, (p, n) => Assert.Fail("Already retired"), null);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.AreEqual(1, player.deck.DiscardPileCount);
    }

    [TestCase(TeamId.CN)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.JP)]
    public void LegacyEntryStillRejectsHeatForEveryTeam(TeamId team)
    {
        var player = CreatePlayer(team);
        player.deck.DrawHeatFromPoolToHand(1);
        CardData heat = player.deck.Hand[0];
        Assert.AreEqual(SpeedCardCommitResult.InvalidCard,
            CardPlayRules.CommitSpeedCard(player, heat, 2));
        Assert.AreEqual(SpeedCardCommitResult.InvalidCard,
            CardPlayRules.CommitSpeedCards(player, new[] { heat }, 2));
        Assert.IsTrue(player.deck.ContainsInHand(heat));
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.AreEqual(5, player.deck.heatPool.remaining);
    }

    [TestCase(TeamId.US, 6)]
    [TestCase(TeamId.US, 15)]
    [TestCase(TeamId.US, 54)]
    [TestCase(TeamId.UK, 6)]
    [TestCase(TeamId.UK, 15)]
    [TestCase(TeamId.UK, 54)]
    public void EnteringRegionOnlyAfterMovementCannotAuthorizeCurrentCommit(TeamId team, int position)
    {
        var player = CreatePlayer(team);
        player.position = position;
        player.totalMovementThisTurn = 55 - position;
        player.deck.DrawHeatFromPoolToHand(1);
        CardData heat = player.deck.Hand[0];
        Assert.AreEqual(SpeedCardCommitResult.InvalidCard,
            CardPlayRules.CommitSpeedCards(player, new[] { heat }, 2, new RaceSession(), 60));
        Assert.IsTrue(player.deck.ContainsInHand(heat));
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
    }

    [TestCase(0)]
    [TestCase(5)]
    [TestCase(25)]
    [TestCase(30)]
    [TestCase(35)]
    [TestCase(55)]
    [TestCase(59)]
    public void InclusiveLandmarkBoundaryAllowsExactlySpeedTwo(int position)
    {
        var player = CreatePlayer();
        player.position = position;
        var heat = CardData.CreateTempHeat();
        player.deck.AddCardsToHand(new[] { heat });
        Assert.AreEqual(SpeedCardCommitResult.Success,
            CardPlayRules.CommitSpeedCards(player, new[] { heat }, 1, new RaceSession(), 60));
        Assert.AreEqual(2, CardPlayRules.SumCommittedSpeedCardValues(player.playedSpeedCardsThisTurn));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void MissingMapLengthCannotAuthorizeHeat(int total)
    {
        var player = CreatePlayer();
        player.deck.DrawHeatFromPoolToHand(1);
        CardData heat = player.deck.Hand[0];
        Assert.AreEqual(SpeedCardCommitResult.InvalidCard,
            CardPlayRules.CommitSpeedCards(player, new[] { heat }, 1, new RaceSession(), total));
        Assert.IsTrue(player.deck.ContainsInHand(heat));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void DisabledOrChangedTechnologyCannotAuthorizeHeat(int variant)
    {
        var player = CreatePlayer(TeamId.UK);
        player.deck.DrawHeatFromPoolToHand(1);
        CardData heat = player.deck.Hand[0];
        var session = new RaceSession();
        if (variant == 0) player.techState = null;
        if (variant == 1) player.techState.sunNeverSetsTarget = TeamId.IT;
        if (variant == 2) session = null;
        Assert.AreEqual(SpeedCardCommitResult.InvalidCard,
            CardPlayRules.CommitSpeedCards(player, new[] { heat }, 1, session, 60));
        Assert.IsTrue(player.deck.ContainsInHand(heat));
    }

    [TestCase(0, SpeedCardCommitResult.CardNotInHand)]
    [TestCase(1, SpeedCardCommitResult.CardNotInHand)]
    [TestCase(2, SpeedCardCommitResult.InvalidCard)]
    [TestCase(3, SpeedCardCommitResult.InvalidCard)]
    [TestCase(4, SpeedCardCommitResult.SpeedLimitReached)]
    public void InvalidMixedSelectionIsAtomicAndDoesNotConsumeAttack(int variant, SpeedCardCommitResult expected)
    {
        var player = CreatePlayer();
        player.trickState.hotpotBaseActive = true;
        player.deck.DrawHeatFromPoolToHand(1);
        CardData heat = player.deck.Hand[0];
        var speed = new CardData(CardType.Speed, 3);
        player.deck.AddCardsToHand(new[] { speed });
        var cards = new List<CardData> { heat };
        cards.Add(variant == 0 ? new CardData(CardType.Speed, 3) :
            variant == 1 ? heat : variant == 2 ? CardData.CreateTrick("us-fries") :
            variant == 3 ? null : speed);
        Assert.AreEqual(expected, CardPlayRules.CommitSpeedCards(player, cards,
            variant == 4 ? 1 : 2, new RaceSession(), 60));
        CollectionAssert.AreEqual(new[] { heat, speed }, player.deck.Hand);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.IsTrue(TrickCardRules.HasHotpotAttack(player.trickState));
        Assert.IsFalse(player.hotpotAttackAppliedThisTurn);
        Assert.AreEqual(5, player.deck.heatPool.remaining);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AttackUsesEffectiveHeatValueWithoutChangingItsOriginalType(bool heatFirst)
    {
        var player = CreatePlayer();
        player.trickState.hotpotBaseActive = true;
        var speed = new CardData(CardType.Speed, 3);
        var heat = CardData.CreateTempHeat();
        player.deck.AddCardsToHand(new[] { speed, heat });
        Assert.AreEqual(SpeedCardCommitResult.Success, CardPlayRules.CommitSpeedCards(player,
            heatFirst ? new[] { heat, speed } : new[] { speed, heat }, 2, new RaceSession(), 60));
        Assert.AreEqual(heatFirst ? 2 : 3, player.hotpotAttackCardValueThisTurn);
        int total = CardPlayRules.SumCommittedSpeedCardValues(player.playedSpeedCardsThisTurn);
        int corner = total - CardPlayRules.GetHotpotCornerExclusion(player);
        Assert.AreEqual(6, corner + CardPlayRules.GetHotpotMovementContribution(player));
        Assert.IsTrue(heat.IsHeat);
        Assert.AreEqual(0, heat.value);
    }

    [Test]
    public void RetiredBBQPaymentCanBeCooledByExactGrillReceiptOnlyOnce()
    {
        var player = CreatePlayer();
        player.deck.DrawHeatFromPool(1, HeatPaymentDestination.Hand, player.heatPaidCardsThisTurn);
        CardData heat = player.deck.Hand[0];
        Assert.AreEqual(SpeedCardCommitResult.Success,
            CardPlayRules.CommitSpeedCards(player, new[] { heat }, 1, new RaceSession(), 60));
        Assert.AreEqual(0, player.deck.CoolRecordedHeatThroughDiscard(player.heatPaidCardsThisTurn, 1));
        RaceTurnCleanup.Execute(player, null, null, p =>
        {
            Assert.AreEqual(1, p.deck.CountRecordedHeat(p.heatPaidCardsThisTurn));
            Assert.AreEqual(1, p.deck.CoolRecordedHeatThroughDiscard(p.heatPaidCardsThisTurn, 1));
            Assert.AreEqual(0, p.deck.CoolRecordedHeatThroughDiscard(p.heatPaidCardsThisTurn, 1));
        });
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.IsEmpty(player.deck.DiscardPile);
    }
}

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
    public void test_hotpot_empowers_next_normal_speed_card_without_extra_slot()
    {
        var first = new CardData(CardType.Speed, 3);
        var second = new CardData(CardType.Speed, 1);
        var player = CreatePlayerWithHand(first, second);
        player.trickState.hotpotBaseActive = true;

        SpeedCardCommitResult result = CardPlayRules.CommitSpeedCards(
            player, new List<CardData> { first, second }, 2);

        Assert.AreEqual(SpeedCardCommitResult.Success, result);
        Assert.IsTrue(player.hotpotAttackAppliedThisTurn);
        Assert.AreEqual(3, player.hotpotAttackCardValueThisTurn);
        Assert.IsFalse(TrickCardRules.HasHotpotAttack(player.trickState));
        Assert.AreEqual(3, CardPlayRules.GetHotpotCornerExclusion(player));
        Assert.AreEqual(4, CardPlayRules.GetHotpotMovementContribution(player));
    }

    [Test]
    public void test_hotpot_excludes_full_effective_card_and_preserves_total_plus_one()
    {
        var player = CreatePlayerWithHand();
        player.hotpotAttackAppliedThisTurn = true;
        player.hotpotAttackCardValueThisTurn = 4;

        Assert.AreEqual(5, CardPlayRules.GetHotpotCornerExclusion(player, 1));
        Assert.AreEqual(6, CardPlayRules.GetHotpotMovementContribution(player, 1));
    }

    [Test]
    public void test_pending_hotpot_does_not_add_optional_speed_card_slot()
    {
        var player = CreatePlayerWithHand();
        player.extraCardSlotsThisTurn = 2;
        player.trickState.hotpotBaseActive = true;

        Assert.AreEqual(2, CardPlayRules.GetOptionalSpeedCardSlots(player));
    }

    [Test]
    public void test_failed_speed_commit_does_not_consume_pending_hotpot_attack()
    {
        var held = new CardData(CardType.Speed, 2);
        var stale = new CardData(CardType.Speed, 4);
        var player = CreatePlayerWithHand(held);
        player.trickState.hotpotBaseActive = true;

        SpeedCardCommitResult result = CardPlayRules.CommitSpeedCard(player, stale, 1);

        Assert.AreEqual(SpeedCardCommitResult.CardNotInHand, result);
        Assert.IsTrue(TrickCardRules.HasHotpotAttack(player.trickState));
        Assert.IsFalse(player.hotpotAttackAppliedThisTurn);
    }
}
