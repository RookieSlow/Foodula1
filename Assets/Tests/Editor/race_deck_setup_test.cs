using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Deck assembly only; no scene startup, persistence or GameLoop.</summary>
public class RaceDeckSetupTests
{
    private GameConfigSO config;
    private Random.State savedRandom;

    [SetUp]
    public void SetUp()
    {
        savedRandom = Random.state;
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.speedCardDistribution = new[] { 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4 };
        config.handSize = 7;
    }

    [TearDown]
    public void TearDown()
    {
        Random.state = savedRandom;
        Object.DestroyImmediate(config);
    }

    private static List<string> Describe(IReadOnlyList<CardData> cards) =>
        new List<CardData>(cards).ConvertAll(c => c.type + ":" + c.value + ":" + c.trickId);

    private static List<CardData> AllCards(PlayerState player)
    {
        var cards = new List<CardData>(player.deck.Hand);
        cards.AddRange(player.deck.DrawPile);
        cards.AddRange(player.deck.DiscardPile);
        return cards;
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void OrdinaryAssemblyMatchesPreviousSequenceAndRandomConsumption(TeamId team)
    {
        config.enableTrickCards = true;
        var session = new RaceSession(new SystemRandomSource(7));
        var profile = session.CreateDemoTechState(team);
        var actual = new PlayerState("actual", false, 0, 1) { teamId = team, techState = profile };
        var expected = new PlayerState("expected", false, 0, 1) { teamId = team, techState = profile };
        Random.InitState(123);
        var initialRandom = Random.state;
        // Explicit pre-extraction sequence is the equivalence oracle.
        expected.trickState = new TrickCardState();
        expected.trickState.ResetPerRace();
        expected.deck.InitializeDeck(config, new HeatPool(13));
        expected.deck.AddTrickCardsToDrawPile(session.CreateInitialTrickCards(team));
        expected.deck.DrawToHand(session.EffectiveHandSize(expected, config.handSize));
        float nextExpected = Random.value;
        Random.state = initialRandom;
        session.InitializeRaceDeck(actual, config, 13);
        CollectionAssert.AreEqual(Describe(expected.deck.Hand), Describe(actual.deck.Hand));
        CollectionAssert.AreEqual(Describe(expected.deck.DrawPile), Describe(actual.deck.DrawPile));
        Assert.AreEqual(nextExpected, Random.value);
        Assert.AreEqual(16, AllCards(actual).Count);
        Assert.AreEqual(13, actual.deck.heatPool.remaining);
        Assert.AreSame(profile, actual.techState);
        Assert.IsFalse(actual.deck.UsesExactOrder);
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void ReassemblyClearsOldZonesAndAllocatesIndependentHeatAndTrickState(TeamId team)
    {
        config.enableTrickCards = false;
        var session = new RaceSession();
        var player = new PlayerState("repeat", true, 11, 3) { teamId = team };
        session.InitializeRaceDeck(player, config, 9);
        var oldCards = AllCards(player);
        var oldPool = player.deck.heatPool;
        var oldTricks = player.trickState;
        player.trickState.schwarzbrotActive = true;
        player.deck.DrawHeatFromPool(2, HeatPaymentDestination.Hand);
        player.deck.DrawHeatFromPool(1, HeatPaymentDestination.Discard);
        session.InitializeRaceDeck(player, config, 5);
        Assert.AreNotSame(oldPool, player.deck.heatPool);
        Assert.AreNotSame(oldTricks, player.trickState);
        Assert.AreEqual(6, oldPool.remaining);
        Assert.AreEqual(5, player.deck.heatPool.remaining);
        Assert.IsFalse(player.trickState.schwarzbrotActive);
        Assert.AreEqual(0, player.deck.DiscardPileCount);
        Assert.AreEqual(0, player.deck.CountPermanentHeatOutsideEngine());
        Assert.AreEqual(12, AllCards(player).Count);
        foreach (var card in AllCards(player)) Assert.IsFalse(oldCards.Contains(card));
        Assert.AreEqual(11, player.position);
        Assert.AreEqual(3, player.gear);
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void TutorialUsesAuthoredPlayerAndOpponentOrdersAndDoesNotConsumeRandom(TeamId team)
    {
        var scenario = TutorialScenarioDefinition.CreateTeamSpecialty(team);
        var session = new RaceSession();
        config.speedCardDistribution = null;
        config.handSize = 99;
        config.enableTrickCards = true;
        foreach (bool ai in new[] { false, true })
        {
            var player = new PlayerState("lesson", ai, 0, 1) { teamId = team };
            var order = ai ? scenario.CreateOpponentDeck() : scenario.CreateExactDeck();
            var opening = Describe(order.GetRange(0, scenario.openingHandSize));
            var remainder = Describe(order.GetRange(scenario.openingHandSize, order.Count - scenario.openingHandSize));
            Random.InitState(ai ? 444 : 777);
            var initialRandom = Random.state;
            float expectedNext = Random.value;
            Random.state = initialRandom;
            session.InitializeRaceDeck(player, config, scenario.engineHeatCapacity, scenario);
            Assert.AreEqual(expectedNext, Random.value);
            Assert.IsTrue(player.deck.UsesExactOrder);
            CollectionAssert.AreEqual(opening, Describe(player.deck.Hand));
            CollectionAssert.AreEqual(remainder, Describe(player.deck.DrawPile));
            var firstCards = AllCards(player);
            session.InitializeRaceDeck(player, null, scenario.engineHeatCapacity, scenario);
            CollectionAssert.AreEqual(opening, Describe(player.deck.Hand));
            foreach (var card in AllCards(player)) Assert.IsFalse(firstCards.Contains(card));
            Assert.AreEqual(scenario.engineHeatCapacity, player.deck.heatPool.remaining);
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(20)]
    public void ShortOrdinaryDeckRetainsExistingDrawFailureSemantics(int handSize)
    {
        config.handSize = handSize;
        config.enableTrickCards = false;
        config.speedCardDistribution = new[] { 2, 4 };
        var player = new PlayerState("short", false, 0, 1);
        new RaceSession().InitializeRaceDeck(player, config, 6);
        Assert.AreEqual(System.Math.Min(handSize, 2), player.deck.HandCount);
        Assert.AreEqual(2, AllCards(player).Count);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
    }

    [Test]
    public void NormalNullConfigStillResetsTricksAndDeckBeforeThrowing()
    {
        var player = new PlayerState("invalid", false, 0, 1);
        var session = new RaceSession();
        session.InitializeRaceDeck(player, config, 6);
        var oldTricks = player.trickState;
        Assert.Throws<System.NullReferenceException>(() => session.InitializeRaceDeck(player, null, 4));
        Assert.AreNotSame(oldTricks, player.trickState);
        Assert.AreEqual(4, player.deck.heatPool.remaining);
        Assert.AreEqual(0, AllCards(player).Count);
    }
}
