using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class AIPlannerTests
{
    private GameConfigSO config;

    [SetUp]
    public void set_up()
    {
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.speedCardDistribution = new[] { 1, 2, 3, 4 };
        config.initialHeatCards = 0;
    }

    [TearDown]
    public void tear_down()
    {
        Object.DestroyImmediate(config);
    }

    [Test]
    public void test_low_heat_selects_highest_speed_cards()
    {
        CardDeck deck = create_full_hand_deck(11);

        List<CardData> chosen = AIPlanner.ChooseSpeedCards(
            deck, 2, 0f, false, 0.7f, 0.5f, 0f, new StubRandomSource());

        assert_values(chosen, 4, 3);
    }

    [Test]
    public void test_high_heat_selects_lowest_speed_cards()
    {
        CardDeck deck = create_full_hand_deck(12);

        List<CardData> chosen = AIPlanner.ChooseSpeedCards(
            deck, 2, 0.7f, false, 0.7f, 0.5f, 0f, new StubRandomSource());

        assert_values(chosen, 1, 2);
    }

    [Test]
    public void test_corner_risk_at_cautious_threshold_selects_lowest_cards()
    {
        CardDeck deck = create_full_hand_deck(13);

        List<CardData> chosen = AIPlanner.ChooseSpeedCards(
            deck, 2, 0.5f, true, 0.7f, 0.5f, 0f, new StubRandomSource());

        assert_values(chosen, 1, 2);
    }

    [Test]
    public void test_corner_risk_with_low_heat_still_selects_lowest_cards()
    {
        CardDeck deck = create_full_hand_deck(15);

        List<CardData> chosen = AIPlanner.ChooseSpeedCards(
            deck, 2, 0f, true, 0.7f, 0.5f, 0f, new StubRandomSource());

        assert_values(chosen, 1, 2);
    }

    [Test]
    public void test_variation_uses_injected_random_source()
    {
        CardDeck deck = create_full_hand_deck(14);
        var randomSource = new StubRandomSource(0d, 0);

        List<CardData> chosen = AIPlanner.ChooseSpeedCards(
            deck, 2, 0f, false, 0.7f, 0.5f, 1f, randomSource);

        assert_values(chosen, 3, 4);
    }

    private CardDeck create_full_hand_deck(int seed)
    {
        var deck = new CardDeck();
        deck.InitializeDeck(config, new HeatPool(0), new SystemRandomSource(seed));
        deck.DrawToHand(config.speedCardDistribution.Length);
        return deck;
    }

    private static void assert_values(IReadOnlyList<CardData> cards, params int[] expected)
    {
        Assert.That(cards.Count, Is.EqualTo(expected.Length));
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.That(cards[i].value, Is.EqualTo(expected[i]));
        }
    }

    private sealed class StubRandomSource : IRandomSource
    {
        private readonly double nextDouble;
        private readonly Queue<int> nextInts;

        public StubRandomSource(double nextDouble = 1d, params int[] nextInts)
        {
            this.nextDouble = nextDouble;
            this.nextInts = new Queue<int>(nextInts);
        }

        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            return nextInts.Count > 0 ? nextInts.Dequeue() : minimumInclusive;
        }

        public double NextDouble()
        {
            return nextDouble;
        }
    }
}

public class RandomSourceTests
{
    [Test]
    public void test_equal_seeds_produce_equal_sequences()
    {
        var first = new SystemRandomSource(12345);
        var second = new SystemRandomSource(12345);

        for (int i = 0; i < 5; i++)
        {
            Assert.That(first.NextInt(0, 100), Is.EqualTo(second.NextInt(0, 100)));
            Assert.That(first.NextDouble(), Is.EqualTo(second.NextDouble()));
        }
    }

    [Test]
    public void test_card_decks_with_equal_seeds_draw_same_order()
    {
        GameConfigSO config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.speedCardDistribution = new[] { 1, 2, 3, 4 };
        config.initialHeatCards = 0;

        try
        {
            var first = new CardDeck();
            var second = new CardDeck();
            first.InitializeDeck(config, new HeatPool(0), new SystemRandomSource(77));
            second.InitializeDeck(config, new HeatPool(0), new SystemRandomSource(77));
            first.DrawToHand(config.speedCardDistribution.Length);
            second.DrawToHand(config.speedCardDistribution.Length);

            Assert.That(first.Hand.Count, Is.EqualTo(second.Hand.Count));
            for (int i = 0; i < first.Hand.Count; i++)
            {
                Assert.That(first.Hand[i].value, Is.EqualTo(second.Hand[i].value));
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }
}

public class AIControllerTests
{
    private GameObject gameObject;
    private GameConfigSO config;

    [SetUp]
    public void set_up()
    {
        gameObject = new GameObject("AIControllerTests");
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.speedCardDistribution = new[] { 4 };
        config.initialHeatCards = 0;
        config.aiCardVariationChance = 0f;
    }

    [TearDown]
    public void tear_down()
    {
        Object.DestroyImmediate(gameObject);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void test_spin_does_not_remove_selected_speed_cards_from_hand()
    {
        MVPGameManager game = gameObject.AddComponent<MVPGameManager>();
        game.config = config;

        PlayerState ai = new PlayerState("AI", true, 0, 2);
        ai.deck.InitializeDeck(config, new HeatPool(0), new SystemRandomSource(1));
        ai.deck.DrawToHand(1);

        AIController controller = gameObject.AddComponent<AIController>();
        controller.Initialize(game, ai, new StubRandomSource());
        controller.SelectCards();

        Assert.That(ai.spinCounter, Is.EqualTo(1));
        Assert.That(ai.gear, Is.EqualTo(config.minGear));
        Assert.That(ai.skipNextTurn, Is.True);
        Assert.That(ai.deck.HandCount, Is.EqualTo(1));
        Assert.That(ai.deck.CountSpeedInHand(), Is.EqualTo(1));
        Assert.That(ai.playedSpeedCardsThisTurn, Is.Empty);
    }

    [Test]
    public void test_engine_failure_payment_uses_schwarzbrot_for_ai()
    {
        MVPGameManager game = gameObject.AddComponent<MVPGameManager>();
        game.config = config;

        PlayerState ai = new PlayerState("AI", true, 0, 3);
        ai.deck.InitializeDeck(config, new HeatPool(1), new SystemRandomSource(1));
        ai.deck.DrawToHand(1);
        ai.trickState.schwarzbrotActive = true;
        ai.trickState.schwarzbrotRemaining = 1;

        AIController controller = gameObject.AddComponent<AIController>();
        controller.Initialize(game, ai, new StubRandomSource());
        controller.SelectCards();

        Assert.That(ai.spinCounter, Is.Zero);
        Assert.That(ai.skipNextTurn, Is.False);
        Assert.That(ai.deck.heatPool.remaining, Is.Zero);
        Assert.That(ai.playedSpeedCardsThisTurn.Count, Is.EqualTo(1));
        Assert.That(ai.deck.CountSpeedInHand(), Is.Zero);
    }

    private sealed class StubRandomSource : IRandomSource
    {
        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            return minimumInclusive;
        }

        public double NextDouble()
        {
            return 1d;
        }
    }
}
