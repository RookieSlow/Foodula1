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

/// <summary>
/// UI evidence, ADVISORY visual gate: real production DriverSelectionUI on an owned
/// orthographic preview Canvas. Pointer/Submit integration is automated; physical input,
/// full menu launch, race modes and other drivers' skill implementation are not accepted here.
/// </summary>
public static class SchumacherPassiveProfileUiCapture
{
    private const string DriverId = "de_michael_schumacher";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Serializable]
    public sealed class Record
    {
        public int width, height, driverCards, teamCount, selectionCallbacks;
        public string screenshot, passiveSummary, renderedPassive, selectedDriver;
        public bool allCardsPointerSelected, keyboardSubmitSelected, lowerMasked, backHidOverlay, lowerRestored;
        public List<string> allOverflows = new List<string>();
        public List<string> changedPassiveOverflows = new List<string>();
    }

    [Serializable]
    private sealed class Manifest
    {
        public string utc, stage, runtimeMvid;
        public string kind = "Owned orthographic EditMode Canvas, unchanged production DriverSelectionUI builds all twelve driver cards/six teams. Real GraphicRaycaster top-hit pointer and EventSystem Submit; no production geometry edits, full-menu launch, OS input or race-mode acceptance.";
        public string boundary = "All other existing profile text overflow is recorded, not repaired or claimed accepted. Only Schumacher passive copy is the changed visual target. Driver selection and launch fields, EventSystem, render target and source font are restored; PlayerPrefs is not read or written by this fixture.";
        public List<Record> records = new List<Record>();
    }

    /// <summary>Captures actual currently loaded copy; refuses a fabricated old before after migration.</summary>
    public static string Capture(string stage)
    {
        if (stage != "before" && stage != "after") throw new ArgumentException("before or after", nameof(stage));
        bool rewritten = DriverCatalog.All.First(d => d.Id == DriverId).PassiveSummary.Contains("真实弯道付款");
        if (stage == "before" && rewritten) throw new InvalidOperationException("Cannot recreate old profile copy after migration.");
        if (stage == "after" && !rewritten) throw new InvalidOperationException("Reworked passive copy is not loaded.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || ModularRegressionRunner.NativeTestsRunning())
            throw new InvalidOperationException("Capture requires idle EditMode.");
        string directory = Path.GetFullPath("production/qa/evidence/tech-driver-I04a-2026-10-27/" + stage +
            "/run-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        if (Directory.Exists(directory)) throw new IOException("Capture directory already exists.");
        Directory.CreateDirectory(directory);
        var manifest = new Manifest {
            utc = DateTime.UtcNow.ToString("o"), stage = stage,
            runtimeMvid = typeof(DriverProfile).Assembly.ManifestModule.ModuleVersionId.ToString()
        };
        foreach (Vector2Int size in Sizes())
            manifest.records.Add(Exercise(size.x, size.y, stage == "after",
                Path.Combine(directory, size.x + "x" + size.y + "-driver-selection.png")));
        string path = Path.Combine(directory, "manifest.json");
        File.WriteAllText(path, JsonUtility.ToJson(manifest, true));
        return path;
    }

    /// <summary>Production profile copy, twelve card selections, Submit, modal mask and actual return callback.</summary>
    public static Record Exercise(int width, int height, bool requireReworkedCopy, string screenshot = null)
    {
        using var launch = new RaceStartupTestScope();
        FieldInfo settingsField = typeof(GameSettingsRuntime).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo audioField = typeof(AudioService).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object savedSettings = settingsField.GetValue(null), savedAudio = audioField.GetValue(null);
        EventSystem previousEvents = EventSystem.current;
        GameObject previousSelection = previousEvents != null ? previousEvents.currentSelectedGameObject : null;
        CareerUiPointerTestScope scope = null;
        EventSystem events = null;
        try
        {
            var settings = GameSettingsData.CreateDefault(); settings.masterVolume = 0f; settings.reduceMotion = true;
            settingsField.SetValue(null, settings); audioField.SetValue(null, null);
            scope = CareerUiPointerTestScope.CreateSized(width, height,
                () => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset"));
            scope.Root.GetComponent<Canvas>().worldCamera.orthographic = true;
            var eventHost = new GameObject("Owned profile EventSystem", typeof(EventSystem));
            eventHost.transform.SetParent(scope.Root.transform, false);
            events = eventHost.GetComponent<EventSystem>();
            typeof(EventSystem).GetMethod("OnEnable", Private).Invoke(events, null);
            EventSystem.current = events;
            int lowerClicks = 0;
            Button lower = CreateLowerButton(scope.Root.transform, () => lowerClicks++);
            scope.Click(lower);
            Assert.That(lowerClicks, Is.EqualTo(1));

            int callbacks = 0;
            string callbackDriver = null;
            DriverSelectionUI ui = scope.Root.AddComponent<DriverSelectionUI>();
            ui.Initialize(id => { callbacks++; callbackDriver = id; });
            ui.Show();
            Transform overlay = scope.Root.transform.Find("DriverSelectionOverlay");
            Assert.That(overlay, Is.Not.Null);
            var record = new Record { width = width, height = height, screenshot = screenshot };
            Render(scope);
            record.lowerMasked = ClickTopInsteadOfLower(scope, lower);
            Assert.That(record.lowerMasked, Is.True, "Production overlay must mask the underlying button.");
            Assert.That(lowerClicks, Is.EqualTo(1), "Overlay click must not dispatch through to the lower action.");
            callbacks = 0;
            var teams = new HashSet<TeamId>();
            foreach (DriverProfile profile in DriverSelectionState.AvailableDrivers)
            {
                Button card = overlay.Find("DriverSelectionPanel/DriverGrid/Driver_" + profile.Id).GetComponent<Button>();
                Assert.That(card.IsInteractable(), Is.True, profile.Id);
                scope.Click(card);
                Assert.That(callbackDriver, Is.EqualTo(profile.Id));
                Assert.That(DriverSelectionState.SelectedDriverId, Is.EqualTo(profile.Id));
                Assert.That(overlay.Find("DriverSelectionPanel/SelectedDriver").GetComponent<TMP_Text>().text,
                    Does.Contain(profile.DisplayName));
                teams.Add(profile.Team);
            }
            record.driverCards = DriverSelectionState.AvailableDrivers.Count;
            record.teamCount = teams.Count;
            record.allCardsPointerSelected = callbacks == record.driverCards;
            Assert.That(record.driverCards, Is.EqualTo(12));
            Assert.That(record.teamCount, Is.EqualTo(6));
            Assert.That(record.allCardsPointerSelected, Is.True);

            Button schumacher = overlay.Find("DriverSelectionPanel/DriverGrid/Driver_" + DriverId).GetComponent<Button>();
            events.SetSelectedGameObject(schumacher.gameObject);
            Assert.That(ExecuteEvents.Execute(schumacher.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler), Is.True);
            record.keyboardSubmitSelected = callbackDriver == DriverId && DriverSelectionState.SelectedDriverId == DriverId;
            Assert.That(record.keyboardSubmitSelected, Is.True);
            record.selectionCallbacks = callbacks;
            DriverProfile driver = DriverCatalog.All.First(d => d.Id == DriverId);
            record.passiveSummary = driver.PassiveSummary;
            record.renderedPassive = schumacher.transform.Find("Passive").GetComponent<TMP_Text>().text;
            record.selectedDriver = DriverSelectionState.SelectedDriverId;
            Assert.That(record.renderedPassive, Does.Contain(driver.PassiveSummary), "Text must come from current production profile data.");
            if (requireReworkedCopy)
                Assert.That(record.passiveSummary, Does.Contain("真实弯道付款"));
            else
                Assert.That(record.passiveSummary.Replace(" ", ""), Does.Contain("每3回合"));
            Render(scope);
            foreach (TMP_Text text in overlay.GetComponentsInChildren<TMP_Text>())
            {
                if (!text.isTextOverflowing) continue;
                string issue = RelativePath(text.transform, overlay) + ": " + text.text;
                record.allOverflows.Add(issue);
                if (text.transform == schumacher.transform.Find("Passive")) record.changedPassiveOverflows.Add(issue);
            }
            if (screenshot != null) SaveScreenshot(scope, screenshot);

            Button back = overlay.Find("DriverSelectionPanel/BackButton").GetComponent<Button>();
            scope.Click(back);
            record.backHidOverlay = !overlay.gameObject.activeSelf;
            Assert.That(record.backHidOverlay, Is.True, "Actual production Back callback must hide the overlay.");
            scope.Click(lower);
            record.lowerRestored = lowerClicks == 2;
            Assert.That(record.lowerRestored, Is.True);
            return record;
        }
        finally
        {
            if (events != null) typeof(EventSystem).GetMethod("OnDisable", Private).Invoke(events, null);
            scope?.Dispose();
            settingsField.SetValue(null, savedSettings); audioField.SetValue(null, savedAudio);
            if (previousEvents != null && previousEvents.isActiveAndEnabled)
            {
                EventSystem.current = previousEvents;
                if (previousSelection != null && previousSelection.activeInHierarchy)
                    previousEvents.SetSelectedGameObject(previousSelection);
            }
        }
    }

    private static IEnumerable<Vector2Int> Sizes()
    {
        yield return new Vector2Int(1920, 1080);
        yield return new Vector2Int(2560, 1440);
        yield return new Vector2Int(1280, 720);
    }

    private static Button CreateLowerButton(Transform parent, Action clicked)
    {
        var host = new GameObject("Owned lower profile action", typeof(RectTransform), typeof(Image), typeof(Button));
        host.transform.SetParent(parent, false);
        host.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 80);
        Button button = host.GetComponent<Button>(); button.targetGraphic = host.GetComponent<Image>();
        button.onClick.AddListener(() => clicked());
        return button;
    }

    private static bool ClickTopInsteadOfLower(CareerUiPointerTestScope scope, Button lower)
    {
        Camera camera = scope.Root.GetComponent<Canvas>().worldCamera;
        RectTransform rect = lower.GetComponent<RectTransform>();
        var pointer = new PointerEventData(scope.EventSystem) {
            position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>();
        scope.Root.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
        Assert.That(hits, Is.Not.Empty);
        GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        if (receiver == lower.gameObject) return false;
        if (receiver != null) ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
        return true;
    }

    private static void Render(CareerUiPointerTestScope scope)
    {
        foreach (Transform child in scope.Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in scope.Root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        RenderTexture previous = RenderTexture.active;
        try { scope.Root.GetComponent<Canvas>().worldCamera.Render(); }
        finally { RenderTexture.active = previous; }
    }

    private static void SaveScreenshot(CareerUiPointerTestScope scope, string path)
    {
        RenderTexture previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            RenderTexture.active = scope.Target;
            image = new Texture2D(scope.Target.width, scope.Target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, scope.Target.width, scope.Target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; if (image != null) Object.DestroyImmediate(image); }
    }

    private static string RelativePath(Transform child, Transform root)
    {
        string path = child.name;
        for (Transform current = child.parent; current != null && current != root; current = current.parent)
            path = current.name + "/" + path;
        return path;
    }
}

/// <summary>UI integration, ADVISORY visual gate. Other pre-existing driver text overflow remains recorded.</summary>
public sealed class SchumacherPassiveProfileUiTests
{
    [TestCase(1920, 1080)]
    [TestCase(2560, 1440)]
    [TestCase(1280, 720)]
    public void RealSelectionCardsRenderCurrentTuningCopyAndPreserveModalInput(int width, int height)
    {
        SchumacherPassiveProfileUiCapture.Record record = SchumacherPassiveProfileUiCapture.Exercise(width, height, true);
        Assert.That(record.changedPassiveOverflows, Is.Empty, "Changed passive summary must fit its production card.");
    }
}
