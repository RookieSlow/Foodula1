using System.Collections.Generic;
using NUnit.Framework;

public class ThunderstormGridRulesTests
{
    [TestCase(2)]
    [TestCase(4)]
    public void TwelveRacersHaveDistinctSlotsAndWrapBehindStart(int laneCount)
    {
        var slots = new HashSet<string>();
        for (int index = 0; index < 12; index++)
        {
            Assert.IsTrue(TrackPresentationRules.TryGetThunderstormGridSlot(
                0, 12, index, 0, 77, laneCount, out int position, out int lane));
            Assert.AreEqual((77 - index / laneCount) % 77, position);
            Assert.AreEqual(index % laneCount, lane);
            Assert.IsTrue(slots.Add(position + ":" + lane));
        }
        Assert.AreEqual(12, slots.Count);
    }

    [TestCase(1, 12, 0, 77)]
    [TestCase(10, 12, 0, 77)]
    [TestCase(0, 4, 0, 77)]
    [TestCase(0, 0, 0, 77)]
    [TestCase(0, 12, -1, 77)]
    [TestCase(0, 12, 0, 0)]
    [TestCase(0, 12, 0, -1)]
    public void IneligibleContextLeavesNoGridOverride(int turn, int roster, int index, int nodes)
    {
        Assert.IsFalse(TrackPresentationRules.TryGetThunderstormGridSlot(
            turn, roster, index, 5, nodes, 2, out int position, out int lane));
        Assert.AreEqual(0, position);
        Assert.AreEqual(0, lane);
    }

    [Test]
    public void NonZeroStartAndLastRowUseAuthoredStartIndex()
    {
        Assert.IsTrue(TrackPresentationRules.TryGetThunderstormGridSlot(
            0, 12, 11, 3, 60, 2, out int position, out int lane));
        Assert.AreEqual(58, position);
        Assert.AreEqual(1, lane);
    }

    [TestCase(0)]
    [TestCase(-3)]
    public void InvalidLaneCountRetainsSingleLaneFallback(int laneCount)
    {
        Assert.IsTrue(TrackPresentationRules.TryGetThunderstormGridSlot(
            0, 12, 11, 0, 60, laneCount, out int position, out int lane));
        Assert.AreEqual(49, position);
        Assert.AreEqual(0, lane);
    }
}
