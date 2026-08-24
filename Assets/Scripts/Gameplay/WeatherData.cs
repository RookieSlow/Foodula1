// ═══════════════════════════════════════════════════════════════════════════════
// WeatherData.cs — Weather types and effect modifiers for Foodula1.
// Pure C# data layer. Track JSONs already define weatherPool / defaultWeather.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Weather conditions during a race.</summary>
public enum WeatherType
{
    Sunny,
    Cloudy,
    LightRain,
    HeavyRain,
    Hot,

    /// <summary>
    /// Legacy name retained for callers and saved test fixtures. The old
    /// two-state weather model treated all rain as one condition; the
    /// compatibility value now means light rain.
    /// </summary>
    Rainy = LightRain
}

/// <summary>Computed modifiers applied by the current weather.</summary>
public struct WeatherModifiers
{
    /// <summary>Corner speed limit reduction (positive = harder).</summary>
    public int cornerLimitReduction;

    /// <summary>Slipstream movement-bonus reduction.</summary>
    public int slipstreamBonusReduction;

    /// <summary>Reserved extra heat per corner overspeed for future weather rules.</summary>
    public int extraHeatPerOverspeed;

    /// <summary>Additional spin-counter points applied to each spin-out.</summary>
    public int extraSpinCounter;

    /// <summary>Cooling points removed by the weather from the reaction step.</summary>
    public int coolingReduction;

    /// <summary>Whether the current weather prevents slipstream completely.</summary>
    public bool disablesSlipstream;

    /// <summary>Clear weather baseline with no modifiers.</summary>
    public static WeatherModifiers Sunny => new WeatherModifiers
    {
        cornerLimitReduction = 0,
        slipstreamBonusReduction = 0,
        extraHeatPerOverspeed = 0,
        extraSpinCounter = 0,
        coolingReduction = 0,
        disablesSlipstream = false
    };

    /// <summary>Cloud cover reduces slipstream efficiency by one movement point.</summary>
    public static WeatherModifiers Cloudy => new WeatherModifiers
    {
        cornerLimitReduction = 0,
        slipstreamBonusReduction = 1,
        extraHeatPerOverspeed = 0,
        extraSpinCounter = 0,
        coolingReduction = 0,
        disablesSlipstream = false
    };

    /// <summary>Light rain reduces corner limits and increases spin risk.</summary>
    public static WeatherModifiers LightRain => new WeatherModifiers
    {
        cornerLimitReduction = 1,
        slipstreamBonusReduction = 0,
        extraHeatPerOverspeed = 0,
        extraSpinCounter = 1,
        coolingReduction = 0,
        disablesSlipstream = false
    };

    /// <summary>Heavy rain is the severe wet-weather profile from the track GDD.</summary>
    public static WeatherModifiers HeavyRain => new WeatherModifiers
    {
        cornerLimitReduction = 2,
        slipstreamBonusReduction = 0,
        extraHeatPerOverspeed = 0,
        extraSpinCounter = 2,
        coolingReduction = 0,
        disablesSlipstream = true
    };

    /// <summary>Hot weather reduces the amount cooled during the reaction step.</summary>
    public static WeatherModifiers Hot => new WeatherModifiers
    {
        cornerLimitReduction = 0,
        slipstreamBonusReduction = 0,
        extraHeatPerOverspeed = 0,
        extraSpinCounter = 0,
        coolingReduction = 1,
        disablesSlipstream = false
    };

    /// <summary>Legacy two-state profile alias for light rain.</summary>
    public static WeatherModifiers Rainy => LightRain;

    /// <summary>Returns the complete effect profile for a weather type.</summary>
    public static WeatherModifiers FromWeather(WeatherType weather)
    {
        switch (weather)
        {
            case WeatherType.Cloudy: return Cloudy;
            case WeatherType.LightRain: return LightRain;
            case WeatherType.HeavyRain: return HeavyRain;
            case WeatherType.Hot: return Hot;
            default: return Sunny;
        }
    }
}
