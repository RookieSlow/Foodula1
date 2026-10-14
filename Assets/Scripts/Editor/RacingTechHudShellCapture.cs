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
public static class RacingTechHudShellCapture
{
    [Serializable] private sealed class Record
    {
        public string screenshot, team, fixture;
        public int width, height, checkedLabels;
        public List<string> overflows = new List<string>();
        public List<LabelGeometry> labels = new List<LabelGeometry>();
    }
    [Serializable] private sealed class LabelGeometry
    {
        public string name, text;
        public float width, height, preferredWidth, preferredHeight, viewportHeight;
        public float scrollbarSize, scrollbarValue, verticalNormalizedPosition;
        public bool tmpOverflow, scrollContent;
    }
    [Serializable] private sealed class Manifest
    {
        public string kind = "Controlled EditMode production RaceCanvas prefab preview; no live race, track or gameplay manager";
        public string utc, stage;
        public string scaling = "Production reference 1920x1080, width/height match 0.5; same-aspect captures use width/1920";
        public string lifecycle = "Controlled EditMode manually calls HUD.Start and production ApplyLayout/Bind. Text events drive production standings sizing; each render then invokes the owned standings ScrollRect native LateUpdate to simulate its normal frame stage and update its thumb. No scrollbar size/value, content height or viewport measurement override. Cleanup explicitly calls owned shell OnDisable before destroying the preview. Before covers 4/12 racers; after additionally covers 2/6 racers.";
        public List<Record> records = new List<Record>();
    }

