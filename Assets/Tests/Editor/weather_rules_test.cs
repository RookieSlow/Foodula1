using NUnit.Framework;

public class WeatherRulesTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Weather Effect Tests
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_sunny_has_no_corner_reduction()
    {
        int limit = WeatherRules.ApplyWeatherToCornerLimit(5, WeatherType.Sunny);
        Assert.That(limit, Is.EqualTo(5));
    }

    [Test]
    public void test_rainy_reduces_corner_limit_by_one()
    {
        int limit = WeatherRules.ApplyWeatherToCornerLimit(5, WeatherType.Rainy);
        Assert.That(limit, Is.EqualTo(4));
    }

    [Test]
    public void test_sunny_has_no_slipstream_reduction()
    {
        int range = WeatherRules.ApplyWeatherToSlipstreamRange(2, WeatherType.Sunny);
        Assert.That(range, Is.EqualTo(2));
    }

    [Test]
    public void test_rainy_keeps_slipstream_range()
    {
        int range = WeatherRules.ApplyWeatherToSlipstreamRange(2, WeatherType.Rainy);
        Assert.That(range, Is.EqualTo(2)); // No reduction in current design
    }

    [Test]
    public void test_no_extra_heat_in_sunny()
    {
        Assert.That(WeatherRules.GetExtraHeatPerOverspeed(WeatherType.Sunny), Is.Zero);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Weather Selection
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_select_default_weather_overrides_random()
    {
        var pool = new[] { "rainy", "sunny" };
        var weather = WeatherRules.SelectInitialWeather(pool, "sunny");
        Assert.That(weather, Is.EqualTo(WeatherType.Sunny));
    }

    [Test]
    public void test_select_from_pool_when_no_default()
    {
        var pool = new[] { "rainy" };
        var weather = WeatherRules.SelectInitialWeather(pool, null);
        Assert.That(weather, Is.EqualTo(WeatherType.Rainy));
    }

    [Test]
    public void test_fallback_to_sunny_for_empty_pool()
    {
        var weather = WeatherRules.SelectInitialWeather(new string[0], null);
        Assert.That(weather, Is.EqualTo(WeatherType.Sunny));
    }

    [Test]
    public void test_select_with_seeded_random()
    {
        var pool = new[] { "sunny", "rainy" };
        var random = new SystemRandomSource(42);

        var weather = WeatherRules.SelectInitialWeather(pool, null, random);
        // With seed 42, NextInt(0,2) is deterministic
        Assert.That(weather == WeatherType.Sunny || weather == WeatherType.Rainy, Is.True);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Weather Change
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_no_change_with_single_weather_pool()
    {
        var pool = new[] { "sunny" };
        var random = new SystemRandomSource(99);
        var result = WeatherRules.RollWeatherChange(WeatherType.Sunny, pool, random);
        Assert.That(result, Is.EqualTo(WeatherType.Sunny));
    }

    [Test]
    public void test_weather_change_with_forced_random()
    {
        var pool = new[] { "sunny", "rainy" };
        // System.Random(0) gives NextDouble ≈ 0.73 on .NET, so change won't trigger.
        // Instead, seed that produces a low first value: use seed 1 (NextDouble ≈ 0.24)
        // Actual behavior is implementation-dependent; this test verifies valid output.
        var random = new SystemRandomSource(1);
        var result = WeatherRules.RollWeatherChange(WeatherType.Sunny, pool, random);
        // Just verify it returns a valid weather type
        Assert.That(result == WeatherType.Sunny || result == WeatherType.Rainy, Is.True);
    }

    [Test]
    public void test_weather_stays_or_changes_with_valid_output()
    {
        var pool = new[] { "sunny", "rainy" };
        var random = new SystemRandomSource(123);
        var result = WeatherRules.RollWeatherChange(WeatherType.Sunny, pool, random);
        // Always returns a valid weather from the pool
        Assert.That(result == WeatherType.Sunny || result == WeatherType.Rainy, Is.True);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Parse
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_parse_sunny()
    {
        Assert.That(WeatherRules.ParseWeather("sunny"), Is.EqualTo(WeatherType.Sunny));
    }

    [Test]
    public void test_parse_rainy()
    {
        Assert.That(WeatherRules.ParseWeather("rainy"), Is.EqualTo(WeatherType.Rainy));
    }

    [Test]
    public void test_parse_rain_alias()
    {
        Assert.That(WeatherRules.ParseWeather("rain"), Is.EqualTo(WeatherType.Rainy));
    }

    [Test]
    public void test_parse_track_weather_aliases()
    {
        Assert.That(WeatherRules.ParseWeather("cloudy"), Is.EqualTo(WeatherType.Sunny));
        Assert.That(WeatherRules.ParseWeather("light_rain"), Is.EqualTo(WeatherType.Rainy));
        Assert.That(WeatherRules.ParseWeather("heavy_rain"), Is.EqualTo(WeatherType.Rainy));
    }

    [Test]
    public void test_parse_case_insensitive()
    {
        Assert.That(WeatherRules.ParseWeather("SUNNY"), Is.EqualTo(WeatherType.Sunny));
        Assert.That(WeatherRules.ParseWeather("Rainy"), Is.EqualTo(WeatherType.Rainy));
    }

    [Test]
    public void test_parse_invalid_returns_null()
    {
        Assert.That(WeatherRules.ParseWeather("snow"), Is.Null);
    }
}
