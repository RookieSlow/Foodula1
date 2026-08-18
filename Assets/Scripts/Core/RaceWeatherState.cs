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
        if (!weatherEnabled || lap == LastRolledLap)
            return false;

        LastRolledLap = lap;
        return true;
    }
}
