using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Test-owned preview Canvas and synchronous real-geometry pointer dispatch.
/// Does not change the active scene, input system, screen or source font.
/// Career/audio/profile isolation remains the responsibility of the fixture.
/// </summary>
internal sealed class CareerUiPointerTestScope : IDisposable
{
    internal GameObject Root { get; private set; }
    internal EventSystem EventSystem { get; private set; }
    internal Scene PreviewScene { get; private set; }
    internal RenderTexture Target { get; private set; }
    internal TMP_FontAsset Font { get; private set; }
    private GameObject eventHost;
    private GameObject cameraHost;
    private Camera renderCamera;
    private Material fontMaterial;
    private GraphicRaycaster raycaster;
    private bool disposed;

    // The loader keeps asset lookup outside resource ownership and lets tests
    // verify cleanup when loading fails after scene/target allocation.
    internal static CareerUiPointerTestScope Create(Func<TMP_FontAsset> loadFont)
    {
        if (loadFont == null) throw new ArgumentNullException(nameof(loadFont));
        var scope = new CareerUiPointerTestScope();
        try
        {
            scope.Initialize(loadFont);
            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    private CareerUiPointerTestScope() { }

    private void Initialize(Func<TMP_FontAsset> loadFont)
    {
        PreviewScene = EditorSceneManager.NewPreviewScene();
        // Inactive EventSystem supplies event data without replacing the user's
        // current EventSystem or starting an input module.
        eventHost = new GameObject("Career pointer event data");
        eventHost.SetActive(false);
        SceneManager.MoveGameObjectToScene(eventHost, PreviewScene);
        EventSystem = eventHost.AddComponent<EventSystem>();
        cameraHost = new GameObject("Career pointer render camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraHost, PreviewScene);
        renderCamera = cameraHost.GetComponent<Camera>();
        renderCamera.enabled = false;
        renderCamera.scene = PreviewScene;
        renderCamera.cullingMask = 1 << 5;
        renderCamera.clearFlags = CameraClearFlags.SolidColor;
        renderCamera.backgroundColor = Color.clear;
        Target = new RenderTexture(1920, 1080, 24);
        Assert.That(Target.Create(), Is.True, "Fixture render target must be available");
        renderCamera.targetTexture = Target;
        Root = new GameObject("Completed career pointer regression", typeof(RectTransform), typeof(Canvas));
        SceneManager.MoveGameObjectToScene(Root, PreviewScene);
        Canvas canvas = Root.GetComponent<Canvas>();
        // Synchronous EditMode has no automatic overlay render. A camera render
        // supplies actual depth without opening Game View or modifying a scene.
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = renderCamera;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 10000;
        CanvasScaler scaler = Root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        raycaster = Root.AddComponent<GraphicRaycaster>();

        TMP_FontAsset source = loadFont();
        Assert.That(source, Is.Not.Null, "Fixture font must be available");
        Font = Object.Instantiate(source);
        Font.atlasPopulationMode = AtlasPopulationMode.Static;
        Font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        fontMaterial = Object.Instantiate(source.material);
        Font.material = fontMaterial;
        GameObject fontSeed = new GameObject("Fixture font", typeof(RectTransform), typeof(TextMeshProUGUI));
        fontSeed.transform.SetParent(Root.transform, false);
        fontSeed.GetComponent<TMP_Text>().font = Font;
        fontSeed.SetActive(false);
    }

    internal void Click(Button button)
    {
        if (disposed) throw new ObjectDisposedException(nameof(CareerUiPointerTestScope));
        Assert.That(button != null && button.transform.IsChildOf(Root.transform), Is.True,
            "Button must belong to this fixture");
        string name = button.name;
        Assert.That(button.IsActive() && button.IsInteractable(), Is.True, name);
        foreach (Transform child in Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text label in Root.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        RenderTexture previousTarget = RenderTexture.active;
        try { renderCamera.Render(); }
        finally { RenderTexture.active = previousTarget; }
        Assert.That(button.targetGraphic.depth, Is.GreaterThanOrEqualTo(0), name + " must be rendered before hit testing");
        RectTransform rect = button.GetComponent<RectTransform>();
        Vector2 center = RectTransformUtility.WorldToScreenPoint(renderCamera, rect.TransformPoint(rect.rect.center));
        Assert.That(renderCamera.pixelRect.Contains(center), Is.True, name + " must be within the fixture viewport");
        var pointer = new PointerEventData(EventSystem) { position = center, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        raycaster.Raycast(pointer, hits);
        Assert.That(hits, Is.Not.Empty, name + " must have live Canvas hit geometry");
        GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.That(receiver, Is.SameAs(button.gameObject), name + " must be the top hit's click receiver");
        Assert.That(ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler), Is.True);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            if (Root != null) Object.DestroyImmediate(Root);
            if (eventHost != null) Object.DestroyImmediate(eventHost);
            if (cameraHost != null) Object.DestroyImmediate(cameraHost);
        }
        finally
        {
            try
            {
                if (Target != null)
                {
                    Target.Release();
                    Object.DestroyImmediate(Target);
                }
                if (Font != null) Object.DestroyImmediate(Font);
                if (fontMaterial != null) Object.DestroyImmediate(fontMaterial);
            }
            finally
            {
                if (PreviewScene.IsValid()) EditorSceneManager.ClosePreviewScene(PreviewScene);
            }
        }
    }
}

public sealed class CareerUiPointerTestScopeTests
{
    private static TMP_FontAsset LoadFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");

    [Test]
    public void Dispose_ReleasesOwnedResourcesIsIdempotentAndPreservesSceneAndEventSystem()
    {
        Scene active = SceneManager.GetActiveScene();
        bool dirty = active.isDirty;
        EventSystem current = EventSystem.current;
        int previews = EditorSceneManager.previewSceneCount;
        TMP_FontAsset source = LoadFont();
        Material sourceMaterial = source.material;
        AtlasPopulationMode population = source.atlasPopulationMode;
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            Scene preview = scope.PreviewScene;
            GameObject root = scope.Root;
            EventSystem events = scope.EventSystem;
            RenderTexture target = scope.Target;
            TMP_FontAsset font = scope.Font;
            Material material = font.material;
            Assert.That(root.scene, Is.EqualTo(preview));
            Assert.That(events.gameObject.scene, Is.EqualTo(preview));
            Assert.That(root.GetComponent<Canvas>().worldCamera.scene, Is.EqualTo(preview));
            Assert.That(target.width, Is.EqualTo(1920));
            Assert.That(target.height, Is.EqualTo(1080));
            Assert.That(font, Is.Not.SameAs(source));
            Assert.That(material, Is.Not.SameAs(sourceMaterial));
            scope.Dispose();
            scope.Dispose();
            Assert.That(root == null && events == null && target == null && font == null && material == null, Is.True);
            Assert.That(preview.IsValid(), Is.False);
            Assert.Throws<ObjectDisposedException>(() => scope.Click(null));
        }
        Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previews));
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
        Assert.That(active.isDirty, Is.EqualTo(dirty));
        Assert.That(EventSystem.current, Is.SameAs(current));
        Assert.That(source.material, Is.SameAs(sourceMaterial));
        Assert.That(source.atlasPopulationMode, Is.EqualTo(population));
    }

