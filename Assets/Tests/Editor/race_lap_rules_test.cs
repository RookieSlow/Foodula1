using NUnit.Framework;

public class RaceLapRulesTests
{
    [Test]
    public void CrossingBeforeFinalLapOnlyIncrementsLap()
    {
        LapProgressResult result = RaceLapRules.Advance(1, 3);

        Assert.That(result.Lap, Is.EqualTo(2));
        Assert.That(result.HasFinished, Is.False);
    }

    [Test]
    public void CrossingFinalLapMarksFinished()
    {
        LapProgressResult result = RaceLapRules.Advance(2, 3);

        Assert.That(result.Lap, Is.EqualTo(3));
        Assert.That(result.HasFinished, Is.True);
    }

    [TestCase(3, 3)]
    [TestCase(0, 0)]
    [TestCase(0, -1)]
    public void FinishBoundaryRemainsStableAtOrBelowTarget(int currentLap, int totalLaps)
    {
        LapProgressResult result = RaceLapRules.Advance(currentLap, totalLaps);

        Assert.That(result.HasFinished, Is.True);
    }
}