    public static string Capture(string stage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Capture requires idle EditMode.");
        if (stage != "before" && stage != "after") throw new ArgumentException("Use before or after.", nameof(stage));
        var manifest = new Manifest { utc = DateTime.UtcNow.ToString("o"), stage = stage };
        string directory = Path.GetFullPath("production/qa/evidence/racing-tech-hud-shell-2026-10-14/" + stage);
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
            foreach (bool stress in new[] { false, true })
            {
                ConfigureFixture(hud, team, stress);
                Render(stress ? "stress-12-top" : "normal-4", team, size, directory, manifest, root, camera, target, fonts, owned);
                ScrollRect scroll = hud.standingsText != null ? hud.standingsText.GetComponentInParent<ScrollRect>() : null;
                if (stress && scroll != null && scroll.content != null && scroll.viewport != null &&
                    scroll.content.rect.height > scroll.viewport.rect.height + 1f)
                {
                    scroll.verticalNormalizedPosition = 0f;
                    Render("stress-12-bottom", team, size, directory, manifest, root, camera, target, fonts, owned);
                }
            }
            if (manifest.stage == "after")
                foreach (int rosterCount in new[] { 2, 6 })
                {
                    ConfigureFixture(hud, team, false, rosterCount);
                    Render("normal-" + rosterCount, team, size, directory, manifest, root, camera, target, fonts, owned);
                }
        }
        finally
        {
            RenderTexture.active = previous;
            var cleanupErrors = new List<Exception>();
            if (root != null)
            {
                // Runtime callbacks are not automatic in synchronous EditMode.
                // Detach only this preview's global TMP subscriptions explicitly.
                Cleanup(() =>
                {
                    foreach (RacingTechHudShellUI shell in root.GetComponentsInChildren<RacingTechHudShellUI>(true))
                        Cleanup(() => typeof(RacingTechHudShellUI).GetMethod("OnDisable",
                            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null), cleanupErrors);
                }, cleanupErrors);
                Cleanup(() => Object.DestroyImmediate(root), cleanupErrors);
            }
            if (staging != null) Cleanup(() => Object.DestroyImmediate(staging), cleanupErrors);
            if (cameraHost != null) Cleanup(() => Object.DestroyImmediate(cameraHost), cleanupErrors);
            if (target != null)
            {
                Cleanup(() => target.Release(), cleanupErrors);
                Cleanup(() => Object.DestroyImmediate(target), cleanupErrors);
            }
            foreach (Object item in owned)
                if (item != null) Cleanup(() => Object.DestroyImmediate(item), cleanupErrors);
            if (previewScene.IsValid()) Cleanup(() => EditorSceneManager.ClosePreviewScene(previewScene), cleanupErrors);
            if (cleanupErrors.Count > 0) throw new AggregateException("Preview cleanup failed.", cleanupErrors);
        }
    }

    private static void Cleanup(Action action, List<Exception> errors)
    {
        try { action(); }
        catch (Exception exception) { errors.Add(exception); }
    }

    private static void ConfigureFixture(HUDUI hud, TeamId team, bool stress, int normalRosterCount = 4)
    {
        const int nodes = 142;
        int gear = team == TeamId.CN ? 2 : 3;
        int lap = stress ? 999 : 2;
        int totalLaps = stress ? 1000 : 3;
        var player = new PlayerState(stress ? "受控预览中国美食赛车超级长中文车手名称" : "受控预览车手", false, 137, gear)
            { teamId = team, selectedGearThisTurn = gear, usesChinaGearSystem = team == TeamId.CN, lap = lap };
        player.deck.InitializeExactOrder(new CardData[0], new HeatPool(12));
        player.deck.DrawHeatFromPoolToHand(5);
        player.turnMovement.BeginBasePlan();
        player.turnMovement.AddPlan("速度牌", 11, TurnMovementKind.Card);
        player.turnMovement.AddPlan("车手增益", 2, TurnMovementKind.Driver);
        player.turnMovement.AddPlan("科技增益", 1, TurnMovementKind.Technology);
        player.turnMovement.Advance(6);
        var roster = new List<PlayerState>();
        TeamId[] teams = { TeamId.CN, TeamId.UK, TeamId.US, TeamId.DE, TeamId.IT, TeamId.JP };
        int count = stress ? 12 : normalRosterCount;
        int playerIndex = Mathf.Min(2, count - 1);
        for (int i = 0; i < count; i++)
        {
            if (i == playerIndex) { roster.Add(player); continue; }
            var rival = new PlayerState(stress ? "美食赛车超长中文名称第" + (i + 1) + "号车手" : "美食车手" + (i + 1), true,
                139 - i, 2) { teamId = teams[i % teams.Length], lap = lap };
            rival.deck.InitializeExactOrder(new CardData[0], new HeatPool(12));
            roster.Add(rival);
        }
        hud.ConfigureGearPresentation(team, gear);
        hud.RefreshPlayerResources(player);
        hud.TeamGearPresentation.Refresh(player, gear, true, false, null, false);
        hud.SetStatus(stress ? "受控 UI 预览 · 第 99999 回合 · 等待选择挡位与确认当前操作，实际与预计数据分别显示" :
            "受控 UI 预览 · 第 12 回合 · 等待选择挡位");
        if (hud.lapText != null) hud.lapText.text = RaceHudTextRules.FormatLap(player, totalLaps);
        if (hud.positionText != null) hud.positionText.text = RaceHudTextRules.FormatPosition(player, roster, nodes);
        if (hud.aiStatusText != null) hud.aiStatusText.text = RaceHudTextRules.FormatAiStatus(roster[0], nodes);
        if (hud.weatherText != null) hud.weatherText.text = stress ? "天气：大雨 · 弯道限速降低" : "天气：多云";
        if (hud.standingsText != null) hud.standingsText.text = RaceHudTextRules.FormatStandings(roster, player, nodes);
        Canvas.ForceUpdateCanvases();
        ScrollRect scroll = hud.standingsText != null ? hud.standingsText.GetComponentInParent<ScrollRect>() : null;
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
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

    private static void Render(string fixture, TeamId team, Vector2Int size, string directory, Manifest manifest,
        GameObject root, Camera camera, RenderTexture target, Dictionary<TMP_FontAsset, TMP_FontAsset> fonts, List<Object> owned)
    {
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 5;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) CloneFont(text, fonts, owned);
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        // Synchronous EditMode has no native player-loop LateUpdate. Run only
        // this preview's standings ScrollRect stage, never assign thumb size.
        MethodInfo scrollLateUpdate = typeof(ScrollRect).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        if (scrollLateUpdate == null) throw new MissingMethodException(typeof(ScrollRect).FullName, "LateUpdate");
        foreach (RacingTechHudShellUI shell in root.GetComponentsInChildren<RacingTechHudShellUI>())
        {
            ScrollRect standingsScroll = shell.StandingsScroll;
            if (standingsScroll != null && standingsScroll.isActiveAndEnabled)
                scrollLateUpdate.Invoke(standingsScroll, null);
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var record = new Record { team = team.ToString(), width = size.x, height = size.y, fixture = fixture,
            screenshot = Path.Combine(directory, team + "-" + size.x + "x" + size.y + "-" + fixture + ".png") };
        HUDUI hud = root.GetComponentInChildren<HUDUI>(true);
        foreach (TMP_Text text in new[] { hud.statusText, hud.gearText, hud.heatText, hud.lapText, hud.positionText,
            hud.aiStatusText, hud.weatherText, hud.standingsText })
            if (text != null && text.gameObject.activeInHierarchy)
            {
                record.checkedLabels++;
                ScrollRect scroll = text.GetComponentInParent<ScrollRect>();
                bool scrollContent = scroll != null && scroll.content != null &&
                    (text.transform == scroll.content || text.transform.IsChildOf(scroll.content));
                record.labels.Add(new LabelGeometry { name = text.name, text = text.text, width = text.rectTransform.rect.width,
                    height = text.rectTransform.rect.height, preferredWidth = text.preferredWidth, preferredHeight = text.preferredHeight,
                    tmpOverflow = text.isTextOverflowing, scrollContent = scrollContent,
                    viewportHeight = scroll != null && scroll.viewport != null ? scroll.viewport.rect.height : 0f,
                    scrollbarSize = scroll != null && scroll.verticalScrollbar != null ? scroll.verticalScrollbar.size : 0f,
                    scrollbarValue = scroll != null && scroll.verticalScrollbar != null ? scroll.verticalScrollbar.value : 0f,
                    verticalNormalizedPosition = scroll != null ? scroll.verticalNormalizedPosition : 0f });
                // Scroll content may exceed its viewport intentionally, but its
                // own text rect must still contain the laid-out text.
                if (text.isTextOverflowing) record.overflows.Add(text.name + (scrollContent ? " (scroll content rect)" : "") + ": " + text.text);
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
