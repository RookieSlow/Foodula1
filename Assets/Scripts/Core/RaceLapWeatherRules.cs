using System.Collections.Generic;

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

    /// <summary>
    /// Resolves an ordered batch of start/finish crossings from one movement.
    ///
    /// A high movement bonus can cross the line more than once. The batch keeps
    /// the weather gate local to this pure calculation and stops at the first
    /// finishing crossing, matching the runtime adapter's terminal semantics.
    /// </summary>
    public static RaceLapWeatherBatchTransition AdvanceCrossings(
        int currentLap,
        int totalLaps,
        int lastWeatherRolledLap,
        bool weatherEnabled,
        int crossingCount)
    {
        var transitions = new List<RaceLapWeatherTransition>();
        int lap = currentLap;
        int lastRolledLap = lastWeatherRolledLap;

        for (int i = 0; i < crossingCount; i++)
        {
            RaceLapWeatherTransition transition = Advance(
                lap,
                totalLaps,
                lastRolledLap,
                weatherEnabled);
            transitions.Add(transition);
            lap = transition.Lap;
            if (transition.ShouldRollWeather)
                lastRolledLap = transition.Lap;

            if (transition.HasFinished)
                break;
        }

        return new RaceLapWeatherBatchTransition(
            lap,
            lastRolledLap,
            transitions);
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

/// <summary>Immutable result of resolving multiple ordered line crossings.</summary>
public readonly struct RaceLapWeatherBatchTransition
{
    public RaceLapWeatherBatchTransition(
        int finalLap,
        int lastWeatherRolledLap,
        IReadOnlyList<RaceLapWeatherTransition> transitions)
    {
        FinalLap = finalLap;
        LastWeatherRolledLap = lastWeatherRolledLap;
        Transitions = transitions;
    }

    /// <summary>Lap after the last processed crossing.</summary>
    public int FinalLap { get; }

    /// <summary>Weather gate after the last processed crossing.</summary>
    public int LastWeatherRolledLap { get; }

    /// <summary>Ordered crossings, ending at finish when the car finishes.</summary>
    public IReadOnlyList<RaceLapWeatherTransition> Transitions { get; }
}
