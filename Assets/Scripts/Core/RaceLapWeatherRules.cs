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
        bool shouldRollWeather = ShouldRollWeatherForLap(
            progress.Lap,
            lastWeatherRolledLap,
            weatherEnabled);
        return new RaceLapWeatherTransition(progress.Lap, progress.HasFinished, shouldRollWeather);
    }

    /// <summary>
    /// Returns whether the requested lap may consume the once-per-lap weather gate.
    /// Kept separate from lap progression for state adapters that already receive a lap number.
    /// </summary>
    public static bool ShouldRollWeatherForLap(int lap, int lastWeatherRolledLap, bool weatherEnabled)
    {
        // The shared lap is a high-water mark: a lagging car must not roll an
        // earlier lap again or move the gate backwards after the leader.
        return weatherEnabled && lap > lastWeatherRolledLap;
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
