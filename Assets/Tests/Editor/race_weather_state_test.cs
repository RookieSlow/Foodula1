using NUnit.Framework;

public class RaceWeatherStateTests
{
    [Test]
    public void EachLapRollsAtMostOnce()
    {
        var state = new RaceWeatherState();
        state.Reset();

        Assert.That(state.TryBeginLapRoll(1, true), Is.True);
        Assert.That(state.TryBeginLapRoll(1, true), Is.False);
        Assert.That(state.TryBeginLapRoll(2, true), Is.True);
        Assert.That(state.LastRolledLap, Is.EqualTo(2));
    }

    [Test]
    public void DisabledWeatherDoesNotConsumeLapGate()
    {
        var state = new RaceWeatherState();

        Assert.That(state.TryBeginLapRoll(1, false), Is.False);
        Assert.That(state.LastRolledLap, Is.EqualTo(0));
        Assert.That(state.TryBeginLapRoll(1, true), Is.True);
    }

    [Test]
    public void ResetAllowsTheNextRaceToRollFromTheFirstLap()
    {
        var state = new RaceWeatherState();
        state.TryBeginLapRoll(3, true);
        state.Reset();

        Assert.That(state.LastRolledLap, Is.EqualTo(0));
        Assert.That(state.TryBeginLapRoll(1, true), Is.True);
    }
}
