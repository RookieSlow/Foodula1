using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// CardDeck 单元测试 — 抽牌/弃牌/热量管理/牌库耗尽边界 + 特技牌扩展。
/// 使用种子随机源保证确定性。
/// </summary>
public class CardDeckTest
{
    private GameConfigSO CreateConfig()
    {
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.speedCardDistribution = new[] { 1, 1, 2, 2, 3, 3, 4 };
        config.initialHeatCards = 2; // legacy field: must not enter the normal deck
        config.heatPoolPerPlayer = 5;
        config.handSize = 4;
        return config;
    }

    private CardDeck CreateDeck(GameConfigSO config, int seed = 42, int poolSize = 5)
    {
        var deck = new CardDeck();
        deck.InitializeDeck(config, new HeatPool(poolSize), new SystemRandomSource(seed));
        return deck;
    }

    // ===== 初始化 =====

    [Test]
    public void test_deck_init_builds_expected_piles()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);

        Assert.AreEqual(7, deck.DrawPileCount);            // 仅 7 张速度牌
        Assert.AreEqual(0, deck.HandCount);                // 未抽牌
        Assert.AreEqual(0, deck.DiscardPileCount);
        Assert.AreEqual(0, deck.CountHeatInDeck());        // 热量只存在于独立引擎池
        Assert.IsNotNull(deck.heatPool);
        Assert.AreEqual(5, deck.heatPool.remaining);
    }

    [Test]
    public void test_initial_deck_size_includes_enabled_trick_cards()
    {
        var config = CreateConfig();
        config.enableTrickCards = true;
        Assert.AreEqual(11, config.InitialDeckSize); // 7 speed + 4 trick

        config.enableTrickCards = false;
        Assert.AreEqual(7, config.InitialDeckSize);
    }

    [Test]
    public void test_draw_to_hand_fills_to_hand_size()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);

        bool ok = deck.DrawToHand(config.handSize);

        Assert.IsTrue(ok);
        Assert.AreEqual(4, deck.HandCount);
        Assert.AreEqual(3, deck.DrawPileCount); // 7 - 4
    }

    // ===== 抽牌与牌库耗尽 =====

    [Test]
    public void test_draw_exhausts_deck_then_shuffles_discard_back()
    {
        var config = CreateConfig();
        config.handSize = 3;
        var deck = CreateDeck(config);
        deck.DrawToHand(3); // 抽 3

        // 打出并弃掉手牌 → 弃牌堆有牌
        var cards = new List<CardData>(deck.Hand);
        deck.RemoveFromHand(cards);
        deck.DiscardSpeedCards(cards);
        Assert.AreEqual(3, deck.DiscardPileCount);

        // 抽到普通牌组抽干（剩余 4 张速度牌）
        bool ok1 = deck.DrawToHand(4);
        Assert.IsTrue(ok1);
        Assert.AreEqual(4, deck.HandCount);
        Assert.AreEqual(0, deck.DrawPileCount);

        // 再抽 → 只将非热量弃牌洗回普通牌组，补足 3 张
        bool ok2 = deck.DrawToHand(7);
        Assert.IsTrue(ok2);
        Assert.AreEqual(7, deck.HandCount);
        Assert.AreEqual(0, deck.DrawPileCount);
        Assert.AreEqual(0, deck.DiscardPileCount);

        // 超出总量 → false
        Assert.IsFalse(deck.DrawToHand(8));
    }

    [Test]
    public void test_draw_returns_false_when_everything_exhausted()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);

        // 抽完所有牌
        while (deck.DrawPileCount > 0 || deck.DiscardPileCount > 0)
        {
            if (!deck.DrawToHand(deck.HandCount + 1)) break;
        }

        // 再要求更大手牌 → false
        bool ok = deck.DrawToHand(deck.HandCount + 1);
        Assert.IsFalse(ok);
    }

    // ===== 热量管理 =====

    [Test]
    public void test_draw_heat_from_pool_adds_to_discard_and_reduces_pool()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);

        int drawn = deck.DrawHeatFromPool(3);

        Assert.AreEqual(3, drawn);
        Assert.AreEqual(2, deck.heatPool.remaining);
        Assert.AreEqual(3, deck.DiscardPileCount);
    }

    [Test]
    public void test_draw_heat_from_pool_caps_at_pool_remaining()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config, poolSize: 2);

        int drawn = deck.DrawHeatFromPool(5);

        Assert.AreEqual(2, drawn);
        Assert.AreEqual(0, deck.heatPool.remaining);
    }

    [Test]
    public void test_heat_payment_destination_can_put_heat_in_hand_without_normal_draw()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config, poolSize: 3);

        Assert.AreEqual(2, deck.DrawHeatFromPool(2, HeatPaymentDestination.Hand));
        Assert.AreEqual(2, deck.CountHeatInHand());
        Assert.AreEqual(0, deck.CountHeatInDeck());
        Assert.AreEqual(1, deck.heatPool.remaining);
    }

    [Test]
    public void test_remove_heat_from_hand_returns_to_pool()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        Assert.AreEqual(1, deck.DrawHeatFromPoolToHand(1));
        int poolBefore = deck.heatPool.remaining;
        int removed = deck.RemoveHeatFromHand(1);

        Assert.AreEqual(1, removed);
        Assert.AreEqual(poolBefore + 1, deck.heatPool.remaining);
        Assert.AreEqual(0, deck.CountHeatInHand());
    }

    [Test]
    public void test_return_heat_cards_to_pool_removes_exact_hand_cards_without_duplication()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        var speed = new CardData(CardType.Speed, 3);
        var unheldHeat = new CardData(CardType.Heat, 0);
        deck.AddCardsToHand(new List<CardData> { speed });
        Assert.AreEqual(1, deck.DrawHeatFromPoolToHand(1));
        CardData heat = null;
        foreach (CardData card in deck.Hand)
            if (card.IsHeat) { heat = card; break; }
        int poolBefore = deck.heatPool.remaining;

        int returned = deck.ReturnHeatCardsToPool(
            new List<CardData> { heat, heat, unheldHeat, speed });

        Assert.AreEqual(1, returned);
        Assert.AreEqual(poolBefore + 1, deck.heatPool.remaining);
        Assert.IsFalse(deck.ContainsInHand(heat));
        Assert.IsTrue(deck.ContainsInHand(speed));
        Assert.AreEqual(1, deck.HandCount);
    }

    [Test]
    public void test_temporary_heat_is_destroyed_by_cooling_without_inflating_engine_pool()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        var temporaryHeat = CardData.CreateTempHeat();
        deck.AddCardsToHand(new List<CardData> { temporaryHeat });
        int poolBefore = deck.heatPool.remaining;

        int removed = deck.RemoveHeatFromHand(1);

        Assert.AreEqual(1, removed);
        Assert.AreEqual(poolBefore, deck.heatPool.remaining);
        Assert.IsFalse(deck.ContainsInHand(temporaryHeat));
    }

    [Test]
    public void test_cool_heat_uses_hand_then_discard_when_normal_draw_pile_has_no_heat()
    {
        var config = CreateConfig();
        config.speedCardDistribution = new int[0];
        var deck = CreateDeck(config, poolSize: 4);

        // Heat paid from the engine enters hand or discard explicitly; it
        // never becomes a normal draw-pile card.
        Assert.AreEqual(1, deck.DrawHeatFromPoolToHand(1));
        Assert.AreEqual(1, deck.CountHeatInHand());
        Assert.AreEqual(2, deck.DrawHeatFromPool(2));
        int poolBefore = deck.heatPool.remaining;

        int cooled = deck.CoolHeat(2);

        Assert.AreEqual(2, cooled);
        Assert.AreEqual(0, deck.CountHeatInHand(), "hand must be cooled first");
        Assert.AreEqual(1, deck.DiscardPileCount, "one discard-pile heat should remain");
        Assert.AreEqual(poolBefore + 2, deck.heatPool.remaining);

        cooled = deck.CoolHeat(2);

        Assert.AreEqual(1, cooled);
        Assert.AreEqual(0, deck.DiscardPileCount);
        Assert.AreEqual(poolBefore + 3, deck.heatPool.remaining);
    }

    [Test]
    public void test_temporary_heat_is_destroyed_during_full_recovery()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        deck.AddCardsToHand(new List<CardData> { CardData.CreateTempHeat() });
        int poolBefore = deck.heatPool.remaining;

        deck.RecoverAllHeatToPool();

        Assert.AreEqual(poolBefore, deck.heatPool.remaining);
        Assert.AreEqual(0, deck.CountHeatInHand());
        Assert.AreEqual(0, deck.CountHeatInDeck());
    }

    [Test]
    public void test_recover_all_heat_to_pool_returns_everything()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        deck.DrawToHand(config.handSize);

        // 从池抽热量制造散落热量
        deck.DrawHeatFromPool(3);
        // 弃牌堆现有 3 张热量；普通手牌中没有热量

        int poolBefore = deck.heatPool.remaining;
        deck.RecoverAllHeatToPool();

        // 系统内永久热量总量始终等于独立引擎池容量
        Assert.AreEqual(5, deck.heatPool.remaining);
        Assert.AreEqual(0, deck.CountHeatInHand());
        Assert.AreEqual(0, deck.CountHeatInDeck());
        Assert.IsTrue(poolBefore <= 5);
    }

    [Test]
    public void test_remove_one_heat_from_deck_removes_discard_heat()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);

        deck.DrawHeatFromPool(2); // 2 张热量入弃牌堆；普通牌组不含热量
        int before = deck.heatPool.remaining;

        bool ok = deck.RemoveOneHeatFromDeck();

        Assert.IsTrue(ok);
        Assert.AreEqual(before + 1, deck.heatPool.remaining);
        // 没有普通牌组热量时，从弃牌堆移除
        Assert.AreEqual(1, deck.CountHeatInDeck());
    }

    [Test]
    public void test_remove_one_heat_from_deck_returns_false_when_none()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        deck.DrawToHand(config.handSize);

        // 构造无热量普通牌组
        var noHeatConfig = CreateConfig();
        var cleanDeck = new CardDeck();
        cleanDeck.InitializeDeck(noHeatConfig, new HeatPool(0), new SystemRandomSource(1));

        bool ok = cleanDeck.RemoveOneHeatFromDeck();
        Assert.IsFalse(ok);
    }

    // ===== 速度牌辅助 =====

    [Test]
    public void test_get_top_n_speed_cards_returns_largest()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        deck.DrawToHand(config.handSize);

        var top2 = deck.GetTopNSpeedCards(2);
        Assert.AreEqual(2, top2.Count);
        Assert.IsTrue(top2[0].value >= top2[1].value);
    }

    [Test]
    public void test_get_bottom_n_speed_cards_returns_smallest()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        deck.DrawToHand(config.handSize);

        var bottom2 = deck.GetBottomNSpeedCards(2);
        Assert.AreEqual(2, bottom2.Count);
        Assert.IsTrue(bottom2[0].value <= bottom2[1].value);
    }

    // ===== 特技牌扩展 =====

    [Test]
    public void test_add_trick_cards_to_draw_pile_ignores_non_tricks_and_does_not_inject_hand()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);

        int added = deck.AddTrickCardsToDrawPile(new List<CardData>
        {
            CardData.CreateTrick("uk-scone"),
            new CardData(CardType.Speed, 2),
            CardData.CreateTrick("uk-english-breakfast-tea")
        });

        Assert.AreEqual(2, added);
        Assert.AreEqual(9, deck.DrawPileCount);
        Assert.AreEqual(2, deck.CountTricksInDeck());
        Assert.AreEqual(0, deck.HandCount);
        Assert.AreEqual(0, deck.GetTricksInHand().Count);
    }

    [Test]
    public void test_mixed_deck_opening_draw_preserves_all_card_types()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        var tricks = new List<CardData>
        {
            CardData.CreateTrick("uk-scone"),
            CardData.CreateTrick("uk-english-breakfast-tea"),
            CardData.CreateTrick("uk-scone"),
            CardData.CreateTrick("uk-english-breakfast-tea")
        };
        Assert.AreEqual(4, deck.AddTrickCardsToDrawPile(tricks));

        Assert.IsTrue(deck.DrawToHand(config.handSize));

        Assert.AreEqual(config.handSize, deck.HandCount);
        Assert.AreEqual(config.speedCardDistribution.Length,
            deck.CountSpeedInDeck() + deck.CountSpeedInHand());
        Assert.AreEqual(0,
            deck.CountHeatInDeck() + deck.CountHeatInHand());
        Assert.AreEqual(4,
            deck.CountTricksInDeck() + deck.GetTricksInHand().Count);
    }

    [Test]
    public void test_ensure_trick_card_in_hand_preserves_hand_size_and_card_count()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        var attack = CardData.CreateTrick("cn-hotpot-base");
        var defense = CardData.CreateTrick("cn-ice-jelly");
        Assert.AreEqual(2, deck.AddTrickCardsToDrawPile(new List<CardData> { attack, defense }));
        Assert.IsTrue(deck.DrawToHand(config.handSize));

        int handSizeBefore = deck.HandCount;
        int totalBefore = deck.DrawPileCount + deck.HandCount + deck.DiscardPileCount;

        Assert.IsTrue(deck.EnsureTrickCardInHand("cn-hotpot-base"));
        Assert.AreEqual(handSizeBefore, deck.HandCount);
        Assert.IsTrue(deck.GetTricksInHand().Exists(card => card.trickId == "cn-hotpot-base"));
        Assert.AreEqual(totalBefore, deck.DrawPileCount + deck.HandCount + deck.DiscardPileCount);
    }

    [Test]
    public void test_discard_trick_card_removes_from_hand_to_discard()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        var trick = CardData.CreateTrick("cn-hotpot-base");
        deck.AddCardsToHand(new List<CardData> { trick });

        bool ok = deck.DiscardTrickCard(trick);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, deck.GetTricksInHand().Count);
        Assert.AreEqual(1, deck.DiscardPileCount);
    }

    [Test]
    public void test_discard_trick_card_fails_for_unknown_card()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);

        bool ok = deck.DiscardTrickCard(new CardData(CardType.Speed, 1));

        Assert.IsFalse(ok);
    }

    [Test]
    public void test_discard_trick_card_fails_for_different_trick_not_in_hand_without_mutation()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        var held = CardData.CreateTrick("uk-scone");
        deck.AddCardsToHand(new List<CardData> { held });

        bool ok = deck.DiscardTrickCard(CardData.CreateTrick("uk-scone"));

        Assert.IsFalse(ok);
        Assert.AreEqual(1, deck.HandCount);
        Assert.AreEqual(0, deck.DiscardPileCount);
        Assert.IsTrue(deck.ContainsInHand(held));
    }

    [Test]
    public void test_discard_trick_card_removes_only_confirmed_duplicate_instance()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        var first = CardData.CreateTrick("jp-kanto-oden");
        var second = CardData.CreateTrick("jp-kanto-oden");
        deck.AddCardsToHand(new List<CardData> { first, second });

        Assert.IsTrue(deck.DiscardTrickCard(second));

        Assert.IsTrue(deck.ContainsInHand(first));
        Assert.IsFalse(deck.ContainsInHand(second));
        Assert.AreEqual(1, deck.HandCount);
        Assert.AreEqual(1, deck.DiscardPileCount);
    }

    [Test]
    public void test_played_trick_reshuffles_and_can_be_drawn_again()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        var trick = CardData.CreateTrick("de-sauerkraut");
        deck.AddTrickCardsToDrawPile(new List<CardData> { trick });

        Assert.IsTrue(deck.DrawToHand(deck.DrawPileCount));
        Assert.IsTrue(deck.ContainsInHand(trick));
        Assert.IsTrue(deck.DiscardTrickCard(trick));
        Assert.AreEqual(1, deck.DiscardPileCount);

        Assert.IsTrue(deck.DrawToHand(deck.HandCount + 1));
        Assert.IsTrue(deck.ContainsInHand(trick));
        Assert.AreEqual(0, deck.DiscardPileCount);
    }

    [Test]
    public void test_optional_discard_moves_speed_and_trick_but_never_heat()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        var speed = new CardData(CardType.Speed, 2);
        var trick = CardData.CreateTrick("it-parmigiano");
        deck.AddCardsToHand(new List<CardData> { speed, trick });
        Assert.AreEqual(1, deck.DrawHeatFromPoolToHand(1));
        CardData heat = null;
        foreach (CardData card in deck.Hand)
            if (card.IsHeat) { heat = card; break; }

        int discarded = deck.DiscardPlayableCardsFromHand(new List<CardData> { speed, trick, heat });

        Assert.AreEqual(2, discarded);
        Assert.AreEqual(1, deck.HandCount);
        Assert.IsTrue(deck.ContainsInHand(heat));
        Assert.AreEqual(2, deck.DiscardPileCount);
    }

    [Test]
    public void test_remove_temp_cards_from_hand_removes_only_temp()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        var temp = CardData.CreateTempHeat();
        deck.AddCardsToHand(new List<CardData> { temp });
        Assert.AreEqual(1, deck.DrawHeatFromPoolToHand(1));

        int removed = deck.RemoveTempCardsFromHand();

        Assert.AreEqual(1, removed);
        Assert.AreEqual(1, deck.CountHeatInHand());
    }
}
