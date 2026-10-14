using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Native player-loop check with real RaceCanvas/CardPrefab callbacks and isolated state.</summary>
public class LiveTurnTelemetryRuntimeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator AuthoredHudRefreshesBeforeConfirmAndRestoresRealHeatAfterInputCloses()
    {
        yield return new EnterPlayMode();
        Scene scope = SceneManager.CreateScene("Live turn telemetry test");
        GameObject host = null;
        TMP_FontAsset font = null;
        Material material = null;
        GameConfigSO config = null;
        GameSettingsData previousSettings = GameSettingsRuntime.Current;
        var settingsField = typeof(GameSettingsRuntime).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
        settingsField.SetValue(null, new GameSettingsData { inRaceConfirmationMask = 0 });
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");
            bool dirty = EditorUtility.IsDirty(source);
            font = Object.Instantiate(source);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            material = Object.Instantiate(source.material); font.material = material;
            host = new GameObject("Inactive telemetry fixture"); host.SetActive(false);
            SceneManager.MoveGameObjectToScene(host, scope);
            var canvas = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/RaceCanvas.prefab"), host.transform, false);
            var hud = canvas.GetComponentInChildren<HUDUI>(true);
            var hand = canvas.GetComponentInChildren<CardHandUI>(true);
            var templates = new GameObject("Inactive card template"); templates.SetActive(false);
            templates.transform.SetParent(host.transform, false);
            var template = Object.Instantiate(hand.cardPrefab, templates.transform, false);
            SetFont(template, font, material); hand.cardPrefab = template;
            var managerHost = new GameObject("Inactive telemetry manager"); managerHost.SetActive(false);
            managerHost.transform.SetParent(host.transform, false);
            var manager = managerHost.AddComponent<MVPGameManager>();
            config = ScriptableObject.CreateInstance<GameConfigSO>(); config.enableTechTree = false;
            manager.config = config; manager.hudUI = hud; manager.cardHandUI = hand;
            var player = new PlayerState("预览", false, 0, 1) { teamId = TeamId.UK };
            player.deck.heatPool = new HeatPool(6);
            var four = new CardData(CardType.Speed, 4);
            var two = new CardData(CardType.Speed, 2);
            player.deck.AddCardsToHand(new[] { four, two, new CardData(CardType.Speed, 1) });
            player.deck.DrawHeatFromPoolToHand(2);
            var session = new RaceSession(new SystemRandomSource(1)) { TeamVehicleBonusesEnabled = false };
            session.Players.Add(player);
            typeof(MVPGameManager).GetField("session", Private).SetValue(manager, session);
            var phase = (RacePhaseState)typeof(MVPGameManager).GetField("phaseState", Private).GetValue(manager);
            var input = (RaceInputState)typeof(MVPGameManager).GetField("inputState", Private).GetValue(manager);
            phase.BeginGearSelection(); input.BeginGearSelection(1);
            SetFont(canvas, font, material);
            typeof(HUDUI).GetMethod("Start", Private).Invoke(hud, null);
            hud.SetGameManager(manager); hud.ConfigureGearPresentation(TeamId.UK, 1);
            hud.SetTeamGearControlsInteractable(true);
            var layout = canvas.GetComponent<RaceUILayoutController>();
            if (layout == null) layout = canvas.AddComponent<RaceUILayoutController>();
            layout.ApplyLayout(hud, hand);
            hand.ShowHand(manager, player); hand.SetGearSelectionMode(true);
            SetFont(canvas, font, material);
            host.SetActive(true);
            yield return null; // Run real Awake/Start/TMP/card animation updates.
            hud.gear3Button.onClick.Invoke();
            var telemetry = hud.TeamGearPresentation.Telemetry;
            Assert.That(telemetry.LivePreview.Gear, Is.EqualTo(3));
            Assert.That(telemetry.MovementText, Does.Contain("手牌试算 7"));
            Assert.That(hud.HeatThermometer.CurrentState.TotalHeat, Is.EqualTo(2));
            Assert.That(hud.HeatThermometer.ForecastState.Value.TotalHeat, Is.EqualTo(3));
            Assert.That(player.gear, Is.EqualTo(1));
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
            player.gear = 2; phase.BeginCardSelection(); input.BeginCardSelection();
            hand.SetGearSelectionMode(false);
            var cards = hand.handContainer.GetComponentsInChildren<CardUI>();
            CardUI first = System.Array.Find(cards, c => c.cardData == four);
            CardUI second = System.Array.Find(cards, c => c.cardData == two);
            first.OnCardClicked();
            Assert.That(telemetry.MovementText, Does.Contain("预计 4"));
            Assert.That(telemetry.ForecastHeatState.TotalHeat, Is.EqualTo(2), "Includes one missing mandatory card.");
            second.OnCardClicked();
            Assert.That(telemetry.MovementText, Does.Contain("预计 6"));
            hud.RefreshPlayerResources(player); // A normal resource pass must not erase the thermometer forecast.
            Assert.That(hud.HeatThermometer.ForecastState.Value.TotalHeat, Is.EqualTo(1));
            first.OnCardClicked();
            Assert.That(telemetry.MovementText, Does.Contain("预计 2"));
            Assert.That(telemetry.ForecastHeatState.TotalHeat, Is.EqualTo(2));
            Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
            Assert.That(player.turnMovement.Entries, Is.Empty);
            input.EndCardSelection(); hud.RefreshTurnTelemetry(player);
            Assert.That(telemetry.LivePreview, Is.Null);
            Assert.That(hud.HeatThermometer.ForecastState, Is.Null);
            Assert.That(hud.HeatThermometer.CurrentState.TotalHeat, Is.EqualTo(2));
            Assert.That(EditorUtility.IsDirty(source), Is.EqualTo(dirty));
        }
        finally
        {
            if (host != null) Object.DestroyImmediate(host);
            if (config != null) Object.DestroyImmediate(config);
            if (font != null) Object.DestroyImmediate(font);
            if (material != null) Object.DestroyImmediate(material);
            settingsField.SetValue(null, previousSettings);
        }
        yield return SceneManager.UnloadSceneAsync(scope);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditMode()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    private static void SetFont(GameObject root, TMP_FontAsset font, Material material)
    {
        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            label.font = font; label.fontSharedMaterial = material;
        }
    }
}
