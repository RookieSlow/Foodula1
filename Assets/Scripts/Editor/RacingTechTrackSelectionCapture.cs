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

/// <summary>Controlled production TrackSelectionUI entry point; no menu Start or race launch.</summary>
public static class RacingTechTrackSelectionCapture
{
    [Serializable] private sealed class Label
    {
        public string name, text;
        public float width, height, preferredWidth, preferredHeight;
        public bool overflow;
    }
    [Serializable] private sealed class Record
    {
        public string screenshot, fixture, selectedTrack, callbackTrack, nativeStateTrack;
        public int width, height, catalogCount, visibleCards;
        public bool overlayActive, nativeStateInteractable;
        public List<string> overflows = new List<string>();
        public List<Label> labels = new List<Label>();
    }
    [Serializable] private sealed class Manifest
    {
        public string kind = "Controlled EditMode real TrackSelectionUI.Initialize/Show, original track buttons and Hide. No MainMenuUI.Start, gameplay manager, launch or authored scene changes.";
        public string lifecycle = "Isolated preview Canvas/camera; original localized font and material cloned static before production UI creation. Callback only records ID. Session selectedTrackId restored exactly in finally. Hover/pressed use native Selectable.DoStateTransition with instant=true because synchronous EditMode has no tween player loop; disabled also sets real interactable=false. No click is synthesized for these states. Closed image intentionally shows empty preview host, not a fabricated main menu. Geometry counts only activeInHierarchy labels/cards. No fake ninth track.";
        public string utc, stage;
        public List<Record> records = new List<Record>();
    }
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string Capture(string stage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Capture requires idle EditMode.");
        if (stage != "before" && stage != "after") throw new ArgumentException("Use before or after.");
        var manifest = new Manifest { utc = DateTime.UtcNow.ToString("o"), stage = stage };
        string dir = Path.GetFullPath("production/qa/evidence/racing-tech-track-selection-2026-10-15/" + stage);
        Directory.CreateDirectory(dir);
        foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1280, 720) })
            CaptureSize(stage, size, dir, manifest);
        string path = Path.Combine(dir, "manifest.json");
        File.WriteAllText(path, JsonUtility.ToJson(manifest, true));
        int overflow = 0;
        foreach (Record record in manifest.records) overflow += record.overflows.Count;
        return "screenshots=" + manifest.records.Count + "; overflows=" + overflow + "; manifest=" + path;
    }

    private static void CaptureSize(string stage, Vector2Int size, string dir, Manifest manifest)
    {
        Scene scene = default;
        GameObject root = null, cameraHost = null;
        RenderTexture target = null;
        RenderTexture previous = RenderTexture.active;
        var owned = new List<Object>();
        FieldInfo selected = typeof(TrackSelectionState).GetField("selectedTrackId", BindingFlags.Static | BindingFlags.NonPublic);
        object priorSelected = selected.GetValue(null);
        try
        {
            scene = EditorSceneManager.NewPreviewScene();
            root = new GameObject("Track selection production entry preview", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.SetActive(false);
            TMP_FontAsset original = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");
            if (original == null) throw new InvalidOperationException("Existing production Chinese font missing.");
            TMP_FontAsset font = Object.Instantiate(original);
            owned.Add(font);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            Material material = Object.Instantiate(original.material);
            owned.Add(material);
            font.material = material;
            GameObject seed = new GameObject("Existing font seed", typeof(RectTransform));
            seed.transform.SetParent(root.transform, false);
            TextMeshProUGUI seedText = seed.AddComponent<TextMeshProUGUI>();
            seedText.font = font;
            seedText.fontSharedMaterial = material;
            seed.SetActive(false);
            cameraHost = new GameObject("Track selection preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            Camera camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.cullingMask = 1 << 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f, 1f);
            target = new RenderTexture(size.x, size.y, 24);
            if (!target.Create()) throw new InvalidOperationException("Render allocation failed.");
            camera.targetTexture = target;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.scaleFactor = size.x / 1920f;
            root.SetActive(true);
            TrackSelectionUI view = root.AddComponent<TrackSelectionUI>();
            string callback = string.Empty;
            view.Initialize(id => callback = id);
            selected.SetValue(null, null);
            view.Show();
            Render("default", size, dir, manifest, root, camera, target, callback);
            if (stage == "after")
            {
                foreach (TrackSelectionOption track in TrackSelectionState.AvailableTracks)
                {
                    Transform card = root.transform.Find("TrackSelectionOverlay/TrackSelectionPanel/TrackGrid/Track_" + track.TrackId);
                    if (card == null) throw new InvalidOperationException("Production card missing: " + track.TrackId);
                    card.GetComponent<Button>().onClick.Invoke();
                    view.Show();
                    Render("selected-" + track.TrackId, size, dir, manifest, root, camera, target, callback);
                }
                Button stateButton = root.transform.Find("TrackSelectionOverlay/TrackSelectionPanel/TrackGrid/Track_" +
                    TrackSelectionState.AvailableTracks[0].TrackId).GetComponent<Button>();
                foreach (string state in new[] { "Highlighted", "Pressed", "Disabled" })
                {
                    bool priorInteractable = stateButton.interactable;
                    try
                    {
                        if (state == "Disabled") stateButton.interactable = false;
                        ApplyNativeState(stateButton, state);
                        Render("selectable-" + state.ToLowerInvariant(), size, dir, manifest, root, camera, target, callback);
                    }
                    finally
                    {
                        stateButton.interactable = priorInteractable;
                        ApplyNativeState(stateButton, "Normal");
                    }
                }
                root.transform.Find("TrackSelectionOverlay/TrackSelectionPanel/BackButton").GetComponent<Button>().onClick.Invoke();
                Render("closed", size, dir, manifest, root, camera, target, callback);
            }
        }
        finally
        {
            RenderTexture.active = previous;
            selected.SetValue(null, priorSelected);
            var errors = new List<Exception>();
            if (root != null)
            {
                Cleanup(() =>
                {
                    foreach (Button button in root.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
                    foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (script == null || script.GetType().Assembly != typeof(TrackSelectionUI).Assembly) continue;
                        MethodInfo disable = script.GetType().GetMethod("OnDisable", Private);
                        if (disable != null) Cleanup(() => disable.Invoke(script, null), errors);
                    }
                }, errors);
                Cleanup(() => Object.DestroyImmediate(root), errors);
            }
            if (cameraHost != null) Cleanup(() => Object.DestroyImmediate(cameraHost), errors);
            if (target != null) { Cleanup(() => target.Release(), errors); Cleanup(() => Object.DestroyImmediate(target), errors); }
            foreach (Object item in owned) if (item != null) Cleanup(() => Object.DestroyImmediate(item), errors);
            if (scene.IsValid()) Cleanup(() => EditorSceneManager.ClosePreviewScene(scene), errors);
            if (errors.Count > 0) throw new AggregateException("Preview cleanup errors", errors);
        }
    }

    private static void Render(string fixture, Vector2Int size, string dir, Manifest manifest, GameObject root, Camera camera, RenderTexture target, string callback)
    {
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 5;
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        camera.Render();
        Transform overlay = root.transform.Find("TrackSelectionOverlay");
        var record = new Record { fixture = fixture, width = size.x, height = size.y, catalogCount = TrackSelectionState.AvailableTracks.Count,
            selectedTrack = TrackSelectionState.ResolveTrackId(string.Empty), callbackTrack = callback, overlayActive = overlay.gameObject.activeSelf,
            screenshot = Path.Combine(dir, size.x + "x" + size.y + "-" + fixture + ".png") };
        if (fixture.StartsWith("selectable-"))
        {
            record.nativeStateTrack = TrackSelectionState.AvailableTracks[0].TrackId;
            record.nativeStateInteractable = overlay.Find("TrackSelectionPanel/TrackGrid/Track_" + record.nativeStateTrack).GetComponent<Button>().interactable;
        }
        foreach (TMP_Text text in overlay.GetComponentsInChildren<TMP_Text>())
        {
            if (!text.gameObject.activeInHierarchy) continue;
            Rect rect = text.rectTransform.rect;
            record.labels.Add(new Label { name = text.name, text = text.text, width = rect.width, height = rect.height,
                preferredWidth = text.preferredWidth, preferredHeight = text.preferredHeight, overflow = text.isTextOverflowing });
            if (text.isTextOverflowing) record.overflows.Add(text.name + ":" + text.text);
        }
        foreach (Button button in overlay.GetComponentsInChildren<Button>())
            if (button.gameObject.activeInHierarchy && button.name.StartsWith("Track_")) record.visibleCards++;
        RenderTexture prior = RenderTexture.active;
        Texture2D texture = null;
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

    private static void Cleanup(Action action, List<Exception> errors)
    {
        try { action(); } catch (Exception error) { errors.Add(error); }
    }

    private static void ApplyNativeState(Button button, string name)
    {
        Type stateType = typeof(Selectable).GetNestedType("SelectionState", BindingFlags.NonPublic);
        MethodInfo transition = typeof(Selectable).GetMethod("DoStateTransition", Private);
        if (stateType == null || transition == null) throw new InvalidOperationException("Native Selectable state API unavailable.");
        transition.Invoke(button, new[] { Enum.Parse(stateType, name), (object)true });
    }
}