    [Test]
    public void FailingFontLoader_CleansPartialAllocationAndPreservesCallerRenderTarget()
    {
        int previews = EditorSceneManager.previewSceneCount;
        int owned = CountFixtureObjects();
        RenderTexture active = RenderTexture.active;
        var expected = new InvalidOperationException("Fixture font load failure");
        Assert.That(Assert.Throws<InvalidOperationException>(() =>
            CareerUiPointerTestScope.Create(() => { throw expected; })), Is.SameAs(expected));
        Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previews));
        Assert.That(CountFixtureObjects(), Is.EqualTo(owned));
        Assert.That(RenderTexture.active, Is.SameAs(active));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RealGeometryClick_RestoresCallerTargetAndRejectsOcclusion(bool occluded)
    {
        RenderTexture previous = RenderTexture.active;
        var caller = new RenderTexture(16, 16, 0);
        try
        {
            Assert.That(caller.Create(), Is.True);
            RenderTexture.active = caller;
            using (var scope = CareerUiPointerTestScope.Create(LoadFont))
            {
                var host = new GameObject("Scope test button", typeof(RectTransform), typeof(Image), typeof(Button));
                host.transform.SetParent(scope.Root.transform, false);
                host.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 50);
                Button button = host.GetComponent<Button>();
                button.targetGraphic = host.GetComponent<Image>();
                int calls = 0;
                button.onClick.AddListener(() => calls++);
                if (occluded)
                {
                    var blocker = new GameObject("Scope test blocker", typeof(RectTransform), typeof(Image));
                    blocker.transform.SetParent(scope.Root.transform, false);
                    blocker.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 50);
                    Assert.Throws<AssertionException>(() => scope.Click(button));
                }
                else scope.Click(button);
                Assert.That(calls, Is.EqualTo(occluded ? 0 : 1));
                Assert.That(RenderTexture.active, Is.SameAs(caller));
            }
            Assert.That(RenderTexture.active, Is.SameAs(caller));
        }
        finally
        {
            RenderTexture.active = previous;
            caller.Release();
            Object.DestroyImmediate(caller);
        }
    }

    private static int CountFixtureObjects()
    {
        int count = 0;
        foreach (GameObject item in Resources.FindObjectsOfTypeAll<GameObject>())
            if (item.name == "Completed career pointer regression" || item.name == "Career pointer event data" ||
                item.name == "Career pointer render camera") count++;
        return count;
    }
}
