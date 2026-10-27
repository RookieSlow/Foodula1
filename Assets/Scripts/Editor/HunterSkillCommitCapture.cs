using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Owned production-HUD fixtures. Does not load a match or persist any setting.</summary>
public static class HunterSkillCommitCapture
{
    [Serializable] private sealed class Label
    {
        public string name, text;
        public float width, height, preferredHeight;
        public bool overflow, raycastTarget;
    }

    [Serializable] private sealed class Record
    {
        public string screenshot, fixture;
        public int width, height, cancelCallbacks, confirmCallbacks;
        public float cardWidth, cardHeight;
        public bool overlayRaycast, confirmInteractable, selectedExactInstance;
        public List<Label> labels = new List<Label>();
        public List<string> overflows = new List<string>();
    }

    [Serializable] private sealed class Manifest
    {
        public string utc, stage;
        public string kind = "Controlled EditMode production RaceCanvas fixture, not a running match.";
        public string lifecycle = "Owned preview scene and prefab clone; disabled project behaviours; explicit HUD.Start. Settings current/repository restored exactly. Font/material clones are static and destroyed. Callback counters exercise original HUD cancel/confirm handlers; no gameplay state or navigation callback. After fixtures call ShowDriverSkillCardChoice through reflection so this helper compiles before that API exists.";
        public List<Record> records = new List<Record>();
    }

    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    public static string Capture(string stage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Capture requires idle EditMode.");
        if (stage != "before" && stage != "after")
            throw new ArgumentException("Use before or after.", nameof(stage));
        if (stage == "after" && typeof(HUDUI).GetMethod("ShowDriverSkillCardChoice") == null)
            throw new InvalidOperationException("The new HUD API has not been compiled yet.");

        string directory = Path.GetFullPath("production/qa/evidence/tech-driver-I00b-2026-10-22/" + stage +
            "/run-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        if (Directory.Exists(directory)) throw new IOException("Evidence directory already exists.");
        Directory.CreateDirectory(directory);
        var manifest = new Manifest { utc = DateTime.UtcNow.ToString("o"), stage = stage };
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        FieldInfo current = typeof(GameSettingsRuntime).GetField("current", flags);
        FieldInfo repository = typeof(GameSettingsRuntime).GetField("repository", flags);
        object priorCurrent = current.GetValue(null), priorRepository = repository.GetValue(null);
        try
        {
            current.SetValue(null, new GameSettingsData { inRaceConfirmationMask = (int)InRaceConfirmationAction.All });
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1280, 720) })
                CaptureSize(stage, size, directory, manifest);
        }
        finally
        {
            current.SetValue(null, priorCurrent);
            repository.SetValue(null, priorRepository);
        }

        string path = Path.Combine(directory, "manifest.json");
        File.WriteAllText(path, JsonUtility.ToJson(manifest, true));
        int overflow = 0;
        foreach (Record record in manifest.records) overflow += record.overflows.Count;
        return "{\"screenshots\":" + manifest.records.Count + ",\"overflowRecords\":" + overflow +
            ",\"manifest\":\"" + path.Replace("\\", "/") + "\"}";
    }

    private static void CaptureSize(string stage, Vector2Int size, string directory, Manifest manifest)
    {
        Scene scene = default;
        GameObject staging = null, root = null, cameraHost = null;
        RenderTexture target = null, previous = RenderTexture.active;
        var fonts = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        var owned = new List<Object>();
        try
        {
            scene = EditorSceneManager.NewPreviewScene();
            staging = new GameObject("Hunter confirmation inactive staging");
            staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, scene);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/RaceCanvas.prefab");
            if (prefab == null) throw new InvalidOperationException("Production RaceCanvas missing.");
            root = Object.Instantiate(prefab, staging.transform, false);
            root.SetActive(false);
            root.transform.SetParent(null, false);
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) CloneFont(text, fonts, owned);
            foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null && script.GetType().Assembly == typeof(HUDUI).Assembly) script.enabled = false;

            cameraHost = new GameObject("Hunter confirmation preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            Camera camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.cullingMask = 1 << 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .03f, .04f, 1f);
            target = new RenderTexture(size.x, size.y, 24);
            if (!target.Create()) throw new InvalidOperationException("Render allocation failed.");
            camera.targetTexture = target;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            canvas.scaleFactor = size.x / 1920f;
            root.SetActive(true);
            HUDUI hud = root.GetComponentInChildren<HUDUI>(true);
            if (hud == null) throw new InvalidOperationException("HUD missing.");
            typeof(HUDUI).GetMethod("Start", PrivateInstance).Invoke(hud, null);
            hud.confirmReturnToMenuButton.onClick.RemoveAllListeners();
            hud.confirmReturnToMenuButton.onClick.AddListener(hud.ConfirmReturnToMenu);
            hud.cancelReturnToMenuButton.onClick.RemoveAllListeners();
            hud.cancelReturnToMenuButton.onClick.AddListener(hud.CancelReturnToMenu);
            if (stage == "before")
                CaptureFixture("generic-driver-skill", false, size, directory, manifest, hud, root, camera, target, fonts, owned);
            else
                foreach (string fixture in new[] { "choice-normal", "choice-second-card", "choice-long-copy", "choice-disabled", "choice-cancel-reopen" })
                    CaptureFixture(fixture, true, size, directory, manifest, hud, root, camera, target, fonts, owned);
        }
        finally
        {
            RenderTexture.active = previous;
            var errors = new List<Exception>();
            if (root != null)
            {
                foreach (RacingTechHudShellUI shell in root.GetComponentsInChildren<RacingTechHudShellUI>(true))
                    Cleanup(() => typeof(RacingTechHudShellUI).GetMethod("OnDisable", PrivateInstance).Invoke(shell, null), errors);
                Cleanup(() => Object.DestroyImmediate(root), errors);
            }
            if (staging != null) Cleanup(() => Object.DestroyImmediate(staging), errors);
            if (cameraHost != null) Cleanup(() => Object.DestroyImmediate(cameraHost), errors);
            if (target != null)
            {
                Cleanup(() => target.Release(), errors);
                Cleanup(() => Object.DestroyImmediate(target), errors);
            }
            foreach (Object item in owned) if (item != null) Cleanup(() => Object.DestroyImmediate(item), errors);
            if (scene.IsValid()) Cleanup(() => EditorSceneManager.ClosePreviewScene(scene), errors);
            if (errors.Count > 0) throw new AggregateException("Hunter confirmation preview cleanup failed.", errors);
        }
    }

    private static void CaptureFixture(string fixture, bool choiceMode, Vector2Int size, string directory, Manifest manifest,
        HUDUI hud, GameObject root, Camera camera, RenderTexture target, Dictionary<TMP_FontAsset, TMP_FontAsset> fonts, List<Object> owned)
    {
        int confirmed = 0, cancelled = 0;
        CardData selected = null;
        var cards = new List<CardData> { new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 4) };
        bool disabled = fixture == "choice-disabled";
        string title = fixture == "choice-long-copy" ? "决胜抉择 · 选择弃牌费用" : "决胜抉择 · 选择费用";
        string message = fixture == "choice-long-copy"
            ? "目标：英国队 · 长中文车手名称 1234567890\n前方距离 5 格；弃置所选普通速度牌。\n本回合移动 +3；剩余 3 / 3 次。"
            : "目标：英国队 · 前方 3 格\n弃置所选普通速度牌，本回合移动 +2。\n剩余 2 / 2 次；取消不扣牌或次数。";
        Action open = () =>
        {
            if (choiceMode)
                typeof(HUDUI).GetMethod("ShowDriverSkillCardChoice").Invoke(hud, new object[]
                {
                    title, disabled ? "没有可选的普通速度牌，请取消并继续比赛。" : message,
                    disabled ? new List<CardData>() : cards,
                    (UnityAction<CardData>)(card => { selected = card; confirmed++; }),
                    (UnityAction)(() => cancelled++)
                });
            else
                hud.RequestInRaceAction(InRaceConfirmationAction.DriverSkill, "确认发动技能",
                    "车手技能发动后会消耗本场次数，确定继续吗？", () => confirmed++);
        };

        open();
        if (!hud.IsReturnToMenuConfirmationVisible || confirmed != 0)
            throw new InvalidOperationException("Hunter confirmation did not open without committing.");
        if (fixture == "choice-cancel-reopen")
        {
            hud.cancelReturnToMenuButton.onClick.Invoke();
            if (confirmed != 0 || cancelled != 1 || hud.IsReturnToMenuConfirmationVisible)
                throw new InvalidOperationException("Initial cancel did not clear the choice.");
            open();
        }
        if (fixture == "choice-second-card")
        {
            Transform next = Find(hud.returnToMenuConfirmPanel.transform, "DriverSkillChoiceNextButton");
            if (next == null) throw new InvalidOperationException("Choice next button missing.");
            next.GetComponent<Button>().onClick.Invoke();
        }

        try
        {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 5;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) CloneFont(text, fonts, owned);
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in hud.returnToMenuConfirmPanel.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            Rect cardRect = hud.returnToMenuConfirmPanel.transform.Find("ReturnToMenuConfirmCard").GetComponent<RectTransform>().rect;
            var record = new Record
            {
                fixture = fixture, width = size.x, height = size.y,
                screenshot = Path.Combine(directory, size.x + "x" + size.y + "-" + fixture + ".png"),
                cardWidth = cardRect.width, cardHeight = cardRect.height,
                overlayRaycast = hud.returnToMenuConfirmPanel.GetComponent<Image>().raycastTarget,
                confirmInteractable = hud.confirmReturnToMenuButton.interactable
            };
            foreach (TMP_Text text in hud.returnToMenuConfirmPanel.GetComponentsInChildren<TMP_Text>())
            {
                record.labels.Add(new Label { name = text.name, text = text.text, width = text.rectTransform.rect.width,
                    height = text.rectTransform.rect.height, preferredHeight = text.preferredHeight,
                    overflow = text.isTextOverflowing, raycastTarget = text.raycastTarget });
                if (text.isTextOverflowing) record.overflows.Add(text.name + ": " + text.text);
            }
            Texture2D image = null;
            RenderTexture prior = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                image.Apply();
                File.WriteAllBytes(record.screenshot, image.EncodeToPNG());
            }
            finally { RenderTexture.active = prior; if (image != null) Object.DestroyImmediate(image); }

            if (fixture == "choice-second-card")
            {
                hud.confirmReturnToMenuButton.onClick.Invoke();
                record.selectedExactInstance = ReferenceEquals(selected, cards[1]);
                if (confirmed != 1 || !record.selectedExactInstance || hud.IsReturnToMenuConfirmationVisible)
                    throw new InvalidOperationException("Second equal-value card was not committed by exact instance.");
                hud.ConfirmReturnToMenu();
                if (confirmed != 1) throw new InvalidOperationException("Repeated confirmation committed twice.");
            }
            else
            {
                if (disabled)
                {
                    hud.ConfirmReturnToMenu();
                    if (confirmed != 0 || !hud.IsReturnToMenuConfirmationVisible)
                        throw new InvalidOperationException("Empty choice was submitted.");
                }
                int expectedCancel = fixture == "choice-cancel-reopen" ? 2 : 1;
                hud.cancelReturnToMenuButton.onClick.Invoke();
                if (confirmed != 0 || hud.IsReturnToMenuConfirmationVisible || (choiceMode && cancelled != expectedCancel))
                    throw new InvalidOperationException("Cancel did not preserve the no-commit boundary.");
                if (!disabled)
                {
                    open();
                    hud.confirmReturnToMenuButton.onClick.Invoke();
                    record.selectedExactInstance = !choiceMode || ReferenceEquals(selected, cards[0]);
                    if (confirmed != 1 || !record.selectedExactInstance || hud.IsReturnToMenuConfirmationVisible)
                        throw new InvalidOperationException("Reopened confirmation did not commit the first choice once.");
                }
            }
            record.cancelCallbacks = cancelled;
            record.confirmCallbacks = confirmed;
            manifest.records.Add(record);
        }
        finally { hud.CancelReturnToMenu(); }
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) if (node.name == name) return node;
        return null;
    }

    private static void CloneFont(TMP_Text text, Dictionary<TMP_FontAsset, TMP_FontAsset> fonts, List<Object> owned)
    {
        TMP_FontAsset source = text.font;
        if (source == null || owned.Contains(source)) return;
        if (!fonts.TryGetValue(source, out TMP_FontAsset clone))
        {
            clone = Object.Instantiate(source);
            owned.Add(clone);
            clone.atlasPopulationMode = AtlasPopulationMode.Static;
            clone.fallbackFontAssetTable = new List<TMP_FontAsset>();
            Material material = Object.Instantiate(source.material);
            owned.Add(material);
            clone.material = material;
            fonts.Add(source, clone);
        }
        text.font = clone;
        text.fontSharedMaterial = clone.material;
    }

    private static void Cleanup(Action action, List<Exception> errors)
    { try { action(); } catch (Exception exception) { errors.Add(exception); } }
}
