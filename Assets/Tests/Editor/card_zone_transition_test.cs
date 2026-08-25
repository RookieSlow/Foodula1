using NUnit.Framework;
using UnityEngine;

public class CardZoneTransitionTests
{
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
