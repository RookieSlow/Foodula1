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

public sealed class RacingTechHudShellTests
{
    private static TMP_FontAsset LoadFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");

    [Test]
    public void AttachPreservesBindingsFontsButtonsOuterAnchorsAndEntireTrackAppearance()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            HUDUI hud = Build(scope);
            TMP_Text original = hud.standingsText;
            Material material = original.fontSharedMaterial;
            RectTransform track = (RectTransform)scope.Root.transform.Find("TrackFrame");
            Image trackImage = track.GetComponent<Image>();
            Color trackColor = new Color(0.2f, 0.3f, 0.4f, 0f);
            trackImage.color = trackColor;
            trackImage.raycastTarget = true;
            Outline trackOutline = track.GetComponent<Outline>();
            trackOutline.enabled = true;
            Vector2 trackMin = track.anchorMin, trackMax = track.anchorMax;
            RectTransform operation = (RectTransform)scope.Root.transform.Find("OperationPanel");
            Vector2 operationMin = operation.anchorMin, operationMax = operation.anchorMax;
            Button button = MakeButton(operation);
            hud.returnToMenuButton = button;
            button.interactable = false;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            button.navigation = navigation;
            int calls = 0;
            button.onClick.AddListener(() => calls++);
            RacingTechHudShellUI shell = RacingTechHudShellUI.Attach(scope.Root.transform, hud);
            Assert.That(hud.standingsText, Is.SameAs(original));
            Assert.That(shell.StandingsScroll.content, Is.SameAs(original.rectTransform));
            Assert.That(original.font, Is.SameAs(scope.Font));
            Assert.That(original.fontSharedMaterial, Is.SameAs(material));
            Assert.That(original.text, Is.EqualTo("等待排名…"));
            Assert.That(button.interactable, Is.False);
            Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            button.onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(trackImage.color, Is.EqualTo(trackColor));
            Assert.That(trackImage.raycastTarget, Is.True);
            Assert.That(trackOutline.enabled, Is.True);
            Assert.That(track.anchorMin, Is.EqualTo(trackMin));
            Assert.That(track.anchorMax, Is.EqualTo(trackMax));
            Assert.That(operation.anchorMin, Is.EqualTo(operationMin));
            Assert.That(operation.anchorMax, Is.EqualTo(operationMax));
            Assert.That(operation.GetComponent<Outline>().enabled, Is.False);
            Assert.That(scope.Root.transform.Find("DeckTablePanel/HandPanel").GetComponent<Outline>().enabled, Is.True);
            Assert.That(hud.gearText.text, Is.EqualTo("档位: Recover"));
            Assert.That(hud.gearText.fontSize, Is.EqualTo(18f));
            Assert.That(hud.statusText.fontSize, Is.EqualTo(14f));
            Assert.That(hud.statusText.rectTransform.anchorMin.y, Is.EqualTo(0.20f));
            Assert.That(hud.statusText.rectTransform.anchorMax.y, Is.EqualTo(0.29f));
        }
    }

    [Test]
    public void ReattachReusesTreeAndKeepsReaderPositionAndButtonGeometry()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            HUDUI hud = Build(scope);
            hud.standingsText.text = LongStandings();
            RacingTechHudShellUI shell = RacingTechHudShellUI.Attach(scope.Root.transform, hud);
            Render(scope);
            shell.StandingsScroll.verticalNormalizedPosition = 0.35f;
            Vector2 before = hud.standingsText.rectTransform.anchoredPosition;
            int count = scope.Root.GetComponentsInChildren<Transform>(true).Length;
            for (int i = 0; i < 5; i++)
                Assert.That(RacingTechHudShellUI.Attach(scope.Root.transform, hud), Is.SameAs(shell));
            Assert.That(scope.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(hud.standingsText.rectTransform.anchoredPosition.y, Is.EqualTo(before.y).Within(0.5f));
        }
    }

    [Test]
    public void TextNotificationGrowsTwelveRowsShrinksShortTextAndRespondsToViewportHeight()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            HUDUI hud = Build(scope);
            RacingTechHudShellUI shell = RacingTechHudShellUI.Attach(scope.Root.transform, hud);
            Render(scope);
            hud.standingsText.text = LongStandings();
            TMPro_EventManager.ON_TEXT_CHANGED(hud.standingsText);
            float longHeight = shell.StandingsScroll.content.rect.height;
            Assert.That(longHeight, Is.GreaterThan(shell.StandingsScroll.viewport.rect.height));
            hud.standingsText.text = "1. CN";
            TMPro_EventManager.ON_TEXT_CHANGED(hud.standingsText);
            Assert.That(shell.StandingsScroll.content.rect.height,
                Is.EqualTo(shell.StandingsScroll.viewport.rect.height).Within(0.5f));
            RectTransform board = shell.GetComponent<RectTransform>();
            board.anchorMax = new Vector2(board.anchorMax.x, 0.75f);
            Canvas.ForceUpdateCanvases();
            shell.RefreshStandingsSize();
            Assert.That(shell.StandingsScroll.content.rect.height,
                Is.EqualTo(shell.StandingsScroll.viewport.rect.height).Within(0.5f));
            Assert.That(shell.StandingsScroll.content.rect.height, Is.LessThan(longHeight));
        }
    }

    [Test]
    public void ExplicitLifecycleSimulationUnsubscribesAndRebindsWithoutUpdateLoop()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            HUDUI hud = Build(scope);
            RacingTechHudShellUI shell = RacingTechHudShellUI.Attach(scope.Root.transform, hud);
            Render(scope);
            float originalHeight = shell.StandingsScroll.content.rect.height;
            shell.enabled = false;
            typeof(RacingTechHudShellUI).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
            hud.standingsText.text = LongStandings();
            TMPro_EventManager.ON_TEXT_CHANGED(hud.standingsText);
            Assert.That(shell.StandingsScroll.content.rect.height, Is.EqualTo(originalHeight));
            shell.enabled = true;
            typeof(RacingTechHudShellUI).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
            Assert.That(shell.StandingsScroll.content.rect.height, Is.GreaterThan(originalHeight));
            Assert.That(typeof(RacingTechHudShellUI).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
            Assert.That(typeof(RacingTechHudShellUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
        }
    }

    [UnityTest]
    public IEnumerator NativeLifecycleResizesOnTextChangeAndReenableWithoutMutatingSourceFont()
    {
        yield return new EnterPlayMode();
        Scene scene = SceneManager.CreateScene("Racing tech shell native lifecycle");
        GameObject root = null;
        TMP_FontAsset font = null;
        Material material = null;
        try
        {
            TMP_FontAsset source = LoadFont();
            bool dirty = EditorUtility.IsDirty(source);
            font = Object.Instantiate(source);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            material = Object.Instantiate(source.material);
            font.material = material;
            root = new GameObject("Owned shell native canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var hudHost = new GameObject("Inactive data binding owner");
            hudHost.SetActive(false);
            hudHost.transform.SetParent(root.transform, false);
            HUDUI hud = hudHost.AddComponent<HUDUI>();
            RectTransform board = Panel(root.transform, "ScoreboardPanel", new Vector2(0.16f, 0.355f), new Vector2(0.31f, 0.985f));
            hud.standingsText = Label(board, "StandingsText", font, "1. CN", 14f);
            RacingTechHudShellUI shell = RacingTechHudShellUI.Attach(root.transform, hud);
            yield return null;
            Canvas.ForceUpdateCanvases();
            float shortHeight = shell.StandingsScroll.content.rect.height;
            hud.standingsText.text = LongStandings();
            yield return null;
            yield return null;
            float longHeight = shell.StandingsScroll.content.rect.height;
            Assert.That(longHeight, Is.GreaterThan(shortHeight));
            ScrollRect scroll = shell.StandingsScroll;
            Assert.That(scroll.verticalScrollbar.size, Is.GreaterThan(0f).And.LessThan(1f),
                "Native LateUpdate must expose a thumb smaller than the rail for twelve long rows.");
            scroll.verticalScrollbar.value = 0f;
            yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThanOrEqualTo(0.02f));
            var corners = new Vector3[4];
            scroll.content.GetWorldCorners(corners);
            float bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
            Assert.That(bottom, Is.EqualTo(scroll.viewport.rect.yMin).Within(2f),
                "Native scrollbar input must make the final row reachable inside the viewport.");
            Assert.That(hud.standingsText.text, Does.Contain("12."));
            shell.enabled = false;
            hud.standingsText.text = "1. CN";
            yield return null;
            Assert.That(shell.StandingsScroll.content.rect.height, Is.EqualTo(longHeight).Within(0.5f));
            shell.enabled = true;
            yield return null;
            Assert.That(shell.StandingsScroll.content.rect.height, Is.EqualTo(shell.StandingsScroll.viewport.rect.height).Within(0.5f));
            Assert.That(hud.standingsText.font, Is.SameAs(font));
            Assert.That(EditorUtility.IsDirty(source), Is.EqualTo(dirty));
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
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

    [Test]
    public void StandingsBindingKeepsProductionOrderingAndDoesNotChangeAnyRacer()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            HUDUI hud = Build(scope);
            var players = new List<PlayerState>();
            for (int i = 0; i < 12; i++)
                players.Add(new PlayerState("车手 " + i, i != 0, i * 2, 1) { lap = i % 3 });
            string expected = RaceHudTextRules.FormatStandings(players, players[0], 77);
            hud.standingsText.text = expected;
            RacingTechHudShellUI.Attach(scope.Root.transform, hud);
            Render(scope);
            Assert.That(hud.standingsText.text, Is.EqualTo(expected));
            for (int i = 0; i < 12; i++)
            {
                Assert.That(players[i].position, Is.EqualTo(i * 2));
                Assert.That(players[i].lap, Is.EqualTo(i % 3));
                Assert.That(players[i].gear, Is.EqualTo(1));
                Assert.That(players[i].hasFinished || players[i].isBlown, Is.False);
            }
        }
    }

    [Test]
    public void RenderedWheelAndScrollbarDragReachLastRowAndPreserveOriginalButtonClick()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            HUDUI hud = Build(scope);
            hud.standingsText.text = LongStandings();
            Button button = MakeButton(scope.Root.transform.Find("OperationPanel"));
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            RacingTechHudShellUI shell = RacingTechHudShellUI.Attach(scope.Root.transform, hud);
            Render(scope);
            scope.Click(button);
            Assert.That(clicks, Is.EqualTo(1));
            ScrollRect scroll = shell.StandingsScroll;
            Camera camera = scope.Root.GetComponent<Canvas>().worldCamera;
            Vector2 point = ScreenPoint(scroll.viewport, camera, new Vector2(0.5f, 0.5f));
            var pointer = new PointerEventData(scope.EventSystem) { position = point, scrollDelta = new Vector2(0f, -2f) };
            var hits = new List<RaycastResult>();
            scope.Root.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            GameObject receiver = ExecuteEvents.GetEventHandler<IScrollHandler>(hits[0].gameObject);
            Assert.That(receiver, Is.SameAs(scroll.gameObject));
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.scrollHandler);
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(1f));
            Scrollbar bar = scroll.verticalScrollbar;
            Vector2 start = ScreenPoint(bar.handleRect, camera, new Vector2(0.5f, 0.5f));
            Vector2 end = ScreenPoint((RectTransform)bar.transform, camera, new Vector2(0.5f, 0.001f));
            pointer.position = start;
            pointer.pressPosition = start;
            pointer.button = PointerEventData.InputButton.Left;
            pointer.pointerPressRaycast = new RaycastResult { module = scope.Root.GetComponent<GraphicRaycaster>() };
            ExecuteEvents.Execute(bar.gameObject, pointer, ExecuteEvents.beginDragHandler);
            pointer.position = end;
            ExecuteEvents.Execute(bar.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(bar.gameObject, pointer, ExecuteEvents.endDragHandler);
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThanOrEqualTo(0.02f));
            Assert.That(hud.standingsText.text, Does.Contain("12."));
            var corners = new Vector3[4];
            scroll.content.GetWorldCorners(corners);
            float bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
            Assert.That(bottom, Is.EqualTo(scroll.viewport.rect.yMin).Within(2f), "Bottom row is reachable inside the clipped viewport.");
            Assert.That(hud.standingsText.raycastTarget, Is.False);
        }
    }

    private static HUDUI Build(CareerUiPointerTestScope scope)
    {
        Transform root = scope.Root.transform;
        HUDUI hud = scope.Root.AddComponent<HUDUI>();
        Panel(root, "OperationPanel", new Vector2(0.02f, 0.355f), new Vector2(0.15f, 0.985f));
        RectTransform board = Panel(root, "ScoreboardPanel", new Vector2(0.16f, 0.355f), new Vector2(0.31f, 0.985f));
        Panel(root, "TrackFrame", new Vector2(0.32f, 0.355f), new Vector2(0.98f, 0.985f));
        RectTransform deck = Panel(root, "DeckTablePanel", new Vector2(0.02f, 0.015f), new Vector2(0.98f, 0.335f));
        Panel(deck, "HandPanel", Vector2.zero, Vector2.one);
        hud.gearText = Label(board, "GearText", scope.Font, "档位: Recover", 18f);
        hud.heatText = Label(board, "HeatText", scope.Font, "引擎 6/8 热量 2\n手1 抽0 弃1", 16f);
        hud.lapText = Label(board, "LapText", scope.Font, "圈数: 123/123", 17f);
        hud.positionText = Label(board, "PositionText", scope.Font, "名次: 12/12", 16f);
        hud.aiStatusText = Label(board, "AiText", scope.Font, "对手: 11", 15f);
        hud.weatherText = Label(board, "WeatherText", scope.Font, "天气: 多云", 15f);
        hud.standingsText = Label(board, "StandingsText", scope.Font, "等待排名…", 14f);
        hud.statusText = Label(root.Find("OperationPanel"), "StatusText", scope.Font, "请选择档位", 14f);
        return hud;
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        host.transform.SetParent(parent, false);
        RectTransform rect = host.GetComponent<RectTransform>();
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        host.GetComponent<Image>().raycastTarget = false;
        return rect;
    }

    private static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, string text, float size)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        host.transform.SetParent(parent, false);
        TMP_Text label = host.GetComponent<TMP_Text>();
        label.font = font; label.text = text; label.fontSize = size; label.raycastTarget = false;
        return label;
    }

    private static Button MakeButton(Transform parent)
    {
        var host = new GameObject("OriginalButton", typeof(RectTransform), typeof(Image), typeof(Button));
        host.transform.SetParent(parent, false);
        RectTransform rect = host.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.8f); rect.anchorMax = new Vector2(0.9f, 0.88f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Button button = host.GetComponent<Button>(); button.targetGraphic = host.GetComponent<Image>();
        return button;
    }

    private static string LongStandings()
    {
        var rows = new List<string>();
        for (int i = 1; i <= 12; i++) rows.Add(i + ". 中国车队车手名称测试车手名称测试 圈123 格99999");
        return string.Join("\n", rows);
    }

    private static void Render(CareerUiPointerTestScope scope)
    {
        foreach (Transform child in scope.Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text label in scope.Root.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        RenderTexture previous = RenderTexture.active;
        try { scope.Root.GetComponent<Canvas>().worldCamera.Render(); }
        finally { RenderTexture.active = previous; }
    }

    private static Vector2 ScreenPoint(RectTransform rect, Camera camera, Vector2 normalized) =>
        RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(
            new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, normalized.x), Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, normalized.y))));
}
