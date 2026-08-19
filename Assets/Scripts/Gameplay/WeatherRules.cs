using System.Collections.Generic;

/// <summary>
/// Pure-function weather rules — effect computation, random selection,
/// and per-lap weather change logic.
/// </summary>
public static class WeatherRules
{
    /// <summary>Apply weather modifiers to a base corner speed limit.</summary>
    public static int ApplyWeatherToCornerLimit(int baseLimit, WeatherType weather)
    {
        var mods = WeatherModifiers.FromWeather(weather);
        return baseLimit - mods.cornerLimitReduction;
    }

    /// <summary>Apply weather modifiers to slipstream range.</summary>
    public static int ApplyWeatherToSlipstreamRange(int baseRange, WeatherType weather)
    {
        var mods = WeatherModifiers.FromWeather(weather);
        if (mods.disablesSlipstream) return 0;
        return System.Math.Max(0, baseRange - mods.slipstreamRangeReduction);
    }

    /// <summary>Get extra heat for corner overspeed in given weather.</summary>
    public static int GetExtraHeatPerOverspeed(WeatherType weather)
    {
        return WeatherModifiers.FromWeather(weather).extraHeatPerOverspeed;
    }

    /// <summary>Returns whether the weather allows a slipstream bonus.</summary>
    public static bool CanSlipstream(WeatherType weather)
    {
        return !WeatherModifiers.FromWeather(weather).disablesSlipstream;
    }

    /// <summary>Applies weather's cooling penalty without allowing negative cooling.</summary>
    public static int ApplyWeatherToCooling(int baseCooling, WeatherType weather)
    {
        var mods = WeatherModifiers.FromWeather(weather);
        return System.Math.Max(0, baseCooling - mods.coolingReduction);
    }

    /// <summary>Returns additional spin-counter points caused by a failed maneuver.</summary>
    public static int GetExtraSpinCounter(WeatherType weather)
    {
        return WeatherModifiers.FromWeather(weather).extraSpinCounter;
    }

    /// <summary>Returns the Chinese HUD label for a weather type.</summary>
    public static string GetDisplayName(WeatherType weather)
    {
        switch (weather)
        {
            case WeatherType.Cloudy: return "多云";
            case WeatherType.LightRain: return "小雨";
            case WeatherType.HeavyRain: return "大雨";
            case WeatherType.Hot: return "高温";
            default: return "晴天";
        }
    }

    /// <summary>Select starting weather from the track's weather pool.</summary>
    public static WeatherType SelectInitialWeather(
        string[] weatherPool, string defaultWeather, IRandomSource random = null)
    {
        // If default is specified and valid, use it
        if (!string.IsNullOrEmpty(defaultWeather))
        {
            var parsed = ParseWeather(defaultWeather);
            if (parsed != null) return parsed.Value;
        }

        // Otherwise pick randomly from pool
        if (weatherPool != null && weatherPool.Length > 0)
        {
            if (random != null)
            {
                int idx = random.NextInt(0, weatherPool.Length);
                var parsed = ParseWeather(weatherPool[idx]);
                if (parsed != null) return parsed.Value;
            }
            // Fallback: first valid entry
            foreach (var w in weatherPool)
            {
                var parsed = ParseWeather(w);
                if (parsed != null) return parsed.Value;
            }
        }

        return WeatherType.Sunny;
    }

    /// <summary>
    /// Roll for a weather change at the start of a new lap.
    /// Returns the new weather (may be same as current).
    /// </summary>
    public static WeatherType RollWeatherChange(
        WeatherType current, string[] weatherPool, IRandomSource random)
    {
        if (weatherPool == null || weatherPool.Length <= 1 || random == null)
            return current;

        // 30% chance to change weather each lap
        if (random.NextDouble() > 0.3)
            return current;

        // Pick a different weather from the pool
        var available = new List<WeatherType>();
        foreach (var w in weatherPool)
        {
            var parsed = ParseWeather(w);
            if (parsed != null && parsed.Value != current)
                available.Add(parsed.Value);
        }

        if (available.Count == 0) return current;
        return available[random.NextInt(0, available.Count)];
    }

    /// <summary>Parse a weather string from JSON to enum.</summary>
    public static WeatherType? ParseWeather(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        switch (name.ToLowerInvariant())
        {
            case "sunny": return WeatherType.Sunny;
            case "cloudy": return WeatherType.Cloudy;
            case "hot": return WeatherType.Hot;
            case "rainy":
            case "rain":
            case "light_rain": return WeatherType.LightRain;
            case "heavy_rain": return WeatherType.HeavyRain;
            default: return null;
        }
    }
}
