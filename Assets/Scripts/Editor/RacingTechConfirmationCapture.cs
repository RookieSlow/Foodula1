using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Isolated production HUD confirmation evidence. Never loads a race or invokes navigation.</summary>
public static class RacingTechConfirmationCapture
{
    [Serializable] private sealed class Label
    {
        public string name, text;
        public float width, height, preferredHeight;
        public bool overflow, raycastTarget;
    }
    [Serializable] private sealed class Record
    {
        public string screenshot, action, fixture;
        public int width, height, cancelCallbackCount, confirmCallbackCount;
        public float cardWidth, cardHeight, confirmWidth, confirmHeight, cancelWidth, cancelHeight;
        public bool overlayRaycast, confirmInteractable, cancelInteractable;
        public List<Label> labels = new List<Label>();
        public List<string> overflows = new List<string>();
    }
    [Serializable] private sealed class Manifest
    {
        public string utc, stage;
        public string kind = "Controlled EditMode real RaceCanvas HUD.RequestInRaceAction preview. No gameplay manager, live match, scene loading or settings persistence.";
        public string lifecycle = "Disabled project behaviours; HUD.Start invoked explicitly on owned prefab clone. Isolated all-confirmations settings object, exact previous current/repository restored. Owned modal button listeners are rebound only to original HUD handlers after Start, isolating ButtonClickAnimation audio side effects; real cancel and confirm handlers use counter callbacks. Native Selectable protected DoStateTransition instant=true supplies after-state visuals; disabled also sets interactable=false. One explicitly synthetic long-copy stress fixture. Fonts/materials cloned as static; no saved scenes or source font mutation.";
        public List<Record> records = new List<Record>();
    }
    private sealed class Fixture
    {
        public InRaceConfirmationAction action;
        public string name, title, message;
        public Fixture(InRaceConfirmationAction action, string name, string title, string message)
        { this.action = action; this.name = name; this.title = title; this.message = message; }
    }
    public static string Capture(string stage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Capture requires idle EditMode.");
        if (stage != "before" && stage != "before-isolated" && stage != "after") throw new ArgumentException("Use before, before-isolated or after.", nameof(stage));
        var manifest = new Manifest { utc = DateTime.UtcNow.ToString("o"), stage = stage };
        string directory = Path.GetFullPath("production/qa/evidence/racing-tech-actions-2026-10-15/" + stage);
        Directory.CreateDirectory(directory);
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        FieldInfo current = typeof(GameSettingsRuntime).GetField("current", flags);
        FieldInfo repository = typeof(GameSettingsRuntime).GetField("repository", flags);
        object previousCurrent = current.GetValue(null), previousRepository = repository.GetValue(null);
        try
        {
            current.SetValue(null, new GameSettingsData { inRaceConfirmationMask = (int)InRaceConfirmationAction.All });
            foreach (Vector2Int size in new[] { new Vector2Int(1920,1080), new Vector2Int(2560,1440), new Vector2Int(1280,720) })
                CaptureSize(stage, size, directory, manifest);
        }
        finally { current.SetValue(null, previousCurrent); repository.SetValue(null, previousRepository); }
        File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonUtility.ToJson(manifest, true));
        int overflow = 0;
        foreach (Record record in manifest.records) overflow += record.overflows.Count;
        return "{\"screenshots\":" + manifest.records.Count + ",\"overflowRecords\":" + overflow + ",\"manifest\":\"" + Path.Combine(directory,"manifest.json").Replace("\\","/") + "\"}";
    }
    private static IEnumerable<Fixture> Fixtures()
    {
        yield return new Fixture(InRaceConfirmationAction.GearSelection,"gear-selection","确认换挡选择","确定选择 档位 3 吗？");
        yield return new Fixture(InRaceConfirmationAction.GearCommit,"gear-commit","确认锁定档位","确定锁定当前档位并进入出牌阶段吗？");
        yield return new Fixture(InRaceConfirmationAction.CardAction,"card-action","确认出牌","确定按当前选择打出这些牌吗？");
        yield return new Fixture(InRaceConfirmationAction.DriverSkill,"driver-skill","确认发动技能","车手技能发动后会消耗本场次数，确定继续吗？");
        yield return new Fixture(InRaceConfirmationAction.ResetRace,"reset-race","确认重新开始","当前比赛进度会丢失，确定重新开始吗？");
        yield return new Fixture(InRaceConfirmationAction.ReturnToMenu,"return-menu","返回主菜单","确定要退出当前比赛并返回主菜单吗？\n当前比赛进度不会保存。");
        yield return new Fixture(InRaceConfirmationAction.PitDecision,"pit-decision",PitChoicePresentationRules.GetConfirmationTitle(true),PitChoicePresentationRules.GetConfirmationMessage(true));
        yield return new Fixture(InRaceConfirmationAction.LaneChange,"lane-change","确认起点换道","确定选择“" + LaneChoicePresentationRules.GetChoiceLabel(1) + "”并完成本次车道决定吗？");
        yield return new Fixture(InRaceConfirmationAction.CardAction,"synthetic-long-copy","受控长中文与数字压力样本","中国队 / 英国队 / 美国队 / 德国队 / 意大利队 / 日本队\n当前选择：1234567890；预计数据：9 / 12。\n此段为受控排版检查文字，确定继续当前操作吗？");
    }
    private static void CaptureSize(string stage, Vector2Int size, string directory, Manifest manifest)
    {
        Scene scene = default;
        GameObject staging = null, root = null, cameraHost = null;
        RenderTexture target = null, previous = RenderTexture.active;
        var fonts = new Dictionary<TMP_FontAsset,TMP_FontAsset>();
        var owned = new List<Object>();
        try
        {
            scene = EditorSceneManager.NewPreviewScene();
            staging = new GameObject("Confirmation inactive staging");
            staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging,scene);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/RaceCanvas.prefab");
            if (prefab == null) throw new InvalidOperationException("Production RaceCanvas missing.");
            root = Object.Instantiate(prefab,staging.transform,false);
            root.SetActive(false);
            root.transform.SetParent(null,false);
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) CloneFont(text,fonts,owned);
            foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null && script.GetType().Assembly == typeof(HUDUI).Assembly) script.enabled = false;
            cameraHost = new GameObject("Confirmation preview camera",typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraHost,scene);
            Camera camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false; camera.scene = scene; camera.cullingMask = 1 << 5;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.03f,.04f,1f);
            target = new RenderTexture(size.x,size.y,24);
            if (!target.Create()) throw new InvalidOperationException("Render allocation failed.");
            camera.targetTexture = target;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            canvas.scaleFactor = size.x / 1920f;
            root.SetActive(true);
            HUDUI hud = root.GetComponentInChildren<HUDUI>(true);
            if (hud == null) throw new InvalidOperationException("HUD missing.");
            typeof(HUDUI).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
            // Own only preview listeners: preserve production HUD handlers while isolating UI sound.
            hud.confirmReturnToMenuButton.onClick.RemoveAllListeners();
            hud.confirmReturnToMenuButton.onClick.AddListener(hud.ConfirmReturnToMenu);
            hud.cancelReturnToMenuButton.onClick.RemoveAllListeners();
            hud.cancelReturnToMenuButton.onClick.AddListener(hud.CancelReturnToMenu);
            foreach (Fixture fixture in Fixtures())
                CaptureFixture(fixture,"normal",size,directory,manifest,hud,root,camera,target,fonts,owned);
            if (stage == "after")
            {
                var fixture = new Fixture(InRaceConfirmationAction.ReturnToMenu,"return-menu","返回主菜单","确定要退出当前比赛并返回主菜单吗？\n当前比赛进度不会保存。");
                foreach (string state in new[] { "hover", "pressed", "disabled" })
                    CaptureFixture(fixture,state,size,directory,manifest,hud,root,camera,target,fonts,owned);
            }
        }
        finally
        {
            RenderTexture.active = previous;
            var errors = new List<Exception>();
            if (root != null)
            {
                foreach (RacingTechHudShellUI shell in root.GetComponentsInChildren<RacingTechHudShellUI>(true))
                    Cleanup(() => typeof(RacingTechHudShellUI).GetMethod("OnDisable",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(shell,null),errors);
                Cleanup(() => Object.DestroyImmediate(root),errors);
            }
            if (staging != null) Cleanup(() => Object.DestroyImmediate(staging),errors);
            if (cameraHost != null) Cleanup(() => Object.DestroyImmediate(cameraHost),errors);
            if (target != null) { Cleanup(() => target.Release(),errors); Cleanup(() => Object.DestroyImmediate(target),errors); }
            foreach (Object item in owned) if (item != null) Cleanup(() => Object.DestroyImmediate(item),errors);
            if (scene.IsValid()) Cleanup(() => EditorSceneManager.ClosePreviewScene(scene),errors);
            if (errors.Count > 0) throw new AggregateException("Confirmation preview cleanup failed.",errors);
        }
    }
    private static void CaptureFixture(Fixture fixture, string state, Vector2Int size, string directory, Manifest manifest,
        HUDUI hud, GameObject root, Camera camera, RenderTexture target, Dictionary<TMP_FontAsset,TMP_FontAsset> fonts, List<Object> owned)
    {
        int callback = 0;
        hud.RequestInRaceAction(fixture.action,fixture.title,fixture.message,() => callback++);
        if (!hud.IsReturnToMenuConfirmationVisible || callback != 0) throw new InvalidOperationException("Confirmation gate fixture did not open.");
        Button confirm = hud.confirmReturnToMenuButton, cancel = hud.cancelReturnToMenuButton;
        try
        {
            confirm.interactable = state != "disabled";
            if (state != "normal")
            {
                MethodInfo transition = typeof(Selectable).GetMethod("DoStateTransition",BindingFlags.NonPublic|BindingFlags.Instance);
                Type selection = typeof(Selectable).GetNestedType("SelectionState",BindingFlags.NonPublic);
                transition.Invoke(confirm,new[] { Enum.Parse(selection,state == "hover" ? "Highlighted" : state == "pressed" ? "Pressed" : "Disabled"), (object)true });
            }
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 5;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) CloneFont(text,fonts,owned);
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in hud.returnToMenuConfirmPanel.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases(); camera.Render();
            var record = new Record { action = fixture.action.ToString(), fixture = fixture.name + "-" + state, width = size.x, height = size.y,
                screenshot = Path.Combine(directory,size.x + "x" + size.y + "-" + fixture.name + "-" + state + ".png"),
                overlayRaycast = hud.returnToMenuConfirmPanel.GetComponent<Image>().raycastTarget,
                confirmInteractable = confirm.interactable, cancelInteractable = cancel.interactable };
            Rect card = hud.returnToMenuConfirmPanel.transform.Find("ReturnToMenuConfirmCard").GetComponent<RectTransform>().rect;
            Rect cr = confirm.GetComponent<RectTransform>().rect, br = cancel.GetComponent<RectTransform>().rect;
            record.cardWidth = card.width; record.cardHeight = card.height;
            record.confirmWidth = cr.width; record.confirmHeight = cr.height; record.cancelWidth = br.width; record.cancelHeight = br.height;
            foreach (TMP_Text text in hud.returnToMenuConfirmPanel.GetComponentsInChildren<TMP_Text>())
            {
                record.labels.Add(new Label { name = text.name, text = text.text, width = text.rectTransform.rect.width,
                    height = text.rectTransform.rect.height, preferredHeight = text.preferredHeight, overflow = text.isTextOverflowing, raycastTarget = text.raycastTarget });
                if (text.isTextOverflowing) record.overflows.Add(text.name + ": " + text.text);
            }
            Texture2D image = null; RenderTexture prior = RenderTexture.active;
            try { RenderTexture.active = target; image = new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,size.x,size.y),0,0); image.Apply(); File.WriteAllBytes(record.screenshot,image.EncodeToPNG()); }
            finally { RenderTexture.active = prior; if (image != null) Object.DestroyImmediate(image); }
            cancel.onClick.Invoke(); record.cancelCallbackCount = callback;
            if (callback != 0 || hud.IsReturnToMenuConfirmationVisible) throw new InvalidOperationException("Cancel callback boundary failed.");
            confirm.interactable = true;
            hud.RequestInRaceAction(fixture.action,fixture.title,fixture.message,() => callback++);
            confirm.onClick.Invoke(); record.confirmCallbackCount = callback;
            if (callback != 1 || hud.IsReturnToMenuConfirmationVisible) throw new InvalidOperationException("Confirm callback boundary failed.");
            manifest.records.Add(record);
        }
        finally { if (confirm != null) confirm.interactable = true; hud.CancelReturnToMenu(); }
    }
    private static void CloneFont(TMP_Text text, Dictionary<TMP_FontAsset,TMP_FontAsset> fonts, List<Object> owned)
    {
        TMP_FontAsset source = text.font;
        if (source == null || owned.Contains(source)) return;
        if (!fonts.TryGetValue(source,out TMP_FontAsset clone))
        {
            clone = Object.Instantiate(source); owned.Add(clone); clone.atlasPopulationMode = AtlasPopulationMode.Static;
            clone.fallbackFontAssetTable = new List<TMP_FontAsset>();
            Material material = Object.Instantiate(source.material); owned.Add(material); clone.material = material; fonts.Add(source,clone);
        }
        text.font = clone; text.fontSharedMaterial = clone.material;
    }
    private static void Cleanup(Action action,List<Exception> errors)
    { try { action(); } catch (Exception exception) { errors.Add(exception); } }
}
