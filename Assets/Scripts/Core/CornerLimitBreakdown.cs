using System;

/// <summary>
/// One corner's authored limit and every signed modifier that contributes to
/// the current effective limit. This is shared by gameplay and the clickable
/// track explanation UI so the player never sees a second, divergent formula.
/// </summary>
public readonly struct CornerLimitBreakdown
{
    public int BaseLimit { get; }
    public int WeatherModifier { get; }
    public int DriverModifier { get; }
    public int TeamModifier { get; }
    public int TechnologyModifier { get; }
    public int EffectiveLimit { get; }

    public int UnclampedLimit => BaseLimit + WeatherModifier + DriverModifier +
        TeamModifier + TechnologyModifier;

    public CornerLimitBreakdown(
        int baseLimit,
        int weatherModifier,
        int driverModifier,
        int teamModifier,
        int technologyModifier)
    {
        BaseLimit = baseLimit;
        WeatherModifier = weatherModifier;
        DriverModifier = driverModifier;
        TeamModifier = teamModifier;
        TechnologyModifier = technologyModifier;
        EffectiveLimit = baseLimit >= 99
            ? baseLimit
            : Math.Max(1, baseLimit + weatherModifier + driverModifier +
                teamModifier + technologyModifier);
    }

    public static CornerLimitBreakdown FromBase(int baseLimit)
    {
        return new CornerLimitBreakdown(baseLimit, 0, 0, 0, 0);
    }
}
