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
        config.initialHeatCards = 2;
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

        Assert.AreEqual(9, deck.DrawPileCount);            // 7 速度 + 2 初始热量
        Assert.AreEqual(0, deck.HandCount);                // 未抽牌
        Assert.AreEqual(0, deck.DiscardPileCount);
        Assert.IsNotNull(deck.heatPool);
        Assert.AreEqual(5, deck.heatPool.remaining);
    }

    [Test]
    public void test_initial_deck_size_includes_enabled_trick_cards()
    {
        var config = CreateConfig();
        config.enableTrickCards = true;
        Assert.AreEqual(13, config.InitialDeckSize); // 7 speed + 2 heat + 4 trick

        config.enableTrickCards = false;
        Assert.AreEqual(9, config.InitialDeckSize);
    }

    [Test]
    public void test_draw_to_hand_fills_to_hand_size()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);

        bool ok = deck.DrawToHand(config.handSize);

        Assert.IsTrue(ok);
        Assert.AreEqual(4, deck.HandCount);
        Assert.AreEqual(5, deck.DrawPileCount); // 9 - 4（含 2 张初始热量）
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

        // 抽到牌组抽干（剩余 6 张）
        bool ok1 = deck.DrawToHand(6);
        Assert.IsTrue(ok1);
        Assert.AreEqual(0, deck.DrawPileCount);

        // 再抽 → 自动洗入弃牌堆，全部 9 张回到手牌
        bool ok2 = deck.DrawToHand(9);
        Assert.IsTrue(ok2);
        Assert.AreEqual(9, deck.HandCount);
        Assert.AreEqual(0, deck.DrawPileCount);
        Assert.AreEqual(0, deck.DiscardPileCount);

        // 超出总量 → false
        Assert.IsFalse(deck.DrawToHand(10));
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
    public void test_remove_heat_from_hand_returns_to_pool()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        deck.DrawToHand(config.handSize);

        // 手牌热量数可能为 0（取决于种子）— 先确保有热量：从池抽 1 张热量直接进弃牌堆再洗入
        if (deck.CountHeatInHand() == 0)
        {
            deck.DrawHeatFromPool(1);
            // 热量在弃牌堆 — 洗回牌组
            deck.ShuffleDrawPile(); // 弃牌堆不会自动洗入，手动构造：抽出牌组已有牌
        }

        // 更直接的方式：手牌全是热量时移除
        while (deck.HandCount > 0)
            deck.RemoveFromHand(new List<CardData>(deck.Hand));

        // 现在手牌空 → 洗入弃牌堆（含刚抽的热量）→ 抽到手牌
        int poolBefore = deck.heatPool.remaining;
        bool ok = deck.DrawToHand(config.handSize);
        Assert.IsTrue(ok);

        int heatInHand = deck.CountHeatInHand();
        int removed = deck.RemoveHeatFromHand(10);

        Assert.AreEqual(heatInHand, removed);
        Assert.AreEqual(poolBefore + removed, deck.heatPool.remaining);
    }

    [Test]
    public void test_return_heat_cards_to_pool_removes_exact_hand_cards_without_duplication()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        var heat = new CardData(CardType.Heat, 0);
        var speed = new CardData(CardType.Speed, 3);
        var unheldHeat = new CardData(CardType.Heat, 0);
        deck.AddCardsToHand(new List<CardData> { heat, speed });
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
    public void test_cool_heat_prioritizes_hand_then_draw_pile_then_discard_pile()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        deck.AddCardsToHand(new List<CardData> { new CardData(CardType.Heat, 0) });
        deck.DrawHeatFromPool(1); // creates a heat card in the discard pile

        int poolBefore = deck.heatPool.remaining;
        int cooled = deck.CoolHeat(3);

        Assert.That(cooled, Is.EqualTo(3));
        Assert.That(deck.CountHeatInHand(), Is.EqualTo(0));
        Assert.That(deck.CountHeatInDrawPile(), Is.EqualTo(0));
        Assert.That(deck.CountHeatInDiscardPile(), Is.EqualTo(1));
        Assert.That(deck.heatPool.remaining, Is.EqualTo(poolBefore + 3));

        int discardCooled = deck.CoolHeat(1);
        Assert.That(discardCooled, Is.EqualTo(1));
        Assert.That(deck.CountHeatInDiscardPile(), Is.EqualTo(0));
        Assert.That(deck.heatPool.remaining, Is.EqualTo(poolBefore + 4));
    }

    [Test]
    public void test_cool_heat_destroys_temporary_heat_after_permanent_zones_are_empty()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        deck.AddCardsToHand(new List<CardData> { CardData.CreateTempHeat() });

        int poolBefore = deck.heatPool.remaining;
        int cooled = deck.CoolHeat(1);

        Assert.That(cooled, Is.EqualTo(1));
        Assert.That(deck.heatPool.remaining, Is.EqualTo(poolBefore));
        Assert.That(deck.CountHeatInHand(), Is.EqualTo(0));
    }

    [Test]
    public void test_temporary_heat_is_destroyed_during_full_recovery()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        deck.AddCardsToHand(new List<CardData> { CardData.CreateTempHeat() });
        int poolBefore = deck.heatPool.remaining;

        deck.RecoverAllHeatToPool();

        Assert.AreEqual(poolBefore + config.initialHeatCards, deck.heatPool.remaining);
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
        // 弃牌堆现有 3 张热量；手牌也可能有初始热量

        int poolBefore = deck.heatPool.remaining;
        deck.RecoverAllHeatToPool();

        // 系统内热量总量 = 池 + 初始热量牌 = 5 + 2
        Assert.AreEqual(5 + config.initialHeatCards, deck.heatPool.remaining);
        Assert.AreEqual(0, deck.CountHeatInHand());
        Assert.AreEqual(0, deck.CountHeatInDeck());
        Assert.IsTrue(poolBefore <= 5);
    }

    [Test]
    public void test_remove_one_heat_from_deck_prefers_draw_pile()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);

        deck.DrawHeatFromPool(2); // 2 张热量入弃牌堆（初始 2 张热量仍在牌组）
        int before = deck.heatPool.remaining;

        bool ok = deck.RemoveOneHeatFromDeck();

        Assert.IsTrue(ok);
        Assert.AreEqual(before + 1, deck.heatPool.remaining);
        // 优先从牌组移除：牌组 2 → 1，弃牌堆 2 不变
        Assert.AreEqual(3, deck.CountHeatInDeck());
    }

    [Test]
    public void test_remove_one_heat_from_deck_returns_false_when_none()
    {
        var config = CreateConfig();
        var deck = CreateDeck(config);
        deck.DrawToHand(config.handSize);

        // 手动把热量全部清走：抽干热量池以外路径不可行 — 构造无热量牌组
        var noHeatConfig = CreateConfig();
        noHeatConfig.initialHeatCards = 0;
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
        Assert.AreEqual(11, deck.DrawPileCount);
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
        Assert.AreEqual(config.initialHeatCards,
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
        var heat = new CardData(CardType.Heat, 0);
        deck.AddCardsToHand(new List<CardData> { speed, trick, heat });

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
        deck.AddCardsToHand(new List<CardData> { new CardData(CardType.Heat, 0) });

        int removed = deck.RemoveTempCardsFromHand();

        Assert.AreEqual(1, removed);
        Assert.AreEqual(1, deck.CountHeatInHand());
    }
}
