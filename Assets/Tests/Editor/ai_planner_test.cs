using System.Collections.Generic;
using System.Reflection;
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

    [Test]
    public void test_slipstream_target_accounts_for_leader_movement()
    {
        int targetMovement;

        bool found = AIPlanner.TryGetSlipstreamTargetMovement(
            0, 2, 1, 42, 2, out targetMovement);

        Assert.That(found, Is.True);
        Assert.That(targetMovement, Is.EqualTo(2));
    }

    [TestCase(0, 1, 4, 4)]
    [TestCase(41, 0, 3, 3)]
    public void test_slipstream_target_can_maintain_one_cell_gap(
        int followerPosition,
        int leaderPosition,
        int leaderMovement,
        int expectedTarget)
    {
        int targetMovement;

        bool found = AIPlanner.TryGetSlipstreamTargetMovement(
            followerPosition,
            leaderPosition,
            leaderMovement,
            42,
            2,
            out targetMovement);

        Assert.That(found, Is.True);
        Assert.That(targetMovement, Is.EqualTo(expectedTarget));
    }

    [Test]
    public void test_slipstream_target_rejects_distant_or_rear_opponent()
    {
        int targetMovement;

        Assert.That(
            AIPlanner.TryGetSlipstreamTargetMovement(0, 5, 0, 42, 2, out targetMovement),
            Is.False);
        Assert.That(
            AIPlanner.TryGetSlipstreamTargetMovement(10, 5, 0, 42, 2, out targetMovement),
            Is.False);
    }

    [Test]
    public void test_exact_speed_combination_matches_tailwind_target()
    {
        var deck = new CardDeck();
        deck.InitializeDeck(config, new HeatPool(0), new StubRandomSource());
        deck.AddCardsToHand(new List<CardData>
        {
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 3),
            new CardData(CardType.Speed, 4)
        });

        List<CardData> chosen;
        bool found = AIPlanner.TryFindExactSpeedCards(deck, 2, 5, out chosen);

        Assert.That(found, Is.True);
        Assert.That(chosen.Count, Is.EqualTo(2));
        Assert.That(RaceRules.SumCardValues(chosen), Is.EqualTo(5));
        Assert.That(deck.ContainsInHand(chosen[0]), Is.True);
        Assert.That(deck.ContainsInHand(chosen[1]), Is.True);
    }

    [Test]
    public void test_exact_speed_combination_falls_back_when_target_is_impossible()
    {
        var deck = new CardDeck();
        deck.InitializeDeck(config, new HeatPool(0), new StubRandomSource());
        deck.AddCardsToHand(new List<CardData>
        {
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 3)
        });

        List<CardData> chosen;
        bool found = AIPlanner.TryFindExactSpeedCards(deck, 2, 5, out chosen);

        Assert.That(found, Is.False);
        Assert.That(chosen, Is.Null);
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
    private GameObject trackObject;
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
        Object.DestroyImmediate(trackObject);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void test_controller_selects_exact_cards_for_nearby_leader()
    {
        config.speedCardDistribution = new[] { 4 };
        config.aiSlipstreamPlanningRange = 2;

        MVPGameManager game = gameObject.AddComponent<MVPGameManager>();
        game.config = config;

        trackObject = new GameObject("AIControllerTrack");
        trackObject.SetActive(false);
        TrackManager track = trackObject.AddComponent<TrackManager>();
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 42; i++)
            nodes.Add(new TrackNode(i, 99));
        typeof(TrackManager)
            .GetField("nodes", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(track, nodes);
        game.trackManager = track;

        var ai = new PlayerState("AI", true, 0, 2) { teamId = TeamId.UK };
        ai.deck.InitializeDeck(config, new HeatPool(0), new StubRandomSource());
        ai.deck.AddCardsToHand(new List<CardData>
        {
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 3)
        });

        var leader = new PlayerState("Leader", true, 2, 1) { teamId = TeamId.DE };
        leader.deck.InitializeDeck(config, new HeatPool(0), new StubRandomSource());
        leader.deck.AddCardsToHand(new List<CardData>
        {
            new CardData(CardType.Speed, 1)
        });

        var session = new RaceSession();
        session.Players.Add(ai);
        session.Players.Add(leader);
        typeof(MVPGameManager)
            .GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(game, session);

        AIController controller = gameObject.AddComponent<AIController>();
        controller.Initialize(game, ai, new StubRandomSource());
        controller.SelectCards();

        Assert.That(ai.playedSpeedCardsThisTurn.Count, Is.EqualTo(2));
        Assert.That(RaceRules.SumCardValues(ai.playedSpeedCardsThisTurn), Is.EqualTo(2));
        Assert.That(ai.deck.HandCount, Is.EqualTo(1));
        Assert.That(ai.deck.Hand[0].value, Is.EqualTo(3));
    }

    [Test]
    public void test_controller_ignores_leader_on_different_lap()
    {
        config.speedCardDistribution = new[] { 4 };
        config.aiSlipstreamPlanningRange = 2;

        MVPGameManager game = gameObject.AddComponent<MVPGameManager>();
        game.config = config;

        trackObject = new GameObject("AIControllerTrack");
        trackObject.SetActive(false);
        TrackManager track = trackObject.AddComponent<TrackManager>();
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 42; i++)
            nodes.Add(new TrackNode(i, 99));
        typeof(TrackManager)
            .GetField("nodes", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(track, nodes);
        game.trackManager = track;

        var ai = new PlayerState("AI", true, 0, 2) { teamId = TeamId.UK, lap = 0 };
        ai.deck.InitializeDeck(config, new HeatPool(0), new StubRandomSource());
        ai.deck.AddCardsToHand(new List<CardData>
        {
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 3)
        });

        var leader = new PlayerState("Leader", true, 2, 1) { teamId = TeamId.DE, lap = 1 };
        leader.deck.InitializeDeck(config, new HeatPool(0), new StubRandomSource());
        leader.deck.AddCardsToHand(new List<CardData>
        {
            new CardData(CardType.Speed, 1)
        });

        var session = new RaceSession();
        session.Players.Add(ai);
        session.Players.Add(leader);
        typeof(MVPGameManager)
            .GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(game, session);

        AIController controller = gameObject.AddComponent<AIController>();
        controller.Initialize(game, ai, new StubRandomSource());
        controller.SelectCards();

        Assert.That(ai.playedSpeedCardsThisTurn.Count, Is.EqualTo(2));
        Assert.That(RaceRules.SumCardValues(ai.playedSpeedCardsThisTurn), Is.EqualTo(4));
        Assert.That(ai.deck.HandCount, Is.EqualTo(1));
        Assert.That(ai.deck.Hand[0].value, Is.EqualTo(1));
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
    public void test_optional_extra_slot_does_not_create_missing_card_penalty()
    {
        MVPGameManager game = gameObject.AddComponent<MVPGameManager>();
        game.config = config;

        PlayerState ai = new PlayerState("AI", true, 0, 2)
        {
            extraCardSlotsThisTurn = 1
        };
        ai.deck.InitializeDeck(config, new HeatPool(0), new SystemRandomSource(1));
        ai.deck.AddCardsToHand(new List<CardData>
        {
            new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 3)
        });

        AIController controller = gameObject.AddComponent<AIController>();
        controller.Initialize(game, ai, new StubRandomSource());
        controller.SelectCards();

        Assert.That(game.GetMaxSpeedCardsThisTurn(ai), Is.EqualTo(3));
        Assert.That(game.GetRequiredSpeedCardsThisTurn(ai), Is.EqualTo(2));
        Assert.That(ai.playedSpeedCardsThisTurn.Count, Is.EqualTo(2));
        Assert.That(ai.spinCounter, Is.Zero);
        Assert.That(ai.skipNextTurn, Is.False);
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

    [Test]
    public void test_china_ai_recovers_for_corner_beyond_generic_lookahead()
    {
        config.speedCardDistribution = new int[0];
        config.aiHeatWarningThreshold = 0.7f;
        config.aiChinaAffordableCornerHeat = 1;
        config.aiLookAheadNodes = 6;

        MVPGameManager game = gameObject.AddComponent<MVPGameManager>();
        game.config = config;

        trackObject = new GameObject("ChinaAiLongMoveTrack");
        trackObject.SetActive(false);
        TrackManager track = trackObject.AddComponent<TrackManager>();
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 20; i++)
            nodes.Add(new TrackNode(i, 99));
        nodes[8] = new TrackNode(8, 5, "Late Apex", 1, false, true);
        typeof(TrackManager)
            .GetField("nodes", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(track, nodes);
        typeof(TrackManager)
            .GetField("cornerSpeedLimits", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(track, new Dictionary<int, int> { { 1, 5 } });
        game.trackManager = track;

        var ai = new PlayerState("CN AI", true, 0, ChinaGearShiftRules.GoGear)
        {
            teamId = TeamId.CN,
            usesChinaGearSystem = true,
            chinaConsecutiveGearCount = 1
        };
        ai.deck.InitializeDeck(config, new HeatPool(8), new StubRandomSource());
        ai.deck.AddCardsToHand(new List<CardData>
        {
            new CardData(CardType.Speed, 4),
            new CardData(CardType.Speed, 3),
            new CardData(CardType.Speed, 3),
            new CardData(CardType.Speed, 2)
        });

        var session = new RaceSession();
        session.Players.Add(ai);
        typeof(MVPGameManager)
            .GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(game, session);

        AIController controller = gameObject.AddComponent<AIController>();
        controller.Initialize(game, ai, new StubRandomSource());

        Assert.That(controller.DecideGear(), Is.EqualTo(ChinaGearShiftRules.RecoverGear));
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
