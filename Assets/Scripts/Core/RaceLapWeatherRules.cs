/// <summary>
/// Pure transition rules for a start/finish crossing.
/// Combines lap progression with the once-per-lap weather-roll decision so the
/// runtime race loop and pure simulation use the same ordering contract.
/// </summary>
public static class RaceLapWeatherRules
{
    /// <summary>
    /// Advances one lap and decides whether this crossing owns the weather roll.
    /// A disabled weather system never consumes the lap gate.
    /// </summary>
    public static RaceLapWeatherTransition Advance(
        int currentLap,
        int totalLaps,
        int lastWeatherRolledLap,
        bool weatherEnabled)
    {
        LapProgressResult progress = RaceLapRules.Advance(currentLap, totalLaps);
        bool shouldRollWeather = weatherEnabled && progress.Lap != lastWeatherRolledLap;
        return new RaceLapWeatherTransition(progress.Lap, progress.HasFinished, shouldRollWeather);
    }
}

/// <summary>Immutable result of crossing the start/finish line.</summary>
public readonly struct RaceLapWeatherTransition
{
    public RaceLapWeatherTransition(int lap, bool hasFinished, bool shouldRollWeather)
    {
        Lap = lap;
        HasFinished = hasFinished;
        ShouldRollWeather = shouldRollWeather;
    }

    /// <summary>The incremented lap number.</summary>
    public int Lap { get; }

    /// <summary>Whether the incremented lap reaches the race distance.</summary>
    public bool HasFinished { get; }

    /// <summary>Whether this crossing should roll weather and commit the lap gate.</summary>
    public bool ShouldRollWeather { get; }
}
