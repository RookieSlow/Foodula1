using NUnit.Framework;
using UnityEngine;

public class CardZoneTransitionTests
{
    [TestCase(0, 0, 0, false)]
    [TestCase(1, 0, 0, false)]
    [TestCase(0, 2, 0, false)]
    [TestCase(0, 0, 3, false)]
    [TestCase(2, 3, 4, false)]
    [TestCase(0, 0, 0, true)]
    [TestCase(1, 0, 0, true)]
    [TestCase(0, 2, 0, true)]
    [TestCase(0, 0, 3, true)]
    [TestCase(2, 3, 4, true)]
    public void CoolingRoutesPreserveLegacyOrderCountsAndDestination(
        int hand, int draw, int discard, bool throughDiscard)
    {
        var calls = new System.Collections.Generic.List<(int, CardVisualZone, CardVisualZone)>();
        var receipt = new HeatCoolingResult(hand, draw, discard);
        CardZoneTransitionRules.PresentHeatCooling(receipt, throughDiscard,
            (count, source, destination) => calls.Add((count, source, destination)));

        CardVisualZone target = throughDiscard ? CardVisualZone.DiscardPile : CardVisualZone.Engine;
        Assert.That(calls, Is.EqualTo(new[]
        {
            (hand, CardVisualZone.Hand, target),
            (draw, CardVisualZone.DrawPile, target),
            (throughDiscard ? hand + draw + discard : discard,
                CardVisualZone.DiscardPile, CardVisualZone.Engine)
        }));
        Assert.That((receipt.FromHand, receipt.FromDraw, receipt.FromDiscard),
            Is.EqualTo((hand, draw, discard)));
    }

    [TestCase(0, 6, 0)]
    [TestCase(1, 6, 1)]
    [TestCase(4, 6, 4)]
    [TestCase(10, 6, 6)]
    public void VisualBatchCountIsBounded(int eventCount, int maxVisuals, int expected)
    {
        Assert.That(CardZoneTransitionRules.GetVisualCount(eventCount, maxVisuals),
            Is.EqualTo(expected));
    }

    [Test]
    public void ArcPreservesEndpointsAndRaisesMidpoint()
    {
        Vector2 start = new Vector2(-20f, 10f);
        Vector2 end = new Vector2(80f, 30f);

        Assert.That(CardZoneTransitionRules.EvaluateArc(start, end, 60f, 0f), Is.EqualTo(start));
        Assert.That(CardZoneTransitionRules.EvaluateArc(start, end, 60f, 1f), Is.EqualTo(end));
        Assert.That(CardZoneTransitionRules.EvaluateArc(start, end, 60f, 0.5f).y,
            Is.GreaterThan(Vector2.Lerp(start, end, 0.5f).y));
    }
}
