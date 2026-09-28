using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

public class HeatCoolingSourcesTests
{
    private static List<CardData> DrawZone(CardDeck deck) =>
        (List<CardData>)typeof(CardDeck).GetField("drawPile", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(deck);

    private static CardDeck CreateDeck()
    {
        var deck = new CardDeck();
        deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 2) }, new HeatPool(6));
        return deck;
    }

    [TestCase(0, 0, 0, 0)]
    [TestCase(1, 1, 0, 0)]
    [TestCase(3, 3, 0, 0)]
    [TestCase(4, 3, 1, 0)]
    [TestCase(9, 3, 1, 2)]
    public void GenericCoolingReportsActualPriorityAndPreservesTemporarySemantics(
        int requested, int hand, int draw, int discard)
    {
        var deck = CreateDeck();
        CardData speed = deck.DrawPile[0];
        deck.DrawHeatFromPoolToHand(2);
        deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
        DrawZone(deck).Add(new CardData(CardType.Heat, 0)); // Defensive legacy draw-zone state.
        deck.heatPool.remaining--;
        deck.DrawHeatFromPool(2);
        var result = deck.CoolHeatWithSources(requested);
        Assert.AreEqual(hand, result.FromHand);
        Assert.AreEqual(draw, result.FromDraw);
        Assert.AreEqual(discard, result.FromDiscard);
        Assert.AreEqual(hand + draw + discard, result.Total);
        Assert.AreEqual(1 + System.Math.Max(0, result.Total - 1), deck.heatPool.remaining);
        Assert.AreSame(speed, deck.DrawPile[0]);
        Assert.AreEqual(3 - hand, deck.CountHeatInHand());
        Assert.AreEqual(1 - draw, deck.CountHeatInDrawPile());
        Assert.AreEqual(2 - discard, deck.CountHeatInDiscardPile());
        Assert.AreEqual(6, deck.heatPool.remaining + deck.CountPermanentHeatOutsideEngine());
    }

    [TestCase(1, 0, 0, 1)]
    [TestCase(2, 1, 0, 1)]
    [TestCase(9, 1, 1, 1)]
    public void RecordedCoolingReportsOnlyActuallySelectedReceiptsInReceiptOrder(
        int requested, int hand, int draw, int discard)
    {
        var deck = CreateDeck();
        var receipts = new List<CardData>();
        deck.DrawHeatFromPoolToHand(1); // Old heat must stay put.
        deck.DrawHeatFromPool(1, HeatPaymentDestination.Discard, receipts);
        deck.DrawHeatFromPool(1, HeatPaymentDestination.Hand, receipts);
        var legacy = new CardData(CardType.Heat, 0);
        DrawZone(deck).Add(legacy);
        deck.heatPool.remaining--;
        receipts.Add(legacy);
        receipts.Add(receipts[0]);
        receipts.Add(CardData.CreateTempHeat());
        receipts.Add(new CardData(CardType.Heat, 0));
        var result = deck.CoolRecordedHeatWithSources(receipts, requested);
        Assert.AreEqual(hand, result.FromHand);
        Assert.AreEqual(draw, result.FromDraw);
        Assert.AreEqual(discard, result.FromDiscard);
        Assert.AreEqual(hand + draw + discard, result.Total);
        Assert.AreEqual(2 + result.Total, deck.heatPool.remaining);
        Assert.AreEqual(2 - hand, deck.CountHeatInHand());
        Assert.AreEqual(6, deck.heatPool.remaining + deck.CountPermanentHeatOutsideEngine());
        // The compatibility API consumes only the remaining receipts, never repeats a refund.
        Assert.AreEqual(3 - result.Total, deck.CoolRecordedHeatThroughDiscard(receipts, 99));
        Assert.AreEqual(5, deck.heatPool.remaining);
        Assert.AreEqual(1, deck.CountHeatInHand());
    }

    [Test]
    public void EmptyResultsAndCompatibilityApiKeepZeroContract()
    {
        var deck = CreateDeck();
        Assert.AreEqual(0, default(HeatCoolingResult).Total);
        Assert.AreEqual(0, deck.CoolHeatWithSources(-1).Total);
        Assert.AreEqual(0, deck.CoolRecordedHeatWithSources(null, 9).Total);
        Assert.AreEqual(0, deck.CoolRecordedHeatThroughDiscard(null, 9));
        Assert.AreEqual(0, deck.CoolHeat(9));
        Assert.AreEqual(6, deck.heatPool.remaining);
    }
}
