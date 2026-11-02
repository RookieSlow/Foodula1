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

/// <summary>Owned production HUD/manager integration, synthetic roster, no real match or storage acceptance.</summary>
public static class SchumacherStrategyProductionUiCapture
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly FieldInfo Settings = typeof(GameSettingsRuntime).GetField("current", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo Audio = typeof(AudioService).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
    [Serializable] public sealed class Record
    {
        public int width, height, fixture, mask, roster, uses, heat;
        public string screenshot, label, body;
        public bool modal, blocked, focusRestored;
        public List<string> overflows = new List<string>();
    }
    [Serializable] private sealed class Manifest
    {
        public string stage, runtimeMvid;
        public string kind = "Owned orthographic Canvas, real HUD.Start button/GraphicRaycaster/Submit and manager versioned transactions; not hardware/full RaceCanvas/modes.";
        public List<Record> records = new List<Record>();
    }

    /// <summary>Before uses current legacy entry, after uses actual current strategy entry; never fabricates old copy.</summary>
    public static string Capture(string stage)
    {
        bool rewritten = DriverCatalog.All.First(d => d.Id == "de_michael_schumacher").ActiveName == "三段战术";
        if ((stage == "before" && rewritten) || (stage == "after" && !rewritten)) throw new InvalidOperationException("Wrong source stage.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || ModularRegressionRunner.NativeTestsRunning()) throw new InvalidOperationException("Idle editor required.");
        string dir = Path.GetFullPath("production/qa/evidence/tech-driver-I04b2-2026-10-29/" + stage + "/hud-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        Directory.CreateDirectory(dir);
        var manifest = new Manifest { stage = stage, runtimeMvid = typeof(RaceSession).Assembly.ManifestModule.ModuleVersionId.ToString() };
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1280, 720) })
            for (int i = 0; i < (rewritten ? 8 : 1); i++)
                manifest.records.Add(Exercise(size.x, size.y, i, (int)InRaceConfirmationAction.All, rewritten, Path.Combine(dir, size.x + "x" + size.y + "-" + i + ".png")));
        string path = Path.Combine(dir, "manifest.json"); File.WriteAllText(path, JsonUtility.ToJson(manifest, true)); return path;
    }

    /// <summary>Each scenario drives current real callbacks and verifies payment/uses/modal ownership.</summary>
    public static Record Exercise(int width, int height, int fixture, int mask, bool rewritten, string png = null)
    {
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
            hud = scope.Root.AddComponent<HUDUI>();
            hud.driverSkillButton = Button(scope.Root.transform, scope.Font, "Driver", new Vector2(-500, 0));
            var gears = new GameObject("GearButtons", typeof(RectTransform)); gears.transform.SetParent(scope.Root.transform, false);
            var gearRect = gears.GetComponent<RectTransform>(); gearRect.anchorMin = gearRect.anchorMax = new Vector2(0.5f, 0.5f);
            gearRect.anchoredPosition = new Vector2(500, 0); gearRect.sizeDelta = new Vector2(300, 330);
            hud.gear1Button = Button(gears.transform, scope.Font, "Gear 1", Vector2.zero);
            hud.gear2Button = Button(gears.transform, scope.Font, "Gear 2", Vector2.zero);
            hud.gear3Button = Button(gears.transform, scope.Font, "Gear 3", Vector2.zero);
            hud.gear4Button = Button(gears.transform, scope.Font, "Gear 4", Vector2.zero);
            hud.statusText = Label(scope.Root.transform, scope.Font, "Status", new Vector2(0, -390), new Vector2(1400, 100));
            typeof(HUDUI).GetMethod("Start", Private).Invoke(hud, null);
            var eventHost = new GameObject("Owned EventSystem", typeof(EventSystem)); eventHost.transform.SetParent(scope.Root.transform, false);
            events = eventHost.GetComponent<EventSystem>(); typeof(EventSystem).GetMethod("OnEnable", Private).Invoke(events, null); EventSystem.current = events;
            var host = new GameObject("Owned inactive strategy manager"); host.transform.SetParent(scope.Root.transform, false); host.SetActive(false);
            var gm = host.AddComponent<MVPGameManager>(); config = ScriptableObject.CreateInstance<GameConfigSO>(); config.totalLaps = 99; config.enableTechTree = false; config.maxGear = 4; gm.config = config; gm.hudUI = hud;
            gm.trackManager = host.AddComponent<TrackManager>();
            Set(gm.trackManager, "nodes", Enumerable.Range(0, 60).Select(i => new TrackNode(i, 99, isStartFinish: i == 0)).ToList());
            var race = new RaceSession(new SystemRandomSource(9)) { TeamVehicleBonusesEnabled = false };
            var p = new PlayerState("德国队·长中文舒马赫1234567890", false, 0, 1) { teamId = TeamId.DE, driverId = "de_michael_schumacher" };
            p.driverSkill.Initialize(p.DriverProfile, 7, true);
            var cards = Enumerable.Range(0, 12).Select(_ => new CardData(CardType.Speed, 1)).ToArray();
            p.deck.InitializeExactOrder(cards, new HeatPool(8)); p.deck.DrawToHand(8); race.Players.Add(p);
            int roster = new[] { 2, 4, 6, 12 }[fixture % 4];
            foreach (var d in DriverCatalog.All.Where(d => d.Id != p.driverId).Take(roster - 1)) race.Players.Add(new PlayerState(d.ShortName, true, 20 + race.Players.Count, 1) { teamId = d.Team, driverId = d.Id });
            Set(gm, "session", race);
            var phase = Get<RacePhaseState>(gm, "phaseState"); var input = Get<RaceInputState>(gm, "inputState");
            Action<int, int> begin = (turn, gear) => { race.BeginTurn(p, turn); race.BeginDriverSkillInput(p, 60); Set(gm, "raceTurnNumber", turn); phase.BeginGearSelection(); input.BeginGearSelection(p.gear); input.SelectGear(gear); };
            begin(1, 1);
            bool execution = fixture == 2 || fixture == 3 || fixture == 4 || fixture == 5;
            if (execution)
            {
                Assert.That(gm.TryPrepareSchumacherStrategyRequest(out var prep, out string why), Is.True, why);
                Assert.That(gm.TryConfirmSchumacherStrategyRequest(prep, prep.Choices[0], out why), Is.True, why); begin(2, 3);
                if (fixture == 3)
                {
                    Assert.That(gm.TryPrepareSchumacherStrategyRequest(out var first, out why), Is.True, why);
                    Assert.That(gm.TryConfirmSchumacherStrategyRequest(first, null, out why), Is.True, why); begin(3, 3);
                }
                if (fixture == 5) p.deck.heatPool.remaining = 0; // Read-only disabled boundary; no conservation claim for this synthetic setup.
            }
            if (fixture == 6) input.SelectGear(4);
            hud.SetGameManager(gm); hud.RefreshDriverSkill(gm, p); hud.RefreshTeamGearPresentation(); events.SetSelectedGameObject(hud.driverSkillButton.gameObject);
            var record = new Record { width = width, height = height, fixture = fixture, mask = mask, roster = roster, screenshot = png };
            int heat = p.deck.heatPool.remaining, uses = p.driverSkill.UsesRemaining, hand = p.deck.HandCount;
            if (!rewritten || fixture == 5 || fixture == 6)
            {
                if (rewritten) Assert.That(hud.driverSkillButton.IsInteractable(), Is.False);
                record.label = hud.driverSkillButton.GetComponentInChildren<TMP_Text>().text;
                Render(scope, hud, record); return record;
            }
            scope.Click(hud.driverSkillButton);
            record.modal = hud.IsReturnToMenuConfirmationVisible;
            record.body = record.modal ? hud.returnToMenuConfirmText.text : "Confirmation disabled; committed directly";
            if (record.modal)
            {
                Assert.That(events.currentSelectedGameObject, Is.SameAs(hud.cancelReturnToMenuButton.gameObject));
                Render(scope, hud, record);
                Vector2 point = RectTransformUtility.WorldToScreenPoint(scope.Root.GetComponent<Canvas>().worldCamera, hud.driverSkillButton.transform.position);
                var hits = new List<RaycastResult>(); scope.Root.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(events) { position = point }, hits);
                record.blocked = hits.Count > 0 && hits[0].gameObject != hud.driverSkillButton.gameObject;
                Assert.That(record.blocked, Is.True);
                if (fixture == 1 || fixture == 4) scope.Click(hud.cancelReturnToMenuButton);
                else if (fixture == 7)
                {
                    ExecuteEvents.Execute(events.currentSelectedGameObject, new AxisEventData(events) { moveDir = MoveDirection.Right }, ExecuteEvents.moveHandler);
                    Assert.That(events.currentSelectedGameObject, Is.SameAs(hud.confirmReturnToMenuButton.gameObject));
                    ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
                }
                else scope.Click(hud.confirmReturnToMenuButton);
            }
            else Render(scope, hud, record);
            bool cancelled = fixture == 1 || fixture == 4 && mask != 0;
            Assert.That(p.deck.heatPool.remaining, Is.EqualTo(heat - (execution && !cancelled ? 1 : 0)));
            Assert.That(p.driverSkill.UsesRemaining, Is.EqualTo(uses - (!execution && !cancelled ? 1 : 0)));
            Assert.That(p.deck.HandCount, Is.EqualTo(hand + (cancelled ? 0 : execution ? 1 : -1)));
            if (!execution && !cancelled) Assert.That(p.deck.DiscardPile.Contains(cards[0]), Is.True);
            if (execution && !cancelled) Assert.That(p.heatPaidCardsThisTurn.Last().IsHeat, Is.True);
            record.focusRestored = events.currentSelectedGameObject == (cancelled && !execution ? hud.driverSkillButton.gameObject : execution && !cancelled ? hud.gear3Button.gameObject : hud.gear1Button.gameObject);
            Assert.That(record.focusRestored, Is.True);
            record.uses = p.driverSkill.UsesRemaining; record.heat = p.deck.heatPool.remaining;
            hud.ConfirmReturnToMenu(); Assert.That(p.deck.heatPool.remaining, Is.EqualTo(record.heat));
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
    private static void Render(CareerUiPointerTestScope scope, HUDUI hud, Record record)
    {
        var shared = new MansellSkillProductionUiCapture.Record();
        typeof(MansellSkillProductionUiCapture).GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { scope, hud, record.screenshot, shared });
        record.overflows.AddRange(shared.overflows);
    }
    private static Button Button(Transform parent, TMP_FontAsset font, string name, Vector2 position)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); host.transform.SetParent(parent, false);
        var r = host.GetComponent<RectTransform>(); r.anchoredPosition = position; r.sizeDelta = new Vector2(420, 110);
        host.GetComponent<Image>().color = new Color32(23, 27, 32, 255);
        var b = host.GetComponent<Button>(); b.targetGraphic = host.GetComponent<Image>(); Label(host.transform, font, "Label", Vector2.zero, r.sizeDelta).text = name; return b;
    }
    private static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, Vector2 position, Vector2 size)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); host.transform.SetParent(parent, false); var t = host.GetComponent<TMP_Text>();
        t.font = font; t.fontSize = 20; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; t.rectTransform.anchoredPosition = position; t.rectTransform.sizeDelta = size; return t;
    }
    private static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
}

