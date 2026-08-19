using NUnit.Framework;

public class RaceLapWeatherRulesTests
{
    [Test]
    public void test_first_crossing_advances_lap_and_requests_weather_roll()
    {
        RaceLapWeatherTransition result = RaceLapWeatherRules.Advance(0, 3, 0, true);

        Assert.That(result.Lap, Is.EqualTo(1));
        Assert.That(result.HasFinished, Is.False);
        Assert.That(result.ShouldRollWeather, Is.True);
    }

    [Test]
    public void test_second_car_on_same_lap_does_not_request_second_roll()
    {
        RaceLapWeatherTransition result = RaceLapWeatherRules.Advance(0, 3, 1, true);

        Assert.That(result.Lap, Is.EqualTo(1));
        Assert.That(result.ShouldRollWeather, Is.False);
    }

    [Test]
    public void test_disabled_weather_does_not_consume_lap_gate()
    {
        RaceLapWeatherTransition result = RaceLapWeatherRules.Advance(0, 3, 0, false);

        Assert.That(result.Lap, Is.EqualTo(1));
        Assert.That(result.ShouldRollWeather, Is.False);
    }

    [Test]
    public void test_final_crossing_advances_and_marks_finish()
    {
        RaceLapWeatherTransition result = RaceLapWeatherRules.Advance(2, 3, 1, true);

        Assert.That(result.Lap, Is.EqualTo(3));
        Assert.That(result.HasFinished, Is.True);
        Assert.That(result.ShouldRollWeather, Is.True);
    }

    [Test]
    public void test_reset_weather_state_allows_first_lap_roll_again()
    {
        var state = new RaceWeatherState();
        state.MarkLapRolled(2);
        state.Reset();

        RaceLapWeatherTransition result = RaceLapWeatherRules.Advance(
            0,
            3,
            state.LastRolledLap,
            true);

        Assert.That(result.ShouldRollWeather, Is.True);
    }
}
