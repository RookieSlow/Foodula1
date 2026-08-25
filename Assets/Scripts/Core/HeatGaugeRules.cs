using UnityEngine;

/// <summary>Visual warning tiers for the player's engine heat load.</summary>
public enum HeatWarningLevel
{
    Cool,
    Elevated,
    Critical
}

/// <summary>Immutable presentation snapshot derived from the real heat zones.</summary>
public readonly struct HeatGaugeState
{
    public readonly int EngineRemaining;
    public readonly int PermanentHeat;
    public readonly int TemporaryHeat;
    public readonly int Capacity;
    public readonly float Fill01;
    public readonly HeatWarningLevel WarningLevel;

    public int TotalHeat => PermanentHeat + TemporaryHeat;
    public int Percent => Mathf.RoundToInt(Fill01 * 100f);

    public HeatGaugeState(
        int engineRemaining,
        int permanentHeat,
        int temporaryHeat,
        int capacity,
        float fill01,
        HeatWarningLevel warningLevel)
    {
        EngineRemaining = engineRemaining;
        PermanentHeat = permanentHeat;
        TemporaryHeat = temporaryHeat;
        Capacity = capacity;
        Fill01 = fill01;
        WarningLevel = warningLevel;
    }
}

/// <summary>
/// Converts heat-card ownership into the compact thermometer state used by
/// the HUD. Permanent cards determine engine capacity; temporary heat raises
/// the displayed load but can never inflate that capacity.
/// </summary>
public static class HeatGaugeRules
{
    public const float ElevatedThreshold = 0.5f;
    public const float CriticalThreshold = 0.7f;

    public static HeatGaugeState Evaluate(CardDeck deck)
    {
        if (deck == null)
            return new HeatGaugeState(0, 0, 0, 0, 0f, HeatWarningLevel.Cool);

        int engineRemaining = deck.heatPool != null ? Mathf.Max(0, deck.heatPool.remaining) : 0;
        int permanentHeat = deck.CountPermanentHeatOutsideEngine();
        int temporaryHeat = deck.CountTemporaryHeatOutsideEngine();
        int capacity = engineRemaining + permanentHeat;
        int denominator = Mathf.Max(1, capacity);
        float fill = Mathf.Clamp01((permanentHeat + temporaryHeat) / (float)denominator);

        HeatWarningLevel warning = fill >= CriticalThreshold
            ? HeatWarningLevel.Critical
            : fill >= ElevatedThreshold
                ? HeatWarningLevel.Elevated
                : HeatWarningLevel.Cool;

        return new HeatGaugeState(
            engineRemaining,
            permanentHeat,
            temporaryHeat,
            capacity,
            fill,
            warning);
    }
}
