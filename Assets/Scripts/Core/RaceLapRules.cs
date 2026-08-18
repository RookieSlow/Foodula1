/// <summary>Result of advancing a car across the start/finish line.</summary>
public readonly struct LapProgressResult
{
    public LapProgressResult(int lap, bool hasFinished)
    {
        Lap = lap;
        HasFinished = hasFinished;
    }

    /// <summary>The incremented lap number.</summary>
    public int Lap { get; }

    /// <summary>Whether the incremented lap reaches the configured race distance.</summary>
    public bool HasFinished { get; }
}

/// <summary>Pure lap progression rules shared by the track crossing adapter and tests.</summary>
public static class RaceLapRules
{
    /// <summary>Advances one lap and evaluates the finish boundary.</summary>
    public static LapProgressResult Advance(int currentLap, int totalLaps)
    {
        int nextLap = currentLap + 1;
        return new LapProgressResult(nextLap, nextLap >= totalLaps);
    }
}
