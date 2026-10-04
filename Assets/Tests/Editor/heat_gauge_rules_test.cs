using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class HeatGaugeRulesTests
{
    [Test]
    public void ExpiredRegionalHeatDoesNotInflateCapacityGauge()
    {
        CardDeck deck = CreateHeatOnlyDeck(6);
        deck.SetRegionalCapacityBonus(2);
        Assert.AreEqual(8, HeatGaugeRules.Evaluate(deck).Capacity);
        Assert.AreEqual(8, deck.DrawHeatFromPoolToHand(8));
        deck.SetRegionalCapacityBonus(0);
        HeatGaugeState expired = HeatGaugeRules.Evaluate(deck);
        Assert.AreEqual(6, expired.Capacity);
        Assert.AreEqual(2, deck.RegionalHeatPendingRetirement);
        Assert.AreEqual(3, deck.CoolHeat(3));
        Assert.AreEqual(1, deck.heatPool.remaining);
        Assert.AreEqual(0, deck.RegionalHeatPendingRetirement);
        Assert.AreEqual(6, HeatGaugeRules.Evaluate(deck).Capacity);
    }

    [Test]
    public void GaugeIncludesCommittedHeatWithoutChangingItsCardType()
    {
        CardDeck deck = CreateHeatOnlyDeck(6);
        deck.SetRegionalCapacityBonus(2);
        deck.DrawHeatFromPoolToHand(1);
        CardData heat = deck.Hand[0];
        deck.RemoveFromHand(new List<CardData> { heat });
        HeatGaugeState active = HeatGaugeRules.Evaluate(deck, new[] { heat });
        Assert.AreEqual(8, active.Capacity);
        Assert.AreEqual(1, active.PermanentHeat);
        deck.SetRegionalCapacityBonus(0);
        HeatGaugeState outside = HeatGaugeRules.Evaluate(deck, new[] { heat });
        Assert.AreEqual(6, outside.Capacity);
        Assert.AreEqual(1, outside.PermanentHeat);
        Assert.IsTrue(heat.IsHeat);
        Assert.AreEqual(0, heat.value);
        deck.DiscardPlayedSpeedCards(new List<CardData> { heat });
        HeatGaugeState retired = HeatGaugeRules.Evaluate(deck, new[] { heat, heat });
        Assert.AreEqual(6, retired.Capacity);
        Assert.AreEqual(1, retired.PermanentHeat,
            "A committed reference still held during cleanup must not count again after discard.");
    }

    [Test]
    public void RegionalCapacityReentryReactivatesOutstandingHeatWithoutStacking()
    {
        CardDeck deck = CreateHeatOnlyDeck(6);
        deck.SetRegionalCapacityBonus(2);
        Assert.AreEqual(8, deck.DrawHeatFromPoolToHand(8));
        deck.SetRegionalCapacityBonus(0);
        Assert.AreEqual(2, deck.RegionalHeatPendingRetirement);
        deck.SetRegionalCapacityBonus(2);
        deck.SetRegionalCapacityBonus(2);
        Assert.AreEqual(0, deck.RegionalHeatPendingRetirement);
        Assert.AreEqual(0, deck.heatPool.remaining);
        Assert.AreEqual(2, deck.CoolHeat(2));
        Assert.AreEqual(2, deck.heatPool.remaining);
        deck.SetRegionalCapacityBonus(0);
        Assert.AreEqual(0, deck.heatPool.remaining);
        Assert.AreEqual(6, HeatGaugeRules.Evaluate(deck).Capacity);
    }

    [Test]
    public void GaugeCountsEveryZoneButTemporaryHeatDoesNotIncreaseCapacity()
    {
        CardDeck deck = CreateDeck(10);
        Assert.AreEqual(4, deck.DrawHeatFromPool(4));
        deck.AddCardsToHand(new List<CardData> { CardData.CreateTempHeat() });

        HeatGaugeState state = HeatGaugeRules.Evaluate(deck);

        Assert.AreEqual(6, state.EngineRemaining);
        Assert.AreEqual(4, state.PermanentHeat);
        Assert.AreEqual(1, state.TemporaryHeat);
        Assert.AreEqual(10, state.Capacity);
        Assert.AreEqual(0.5f, state.Fill01, 0.001f);
        Assert.AreEqual(HeatWarningLevel.Elevated, state.WarningLevel);
    }

    [Test]
    public void GaugeUsesSeventyPercentCriticalThresholdAndDropsAfterCooling()
    {
        CardDeck deck = CreateDeck(10);
        deck.DrawHeatFromPoolToHand(2);
        deck.DrawHeatFromPool(5);

        Assert.AreEqual(HeatWarningLevel.Critical, HeatGaugeRules.Evaluate(deck).WarningLevel);

        Assert.AreEqual(3, deck.CoolHeat(3));
        HeatGaugeState cooled = HeatGaugeRules.Evaluate(deck);

        Assert.AreEqual(0.4f, cooled.Fill01, 0.001f);
        Assert.AreEqual(HeatWarningLevel.Cool, cooled.WarningLevel);
    }

    [Test]
    public void ThermometerBuildsTenSegmentsAndReflectsRuleState()
    {
        GameObject parent = new GameObject("Scoreboard", typeof(RectTransform));
        GameObject textObject = new GameObject(
            "HeatText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent.transform, false);
        TMP_Text source = textObject.GetComponent<TMP_Text>();
        CardDeck deck = CreateDeck(10);
        deck.DrawHeatFromPool(7);

        HeatThermometerUI thermometer = HeatThermometerUI.Attach(source);
        thermometer.Refresh(deck);

        Assert.NotNull(thermometer);
        Assert.AreEqual(HeatThermometerUI.SegmentTotal, thermometer.SegmentCount);
        Assert.AreEqual(70, thermometer.CurrentState.Percent);
        Assert.AreEqual(HeatWarningLevel.Critical, thermometer.CurrentState.WarningLevel);

        Object.DestroyImmediate(parent);
    }

    [Test]
    public void HudRefreshesAuthoredThermometerWhenHeatLabelReferenceIsMissing()
    {
        GameObject hudObject = new GameObject("HUD");
        HUDUI hud = hudObject.AddComponent<HUDUI>();
        GameObject thermometerObject = new GameObject(
            "HeatThermometer", typeof(RectTransform), typeof(CanvasGroup));
        thermometerObject.transform.SetParent(hudObject.transform, false);
        HeatThermometerUI thermometer = thermometerObject.AddComponent<HeatThermometerUI>();

        for (int i = 0; i < HeatThermometerUI.SegmentTotal; i++)
        {
            GameObject segment = new GameObject(
                $"Segment{i + 1:00}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image));
            segment.transform.SetParent(thermometerObject.transform, false);
        }

        PlayerState player = new PlayerState("Player", false, 0, 1);
        player.deck = CreateDeck(10);
        player.deck.DrawHeatFromPool(7);

        hud.RefreshPlayerResources(player);

        Assert.AreSame(thermometer, hud.HeatThermometer);
        Assert.AreEqual(70, thermometer.CurrentState.Percent);
        Assert.AreEqual(3, thermometer.CurrentState.EngineRemaining);
        Assert.AreEqual(HeatWarningLevel.Critical, thermometer.CurrentState.WarningLevel);

        player.deck.CoolHeat(4);
        hud.RefreshPlayerResources(player);
        Assert.AreEqual(30, thermometer.CurrentState.Percent,
            "The authored gauge must reflect a heat-zone mutation on the same refresh call.");
        Assert.AreEqual(7, thermometer.CurrentState.EngineRemaining);

        Object.DestroyImmediate(hudObject);
    }

    private static CardDeck CreateDeck(int poolSize)
    {
        GameConfigSO config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.speedCardDistribution = new int[0];
        var deck = new CardDeck();
        deck.InitializeDeck(config, new HeatPool(poolSize), new SystemRandomSource(17));
        Object.DestroyImmediate(config);
        return deck;
    }

    // Keeps the regional capacity lifecycle tests runnable without scene or
    // ScriptableObject setup; they exercise the same CardDeck and gauge rules.
    private static CardDeck CreateHeatOnlyDeck(int poolSize)
    {
        return new CardDeck { heatPool = new HeatPool(poolSize) };
    }
}
