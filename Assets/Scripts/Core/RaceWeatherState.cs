/// <summary>
/// Tracks which lap has already triggered the race weather roll.
/// Weather selection itself remains in RaceSession/WeatherRules.
/// </summary>
public sealed class RaceWeatherState
{
    /// <summary>The most recently rolled lap, or zero before the first roll.</summary>
    public int LastRolledLap { get; private set; }

    /// <summary>Clears the per-race weather-roll gate.</summary>
    public void Reset()
    {
        LastRolledLap = 0;
    }

    /// <summary>
    /// Marks a lap as rolled once when weather is enabled.
    /// Returns false for repeated crossings of the same lap or disabled weather.
    /// </summary>
    public bool TryBeginLapRoll(int lap, bool weatherEnabled)
    {
        RaceLapWeatherTransition transition = RaceLapWeatherRules.Advance(
            lap - 1,
            int.MaxValue,
            LastRolledLap,
            weatherEnabled);
        if (!transition.ShouldRollWeather)
            return false;

        MarkLapRolled(transition.Lap);
        return true;
    }

    /// <summary>Commits the lap that has already been awarded the weather roll.</summary>
    public void MarkLapRolled(int lap)
    {
        LastRolledLap = lap;
    }
}
