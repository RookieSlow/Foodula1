using System;

/// <summary>Random source backed by Unity's global random generator.</summary>
public sealed class UnityRandomSource : IRandomSource
{
    public int NextInt(int minimumInclusive, int maximumExclusive)
    {
        return UnityEngine.Random.Range(minimumInclusive, maximumExclusive);
    }

    public double NextDouble()
    {
        return UnityEngine.Random.value;
    }
}

/// <summary>Seedable random source for deterministic gameplay tests.</summary>
public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random random;

    public SystemRandomSource()
        : this(Environment.TickCount)
    {
    }

    public SystemRandomSource(int seed)
    {
        random = new Random(seed);
    }

    public int NextInt(int minimumInclusive, int maximumExclusive)
    {
        return random.Next(minimumInclusive, maximumExclusive);
    }

    public double NextDouble()
    {
        return random.NextDouble();
    }
}
