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

public sealed class RacingTechConfirmationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Settings = typeof(GameSettingsRuntime).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
    private object previousSettings;
    private static TMP_FontAsset LoadFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");

    [SetUp]
    public void IsolateSettings()
    {
        previousSettings = Settings.GetValue(null);
        var settings = GameSettingsData.CreateDefault();
        settings.inRaceConfirmationMask = (int)InRaceConfirmationAction.All;
        settings.reduceMotion = true;
        Settings.SetValue(null, settings);
    }

    [TearDown]
    public void RestoreSettings() => Settings.SetValue(null, previousSettings);

    [Test]
    public void MissingModalBuildsThroughProductionHookAndRepeatedEnsureHasNoGrowth()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            HUDUI hud = scope.Root.AddComponent<HUDUI>();
            hud.EnsureReturnToMenuConfirmationUI();
            GameObject panel = hud.returnToMenuConfirmPanel;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(panel.GetComponent<Image>().raycastTarget, Is.True);
            Image surface = panel.transform.Find("ReturnToMenuConfirmCard").GetComponent<Image>();
            Assert.That(surface.color, Is.EqualTo(RacingTechTheme.Surface));
            Assert.That(surface.sprite, Is.Null);
            Assert.That(surface.GetComponent<Outline>().enabled, Is.False);
            Transform divider = surface.transform.Find("RacingTechConfirmationDivider");
            Assert.That(divider, Is.Not.Null);
            Assert.That(divider.GetComponent<Image>().raycastTarget, Is.False);
            foreach (TMP_Text label in panel.GetComponentsInChildren<TMP_Text>(true))
                Assert.That(label.raycastTarget, Is.False, label.name + " must not intercept the modal controls");
            int count = panel.GetComponentsInChildren<Transform>(true).Length;
            for (int i = 0; i < 4; i++) hud.EnsureReturnToMenuConfirmationUI();
            Assert.That(hud.returnToMenuConfirmPanel, Is.SameAs(panel));
            Assert.That(panel.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(hud.confirmReturnToMenuButton.GetComponents<Outline>().Length, Is.EqualTo(1));
        }
    }

    [Test]
    public void ExistingModalKeepsAuthoredGeometryFontsMaterialCallbacksAndInputConfiguration()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            HUDUI hud = scope.Root.AddComponent<HUDUI>();
            hud.EnsureReturnToMenuConfirmationUI();
            GameObject panel = hud.returnToMenuConfirmPanel;
            TMP_Text title = panel.transform.Find("ReturnToMenuConfirmCard/ReturnToMenuConfirmTitle").GetComponent<TMP_Text>();
            TMP_Text body = hud.returnToMenuConfirmText;
            title.text = "人工排版标题"; body.text = "人工正文，不由主题替换。";
            RectTransform card = (RectTransform)title.transform.parent;
            card.anchorMin = new Vector2(.27f, .41f); card.anchorMax = new Vector2(.71f, .78f);
            card.offsetMin = new Vector2(13f, -34f); card.offsetMax = new Vector2(-29f, 16f);
            Button button = hud.confirmReturnToMenuButton;
            button.interactable = false;
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation; navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnLeft = hud.cancelReturnToMenuButton; button.navigation = navigation;
            int calls = 0; button.onClick.AddListener(() => calls++);
            ButtonClickAnimation animation = button.GetComponent<ButtonClickAnimation>();
            Graphic target = button.targetGraphic;
            var geometry = new Dictionary<RectTransform, Vector4[]>();
            var fonts = new Dictionary<TMP_Text, Tuple<TMP_FontAsset, Material, string, float>>();
            foreach (RectTransform rect in panel.GetComponentsInChildren<RectTransform>(true))
                geometry.Add(rect, Geometry(rect));
            foreach (TMP_Text label in panel.GetComponentsInChildren<TMP_Text>(true))
                fonts.Add(label, Tuple.Create(label.font, label.fontSharedMaterial, label.text, label.fontSize));
            hud.EnsureReturnToMenuConfirmationUI();
            Assert.That(hud.returnToMenuConfirmPanel, Is.SameAs(panel));
            foreach (var entry in geometry) Assert.That(Geometry(entry.Key), Is.EqualTo(entry.Value), entry.Key.name);
            foreach (var entry in fonts)
            {
                Assert.That(entry.Key.font, Is.SameAs(entry.Value.Item1));
                Assert.That(entry.Key.fontSharedMaterial, Is.SameAs(entry.Value.Item2));
                Assert.That(entry.Key.text, Is.EqualTo(entry.Value.Item3));
                Assert.That(entry.Key.fontSize, Is.EqualTo(entry.Value.Item4));
            }
            Assert.That(button.targetGraphic, Is.SameAs(target));
            Assert.That(button.interactable, Is.False);
            Assert.That(button.transition, Is.EqualTo(Selectable.Transition.ColorTint), "Legacy no-transition buttons must receive readable flat color states.");
            Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(button.navigation.selectOnLeft, Is.SameAs(hud.cancelReturnToMenuButton));
            Assert.That(button.GetComponent<ButtonClickAnimation>(), Is.SameAs(animation));
            button.onClick.Invoke(); Assert.That(calls, Is.EqualTo(1), "Styling must preserve the original event object.");
        }
    }

    [TestCase(InRaceConfirmationAction.GearSelection)]
    [TestCase(InRaceConfirmationAction.GearCommit)]
    [TestCase(InRaceConfirmationAction.CardAction)]
    [TestCase(InRaceConfirmationAction.DriverSkill)]
    [TestCase(InRaceConfirmationAction.ResetRace)]
    [TestCase(InRaceConfirmationAction.ReturnToMenu)]
    [TestCase(InRaceConfirmationAction.PitDecision)]
    [TestCase(InRaceConfirmationAction.LaneChange)]
    public void AllActionsKeepSemanticLabelsCancelReopenSingleCommitAndMaskBypass(InRaceConfirmationAction action)
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            HUDUI hud = StartedHud(scope);
            int calls = 0;
            string title = InRaceConfirmationRules.GetDisplayName(action);
            hud.RequestInRaceAction(action, title, "确认后按原规则执行当前动作。", () => calls++);
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
            Assert.That(hud.returnToMenuConfirmText.text, Is.EqualTo("确认后按原规则执行当前动作。"));
            Assert.That(hud.cancelReturnToMenuButton.GetComponentInChildren<TMP_Text>().text,
                Is.EqualTo(InRaceConfirmationRules.GetCancelButtonLabel(action)));
            Assert.That(hud.confirmReturnToMenuButton.GetComponentInChildren<TMP_Text>().text,
                Is.EqualTo(InRaceConfirmationRules.GetConfirmButtonLabel(action)));
            if (action == InRaceConfirmationAction.ResetRace || action == InRaceConfirmationAction.ReturnToMenu)
            {
                Assert.That(hud.TryConfirmKeyboardAction(), Is.False);
                Assert.That(calls, Is.Zero);
                Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
            }
            scope.Click(hud.cancelReturnToMenuButton);
            Assert.That(calls, Is.Zero);
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
            hud.RequestInRaceAction(action, title, "重新打开同一确认卡。", () => calls++);
            scope.Click(hud.confirmReturnToMenuButton);
            hud.confirmReturnToMenuButton.onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
            int expectedCalls = 1;
            if (action != InRaceConfirmationAction.ResetRace && action != InRaceConfirmationAction.ReturnToMenu)
            {
                hud.RequestInRaceAction(action, title, "原普通动作仍可由键盘确认。", () => calls++);
                Assert.That(hud.TryConfirmKeyboardAction(), Is.True);
                expectedCalls++;
                Assert.That(calls, Is.EqualTo(expectedCalls));
                Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
                Assert.That(hud.TryConfirmKeyboardAction(), Is.False);
                Assert.That(calls, Is.EqualTo(expectedCalls));
            }
            GameSettingsRuntime.Current.inRaceConfirmationMask = 0;
            hud.RequestInRaceAction(action, title, "此时直接执行原回调。", () => calls++);
            Assert.That(calls, Is.EqualTo(expectedCalls + 1));
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
        }
    }

    [Test]
    public void RealPointerStatesStayReadableDisabledClickDoesNothingAndOverlayBlocksUnderlay()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            var underlay = new GameObject("Underlying action", typeof(RectTransform), typeof(Image), typeof(Button));
            underlay.transform.SetParent(scope.Root.transform, false);
            RectTransform underRect = underlay.GetComponent<RectTransform>();
            underRect.anchorMin = Vector2.zero; underRect.anchorMax = Vector2.one;
            underRect.offsetMin = underRect.offsetMax = Vector2.zero;
            int underlyingCalls = 0; underlay.GetComponent<Button>().onClick.AddListener(() => underlyingCalls++);
            HUDUI hud = StartedHud(scope); int calls = 0;
            hud.RequestInRaceAction(InRaceConfirmationAction.CardAction, "确认出牌", "保留原有出牌事件。", () => calls++);
            foreach (Button button in new[] { hud.cancelReturnToMenuButton, hud.confirmReturnToMenuButton })
            {
                Navigation saved = button.navigation; Navigation none = saved; none.mode = Navigation.Mode.None; button.navigation = none;
                try
                {
                    var pointer = new PointerEventData(scope.EventSystem) { button = PointerEventData.InputButton.Left };
                    AssertMeasuredContrast(button);
                    button.OnPointerEnter(pointer); AssertMeasuredContrast(button);
                    button.OnPointerDown(pointer); AssertMeasuredContrast(button);
                    button.OnPointerUp(pointer); AssertMeasuredContrast(button);
                    button.OnPointerExit(pointer);
                    button.OnSelect(new BaseEventData(scope.EventSystem)); AssertMeasuredContrast(button);
                    button.OnDeselect(new BaseEventData(scope.EventSystem));
                    button.interactable = false; AssertMeasuredContrast(button);
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    Assert.That(calls, Is.Zero);
                    Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
                    button.interactable = true;
                }
                finally { button.navigation = saved; }
            }
            Render(scope);
            var eventData = new PointerEventData(scope.EventSystem) { position = new Vector2(30f, 30f), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            scope.Root.GetComponent<GraphicRaycaster>().Raycast(eventData, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject, Is.SameAs(hud.returnToMenuConfirmPanel));
            GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            if (receiver != null) ExecuteEvents.Execute(receiver, eventData, ExecuteEvents.pointerClickHandler);
            Assert.That(underlyingCalls, Is.Zero);
            scope.Click(hud.confirmReturnToMenuButton); Assert.That(calls, Is.EqualTo(1));
        }
    }

    [UnityTest]
    public IEnumerator NativeHudStartReusesStyledModalAndDispatchesOneOriginalCallback()
    {
        yield return new EnterPlayMode();
        object saved = Settings.GetValue(null);
        var settings = GameSettingsData.CreateDefault(); settings.inRaceConfirmationMask = (int)InRaceConfirmationAction.All;
        settings.reduceMotion = true; Settings.SetValue(null, settings);
        Scene scene = SceneManager.CreateScene("Owned confirmation native lifecycle");
        GameObject root = null, eventHost = null;
        TMP_FontAsset font = null; Material material = null;
        try
        {
            TMP_FontAsset source = LoadFont(); bool dirty = EditorUtility.IsDirty(source);
            font = Object.Instantiate(source); font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.fallbackFontAssetTable = new List<TMP_FontAsset>(); material = Object.Instantiate(source.material); font.material = material;
            root = new GameObject("Owned modal canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var seed = new GameObject("Owned modal font", typeof(RectTransform), typeof(TextMeshProUGUI));
            seed.transform.SetParent(root.transform, false); seed.GetComponent<TMP_Text>().font = font; seed.SetActive(false);
            HUDUI hud = root.AddComponent<HUDUI>();
            yield return null;
            Assert.That(hud.returnToMenuConfirmPanel, Is.Not.Null, "Native Start must create and style the shared modal.");
            GameObject panel = hud.returnToMenuConfirmPanel;
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(panel.transform.Find("ReturnToMenuConfirmCard").GetComponent<Image>().color, Is.EqualTo(RacingTechTheme.Surface));
            eventHost = new GameObject("Owned inactive modal event data"); eventHost.SetActive(false);
            SceneManager.MoveGameObjectToScene(eventHost, scene);
            var pointer = new PointerEventData(eventHost.AddComponent<EventSystem>()) { button = PointerEventData.InputButton.Left };
            int calls = 0;
            hud.RequestInRaceAction(InRaceConfirmationAction.ResetRace, "重新开始", "仍由原确认回调决定是否重开。", () => calls++);
            yield return null;
            Assert.That(hud.TryConfirmKeyboardAction(), Is.False);
            ExecuteEvents.Execute(hud.cancelReturnToMenuButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(calls, Is.Zero); Assert.That(panel.activeSelf, Is.False);
            hud.RequestInRaceAction(InRaceConfirmationAction.CardAction, "确认出牌", "重新打开并提交一次。", () => calls++);
            yield return null;
            ExecuteEvents.Execute(hud.confirmReturnToMenuButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(hud.confirmReturnToMenuButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(calls, Is.EqualTo(1)); Assert.That(panel.activeSelf, Is.False);
            Assert.That(hud.returnToMenuConfirmPanel, Is.SameAs(panel));
            Assert.That(hud.returnToMenuConfirmText.font, Is.SameAs(font));
            Assert.That(EditorUtility.IsDirty(source), Is.EqualTo(dirty));
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            if (eventHost != null) Object.DestroyImmediate(eventHost);
            if (font != null) Object.DestroyImmediate(font);
            if (material != null) Object.DestroyImmediate(material);
            Settings.SetValue(null, saved);
        }
        yield return SceneManager.UnloadSceneAsync(scene);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator EnsureEditMode() { if (EditorApplication.isPlaying) yield return new ExitPlayMode(); }

    private static HUDUI StartedHud(CareerUiPointerTestScope scope)
    {
        HUDUI hud = scope.Root.AddComponent<HUDUI>();
        typeof(HUDUI).GetMethod("Start", Private).Invoke(hud, null);
        return hud;
    }
    private static Vector4[] Geometry(RectTransform rect) => new[] {
        new Vector4(rect.anchorMin.x, rect.anchorMin.y, rect.anchorMax.x, rect.anchorMax.y),
        new Vector4(rect.offsetMin.x, rect.offsetMin.y, rect.offsetMax.x, rect.offsetMax.y),
        new Vector4(rect.pivot.x, rect.pivot.y, rect.localScale.x, rect.localScale.y) };
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
    private static void AssertMeasuredContrast(Button button)
    {
        Image image = (Image)button.targetGraphic;
        Color background = image.color * image.canvasRenderer.GetColor();
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        Color foreground = label.color * label.canvasRenderer.GetColor();
        float a = Luminance(foreground), b = Luminance(background);
        Assert.That((Mathf.Max(a,b)+.05f)/(Mathf.Min(a,b)+.05f), Is.GreaterThanOrEqualTo(4.5f), button.name);
    }
    private static float Luminance(Color color) => .2126f*Linear(color.r)+.7152f*Linear(color.g)+.0722f*Linear(color.b);
    private static float Linear(float value) => value <= .04045f ? value/12.92f : Mathf.Pow((value+.055f)/1.055f,2.4f);
}
