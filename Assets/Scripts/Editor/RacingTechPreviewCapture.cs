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

/// <summary>Production-prefab controlled EditMode evidence; never opens or saves a gameplay scene.</summary>
public static class RacingTechPreviewCapture
{
    [Serializable] private sealed class Record
    {
        public string screenshot, team, fixture;
        public int width, height, checkedLabels;
        public List<string> overflows = new List<string>();
    }
    [Serializable] private sealed class Manifest
    {
        public string kind = "Controlled EditMode production RaceCanvas prefab preview; no live race, track or gameplay manager";
        public string utc, stage;
        public string scaling = "Production reference 1920x1080, width/height match 0.5; same-aspect captures use width/1920";
        public List<Record> records = new List<Record>();
    }

    public static string Capture(string stage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Capture requires idle EditMode.");
        if (stage != "before" && stage != "after") throw new ArgumentException("Use before or after.", nameof(stage));
        var manifest = new Manifest { utc = DateTime.UtcNow.ToString("o"), stage = stage };
        string directory = Path.GetFullPath("production/qa/evidence/racing-tech-ui-2026-10-14/" + stage);
        Directory.CreateDirectory(directory);
        foreach (TeamId team in new[] { TeamId.CN, TeamId.UK, TeamId.US, TeamId.DE, TeamId.IT, TeamId.JP })
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1280, 720) })
                CaptureOne(team, size, directory, manifest);
        File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonUtility.ToJson(manifest, true));
        int overflow = 0;
        foreach (Record record in manifest.records) overflow += record.overflows.Count;
        return JsonUtility.ToJson(new CaptureSummary { screenshots = manifest.records.Count, overflowRecords = overflow,
            manifest = Path.Combine(directory, "manifest.json") });
    }

    [Serializable] private sealed class CaptureSummary { public int screenshots, overflowRecords; public string manifest; }

    private static void CaptureOne(TeamId team, Vector2Int size, string directory, Manifest manifest)
    {
        Scene previewScene = default;
        GameObject staging = null, root = null, cameraHost = null;
        RenderTexture target = null;
        RenderTexture previous = RenderTexture.active;
        var fonts = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        var owned = new List<Object>();
        try
        {
            previewScene = EditorSceneManager.NewPreviewScene();
            staging = new GameObject("Racing tech inactive staging");
            staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, previewScene);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/RaceCanvas.prefab");
            if (prefab == null) throw new InvalidOperationException("Production RaceCanvas is missing.");
            root = Object.Instantiate(prefab, staging.transform, false);
            root.SetActive(false);
            root.transform.SetParent(null, false);
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) CloneFont(text, fonts, owned);
            foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null && script.GetType().Assembly == typeof(HUDUI).Assembly) script.enabled = false;

            cameraHost = new GameObject("Racing tech preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraHost, previewScene);
            Camera camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = previewScene;
            camera.cullingMask = 1 << 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f, 1f);
            target = new RenderTexture(size.x, size.y, 24);
            if (!target.Create()) throw new InvalidOperationException("Preview render allocation failed.");
            camera.targetTexture = target;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            canvas.scaleFactor = size.x / 1920f;
            root.SetActive(true);
            HUDUI hud = root.GetComponentInChildren<HUDUI>(true);
            if (hud == null) throw new InvalidOperationException("Production prefab has no HUDUI.");
            typeof(HUDUI).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
            RaceUILayoutController layout = root.GetComponent<RaceUILayoutController>();
            if (layout == null) layout = root.AddComponent<RaceUILayoutController>();
            layout.ApplyLayout(hud, root.GetComponentInChildren<CardHandUI>(true));
            int gear = team == TeamId.CN ? 2 : 3;
            hud.ConfigureGearPresentation(team, gear);
            var player = new PlayerState("受控预览车手", false, 0, gear) { teamId = team, gear = gear, selectedGearThisTurn = gear,
                usesChinaGearSystem = team == TeamId.CN, name = "受控预览车手" };
            hud.TeamGearPresentation.Refresh(player, gear, true, false, null, false);
            hud.SetStatus("受控 UI 预览 · 第 12 回合 · 等待选择");
            if (hud.lapText != null) hud.lapText.text = "圈数 2 / 3";
            if (hud.positionText != null) hud.positionText.text = "排名 3 / 12";
            if (hud.standingsText != null) hud.standingsText.text = "1 中国 · 麻辣火锅\n2 英国 · 皇家司康\n3 德国 · 黑森林蛋糕";
            var heat = new HeatGaugeState(5, 4, 1, 9, 5f / 9f, HeatWarningLevel.Elevated);
            player.turnMovement.BeginBasePlan();
            player.turnMovement.AddPlan("速度牌", 11, TurnMovementKind.Card);
            player.turnMovement.AddPlan("车手增益", 2, TurnMovementKind.Driver);
            player.turnMovement.AddPlan("科技增益", 1, TurnMovementKind.Technology);
            player.turnMovement.Advance(6);
            hud.TeamGearPresentation.Telemetry.Refresh(player.turnMovement, heat);
            Render("actual-6-of-14", team, size, directory, manifest, root, camera, target, fonts, owned);

            player.turnMovement.Reset();
            var projected = new TurnMovementBreakdown();
            projected.BeginBasePlan();
            projected.AddPlan("速度牌", 12, TurnMovementKind.Card);
            projected.AddPlan("科技增益", 2, TurnMovementKind.Technology);
            var forecast = new RaceTurnPreview();
            SetPreview(forecast, "Movement", projected);
            SetPreview(forecast, "ProjectedHeat", new HeatGaugeState(3, 6, 1, 9, 7f / 9f, HeatWarningLevel.Critical));
            SetPreview(forecast, "Team", team);
            SetPreview(forecast, "Gear", gear);
            SetPreview(forecast, "CanPay", true);
            hud.TeamGearPresentation.Telemetry.Refresh(player.turnMovement, heat, 0, forecast);
            Render("forecast-14-heat-5-to-7", team, size, directory, manifest, root, camera, target, fonts, owned);
        }
        finally
        {
            RenderTexture.active = previous;
            if (root != null) Object.DestroyImmediate(root);
            if (staging != null) Object.DestroyImmediate(staging);
            if (cameraHost != null) Object.DestroyImmediate(cameraHost);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            foreach (Object item in owned) if (item != null) Object.DestroyImmediate(item);
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static void SetPreview(RaceTurnPreview preview, string property, object value) =>
        typeof(RaceTurnPreview).GetProperty(property).SetValue(preview, value);

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

    private static void Render(string fixture, TeamId team, Vector2Int size, string directory, Manifest manifest,
        GameObject root, Camera camera, RenderTexture target, Dictionary<TMP_FontAsset, TMP_FontAsset> fonts, List<Object> owned)
    {
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 5;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) CloneFont(text, fonts, owned);
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var record = new Record { team = team.ToString(), width = size.x, height = size.y, fixture = fixture,
            screenshot = Path.Combine(directory, team + "-" + size.x + "x" + size.y + "-" + fixture + ".png") };
        foreach (RaceTelemetryStripUI strip in root.GetComponentsInChildren<RaceTelemetryStripUI>())
            foreach (TMP_Text text in strip.GetComponentsInChildren<TMP_Text>())
            {
                record.checkedLabels++;
                if (text.isTextOverflowing) record.overflows.Add(text.name + ": " + text.text);
            }
        Texture2D texture = null;
        RenderTexture prior = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            texture.Apply();
            File.WriteAllBytes(record.screenshot, texture.EncodeToPNG());
        }
        finally { RenderTexture.active = prior; if (texture != null) Object.DestroyImmediate(texture); }
        manifest.records.Add(record);
    }
}
