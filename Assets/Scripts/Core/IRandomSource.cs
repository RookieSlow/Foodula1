/// <summary>
/// Provides deterministic random values to gameplay systems.
/// </summary>
public interface IRandomSource
{
    int NextInt(int minimumInclusive, int maximumExclusive);
    double NextDouble();
}
