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
public static class UkPrepMealCapture
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
        public int width, height, cancelCallbacks, confirmCallbacks, entryCallbacks;
        public bool technologyActive, driverActive, technologyInteractable;
        public float cardWidth, cardHeight;
        public bool overlayRaycast, confirmInteractable, selectedExactInstance;
        public List<Label> labels = new List<Label>();
        public List<string> overflows = new List<string>();
    }

    [Serializable] private sealed class Manifest
    {
        public string utc, stage;
        public string kind = "Controlled EditMode full production RaceCanvas. A4 labels and technology states are explicit presentation fixtures; no running match, actual heat transfer, gameplay balance or complete-mode acceptance.";
        public string lifecycle = "Owned preview scene/canvas/camera, disabled project behaviours and explicitly invoked HUD.Start plus production ApplyLayout. Owned static font/material clones; exact settings current/repository restoration. Before keeps the original disabled A4 driver slot; after calls the production technology widget and ShowTechnologyCardChoice through reflection, with counter callbacks only. No source fonts, scenes, player save or settings persistence.";
        public List<Record> records = new List<Record>();
    }

    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    public static string Capture(string stage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Capture requires idle EditMode.");
        if (stage != "before" && stage != "after")
            throw new ArgumentException("Use before or after.", nameof(stage));
        if (stage == "after" && typeof(HUDUI).GetMethod("ShowTechnologyCardChoice") == null)
            throw new InvalidOperationException("The new HUD API has not been compiled yet.");

        string directory = Path.GetFullPath("production/qa/evidence/tech-driver-I01a-2026-10-22/" + stage +
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
            staging = new GameObject("UK prep-meal inactive staging");
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

            cameraHost = new GameObject("UK prep-meal preview camera", typeof(Camera));
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
            RaceUILayoutController layout = root.GetComponentInChildren<RaceUILayoutController>(true);
            if (layout == null) layout = root.AddComponent<RaceUILayoutController>();
            layout.enabled = false;
            layout.ApplyLayout(hud, root.GetComponentInChildren<CardHandUI>(true));
            hud.ConfigureGearPresentation(TeamId.UK, 2);
            hud.RefreshDriverSkill(null, null);
            TMP_Text driverLabel = hud.driverSkillLabel != null ? hud.driverSkillLabel
                : hud.driverSkillButton != null ? hud.driverSkillButton.GetComponentInChildren<TMP_Text>(true) : null;
            if (driverLabel != null) driverLabel.text = "决胜抉择 · 请在选挡阶段发动";
            hud.SetStatus("出牌阶段 · 首次正式出牌前");
            if (stage == "before")
                CaptureFixture("entry-before", false, size, directory, manifest, hud, root, camera, target, fonts, owned);
            else
                foreach (string fixture in new[] { "entry-normal", "entry-disabled", "entry-used", "modal-one-heat",
                    "modal-two-heat", "modal-retirement-copy", "modal-second-choice", "modal-cancel-reopen", "modal-long-digits" })
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
            if (errors.Count > 0) throw new AggregateException("UK prep-meal preview cleanup failed.", errors);
        }
    }

    private static void CaptureFixture(string fixture, bool after, Vector2Int size, string directory, Manifest manifest,
        HUDUI hud, GameObject root, Camera camera, RenderTexture target, Dictionary<TMP_FontAsset, TMP_FontAsset> fonts, List<Object> owned)
    {
        int confirmed = 0, cancelled = 0, entryCalls = 0;
        CardData selected = null;
        var cards = new List<CardData> { new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 4) };
        bool modal = fixture.StartsWith("modal-", StringComparison.Ordinal);
        object widget = null;
        Button entry = null;
        string entryText = fixture == "entry-used" ? "科技·预备餐\n本圈已使用"
            : fixture == "entry-disabled" ? "科技·预备餐\n缺少可回收的旧永久热" : "科技·预备餐";
        string message = fixture == "modal-two-heat"
            ? "英国主场 · 最多回收旧永久热 2 张。\n旧手牌优先、旧弃牌其次 → 引擎库。\n弃置所选普通速度牌；本动作退役 0。"
            : fixture == "modal-retirement-copy"
                ? "净回引擎库 1 张；本动作退役 0 张。\n临时热不计入回收来源，也不会在此退役。\n取消保留费用牌，不占用本圈机会。"
                : fixture == "modal-long-digits"
                    ? "旧热来源：手牌 1234567890 / 弃牌 9876543210\n净回引擎库 2；本动作退役 0。\n本圈剩余 1 / 1；取消保留所选费用牌。"
                    : "弃置所选普通速度牌到普通弃牌堆。\n回收旧永久热 1 张 → 引擎库。\n取消不弃牌、不回热、不占本圈机会。";
        Action open = () => typeof(HUDUI).GetMethod("ShowTechnologyCardChoice").Invoke(hud, new object[]
        {
            "科技·预备餐 · 选择弃牌费用", message, cards,
            (UnityAction<CardData>)(card => { selected = card; confirmed++; }),
            (UnityAction)(() => cancelled++)
        });
        if (after)
        {
            Type type = typeof(HUDUI).Assembly.GetType("UkPrepMealActionUI");
            if (type == null) throw new InvalidOperationException("Technology widget has not been compiled.");
            widget = type.GetMethod("Attach", BindingFlags.Static | BindingFlags.Public).Invoke(null,
                new object[] { hud, (UnityAction)(() => entryCalls++) });
            entry = (Button)type.GetProperty("Button").GetValue(widget);
            type.GetMethod("Refresh").Invoke(widget, new object[] { true, !modal && fixture == "entry-normal", entryText });
            if (modal)
            {
                open();
                if (fixture == "modal-cancel-reopen")
                {
                    hud.cancelReturnToMenuButton.onClick.Invoke();
                    if (cancelled != 1 || confirmed != 0 || hud.IsReturnToMenuConfirmationVisible)
                        throw new InvalidOperationException("Technology cancel did not clear its intent.");
                    open();
                }
                if (fixture == "modal-second-choice")
                    Find(hud.returnToMenuConfirmPanel.transform, "DriverSkillChoiceNextButton").GetComponent<Button>().onClick.Invoke();
                // No manager is attached: reapply the explicit A4 presentation fixture
                // after HUD refresh, rather than pretending to simulate a real match.
                type.GetMethod("Refresh").Invoke(widget, new object[] { true, false, "科技·预备餐" });
                if (!hud.IsReturnToMenuConfirmationVisible || confirmed != 0)
                    throw new InvalidOperationException("Technology choice did not open without committing.");
            }
        }

        try
        {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 5;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) CloneFont(text, fonts, owned);
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var record = new Record
            {
                fixture = fixture, width = size.x, height = size.y,
                screenshot = Path.Combine(directory, size.x + "x" + size.y + "-" + fixture + ".png"),
                technologyActive = entry != null && entry.gameObject.activeSelf,
                technologyInteractable = entry != null && entry.interactable,
                driverActive = hud.driverSkillButton != null && hud.driverSkillButton.gameObject.activeSelf,
                overlayRaycast = modal && hud.returnToMenuConfirmPanel.GetComponent<Image>().raycastTarget,
                confirmInteractable = modal && hud.confirmReturnToMenuButton.interactable
            };
            if (modal)
            {
                Rect cardRect = hud.returnToMenuConfirmPanel.transform.Find("ReturnToMenuConfirmCard").GetComponent<RectTransform>().rect;
                record.cardWidth = cardRect.width;
                record.cardHeight = cardRect.height;
            }
            Transform operation = Find(root.transform, "OperationPanel");
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>())
            {
                bool affected = operation != null && text.transform.IsChildOf(operation);
                affected |= modal && text.transform.IsChildOf(hud.returnToMenuConfirmPanel.transform);
                if (!affected) continue;
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

            if (modal)
            {
                hud.confirmReturnToMenuButton.onClick.Invoke();
                record.selectedExactInstance = ReferenceEquals(selected, cards[fixture == "modal-second-choice" ? 1 : 0]);
                if (confirmed != 1 || !record.selectedExactInstance || hud.IsReturnToMenuConfirmationVisible)
                    throw new InvalidOperationException("Technology did not commit the exact selected instance once.");
                hud.ConfirmReturnToMenu();
                if (confirmed != 1) throw new InvalidOperationException("Repeated technology confirmation committed twice.");
                open();
                hud.cancelReturnToMenuButton.onClick.Invoke();
                if (confirmed != 1 || hud.IsReturnToMenuConfirmationVisible ||
                    cancelled != (fixture == "modal-cancel-reopen" ? 2 : 1))
                    throw new InvalidOperationException("Reopened technology cancellation boundary failed.");
            }
            else if (after && fixture == "entry-normal")
            {
                entry.onClick.Invoke();
                if (entryCalls != 1) throw new InvalidOperationException("Technology entry event did not dispatch once.");
            }
            record.entryCallbacks = entryCalls;
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
