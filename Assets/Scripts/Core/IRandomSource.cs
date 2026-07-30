/// <summary>
/// Provides deterministic random values to gameplay systems.
/// </summary>
public interface IRandomSource
{
    /// <summary>Returns an integer in [minimumInclusive, maximumExclusive).</summary>
    int NextInt(int minimumInclusive, int maximumExclusive);

    /// <summary>Returns a floating-point value in [0, 1).</summary>
    double NextDouble();
}
