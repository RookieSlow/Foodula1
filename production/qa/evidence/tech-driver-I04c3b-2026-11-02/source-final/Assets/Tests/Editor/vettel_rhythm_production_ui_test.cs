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

/// <summary>UI; production/qa/evidence; ADVISORY. Owned production HUD.Start/Manager/real uGUI,
/// dark theme/orthographic Canvas, legal four gears and synthetic roster. No hardware/full-race/mode acceptance.</summary>
public static class VettelRhythmProductionUiCapture
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly FieldInfo Settings = typeof(GameSettingsRuntime).GetField("current", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo Audio = typeof(AudioService).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
    [Serializable] public sealed class Record
    {
        public int width, height, fixture, mask, roster, rosterTeams, catalogTeams, uses, heat;
        public string screenshot, label, body, selectedSource;
        public bool modal, blocked, focusRestored;
        public List<string> overflows = new List<string>();
    }
    [Serializable] private sealed class Manifest
    {
        public string stage, runtimeMvid;
        public string kind = "Owned orthographic production HUD.Start/manager, GraphicRaycaster and EventSystem callbacks on dark RacingTechTheme Canvas. Synthetic four-gear/roster context; not hardware/full RaceCanvas/modes.";
        public List<Record> records = new List<Record>();
    }

    public static string Capture(string stage, bool rewritten)
    {
        if (stage != "before" && stage != "after") throw new ArgumentException("before or after", nameof(stage));
        bool legacyCopy = DriverCatalog.All.First(d => d.Id == "de_sebastian_vettel").ActiveName == "完美节奏";
        if ((stage == "before" && (rewritten || !legacyCopy)) || (stage == "after" && (!rewritten || legacyCopy)))
            throw new InvalidOperationException("Capture stage does not match current production D04 active source; never fabricate old copy.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || ModularRegressionRunner.NativeTestsRunning())
            throw new InvalidOperationException("Idle editor required.");
        string dir = Path.GetFullPath("production/qa/evidence/tech-driver-I04c3-2026-10-29/" + stage + "/hud-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        if (Directory.Exists(dir)) throw new IOException("Capture directory already exists.");
        Directory.CreateDirectory(dir);
        var manifest = new Manifest { stage = stage, runtimeMvid = typeof(RaceSession).Assembly.ManifestModule.ModuleVersionId.ToString() };
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1280, 720) })
            for (int fixture = 0; fixture < (rewritten ? 8 : 1); fixture++)
                manifest.records.Add(Exercise(size.x, size.y, fixture, (int)InRaceConfirmationAction.All, rewritten,
                    Path.Combine(dir, size.x + "x" + size.y + "-" + fixture + ".png")));
        string path = Path.Combine(dir, "manifest.json"); File.WriteAllText(path, JsonUtility.ToJson(manifest, true)); return path;
    }

    public static Record Exercise(int width, int height, int fixture, int mask, bool rewritten, string png = null)
    {
        if (fixture < 0 || fixture > 7) throw new ArgumentOutOfRangeException(nameof(fixture));
        object oldSettings = Settings.GetValue(null), oldAudio = Audio.GetValue(null);
        EventSystem previous = EventSystem.current; GameObject previousSelection = previous != null ? previous.currentSelectedGameObject : null;
        using var startup = new RaceStartupTestScope();
        CareerUiPointerTestScope scope = null; HUDUI hud = null; EventSystem events = null; GameConfigSO config = null;
        try
        {
            var settings = GameSettingsData.CreateDefault(); settings.inRaceConfirmationMask = mask; settings.masterVolume = 0; settings.reduceMotion = true;
            Settings.SetValue(null, settings); Audio.SetValue(null, null);
            scope = CareerUiPointerTestScope.CreateSized(width, height, () => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset"));
            scope.Root.GetComponent<Canvas>().worldCamera.orthographic = true;
            var background = scope.Root.AddComponent<Image>(); background.color = RacingTechTheme.Graphite; background.raycastTarget = false;
            hud = scope.Root.AddComponent<HUDUI>();
            hud.driverSkillButton = Button(scope.Root.transform, scope.Font, "Driver", new Vector2(-500, 0));
            var gears = new GameObject("GearButtons", typeof(RectTransform)); gears.transform.SetParent(scope.Root.transform, false);
            var gearRect = gears.GetComponent<RectTransform>(); gearRect.anchorMin = gearRect.anchorMax = new Vector2(0.5f, 0.5f);
            gearRect.anchoredPosition = new Vector2(500, 0); gearRect.sizeDelta = new Vector2(300, 330);
            hud.gear1Button = Button(gears.transform, scope.Font, "Gear 1", Vector2.zero);
            hud.gear2Button = Button(gears.transform, scope.Font, "Gear 2", Vector2.zero);
            hud.gear3Button = Button(gears.transform, scope.Font, "Gear 3", Vector2.zero);
            hud.gear4Button = Button(gears.transform, scope.Font, "Gear 4", Vector2.zero);
            hud.statusText = Label(scope.Root.transform, scope.Font, "Status", new Vector2(0, -390), new Vector2(1450, 100));
            var handHost = new GameObject("Owned inactive input hand", typeof(RectTransform)); handHost.transform.SetParent(scope.Root.transform, false);
            var hand = handHost.AddComponent<CardHandUI>(); hand.enabled = false;
            hand.playCardsButton = Button(scope.Root.transform, scope.Font, "Play cards", new Vector2(0, -250));
            typeof(HUDUI).GetMethod("Start", Private).Invoke(hud, null);
            // This synchronous EditMode fixture explicitly starts owned behaviours:
            // native Awake does not initialize the attached feedback's base scale here.
            typeof(ButtonClickAnimation).GetMethod("Awake", Private).Invoke(
                hud.driverSkillButton.GetComponent<ButtonClickAnimation>(), null);
            var eventHost = new GameObject("Owned modal EventSystem", typeof(EventSystem)); eventHost.transform.SetParent(scope.Root.transform, false);
            events = eventHost.GetComponent<EventSystem>(); typeof(EventSystem).GetMethod("OnEnable", Private).Invoke(events, null); EventSystem.current = events;
            var host = new GameObject("Owned inactive rhythm manager"); host.transform.SetParent(scope.Root.transform, false); host.SetActive(false);
            var manager = host.AddComponent<MVPGameManager>(); config = ScriptableObject.CreateInstance<GameConfigSO>();
            config.totalLaps = 99; config.enableTechTree = fixture != 0; config.maxGear = 4; manager.config = config; manager.hudUI = hud; manager.cardHandUI = hand;
            manager.trackManager = host.AddComponent<TrackManager>(); manager.trackManager.config = config;
            Set(manager.trackManager, "nodes", Enumerable.Range(0, 60).Select(i => new TrackNode(i, 99, isStartFinish: i == 0)).ToList());
            var race = new RaceSession(new SystemRandomSource(97)) { TeamVehicleBonusesEnabled = true };
            var player = new PlayerState("德国队·长中文维特尔1234567890", false, 0, 3) { teamId = TeamId.DE, driverId = "de_sebastian_vettel", techState = new TechTreeState(TeamId.DE) };
            player.driverSkill.Initialize(player.DriverProfile, 5, true);
            player.deck.InitializeExactOrder(Enumerable.Range(0, 20).Select(_ => new CardData(CardType.Speed, 1)).ToArray(), new HeatPool(8)); player.deck.DrawToHand(8);
            race.Players.Add(player);
            int roster = new[] { 2, 4, 6, 12 }[fixture % 4];
            foreach (var driver in DriverCatalog.All.Where(d => d.Id != player.driverId).Take(roster - 1))
                race.Players.Add(new PlayerState(driver.ShortName, true, 20 + race.Players.Count, 1) { teamId = driver.Team, driverId = driver.Id });
            Set(manager, "session", race);
            race.BeginTurn(player, 1); race.BeginDriverSkillInput(player, 60); Set(manager, "raceTurnNumber", 1);
            Get<RacePhaseState>(manager, "phaseState").BeginGearSelection(); Get<RaceInputState>(manager, "inputState").BeginGearSelection(3);
            hud.SetGameManager(manager); hud.RefreshDriverSkill(manager, player); hud.RefreshTeamGearPresentation(); events.SetSelectedGameObject(hud.driverSkillButton.gameObject);
            var record = new Record { width = width, height = height, fixture = fixture, mask = mask, roster = roster,
                rosterTeams = race.Players.Select(p => p.teamId).Distinct().Count(), catalogTeams = DriverCatalog.All.Select(d => d.Team).Distinct().Count(),
                screenshot = png, uses = player.driverSkill.UsesRemaining, heat = player.deck.heatPool.remaining,
                label = hud.driverSkillButton.GetComponentInChildren<TMP_Text>().text };
            Label(scope.Root.transform, scope.Font, "Owned context", new Vector2(0, 410), new Vector2(1450, 100)).text =
                "生产HUD受控夹具 · " + player.name + " · 参赛车" + roster + " · 参赛队" + record.rosterTeams + " · 车队目录" + record.catalogTeams;
            if (!rewritten)
            {
                // Capture only current legacy production copy; no click, resources or old behavior acceptance.
                Render(scope, hud, record); return record;
            }
            if (fixture == 1) player.techState.activeNodeIds.UnionWith(new[] { "de-l1-schwarzbier-fuel", "de-l2-wurstplatte" });
            if (fixture == 4) player.deck.DrawHeatFromPoolToHand(8);
            if (fixture == 5) player.driverSkill.Initialize(player.DriverProfile, 2, true);
            hud.RefreshDriverSkill(manager, player);
            record.label = hud.driverSkillButton.GetComponentInChildren<TMP_Text>().text;
            int beforeHeat = player.deck.heatPool.remaining, beforeUses = player.driverSkill.UsesRemaining;
            if (fixture == 4 || fixture == 5)
            {
                Assert.That(hud.driverSkillButton.interactable, Is.False);
                if (fixture == 5) Assert.That(record.label, Does.Contain("3级解锁"));
                manager.OnDriverSkillButtonClicked(); // A direct disabled command still cannot fall through to the legacy path.
                Assert.That(player.driverSkill.IsActive, Is.False);
                Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
                Assert.That(player.deck.heatPool.remaining, Is.EqualTo(beforeHeat));
                Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(beforeUses));
                Render(scope, hud, record); return record;
            }
            Assert.That(hud.driverSkillButton.interactable, Is.True);
            PointerClick(events, scope.Root.GetComponent<GraphicRaycaster>(), hud.driverSkillButton);
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True, "One required source/fee surface even with mask0.");
            var row = hud.GetComponentInChildren<DriverSkillCardChoiceUI>(true);
            Assert.That(row, Is.Not.Null); Assert.That(row.Selected, Is.Null, "A source is never a synthetic speed card.");
            Assert.That(row.SelectedSource, Is.EqualTo(VettelNativeSource.BasicCruise));
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(beforeHeat));
            Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(beforeUses));
            var confirm = hud.confirmReturnToMenuButton;
            var cancel = hud.cancelReturnToMenuButton;
            var next = Get<Button>(row, "nextButton");
            record.modal = true; record.body = hud.returnToMenuConfirmText.text;
            record.label = hud.driverSkillButton.GetComponentInChildren<TMP_Text>().text;
            record.blocked = !CanReceivePointer(events, scope.Root.GetComponent<GraphicRaycaster>(), hud.driverSkillButton);
            Assert.That(record.blocked, Is.True); Assert.That(confirm.interactable, Is.True);
            if (fixture == 1)
            {
                PointerClick(events, scope.Root.GetComponent<GraphicRaycaster>(), next);
                Assert.That(row.SelectedSource, Is.EqualTo(VettelNativeSource.PaidBeer));
                PointerClick(events, scope.Root.GetComponent<GraphicRaycaster>(), next);
                Assert.That(row.SelectedSource, Is.EqualTo(VettelNativeSource.GermanCornerExit));
                Assert.That(next.interactable, Is.False); Assert.That(events.currentSelectedGameObject, Is.Not.EqualTo(next.gameObject));
            }
            record.selectedSource = row.SelectedSource.ToString();
            if (fixture != 3) Render(scope, hud, record); // Modal screenshot precedes the explicitly asserted commit/cancel below.
            var capturedPlan = Get<VettelRhythmActivationPlan>(manager, "pendingVettelRhythmPlan");
            if (fixture == 2)
            {
                events.SetSelectedGameObject(cancel.gameObject);
                ExecuteEvents.Execute(cancel.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
                Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
            }
            else if (fixture == 7)
            {
                typeof(DriverSkillCardChoiceUI).GetMethod("OnDisable", Private).Invoke(row, null);
                Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
            }
            else
            {
                if (fixture == 6) player.deck.Hand[0].value = 2; // Exact-plan snapshot invalidation, local owned card only.
                events.SetSelectedGameObject(confirm.gameObject);
                ExecuteEvents.Execute(confirm.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
                Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False, "Never a second extra confirmation.");
            }
            bool committed = fixture != 2 && fixture != 6 && fixture != 7;
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(beforeHeat - (committed ? 1 : 0)));
            Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(beforeUses - (committed ? 1 : 0)));
            Assert.That(player.driverSkill.Rhythm.HasWindow, Is.EqualTo(committed));
            Assert.That(capturedPlan.IsClosed, Is.True);
            if (committed)
            {
                Assert.That(player.deck.ContainsInHand(capturedPlan.PaidHeat), Is.True);
                Assert.That(player.driverSkill.Rhythm.LockedSource.ToString(), Is.EqualTo(record.selectedSource));
                Assert.That(player.driverSkill.Rhythm.RemainingInputs, Is.EqualTo(2));
            }
            GameObject focused = events.currentSelectedGameObject;
            record.focusRestored = new[] { hud.driverSkillButton, hud.gear1Button, hud.gear2Button, hud.gear3Button, hud.gear4Button }
                .Any(button => focused == button.gameObject && button.isActiveAndEnabled && button.IsInteractable());
            Assert.That(record.focusRestored, Is.True, "Return to available skill or legal gear when the committed skill is disabled.");
            hud.ConfirmReturnToMenu();
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(beforeHeat - (committed ? 1 : 0)), "Repeated confirm cannot charge again.");
            if (fixture == 3)
            {
                hud.RefreshDriverSkill(manager, player); record.modal = false;
                record.label = hud.driverSkillButton.GetComponentInChildren<TMP_Text>().text;
                Assert.That(record.label, Does.Contain("锁巡航").And.Contain("窗口余2输入"));
                Render(scope, hud, record);
            }
            return record;
        }
        finally
        {
            if (hud != null) hud.SetGameManager(null);
            if (events != null) typeof(EventSystem).GetMethod("OnDisable", Private).Invoke(events, null);
            scope?.Dispose(); if (config != null) Object.DestroyImmediate(config); Settings.SetValue(null, oldSettings); Audio.SetValue(null, oldAudio);
            if (previous != null && previous.isActiveAndEnabled) { EventSystem.current = previous; if (previousSelection != null && previousSelection.activeInHierarchy) previous.SetSelectedGameObject(previousSelection); }
        }
    }
    private static bool CanReceivePointer(EventSystem events, GraphicRaycaster raycaster, Button button)
    {
        // Match CareerUiPointerTestScope.Click: newly created graphics have no live
        // hit depth until the owned layer/canvas/text geometry has actually rendered.
        foreach (Transform child in raycaster.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in raycaster.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        Camera camera = raycaster.GetComponent<Canvas>().worldCamera;
        RenderTexture previous = RenderTexture.active;
        try { camera.Render(); }
        finally { RenderTexture.active = previous; }
        Assert.That(button.targetGraphic.depth, Is.GreaterThanOrEqualTo(0), button.name + " must render before hit testing.");
        Vector3[] corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
        var canvas = raycaster.GetComponent<Canvas>();
        var data = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, (corners[0] + corners[2]) * 0.5f) };
        var hits = new List<RaycastResult>(); raycaster.Raycast(data, hits);
        return hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform));
    }
    private static void PointerClick(EventSystem events, GraphicRaycaster raycaster, Button button)
    {
        Assert.That(CanReceivePointer(events, raycaster, button), Is.True, button.name + " must own its real raycast.");
        RectTransform rect = button.GetComponent<RectTransform>();
        var data = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(
            raycaster.GetComponent<Canvas>().worldCamera, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
        Vector3 originalScale = button.transform.localScale;
        Assert.That(originalScale.sqrMagnitude, Is.GreaterThan(0f), button.name + " must have a visible owned scale.");
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        Assert.That(button.transform.localScale, Is.EqualTo(originalScale), button.name + " must restore its scale after pointer release.");
    }
    private static void Render(CareerUiPointerTestScope scope, HUDUI hud, Record record)
    {
        var shared = new MansellSkillProductionUiCapture.Record();
        typeof(MansellSkillProductionUiCapture).GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { scope, hud, record.screenshot, shared });
        record.overflows.AddRange(shared.overflows);
    }
    private static Button Button(Transform parent, TMP_FontAsset font, string name, Vector2 position)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); host.transform.SetParent(parent, false);
        var rect = host.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = new Vector2(420, 110);
        host.GetComponent<Image>().color = RacingTechTheme.Surface;
        var button = host.GetComponent<Button>(); button.targetGraphic = host.GetComponent<Image>();
        Label(host.transform, font, "Label", Vector2.zero, rect.sizeDelta).text = name; return button;
    }
    private static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, Vector2 position, Vector2 size)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); host.transform.SetParent(parent, false);
        var text = host.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 20; text.color = RacingTechTheme.Primary; text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false; text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size; return text;
    }
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
}

public class VettelRhythmProductionUiTests
{
    private static IEnumerable<TestCaseData> Cases()
    {
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1280, 720) })
            for (int fixture = 0; fixture < 8; fixture++)
                foreach (int mask in new[] { 0, (int)InRaceConfirmationAction.All }) yield return new TestCaseData(size.x, size.y, fixture, mask);
    }
    [TestCaseSource(nameof(Cases))]
    public void ActualSourceModalIsAtomicAndRetainsFocusAndMask(int w, int h, int fixture, int mask)
    {
        var result = VettelRhythmProductionUiCapture.Exercise(w, h, fixture, mask, true);
        Assert.That(result.overflows, Is.Empty);
    }
    [TestCase(1920,1080)] [TestCase(2560,1440)] [TestCase(1280,720)]
    public void ReworkedActiveProfileFitsWithoutAcceptingOtherExistingOverflows(int w, int h)
    {
        var record = SchumacherPassiveProfileUiCapture.Exercise(w, h, true);
        Assert.That(DriverCatalog.All.First(d => d.Id == "de_sebastian_vettel").ActiveName, Is.EqualTo("人车合拍"));
        Assert.That(record.allOverflows.Where(s => s.Contains("de_sebastian_vettel/Active")), Is.Empty);
    }
}
