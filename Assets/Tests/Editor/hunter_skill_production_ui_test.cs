using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Actual manager cost plan and production HUD on an owned preview Canvas. No match, hardware input, or storage.</summary>
public static class HunterSkillProductionUiCapture
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Settings = typeof(GameSettingsRuntime).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly string[] Fixtures = { "normal", "equal-second", "long-copy", "keyboard", "cancel-reopen", "unavailable" };

    [Serializable] public sealed class Record
    {
        public int width, height, roster, mask, usesAfter;
        public string fixture, targetTeam, text, screenshot;
        public bool modal, blockedUnderlyingPointer, exactCost, focusRestored;
        public List<string> overflows = new List<string>();
    }
    [Serializable] private sealed class Manifest
    {
        public string stage, utc, runtimeMvid;
        public string kind = "Owned EditMode preview Canvas; real HUD.Start, driver button and MVP manager plan. EventSystem/GraphicRaycaster dispatch, not hardware input or a running match.";
        public List<Record> records = new List<Record>();
    }

    public static string Capture(string stage)
    {
        if (stage != "before" && stage != "after") throw new ArgumentException(nameof(stage));
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || ModularRegressionRunner.NativeTestsRunning())
            throw new InvalidOperationException("Capture requires idle EditMode.");
        string directory = Path.GetFullPath("production/qa/evidence/tech-driver-I01f-2026-10-24/" + stage + "/run-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        if (Directory.Exists(directory)) throw new IOException("Capture directory already exists.");
        Directory.CreateDirectory(directory);
        var manifest = new Manifest { stage = stage, utc = DateTime.UtcNow.ToString("o"), runtimeMvid = typeof(MVPGameManager).Assembly.ManifestModule.ModuleVersionId.ToString() };
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1280, 720) })
            for (int index = 0; index < Fixtures.Length; index++)
                manifest.records.Add(Exercise(size.x, size.y, index, (int)InRaceConfirmationAction.All, stage == "after",
                    Path.Combine(directory, size.x + "x" + size.y + "-" + Fixtures[index] + ".png")));
        string path = Path.Combine(directory, "manifest.json");
        File.WriteAllText(path, JsonUtility.ToJson(manifest, true));
        return path;
    }

    public static Record Exercise(int width, int height, int index, int mask, bool requireRisk, string screenshot = null)
    {
        object previousSettings = Settings.GetValue(null);
        var audioField = typeof(AudioService).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousAudio = audioField.GetValue(null);
        using var startup = new RaceStartupTestScope();
        EventSystem previousEvents = EventSystem.current;
        GameObject previousSelection = previousEvents != null ? previousEvents.currentSelectedGameObject : null;
        CareerUiPointerTestScope scope = null;
        EventSystem events = null;
        HUDUI hud = null;
        GameConfigSO config = null;
        try
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.inRaceConfirmationMask = mask; settings.reduceMotion = true; settings.masterVolume = 0;
            Settings.SetValue(null, settings);
            audioField.SetValue(null, null); // Do not play or mutate the user's existing audio service.
            scope = CareerUiPointerTestScope.CreateSized(width, height, () => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset"));
            hud = scope.Root.AddComponent<HUDUI>();
            hud.driverSkillButton = Button(scope.Root.transform, scope.Font, "Owned actual driver entry", -500);
            var gearHost = new GameObject("GearButtons", typeof(RectTransform));
            gearHost.transform.SetParent(scope.Root.transform, false);
            var gearRect = gearHost.GetComponent<RectTransform>();
            gearRect.anchorMin = gearRect.anchorMax = new Vector2(0.5f, 0.5f);
            gearRect.anchoredPosition = new Vector2(500, 0); gearRect.sizeDelta = new Vector2(300, 330);
            hud.gear1Button = Button(gearHost.transform, scope.Font, "Owned legal gear return", 0);
            hud.statusText = Label(scope.Root.transform, scope.Font, "Owned actual status", new Vector2(0, -390), new Vector2(1400, 100));
            typeof(HUDUI).GetMethod("Start", Private).Invoke(hud, null);
            var eventHost = new GameObject("Owned active modal EventSystem", typeof(EventSystem));
            eventHost.transform.SetParent(scope.Root.transform, false);
            events = eventHost.GetComponent<EventSystem>();
            typeof(EventSystem).GetMethod("OnEnable", Private).Invoke(events, null);
            EventSystem.current = events;
            var host = new GameObject("Owned inactive production manager");
            host.SetActive(false); host.transform.SetParent(scope.Root.transform, false);
            MVPGameManager manager = host.AddComponent<MVPGameManager>();
            config = ScriptableObject.CreateInstance<GameConfigSO>(); config.enableTechTree = false;
            manager.config = config; manager.hudUI = hud;
            manager.trackManager = host.AddComponent<TrackManager>();
            typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager,
                Enumerable.Range(0, 60).Select(i => new TrackNode(i, 99, isStartFinish: i == 0)).ToList());
            var race = new RaceSession(new SystemRandomSource(1)) { TeamVehicleBonusesEnabled = false };
            var owner = new PlayerState("哈特", false, 0, 1) { teamId = TeamId.UK, driverId = "uk_hunter_hart" };
            owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
            var cards = new[] { new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 4) };
            owner.deck.InitializeExactOrder(cards, new HeatPool(8)); owner.deck.DrawToHand(3); race.Players.Add(owner);
            TeamId team = new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP }[index];
            DriverProfile targetProfile = DriverCatalog.All.First(d => d.Team == team && d.Id != owner.driverId);
            string targetName = index == 2 ? "意大利队·长中文车手名称1234567890" : targetProfile.ShortName;
            var target = new PlayerState(targetName, true, index == 5 ? 20 : 3, 1) { teamId = team, driverId = targetProfile.Id };
            race.Players.Add(target);
            int roster = new[] { 2, 4, 6, 12, 4, 2 }[index];
            foreach (var driver in DriverCatalog.All.Where(d => d.Id != owner.driverId && d.Id != target.driverId).Take(roster - 2))
                race.Players.Add(new PlayerState(driver.ShortName, true, 30 + race.Players.Count, 1) { teamId = driver.Team, driverId = driver.Id });
            Assert.That(race.Players.Count, Is.EqualTo(roster));
            typeof(MVPGameManager).GetField("session", Private).SetValue(manager, race);
            typeof(MVPGameManager).GetField("raceTurnNumber", Private).SetValue(manager, 1);
            ((RacePhaseState)typeof(MVPGameManager).GetField("phaseState", Private).GetValue(manager)).BeginGearSelection();
            ((RaceInputState)typeof(MVPGameManager).GetField("inputState", Private).GetValue(manager)).BeginGearSelection(1);
            race.BeginTurn(owner); race.BeginDriverSkillInput(owner, 60);
            hud.SetGameManager(manager); hud.RefreshDriverSkill(manager, owner); hud.RefreshTeamGearPresentation();
            events.SetSelectedGameObject(hud.driverSkillButton.gameObject);
            var record = new Record { width = width, height = height, fixture = Fixtures[index], roster = race.Players.Count, mask = mask, targetTeam = team.ToString(), screenshot = screenshot };
            Action open = () => scope.Click(hud.driverSkillButton);
            if (index == 5)
            {
                Assert.That(hud.driverSkillButton.IsInteractable(), Is.False);
                record.text = hud.driverSkillButton.GetComponentInChildren<TMP_Text>().text;
                Assert.That(record.text, Does.Contain("前方"));
                record.modal = false; Render(scope, hud, screenshot, record);
                Assert.That(owner.deck.HandCount, Is.EqualTo(3));
                record.usesAfter = owner.driverSkill.UsesRemaining;
                return record;
            }
            open();
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(hud.cancelReturnToMenuButton.gameObject));
            if (index == 3 || index == 4)
            {
                if (index == 3) Submit(events);
                else scope.Click(hud.cancelReturnToMenuButton);
                Assert.That(owner.deck.HandCount, Is.EqualTo(3));
                Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(3));
                Assert.That(events.currentSelectedGameObject, Is.SameAs(hud.driverSkillButton.gameObject));
                open();
            }
            if (index == 1) scope.Click(Find(hud, "DriverSkillChoiceNextButton").GetComponent<Button>());
            record.text = hud.returnToMenuConfirmText.text;
            Assert.That(record.text, Does.Contain("目标：").And.Contain("剩余"));
            if (requireRisk) Assert.That(record.text, Does.Contain("新增弯心").And.Contain("原速度").And.Contain("不足会失控"));
            Assert.That(owner.deck.HandCount, Is.EqualTo(3)); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(3));
            record.modal = true;
            record.blockedUnderlyingPointer = UnderlyingBlocked(scope, hud.driverSkillButton, hud.returnToMenuConfirmPanel);
            Assert.That(record.blockedUnderlyingPointer, Is.True);
            Render(scope, hud, screenshot, record);
            if (index == 3)
            {
                ExecuteEvents.Execute(events.currentSelectedGameObject, new AxisEventData(events) { moveDir = MoveDirection.Right }, ExecuteEvents.moveHandler);
                Assert.That(events.currentSelectedGameObject, Is.SameAs(hud.confirmReturnToMenuButton.gameObject));
                Submit(events);
            }
            else scope.Click(hud.confirmReturnToMenuButton);
            CardData cost = cards[index == 1 ? 1 : 0];
            record.exactCost = !owner.deck.ContainsInHand(cost) && owner.deck.DiscardPile.Contains(cost);
            Assert.That(record.exactCost, Is.True);
            Assert.That(owner.deck.HandCount, Is.EqualTo(2)); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
            Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(8), "Confirmation spends the card, never forecast corner heat.");
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
            record.focusRestored = events.currentSelectedGameObject == hud.gear1Button.gameObject;
            Assert.That(record.focusRestored, Is.True, "Successful activation returns to the legal gear control.");
            hud.ConfirmReturnToMenu();
            Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2)); record.usesAfter = owner.driverSkill.UsesRemaining;
            return record;
        }
        finally
        {
            if (hud != null) hud.SetGameManager(null);
            if (events != null) typeof(EventSystem).GetMethod("OnDisable", Private).Invoke(events, null);
            scope?.Dispose();
            if (config != null) Object.DestroyImmediate(config);
            Settings.SetValue(null, previousSettings);
            audioField.SetValue(null, previousAudio);
            if (previousEvents != null && previousEvents.isActiveAndEnabled)
            {
                EventSystem.current = previousEvents;
                if (previousSelection != null && previousSelection.activeInHierarchy) previousEvents.SetSelectedGameObject(previousSelection);
            }
        }
    }

    private static bool UnderlyingBlocked(CareerUiPointerTestScope scope, Button lower, GameObject overlay)
    {
        Render(scope, null, null, null);
        var rect = lower.GetComponent<RectTransform>();
        Vector2 center = RectTransformUtility.WorldToScreenPoint(scope.Root.GetComponent<Canvas>().worldCamera, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(scope.EventSystem) { position = center };
        var hits = new List<RaycastResult>(); scope.Root.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
        return hits.Count > 0 && hits[0].gameObject == overlay;
    }

    private static void Render(CareerUiPointerTestScope scope, HUDUI hud, string path, Record record)
    {
        foreach (Transform child in scope.Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in scope.Root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        if (hud != null && record != null)
            foreach (TMP_Text text in (hud.IsReturnToMenuConfirmationVisible ? hud.returnToMenuConfirmPanel : scope.Root).GetComponentsInChildren<TMP_Text>())
                if (text.isTextOverflowing) record.overflows.Add(text.name + ": " + text.text);
        RenderTexture old = RenderTexture.active;
        Texture2D image = null;
        try
        {
            scope.Root.GetComponent<Canvas>().worldCamera.Render();
            if (path == null) return;
            RenderTexture.active = scope.Target;
            image = new Texture2D(scope.Target.width, scope.Target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, scope.Target.width, scope.Target.height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = old; if (image != null) Object.DestroyImmediate(image); }
    }

    private static Button Button(Transform parent, TMP_FontAsset font, string name, float x)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); host.transform.SetParent(parent, false);
        var rect = host.GetComponent<RectTransform>(); rect.anchoredPosition = new Vector2(x, 0); rect.sizeDelta = new Vector2(420, 110);
        Button button = host.GetComponent<Button>(); button.targetGraphic = host.GetComponent<Image>();
        Label(host.transform, font, "Label", Vector2.zero, rect.sizeDelta).text = name;
        return button;
    }
    private static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, Vector2 position, Vector2 size)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); host.transform.SetParent(parent, false);
        var text = host.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 20; text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size; text.raycastTarget = false; return text;
    }
    private static Transform Find(HUDUI hud, string name) => hud.returnToMenuConfirmPanel.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
    private static void Submit(EventSystem events) => ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
}

public class HunterSkillProductionUiTests
{
    private static IEnumerable<TestCaseData> Cases()
    {
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1280, 720) })
            for (int index = 0; index < 6; index++)
                foreach (int mask in new[] { 0, (int)InRaceConfirmationAction.All })
                    yield return new TestCaseData(size.x, size.y, index, mask);
    }
    [TestCaseSource(nameof(Cases))]
    public void RealManagerPromptFitsAndCommitsOrCancelsAtThreeSizes(int width, int height, int fixture, int mask)
    {
        var result = HunterSkillProductionUiCapture.Exercise(width, height, fixture, mask, true);
        Assert.That(result.overflows, Is.Empty);
    }
}
