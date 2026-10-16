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

public sealed class RacingTechActionButtonTests
{
    private static TMP_FontAsset LoadFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");

    [TestCase(false)]
    [TestCase(true)]
    public void BothLayoutEntrypointsStyleExactlyFiveTargetsAndKeepListeners(bool authored)
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            HUDUI hud = scope.Root.AddComponent<HUDUI>();
            CardHandUI hand = scope.Root.AddComponent<CardHandUI>();
            if (authored)
                foreach (string name in new[] { "OperationPanel", "ScoreboardPanel", "TrackFrame", "DeckTablePanel" })
                {
                    var panel = new GameObject(name, typeof(RectTransform)); panel.transform.SetParent(scope.Root.transform, false);
                    var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                }
            hud.confirmGearButton = MakeButton(scope.Root.transform, scope.Font, "ConfirmGearBtn");
            hud.driverSkillButton = MakeButton(scope.Root.transform, scope.Font, "DriverSkillBtn");
            hud.resetButton = MakeButton(scope.Root.transform, scope.Font, "ResetBtn");
            hud.returnToMenuButton = MakeButton(scope.Root.transform, scope.Font, "ReturnToMenuBtn");
            hand.SetPlayCardsButton(MakeButton(scope.Root.transform, scope.Font, "PlayBtn"));
            Button unrelated = MakeButton(scope.Root.transform, scope.Font, "Unrelated result and gear sentinel");
            Color oldColor = unrelated.targetGraphic.color; ColorBlock oldStates = unrelated.colors;
            int calls = 0;
            Button[] targets = { hud.confirmGearButton, hud.driverSkillButton, hud.resetButton, hud.returnToMenuButton, hand.playCardsButton };
            foreach (Button button in targets) button.onClick.AddListener(() => calls++);
            RaceUILayoutController layout = scope.Root.AddComponent<RaceUILayoutController>();
            layout.ApplyLayout(hud, hand);
            Assert.That(layout.UsesAuthoredLayout, Is.EqualTo(authored));
            foreach (Button button in targets)
            {
                Assert.That(((Image)button.targetGraphic).sprite, Is.Null, button.name);
                Assert.That(button.targetGraphic.color, Is.EqualTo(Color.white), button.name);
                Assert.That(button.GetComponentInChildren<TMP_Text>().color, Is.EqualTo(RacingTechTheme.Primary));
                Assert.That(button.GetComponentInChildren<TMP_Text>().font, Is.SameAs(scope.Font));
            }
            Assert.That(hud.confirmGearButton.colors.normalColor, Is.EqualTo(hand.playCardsButton.colors.normalColor));
            Assert.That(hud.resetButton.colors.normalColor, Is.EqualTo(RacingTechTheme.Raised));
            Assert.That(hud.returnToMenuButton.colors.normalColor, Is.EqualTo(RacingTechTheme.Raised));
            Assert.That(unrelated.targetGraphic.color, Is.EqualTo(oldColor));
            Assert.That(unrelated.colors, Is.EqualTo(oldStates));
            int count = scope.Root.GetComponentsInChildren<Transform>(true).Length;
            layout.ApplyLayout(hud, hand);
            Assert.That(scope.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            // Deliberately exercise the original event object, without requiring a gameplay manager.
            foreach (Button button in targets) button.onClick.Invoke();
            Assert.That(calls, Is.EqualTo(5));
            // Preserve the existing overlap: play stays above the disabled confirm control in card mode.
            hud.confirmGearButton.interactable=false;
            hand.SetGearSelectionMode(false);
            Assert.That(hud.confirmGearButton.gameObject.activeSelf,Is.True);
            Assert.That(hand.playCardsButton.gameObject.activeSelf,Is.True);
            int confirmHits=0,playHits=0;
            hud.confirmGearButton.onClick.AddListener(()=>confirmHits++);
            hand.playCardsButton.onClick.AddListener(()=>playHits++);
            scope.Click(hand.playCardsButton);
            Assert.That(playHits,Is.EqualTo(1));
            Assert.That(confirmHits,Is.Zero,"The existing underlying disabled confirm must never receive the play click.");
        }
    }

    [Test]
    public void ActionThemeDoesNotRestyleFourGearButtonsOrResultReturnButton()
    {
        using (var scope=CareerUiPointerTestScope.Create(LoadFont))
        {
            HUDUI hud=scope.Root.AddComponent<HUDUI>();
            hud.gear1Button=MakeButton(scope.Root.transform,scope.Font,"Gear1Btn");
            hud.gear2Button=MakeButton(scope.Root.transform,scope.Font,"Gear2Btn");
            hud.gear3Button=MakeButton(scope.Root.transform,scope.Font,"Gear3Btn");
            hud.gear4Button=MakeButton(scope.Root.transform,scope.Font,"Gear4Btn");
            hud.backToMenuButton=MakeButton(scope.Root.transform,scope.Font,"BackToMenuBtn");
            hud.confirmGearButton=MakeButton(scope.Root.transform,scope.Font,"ConfirmGearBtn");
            const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
            typeof(RaceUILayoutController).GetMethod("ApplyAuthoredButtonStyles",flags).Invoke(null,new object[]{hud,null});
            Button[] excluded={hud.gear1Button,hud.gear2Button,hud.gear3Button,hud.gear4Button,hud.backToMenuButton};
            var colors=new ColorBlock[excluded.Length]; var sprites=new Sprite[excluded.Length];
            for(int i=0;i<excluded.Length;i++){ colors[i]=excluded[i].colors; sprites[i]=((Image)excluded[i].targetGraphic).sprite; }
            typeof(RaceUILayoutController).GetMethod("ApplyActionButtonTheme",flags).Invoke(null,new object[]{hud,null});
            for(int i=0;i<excluded.Length;i++)
            {
                Assert.That(excluded[i].colors,Is.EqualTo(colors[i]),excluded[i].name);
                Assert.That(((Image)excluded[i].targetGraphic).sprite,Is.SameAs(sprites[i]),excluded[i].name);
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PointerStatesKeepEffectiveContrastAndDisabledButtonRejectsClick(bool primary)
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            Button button = MakeButton(scope.Root.transform, scope.Font, "Owned action pointer");
            var rect = button.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
            rect.sizeDelta = new Vector2(220f,64f);
            Navigation none = button.navigation; none.mode = Navigation.Mode.None; button.navigation = none;
            int calls = 0; button.onClick.AddListener(() => calls++);
            button.transition = Selectable.Transition.None;
            RacingTechActionButtonStyle.Apply(button, primary);
            Assert.That(button.transition, Is.EqualTo(Selectable.Transition.ColorTint));
            var pointer = new PointerEventData(scope.EventSystem) { button = PointerEventData.InputButton.Left };
            CheckContrast(button);
            button.OnPointerEnter(pointer); CheckContrast(button);
            button.OnPointerDown(pointer); CheckContrast(button);
            button.OnPointerUp(pointer); CheckContrast(button);
            button.OnPointerExit(pointer);
            button.OnSelect(new BaseEventData(scope.EventSystem)); CheckContrast(button);
            button.OnDeselect(new BaseEventData(scope.EventSystem));
            button.interactable = false; CheckContrast(button);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(calls, Is.Zero);
            button.interactable = true;
            scope.Click(button); Assert.That(calls, Is.EqualTo(1), "The original full button image must remain the live top hit.");
        }
    }

    [Test]
    public void RepeatedStylePreservesGeometryFontsMaterialsNavigationAndLegacyText()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            Button button = MakeButton(scope.Root.transform, scope.Font, "Owned authored action");
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(); text.text = "保留动作文本"; text.fontSize = 23f;
            var legacyHost = new GameObject("Existing legacy label", typeof(RectTransform), typeof(Text));
            legacyHost.transform.SetParent(button.transform, false); Text legacy = legacyHost.GetComponent<Text>();
            legacy.text = "Legacy"; legacy.fontSize = 21; Font oldLegacyFont = legacy.font;
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.12f,.23f); rect.anchorMax = new Vector2(.67f,.82f);
            rect.offsetMin = new Vector2(3f,8f); rect.offsetMax = new Vector2(-11f,-9f);
            Vector2 min = rect.anchorMin, max = rect.anchorMax, low = rect.offsetMin, high = rect.offsetMax;
            Material material = text.fontSharedMaterial;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.Explicit; button.navigation = navigation;
            button.interactable = false;
            Graphic target = button.targetGraphic; int calls = 0; button.onClick.AddListener(() => calls++);
            int children = button.GetComponentsInChildren<Transform>(true).Length;
            for (int i = 0; i < 4; i++) RacingTechActionButtonStyle.Apply(button, true);
            Assert.That(rect.anchorMin, Is.EqualTo(min)); Assert.That(rect.anchorMax, Is.EqualTo(max));
            Assert.That(rect.offsetMin, Is.EqualTo(low)); Assert.That(rect.offsetMax, Is.EqualTo(high));
            Assert.That(text.font, Is.SameAs(scope.Font)); Assert.That(text.fontSharedMaterial, Is.SameAs(material));
            Assert.That(text.fontSize, Is.EqualTo(23f)); Assert.That(text.text, Is.EqualTo("保留动作文本"));
            Assert.That(legacy.font, Is.SameAs(oldLegacyFont)); Assert.That(legacy.fontSize, Is.EqualTo(21));
            Assert.That(legacy.text, Is.EqualTo("Legacy")); Assert.That(legacy.color, Is.EqualTo(RacingTechTheme.Primary));
            Assert.That(legacy.raycastTarget, Is.False);
            Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit)); Assert.That(button.interactable, Is.False);
            Assert.That(button.targetGraphic, Is.SameAs(target));
            Assert.That(button.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(children));
            Assert.That(button.GetComponents<Outline>().Length, Is.EqualTo(1));
            button.onClick.Invoke(); Assert.That(calls, Is.EqualTo(1));
        }
    }

    [Test]
    public void OriginalHandAndSkillRefreshChangeTextAndAvailabilityWithoutReplacingTheme()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            CardHandUI hand = scope.Root.AddComponent<CardHandUI>();
            Button play = MakeButton(scope.Root.transform, scope.Font, "PlayBtn"); hand.SetPlayCardsButton(play);
            RacingTechActionButtonStyle.Apply(play, true); ColorBlock colors = play.colors;
            hand.SetGearSelectionMode(false);
            Assert.That(play.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("结束出牌"));
            hand.SetDiscardMode(true);
            Assert.That(play.GetComponentInChildren<TMP_Text>().text, Does.Contain("弃置所选牌"));
            hand.SetGearSelectionMode(true); Assert.That(play.gameObject.activeSelf, Is.False);
            hand.SetGearSelectionMode(false); Assert.That(play.gameObject.activeSelf, Is.True);
            Assert.That(play.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("结束出牌"));
            Assert.That(play.colors, Is.EqualTo(colors));
            HUDUI hud = scope.Root.AddComponent<HUDUI>(); hud.driverSkillButton = MakeButton(scope.Root.transform, scope.Font, "DriverSkillBtn");
            RacingTechActionButtonStyle.Apply(hud.driverSkillButton, false); colors = hud.driverSkillButton.colors;
            hud.RefreshDriverSkill(null, null);
            Assert.That(hud.driverSkillButton.interactable, Is.False);
            Assert.That(hud.driverSkillButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("车手技能"));
            Assert.That(hud.driverSkillButton.colors, Is.EqualTo(colors));
        }
    }

    [UnityTest]
    public IEnumerator NativeActionClickStillUsesExistingResetConfirmationAndCancellation()
    {
        yield return new EnterPlayMode();
        FieldInfo settingsField = typeof(GameSettingsRuntime).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
        object savedSettings = settingsField.GetValue(null);
        var settings = GameSettingsData.CreateDefault(); settings.inRaceConfirmationMask = (int)InRaceConfirmationAction.All;
        settings.reduceMotion = true; settingsField.SetValue(null, settings);
        Scene scene = SceneManager.CreateScene("Owned action button native lifecycle");
        GameObject root = null, eventHost = null; TMP_FontAsset font = null; Material material = null;
        try
        {
            TMP_FontAsset source = LoadFont(); bool dirty = EditorUtility.IsDirty(source);
            font = Object.Instantiate(source); font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.fallbackFontAssetTable = new List<TMP_FontAsset>(); material = Object.Instantiate(source.material); font.material = material;
            root = new GameObject("Owned action canvas", typeof(RectTransform), typeof(Canvas)); SceneManager.MoveGameObjectToScene(root, scene);
            var seed = new GameObject("Owned font", typeof(RectTransform), typeof(TextMeshProUGUI));
            seed.transform.SetParent(root.transform, false); seed.GetComponent<TMP_Text>().font = font; seed.SetActive(false);
            HUDUI hud = root.AddComponent<HUDUI>(); hud.resetButton = MakeButton(root.transform, font, "ResetBtn");
            CardHandUI hand = root.AddComponent<CardHandUI>();
            hand.SetPlayCardsButton(MakeButton(root.transform,font,"PlayBtn"));
            RaceUILayoutController layout = root.AddComponent<RaceUILayoutController>(); layout.ApplyLayout(hud,hand);
            yield return null;
            Assert.That(hud.resetButton.colors.normalColor, Is.EqualTo(RacingTechTheme.Raised));
            eventHost = new GameObject("Owned inactive action event data"); eventHost.SetActive(false); SceneManager.MoveGameObjectToScene(eventHost,scene);
            var pointer = new PointerEventData(eventHost.AddComponent<EventSystem>()) { button = PointerEventData.InputButton.Left };
            hand.SetGearSelectionMode(false);
            int playCalls=0; hand.playCardsButton.onClick.AddListener(()=>playCalls++);
            ColorBlock playColors=hand.playCardsButton.colors;
            hand.BlockActionButtonBriefly(.05f);
            Assert.That(hand.playCardsButton.interactable,Is.False);
            ExecuteEvents.Execute(hand.playCardsButton.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Assert.That(playCalls,Is.Zero);
            yield return new WaitForSecondsRealtime(.09f);
            Assert.That(hand.playCardsButton.interactable,Is.True);
            Assert.That(hand.playCardsButton.colors,Is.EqualTo(playColors));
            ExecuteEvents.Execute(hand.playCardsButton.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Assert.That(playCalls,Is.EqualTo(1));
            ExecuteEvents.Execute(hud.resetButton.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
            Assert.That(hud.returnToMenuConfirmText.text, Does.Contain("重新开始"));
            Assert.That(hud.TryConfirmKeyboardAction(), Is.False);
            ExecuteEvents.Execute(hud.cancelReturnToMenuButton.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
            ExecuteEvents.Execute(hud.resetButton.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
            Assert.That(EditorUtility.IsDirty(source), Is.EqualTo(dirty));
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            if (eventHost != null) Object.DestroyImmediate(eventHost);
            if (font != null) Object.DestroyImmediate(font); if (material != null) Object.DestroyImmediate(material);
            settingsField.SetValue(null,savedSettings);
        }
        yield return SceneManager.UnloadSceneAsync(scene);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator EnsureEditMode() { if (EditorApplication.isPlaying) yield return new ExitPlayMode(); }

    private static Button MakeButton(Transform parent, TMP_FontAsset font, string name)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); host.transform.SetParent(parent,false);
        Button button = host.GetComponent<Button>(); button.targetGraphic = host.GetComponent<Image>();
        var label = new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI)); label.transform.SetParent(host.transform,false);
        RectTransform rect = label.GetComponent<RectTransform>(); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
        TMP_Text text = label.GetComponent<TMP_Text>(); text.font=font; text.text=name; return button;
    }
    private static void CheckContrast(Button button)
    {
        Color background=button.targetGraphic.color*button.targetGraphic.canvasRenderer.GetColor();
        TMP_Text text=button.GetComponentInChildren<TMP_Text>(); Color foreground=text.color*text.canvasRenderer.GetColor();
        float a=Luminance(foreground),b=Luminance(background);
        Assert.That((Mathf.Max(a,b)+.05f)/(Mathf.Min(a,b)+.05f),Is.GreaterThanOrEqualTo(4.5f));
    }
    private static float Luminance(Color c)=>.2126f*Linear(c.r)+.7152f*Linear(c.g)+.0722f*Linear(c.b);
    private static float Linear(float c)=>c<=.04045f?c/12.92f:Mathf.Pow((c+.055f)/1.055f,2.4f);
}
