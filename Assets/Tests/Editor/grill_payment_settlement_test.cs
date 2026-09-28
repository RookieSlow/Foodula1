using System.Collections.Generic;
using NUnit.Framework;

public class GrillPaymentSettlementTests
{
    private CardDeck deck;
    private List<CardData> receipts;

    [SetUp]
    public void Setup()
    {
        deck = new CardDeck();
        deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 2) }, new HeatPool(6));
        receipts = new List<CardData>();
    }

    [TestCase(HeatPaymentDestination.Hand)]
    [TestCase(HeatPaymentDestination.Discard)]
    public void OnlyCurrentPaymentInstancesReturnAndDuplicatesCannotRefundTwice(HeatPaymentDestination target)
    {
        deck.DrawHeatFromPoolToHand(1); // Old hand heat must not be substituted.
        deck.DrawHeatFromPool(1); // Old discard heat must remain too.
        Assert.AreEqual(2, deck.DrawHeatFromPool(2, target, receipts));
        receipts.Add(receipts[0]);
        receipts.Add(CardData.CreateTempHeat());
        receipts.Add(new CardData(CardType.Heat, 0)); // A foreign receipt is not owned.
        Assert.AreEqual(2, deck.CountRecordedHeat(receipts));
        Assert.AreEqual(2, deck.CoolRecordedHeatThroughDiscard(receipts, 99));
        Assert.AreEqual(4, deck.heatPool.remaining);
        Assert.AreEqual(1, deck.CountHeatInHand());
        Assert.AreEqual(1, deck.CountHeatInDiscardPile());
        Assert.AreEqual(0, deck.CoolRecordedHeatThroughDiscard(receipts, 99));
        Assert.AreEqual(4, deck.heatPool.remaining);
    }

    [Test]
    public void AlreadyCooledReceiptDoesNotStealOldDiscardHeat()
    {
        deck.DrawHeatFromPool(2);
        deck.DrawHeatFromPool(1, HeatPaymentDestination.Hand, receipts);
        Assert.AreEqual(1, deck.RemoveHeatFromHand(1));
        Assert.AreEqual(0, deck.CountRecordedHeat(receipts));
        Assert.AreEqual(0, deck.CoolRecordedHeatThroughDiscard(receipts, 1));
        Assert.AreEqual(2, deck.CountHeatInDiscardPile());
        Assert.AreEqual(4, deck.heatPool.remaining);
    }

    [Test]
    public void PartialPaymentTracksActualInstancesOnly()
    {
        Assert.AreEqual(6, deck.DrawHeatFromPool(10, HeatPaymentDestination.Hand, receipts));
        Assert.AreEqual(6, receipts.Count);
        Assert.AreEqual(2, deck.CoolRecordedHeatThroughDiscard(receipts, 2));
        Assert.AreEqual(4, deck.CountRecordedHeat(receipts));
        Assert.AreEqual(2, deck.heatPool.remaining);
    }

    [Test]
    public void BeginTurnClearsReceiptsAndCountsWithoutResettingOnceFlag()
    {
        var player = new PlayerState("DE", false, 0, 1) { techState = new TechTreeState(TeamId.DE, 0) };
        player.heatPaidCardsThisTurn.Add(new CardData(CardType.Heat, 0));
        player.techState.grillSpezialUsed = true;
        player.techState.grillSpezialHeatPaidThisTurn = 2;
        var session = new RaceSession(new SystemRandomSource(1));
        session.BeginTurn(null);
        session.BeginTurn(player);
        Assert.IsEmpty(player.heatPaidCardsThisTurn);
        Assert.AreEqual(0, player.techState.grillSpezialHeatPaidThisTurn);
        Assert.IsTrue(player.techState.grillSpezialUsed);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TerminalParticipantCannotTriggerGrill(bool blown)
    {
        var player = new PlayerState("DE", false, 0, 1) { teamId = TeamId.DE,
            techState = new TechTreeState(TeamId.DE, 0), isBlown = blown, hasFinished = !blown };
        player.techState.activeNodeIds.Add("de-l3-grill-spezial");
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 2) }, new HeatPool(6));
        player.deck.DrawHeatFromPool(2, HeatPaymentDestination.Discard, player.heatPaidCardsThisTurn);
        player.techState.grillSpezialHeatPaidThisTurn = 2;
        var session = new RaceSession(new SystemRandomSource(1)) { TechDb = TechTreeDatabaseFactory.CreateDefault() };
        Assert.AreEqual(0, session.GetGrillSpezialCooldown(player));
        Assert.IsFalse(player.techState.grillSpezialUsed);
    }
}
