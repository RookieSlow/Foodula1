using System.Collections.Generic;
using NUnit.Framework;

public class DriverBalanceBenchmarkTests
{
    [Test]
    public void BenchmarkExposesEveryCatalogDriverAtBothComparisonLevels()
    {
        var ids = new HashSet<string>(DriverBalanceBenchmark.GetDriverIdsForTests());

        Assert.That(ids.Count, Is.EqualTo(12));
        foreach (DriverProfile driver in DriverCatalog.All)
            Assert.That(ids.Contains(driver.Id), Is.True, driver.Id);
        Assert.That(DriverBalanceBenchmark.GetLevelsForTests(), Is.EqualTo(new[] { 3, 7 }));
    }

    [Test]
    public void PairedRaceIsDeterministicAndKeepsTheControlSeparate()
    {
        DriverBalanceBenchmark.RaceSnapshot first = DriverBalanceBenchmark.SimulateForTests(
            "silverstone_afternoon_tea",
            "de_michael_schumacher",
            3,
            true,
            DriverBalanceBenchmark.BaseSeed,
            0);
        DriverBalanceBenchmark.RaceSnapshot replay = DriverBalanceBenchmark.SimulateForTests(
            "silverstone_afternoon_tea",
            "de_michael_schumacher",
            3,
            true,
            DriverBalanceBenchmark.BaseSeed,
            0);
        DriverBalanceBenchmark.RaceSnapshot control = DriverBalanceBenchmark.SimulateForTests(
            "silverstone_afternoon_tea",
            "de_michael_schumacher",
            3,
            false,
            DriverBalanceBenchmark.BaseSeed,
            0);

        Assert.That(first.Enabled, Is.True);
        Assert.That(control.Enabled, Is.False);
        Assert.That(first.DriverId, Is.EqualTo("de_michael_schumacher"));
        Assert.That(first.Rank, Is.EqualTo(replay.Rank));
        Assert.That(first.Finished, Is.EqualTo(replay.Finished));
        Assert.That(first.Blown, Is.EqualTo(replay.Blown));
        Assert.That(first.Turns, Is.EqualTo(replay.Turns));
        Assert.That(first.HeatPaid, Is.EqualTo(replay.HeatPaid));
        Assert.That(first.Activations, Is.EqualTo(replay.Activations));
        Assert.That(first.Rank, Is.GreaterThanOrEqualTo(1));
        Assert.That(control.Rank, Is.GreaterThanOrEqualTo(1));
    }
}
