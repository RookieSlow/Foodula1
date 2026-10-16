using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RacingTechTrackSelectionTests
{
    private string previousTrack;
    private static TMP_FontAsset LoadFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");

    [SetUp]
    public void SaveSelection() { previousTrack = TrackSelectionState.SelectedTrackId; TrackSelectionState.Reset(); }

    [TearDown]
    public void RestoreSelection()
    {
        TrackSelectionState.Reset();
        if (!string.IsNullOrEmpty(previousTrack)) Assert.That(TrackSelectionState.TrySelect(previousTrack), Is.True);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    public void RenderedCardClickKeepsCatalogIdentityAndInvokesOriginalSelectionOnce(int index)
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            SeedFont(scope);
            TrackSelectionUI view = scope.Root.AddComponent<TrackSelectionUI>();
            int calls = 0; string chosen = null;
            view.Initialize(id => { calls++; chosen = id; });
            view.Show();
            TrackSelectionOption option = TrackSelectionState.AvailableTracks[index];
            Transform card = Card(scope.Root.transform, option.TrackId);
            Assert.That(card.Find("TrackName").GetComponent<TMP_Text>().text, Is.EqualTo(option.DisplayName));
            Assert.That(card.Find("Subtitle").GetComponent<TMP_Text>().text, Is.EqualTo(option.Subtitle));
            Assert.That(card.Find("TrackName").GetComponent<TMP_Text>().font, Is.SameAs(scope.Font));
            scope.Click(card.GetComponent<Button>());
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(chosen, Is.EqualTo(option.TrackId));
            Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo(option.TrackId));
            Assert.That(card.Find("SelectionEdge").gameObject.activeSelf, Is.True);
            Assert.That(card.Find("Action").GetComponent<TMP_Text>().text, Is.EqualTo("当前选择 · 点击开始"));
            Assert.That(card.Find("SelectionEdge").GetComponent<Image>().raycastTarget, Is.False);
        }
    }

    [Test]
    public void ReinitializeReplacesCallbackAndShowHideReuseTreeWithoutChangingSelection()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            SeedFont(scope);
            TrackSelectionUI view = scope.Root.AddComponent<TrackSelectionUI>();
            int oldCalls = 0, newCalls = 0;
            view.Initialize(id => oldCalls++);
            int count = scope.Root.GetComponentsInChildren<Transform>(true).Length;
            view.Initialize(id => newCalls++);
            Assert.That(TrackSelectionState.TrySelect("monza_pasta"), Is.True);
            for (int i = 0; i < 4; i++) { view.Show(); view.Hide(); }
            Assert.That(scope.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo("monza_pasta"));
            Assert.That(oldCalls + newCalls, Is.Zero);
            view.Show();
            scope.Click(Card(scope.Root.transform, "monza_pasta").GetComponent<Button>());
            Assert.That(oldCalls, Is.Zero);
            Assert.That(newCalls, Is.EqualTo(1));
            Assert.That(scope.Root.transform.Find("TrackSelectionOverlay").GetSiblingIndex(), Is.EqualTo(scope.Root.transform.childCount - 1));
        }
    }

    [Test]
    public void BackClosesWithoutStartingAndOriginalPanelGridFontsRemain()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            SeedFont(scope);
            TrackSelectionUI view = scope.Root.AddComponent<TrackSelectionUI>();
            int calls = 0;
            view.Initialize(id => calls++);
            view.Show();
            Transform panel = scope.Root.transform.Find("TrackSelectionOverlay/TrackSelectionPanel");
            Assert.That(((RectTransform)panel).sizeDelta, Is.EqualTo(new Vector2(1360f, 820f)));
            Assert.That(panel.GetComponent<Outline>(), Is.Null);
            Assert.That(panel.GetComponent<Image>().color, Is.EqualTo(RacingTechTheme.Surface));
            GridLayoutGroup grid = panel.Find("TrackGrid").GetComponent<GridLayoutGroup>();
            Assert.That(grid.constraintCount, Is.EqualTo(4));
            Assert.That(grid.cellSize, Is.EqualTo(new Vector2(290f, 200f)));
            Assert.That(grid.transform.childCount, Is.EqualTo(8));
            foreach (TMP_Text label in panel.GetComponentsInChildren<TMP_Text>(true))
                Assert.That(label.font, Is.SameAs(scope.Font), label.name);
            scope.Click(panel.Find("BackButton").GetComponent<Button>());
            Assert.That(panel.parent.gameObject.activeSelf, Is.False);
            Assert.That(calls, Is.Zero);
            Assert.That(TrackSelectionState.SelectedTrackId, Is.Empty);
        }
    }

    [Test]
    public void BackgroundOverlayInterceptsClicksOutsidePanelAndDecorationDoesNotReceiveInput()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            SeedFont(scope);
            TrackSelectionUI view = scope.Root.AddComponent<TrackSelectionUI>();
            view.Initialize(null); view.Show();
            foreach (Transform child in scope.Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            Canvas.ForceUpdateCanvases();
            Camera camera = scope.Root.GetComponent<Canvas>().worldCamera;
            RenderTexture previous = RenderTexture.active;
            try { camera.Render(); } finally { RenderTexture.active = previous; }
            var data = new PointerEventData(scope.EventSystem) { position = new Vector2(25f, 25f) };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            scope.Root.GetComponent<GraphicRaycaster>().Raycast(data, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.name, Is.EqualTo("TrackSelectionOverlay"));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.Null);
            Transform panel = scope.Root.transform.Find("TrackSelectionOverlay/TrackSelectionPanel");
            foreach (Image image in panel.GetComponentsInChildren<Image>(true))
                if (image.name == "HeaderSeparator" || image.name == "TrackIdentity" || image.name == "SelectionEdge")
                    Assert.That(image.raycastTarget, Is.False, image.name);
        }
    }

    [Test]
    public void PointerStatesKeepMeasuredContrastAndDisabledClickDoesNotSelectOrChangeMarker()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            Button targetButton = null;
            Navigation originalNavigation = default;
            try
            {
                SeedFont(scope);
                TrackSelectionUI view = scope.Root.AddComponent<TrackSelectionUI>();
                int calls = 0;
                view.Initialize(id => calls++); view.Show();
                Transform selected = Card(scope.Root.transform, "silverstone_afternoon_tea");
                Transform target = Card(scope.Root.transform, "monza_pasta");
                Button button = target.GetComponent<Button>();
                targetButton = button;
                originalNavigation = button.navigation;
                Navigation pointerOnly = originalNavigation;
                pointerOnly.mode = Navigation.Mode.None;
                button.navigation = pointerOnly;
                var pointer = new PointerEventData(scope.EventSystem) { button = PointerEventData.InputButton.Left };
                AssertCardTextContrast(target);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                AssertCardTextContrast(target);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                AssertCardTextContrast(target);
                Assert.That(target.Find("SelectionEdge").gameObject.activeSelf, Is.False,
                    "Pointer focus must not masquerade as the selected track.");
                Assert.That(selected.Find("SelectionEdge").gameObject.activeSelf, Is.True);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                AssertCardTextContrast(target);
                button.interactable = false;
                AssertCardTextContrast(target);
                scope.Click(selected.GetComponent<Button>());
                int acceptedCalls = calls;
                string acceptedTrack = TrackSelectionState.SelectedTrackId;
                RectTransform targetRect = target.GetComponent<RectTransform>();
                Camera camera = scope.Root.GetComponent<Canvas>().worldCamera;
                pointer.position = RectTransformUtility.WorldToScreenPoint(camera, targetRect.TransformPoint(targetRect.rect.center));
                var hits = new List<RaycastResult>();
                scope.Root.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
                Assert.That(hits, Is.Not.Empty);
                GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                Assert.That(receiver, Is.SameAs(button.gameObject));
                Assert.That(ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler), Is.True);
                Assert.That(calls, Is.EqualTo(acceptedCalls));
                Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo(acceptedTrack));
                Assert.That(target.Find("SelectionEdge").gameObject.activeSelf, Is.False);
                Assert.That(selected.Find("SelectionEdge").gameObject.activeSelf, Is.True);
            }
            finally { if (targetButton != null) targetButton.navigation = originalNavigation; }
        }
    }

    [UnityTest]
    public IEnumerator NativeShowClickHideReopenKeepsMarkerCallbackAndDisabledGate()
    {
        yield return new EnterPlayMode();
        Scene scene = SceneManager.CreateScene("Owned track selection native lifecycle");
        GameObject root = null, events = null;
        TMP_FontAsset font = null;
        Material material = null;
        try
        {
            TMP_FontAsset source = LoadFont();
            bool sourceDirty = EditorUtility.IsDirty(source);
            font = Object.Instantiate(source);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            material = Object.Instantiate(source.material); font.material = material;
            root = new GameObject("Owned track selection canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var seed = new GameObject("Owned track font", typeof(RectTransform), typeof(TextMeshProUGUI));
            seed.transform.SetParent(root.transform, false); seed.GetComponent<TMP_Text>().font = font;
            seed.SetActive(false);
            events = new GameObject("Owned inactive track event data"); events.SetActive(false);
            SceneManager.MoveGameObjectToScene(events, scene);
            EventSystem eventSystem = events.AddComponent<EventSystem>();
            TrackSelectionUI view = root.AddComponent<TrackSelectionUI>();
            int calls = 0;
            view.Initialize(id => calls++); view.Show();
            yield return null;
            Transform target = Card(root.transform, "monza_pasta");
            Button button = target.GetComponent<Button>();
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo("monza_pasta"));
            Assert.That(target.Find("SelectionEdge").gameObject.activeSelf, Is.True);
            view.Hide(); yield return null;
            Assert.That(root.transform.Find("TrackSelectionOverlay").gameObject.activeSelf, Is.False);
            view.Show(); yield return null;
            Assert.That(target.Find("SelectionEdge").gameObject.activeSelf, Is.True);
            Assert.That(target.Find("Action").GetComponent<TMP_Text>().text, Is.EqualTo("当前选择 · 点击开始"));
            Assert.That(calls, Is.EqualTo(1));
            Button other = Card(root.transform, "suzuka_sushi").GetComponent<Button>();
            other.interactable = false;
            ExecuteEvents.Execute(other.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo("monza_pasta"));
            Assert.That(EditorUtility.IsDirty(source), Is.EqualTo(sourceDirty));
            Assert.That(target.Find("TrackName").GetComponent<TMP_Text>().font, Is.SameAs(font));
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            if (events != null) Object.DestroyImmediate(events);
            if (font != null) Object.DestroyImmediate(font);
            if (material != null) Object.DestroyImmediate(material);
        }
        yield return SceneManager.UnloadSceneAsync(scene);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditMode()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    private static void AssertCardTextContrast(Transform card)
    {
        Image background = card.GetComponent<Image>();
        Color effective = background.color * background.canvasRenderer.GetColor();
        foreach (TMP_Text label in card.GetComponentsInChildren<TMP_Text>())
        {
            Color text = label.color * label.canvasRenderer.GetColor();
            Assert.That(Contrast(text, effective), Is.GreaterThanOrEqualTo(4.5f), label.name);
        }
    }

    private static float Contrast(Color a, Color b)
    {
        float x = Luminance(a), y = Luminance(b);
        return (Mathf.Max(x, y) + 0.05f) / (Mathf.Min(x, y) + 0.05f);
    }
    private static float Luminance(Color c) => .2126f * Linear(c.r) + .7152f * Linear(c.g) + .0722f * Linear(c.b);
    private static float Linear(float c) => c <= .04045f ? c / 12.92f : Mathf.Pow((c + .055f) / 1.055f, 2.4f);

    private static void SeedFont(CareerUiPointerTestScope scope)
    {
        var host = new GameObject("Owned track selection font", typeof(RectTransform), typeof(TextMeshProUGUI));
        host.transform.SetParent(scope.Root.transform, false);
        host.GetComponent<TMP_Text>().font = scope.Font;
        host.SetActive(false);
    }

    private static Transform Card(Transform root, string id) => root.Find("TrackSelectionOverlay/TrackSelectionPanel/TrackGrid/Track_" + id);
}
