// ═══════════════════════════════════════════════════════════════════════════════
// WeatherData.cs — Weather types and effect modifiers for Foodula1.
// Pure C# data layer. Track JSONs already define weatherPool / defaultWeather.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Weather conditions during a race.</summary>
public enum WeatherType
{
    Sunny,
    Rainy
}

/// <summary>Computed modifiers applied by the current weather.</summary>
public struct WeatherModifiers
{
    /// <summary>Corner speed limit reduction in rain (positive = harder).</summary>
    public int cornerLimitReduction;

    /// <summary>Slipstream range reduction in rain (cells).</summary>
    public int slipstreamRangeReduction;

    /// <summary>Extra heat generated per corner overspeed in rain.</summary>
    public int extraHeatPerOverspeed;

    public static WeatherModifiers Sunny => new WeatherModifiers
    {
        cornerLimitReduction = 0,
        slipstreamRangeReduction = 0,
        extraHeatPerOverspeed = 0
    };

    public static WeatherModifiers Rainy => new WeatherModifiers
    {
        cornerLimitReduction = 1,    // 雨天弯道限速 -1
        slipstreamRangeReduction = 0, // 尾流距离不变
        extraHeatPerOverspeed = 0     // 超速惩罚不变
    };

    public static WeatherModifiers FromWeather(WeatherType weather)
    {
        return weather == WeatherType.Rainy ? Rainy : Sunny;
    }
}