public class SchumacherStrategyProductionUiTests
{
    [TestCase(1920, 1080)] [TestCase(2560, 1440)] [TestCase(1280, 720)]
    public void ProfileStrategyCopyFitsWithoutAcceptingOtherPreexistingProfileOverflows(int w, int h)
    {
        var record = SchumacherPassiveProfileUiCapture.Exercise(w, h, true);
        Assert.That(DriverCatalog.All.First(d => d.Id == "de_michael_schumacher").ActiveName, Is.EqualTo("三段战术"));
        Assert.That(record.allOverflows.Where(s => s.Contains("de_michael_schumacher/Active")), Is.Empty);
    }
    private static IEnumerable<TestCaseData> Cases()
    {
        foreach (var s in new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1280, 720) })
            for (int i = 0; i < 8; i++) foreach (int mask in new[] { 0, (int)InRaceConfirmationAction.All }) yield return new TestCaseData(s.x, s.y, i, mask);
    }
    [TestCaseSource(nameof(Cases))]
    public void RealButtonUsesOneCostSurfaceAndExactResources(int w, int h, int fixture, int mask)
    {
        var result = SchumacherStrategyProductionUiCapture.Exercise(w, h, fixture, mask, true); Assert.That(result.overflows, Is.Empty);
    }
}
