using System.Collections.Generic;
using NUnit.Framework;

public class EncyclopediaCatalogTests
{
    [Test]
    public void DefaultCatalogLoadsAndCoversEveryRequiredTopic()
    {
        EncyclopediaCatalogData catalog = EncyclopediaCatalog.LoadDefault();

        Assert.That(catalog, Is.Not.Null);
        Assert.That(EncyclopediaCatalog.Validate(catalog), Is.Empty);
        Assert.That(catalog.entries.Length, Is.GreaterThanOrEqualTo(EncyclopediaCatalog.RequiredTopicIds.Length));
    }

    [Test]
    public void EveryEntryHasStableUniqueIdAndReadableContent()
    {
        EncyclopediaCatalogData catalog = EncyclopediaCatalog.LoadDefault();
        var ids = new HashSet<string>();

        foreach (EncyclopediaEntry entry in catalog.entries)
        {
            Assert.That(ids.Add(entry.id), Is.True, $"Duplicate encyclopedia id: {entry.id}");
            Assert.That(entry.category, Is.Not.Empty, entry.id);
            Assert.That(entry.title, Is.Not.Empty, entry.id);
            Assert.That(entry.summary, Is.Not.Empty, entry.id);
            Assert.That(entry.body, Is.Not.Empty, entry.id);
        }
    }

    [Test]
    public void SpecialCardEntryTracesAllRuntimeTrickDefinitions()
    {
        EncyclopediaEntry entry = EncyclopediaCatalog.FindById(
            EncyclopediaCatalog.LoadDefault(), "special-cards");
        TrickCardDatabase database = TrickCardDatabaseFactory.CreateDefault();

        Assert.That(entry, Is.Not.Null);
        Assert.That(entry.relatedRuleIds, Is.EquivalentTo(database.cards.Keys));
        Assert.That(entry.relatedRuleIds, Has.Length.EqualTo(12));
    }

    [Test]
    public void DriverEntryTracesAllRuntimeDriverDefinitions()
    {
        EncyclopediaEntry entry = EncyclopediaCatalog.FindById(
            EncyclopediaCatalog.LoadDefault(), "drivers");
        var driverIds = new List<string>();
        foreach (DriverProfile driver in DriverCatalog.All) driverIds.Add(driver.Id);

        Assert.That(entry, Is.Not.Null);
        Assert.That(entry.relatedRuleIds, Is.EquivalentTo(driverIds));
        Assert.That(entry.relatedRuleIds, Has.Length.EqualTo(12));
        Assert.That(entry.body, Does.Contain("尚未").And.Contain("比赛规则结算"));
    }

    [Test]
    public void WeatherEntryTracesEveryCurrentWeatherProfile()
    {
        EncyclopediaEntry entry = EncyclopediaCatalog.FindById(
            EncyclopediaCatalog.LoadDefault(), "weather");

        Assert.That(entry.relatedRuleIds, Is.EquivalentTo(new[]
        {
            "Sunny", "Cloudy", "LightRain", "HeavyRain", "Hot"
        }));
        Assert.That(entry.body, Does.Contain("多云").And.Contain("大雨").And.Contain("高温"));
    }

    [Test]
    public void ValidationRejectsDuplicateAndMissingRequiredTopics()
    {
        EncyclopediaCatalogData malformed = EncyclopediaCatalog.FromJson(
            "{\"version\":1,\"entries\":[" +
            "{\"id\":\"turn-flow\",\"category\":\"a\",\"title\":\"a\",\"summary\":\"a\",\"body\":\"a\"}," +
            "{\"id\":\"turn-flow\",\"category\":\"b\",\"title\":\"b\",\"summary\":\"b\",\"body\":\"b\"}]}" );

        List<string> errors = EncyclopediaCatalog.Validate(malformed);

        Assert.That(errors, Has.Some.Contains("Duplicate entry id"));
        Assert.That(errors, Has.Some.Contains("Missing required topic"));
    }
}
