using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Integration/UI; Assets/Tests/Editor; BLOCKING for the production action route.
/// Native GameLoop plus HUD.Start, rendered GraphicRaycaster pointer dispatch and
/// EventSystem Submit, real CardHandUI action binding and the shared CardAction gate.
/// Owned synthetic track/Canvas/profile/settings, no hardware, screenshots, storage
/// or complete-mode acceptance. Three-size visual evidence belongs to the UI fixture.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator ItalyPrecisionTuneHudMergedConfirmationCancelsFreelyAndActuallySettlesSelectedCard()
        => DriveItalyPrecisionTuneThroughNativeUi(true);

    [UnityTest]
    public IEnumerator ItalyPrecisionTuneHudConfirmationDisabledCommitsDirectlyThroughActualCardAction()
        => DriveItalyPrecisionTuneThroughNativeUi(false);

    private IEnumerator DriveItalyPrecisionTuneThroughNativeUi(bool confirmation)
    {
        ItalyPrecisionTuneNativeUiScope ui = null;
        CreateRace(TeamId.IT);
        try
        {
            ConfigureItalyPrecisionTuneLoop();
            PlayerState human = session.Human;
            Get<List<TrackNode>>(manager.trackManager, "nodes")[2] =
                new TrackNode(2, 2, "Precision UI actual apex", 1, false, true);
            Get<Dictionary<int, int>>(manager.trackManager, "cornerSpeedLimits")[1] = 2;
            Get<Dictionary<int, string>>(manager.trackManager, "cornerNames")[1] = "Precision UI actual apex";
            ui = new ItalyPrecisionTuneNativeUiScope(host, manager, confirmation);
            yield return null; // Allow the real HUD.Start to bind its authored controls.
            ui.AssertSingleActiveEventSystem();
            Assert.That(manager.hudUI.confirmReturnToMenuButton, Is.Not.Null);
            manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
            yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "Italy precision HUD actual gear input");
            manager.OnGearButtonClicked(1);
            manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Italy precision HUD post-draw card input");
            HUDUI hud = manager.hudUI;
            RefreshItalyPrecisionTuneUi(hud, manager, human);
            Assert.That(ItalyPrecisionTuneUiButton(hud), Is.Not.Null);
            Assert.That(ItalyPrecisionTuneUiButton(hud).IsInteractable(), Is.False, "No selected ordinary target is an explicit disabled state.");
            CardUI card = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(c => c.cardData.IsSpeed);
            CardData original = card.cardData;
            int balance = human.techState.rpBalance, hand = human.deck.HandCount;
            card.OnCardClicked(); // Real CardUI -> CardHandUI selection callback; physical card dragging is not claimed.
            Assert.That(card.isSelected, Is.True);
            Assert.That(ItalyPrecisionTuneUiButton(hud).IsInteractable(), Is.True);

            if (confirmation)
            {
                ui.Click(ItalyPrecisionTuneUiButton(hud));
                AssertItalyPrecisionTargetChoice(hud, original);
                Assert.That(ui.IsTopClickReceiver(ItalyPrecisionTuneUiButton(hud)), Is.False,
                    "The choice overlay must block the underlying action slot.");
                ui.Click(hud.cancelReturnToMenuButton);
                Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
                Assert.That(HasPendingItalyPrecisionTuneUi(manager), Is.False);
                Assert.That(human.techState.precisionTune.UsedThisRace, Is.False);
                Assert.That(human.deck.HandCount, Is.EqualTo(hand));
            }

            ui.Submit(ItalyPrecisionTuneUiButton(hud));
            AssertItalyPrecisionTargetChoice(hud, original);
            ui.Submit(hud.confirmReturnToMenuButton); // Only add the chosen original instance to this card plan.
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False, "Choosing a target must not consume or immediately open a second fee modal.");
            Assert.That(HasPendingItalyPrecisionTuneUi(manager), Is.True);
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.False);
            Assert.That(human.deck.HandCount, Is.EqualTo(hand));
            Assert.That(manager.BuildTurnTelemetryPreview(human).Movement.TotalCells, Is.EqualTo(2));

            if (confirmation)
            {
                card.OnCardClicked(); card.OnCardClicked();
                Assert.That(HasPendingItalyPrecisionTuneUi(manager), Is.False, "Leaving then restoring a selection invalidates the target plan.");
                Assert.That(manager.BuildTurnTelemetryPreview(human).Movement.TotalCells, Is.EqualTo(3));
                ui.Click(ItalyPrecisionTuneUiButton(hud));
                AssertItalyPrecisionTargetChoice(hud, original);
                ui.Submit(hud.confirmReturnToMenuButton);
                ui.Click(ui.PlayButton); // Actual Button -> CardHandUI.OnPlayClicked -> shared CardAction confirmation.
                Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
                Assert.That(hud.returnToMenuConfirmText.text, Does.Contain("3").And.Contain("2"));
                Assert.That(human.techState.precisionTune.UsedThisRace, Is.False);
                Assert.That(human.deck.ContainsInHand(original), Is.True);
                Assert.That(ui.IsTopClickReceiver(ItalyPrecisionTuneUiButton(hud)), Is.False);
                ui.Submit(hud.cancelReturnToMenuButton);
                Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
                Assert.That(HasPendingItalyPrecisionTuneUi(manager), Is.False, "Cancel the merged final request rather than retaining a hidden target intent.");
                Assert.That(card.isSelected, Is.True, "Cancelling a confirmation preserves the player's card selection.");
                Assert.That(human.techState.precisionTune.UsedThisRace, Is.False);
                Assert.That(human.deck.HandCount, Is.EqualTo(hand));
                ui.Click(ItalyPrecisionTuneUiButton(hud));
                AssertItalyPrecisionTargetChoice(hud, original);
                ui.Submit(hud.confirmReturnToMenuButton);
            }

            ui.Submit(ui.PlayButton);
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.EqualTo(confirmation),
                "The selected CardAction setting alone controls the final merged confirmation.");
            if (confirmation)
            {
                Assert.That(human.techState.precisionTune.UsedThisRace, Is.False);
                ui.Submit(hud.confirmReturnToMenuButton);
            }
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False, "A successful final confirmation must not create another modal.");
            Assert.That(human.playedSpeedCardsThisTurn, Is.EqualTo(new[] { original }));
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.True);
            Assert.That(human.techState.precisionTune.CommittedTarget, Is.SameAs(original));
            Assert.That(human.deck.HandCount, Is.EqualTo(hand - 1));
            Assert.That(human.techState.rpBalance, Is.EqualTo(balance));
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));
            Assert.That(original.value, Is.EqualTo(3));

            ui.Submit(ui.PlayButton); // Empty refreshed selection ends real card input through the same binding.
            if (confirmation)
            {
                Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
                ui.Submit(hud.confirmReturnToMenuButton);
            }
            yield return WaitFor(() => manager.TutorialInputPhase == "discard", "Italy precision HUD actual movement/apex settlement");
            Assert.That(human.position, Is.EqualTo(2));
            Assert.That(human.cornerTotalThisTurn, Is.EqualTo(2));
            Assert.That(human.totalMovementThisTurn, Is.EqualTo(2));
            Assert.That(human.turnMovement.ActualCells, Is.EqualTo(2));
            Assert.That(human.heatPaidCardsThisTurn, Is.Empty);
            Assert.That(human.spinCounter, Is.Zero);
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.True);
            Assert.That(human.techState.rpBalance, Is.EqualTo(balance));
            AssertActiveAndHeatConserved();
            TestContext.WriteLine("Native GameLoop Italy precision HUD: target choice via real raycast/Submit, CardHandUI final binding with CardAction mask=" +
                (confirmation ? "enabled" : "disabled") + ", one consumed use, exact original card3 unchanged, actual movement/apex2. Owned Canvas/profile/settings, no real storage/hardware/visual acceptance.");
        }
        finally
        {
            DestroyRace();
            ui?.Dispose();
        }
    }

    // Reflection keeps before-capture compilation possible on the real pre-UI source.
    // Running these tests without the production entry still fails explicitly; no skip or fallback.
    private static Button ItalyPrecisionTuneUiButton(HUDUI hud)
    {
        PropertyInfo property = typeof(HUDUI).GetProperty("ItalyPrecisionTuneButton", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, "Production HUD must expose its actual precision-tune button.");
        return (Button)property.GetValue(hud);
    }

    private static bool HasPendingItalyPrecisionTuneUi(MVPGameManager manager)
    {
        PropertyInfo property = typeof(MVPGameManager).GetProperty("HasPendingItalyPrecisionTune", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, "Production manager must expose its current pending state.");
        return (bool)property.GetValue(manager);
    }

    private static void RefreshItalyPrecisionTuneUi(HUDUI hud, MVPGameManager manager, PlayerState player)
    {
        MethodInfo method = typeof(HUDUI).GetMethod("RefreshItalyPrecisionTune", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, "Production HUD must implement its actual precision-tune refresh.");
        method.Invoke(hud, new object[] { manager, player });
    }

    private static void AssertItalyPrecisionTargetChoice(HUDUI hud, CardData original)
    {
        Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
        DriverSkillCardChoiceUI row = hud.returnToMenuConfirmPanel.GetComponentInChildren<DriverSkillCardChoiceUI>(true);
        Assert.That(row, Is.Not.Null);
        Assert.That(row.Selected, Is.SameAs(original), "The UI must retain the player's selected ordinary card instance.");
        Assert.That(hud.confirmReturnToMenuButton.IsInteractable(), Is.True);
    }

    /// <summary>Active owned Canvas for native HUD.Start; rendered hit geometry, no Screen.SetResolution or saved scene.</summary>
    private sealed class ItalyPrecisionTuneNativeUiScope : IDisposable
    {
        private static readonly FieldInfo Settings = typeof(GameSettingsRuntime).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo Audio = typeof(AudioService).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly object oldSettings, oldAudio;
        private readonly EventSystem previousEvents;
        private readonly GameObject previousSelection;
        private readonly EventSystem[] previousActiveEvents;
        private readonly GameObject root;
        private readonly EventSystem events;
        private readonly Camera camera;
        private readonly GraphicRaycaster raycaster;
        private readonly RenderTexture target;
        private readonly TMP_FontAsset font;
        private readonly Material material;
        private bool disposed;
        private bool ownsEvents;
        public Button PlayButton { get; }

        public ItalyPrecisionTuneNativeUiScope(GameObject host, MVPGameManager manager, bool confirmation)
        {
            root = host;
            oldSettings = Settings.GetValue(null); oldAudio = Audio.GetValue(null);
            previousEvents = EventSystem.current;
            previousSelection = previousEvents != null ? previousEvents.currentSelectedGameObject : null;
            previousActiveEvents = ActiveEventSystems();
            try
            {
            Assert.That(previousActiveEvents.Length, Is.LessThanOrEqualTo(1),
                "The native UI fixture must not hide an already duplicated EventSystem setup.");
            Settings.SetValue(null, new GameSettingsData { reduceMotion = true,
                inRaceConfirmationMask = confirmation ? (int)InRaceConfirmationAction.CardAction : 0 });
            Audio.SetValue(null, null); // Existing service is restored; owned UI does not play through another fixture.
            // EnterPlayMode may load MainMenu through EditorPlayBootstrapper. Reuse
            // its single EventSystem rather than adding a second frame-processing
            // system. Never disable, destroy or reparent that existing object.
            if (previousActiveEvents.Length == 1) events = previousActiveEvents[0];
            else
            {
                events = ChildOf(root.transform, "Italy precision native UI events").AddComponent<EventSystem>();
                ownsEvents = true;
            }
            AssertSingleActiveEventSystem();
            // The camera must stay outside its ScreenSpaceCamera Canvas hierarchy.
            // Parenting it under that Canvas would create transform feedback during layout.
            var cameraHost = new GameObject("Italy precision native UI camera");
            camera = cameraHost.AddComponent<Camera>(); camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 540f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.cullingMask = 1 << 5; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RacingTechTheme.Graphite;
            target = new RenderTexture(1920, 1080, 24);
            Assert.That(target.Create(), Is.True); camera.targetTexture = target;
            Canvas canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1f;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            raycaster = root.AddComponent<GraphicRaycaster>();
            TMP_FontAsset source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");
            Assert.That(source, Is.Not.Null);
            font = Object.Instantiate(source); font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            material = Object.Instantiate(source.material); font.material = material;
            HUDUI hud = manager.hudUI;
            RectTransform hudRect = (RectTransform)hud.transform;
            hudRect.anchorMin = Vector2.zero; hudRect.anchorMax = Vector2.one;
            hudRect.offsetMin = hudRect.offsetMax = Vector2.zero;
            var gears = ChildOf(hud.transform, "GearButtons").GetComponent<RectTransform>();
            gears.anchoredPosition = new Vector2(-600, 220); gears.sizeDelta = new Vector2(420, 240);
            hud.gear1Button = Button(gears, "Gear1", Vector2.zero, new Vector2(80, 70));
            hud.gear2Button = Button(gears, "Gear2", Vector2.zero, new Vector2(80, 70));
            hud.gear3Button = Button(gears, "Gear3", Vector2.zero, new Vector2(80, 70));
            hud.gear4Button = Button(gears, "Gear4", Vector2.zero, new Vector2(80, 70));
            hud.driverSkillButton = Button(hud.transform, "NativeDriverActionSlot", new Vector2(-550, -280), new Vector2(420, 100));
            hud.confirmGearButton = Button(hud.transform, "NativeGearConfirm", new Vector2(600, -280), new Vector2(300, 100));
            PlayButton = Button(hud.transform, "NativeCardAction", new Vector2(50, -280), new Vector2(420, 100));
            manager.cardHandUI.SetPlayCardsButton(PlayButton);
            hud.SetGameManager(manager);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private static GameObject ChildOf(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform)); child.transform.SetParent(parent, false); return child;
        }

        private Button Button(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject child = ChildOf(parent, name);
            RectTransform rect = (RectTransform)child.transform; rect.anchoredPosition = position; rect.sizeDelta = size;
            Image image = child.AddComponent<Image>(); image.color = RacingTechTheme.Surface;
            Button button = child.AddComponent<Button>(); button.targetGraphic = image;
            TMP_Text label = ChildOf(child.transform, "Label").AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = 20; label.text = name; label.color = RacingTechTheme.Primary;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private void Render()
        {
            AssertSingleActiveEventSystem();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            RenderTexture previous = RenderTexture.active;
            try { camera.Render(); } finally { RenderTexture.active = previous; }
        }

        public bool IsTopClickReceiver(Button button)
        {
            Render();
            RectTransform rect = (RectTransform)button.transform;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(events) { position = center, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); raycaster.Raycast(pointer, hits);
            return hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject;
        }

        public void Click(Button button)
        {
            Assert.That(button != null && button.IsActive() && button.IsInteractable(), Is.True, "Native UI button must be active and enabled.");
            Assert.That(IsTopClickReceiver(button), Is.True, button.name + " must be the actual rendered top hit.");
            RectTransform rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(camera,
                rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        public void Submit(Button button)
        {
            Assert.That(button != null && button.IsActive() && button.IsInteractable(), Is.True, "Native UI Submit requires an active enabled button.");
            Assert.That(IsTopClickReceiver(button), Is.True, button.name + " must have unmasked rendered geometry before Submit.");
            events.SetSelectedGameObject(button.gameObject);
            Assert.That(ExecuteEvents.Execute(button.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler), Is.True);
        }

        private static EventSystem[] ActiveEventSystems()
            => Object.FindObjectsOfType<EventSystem>().Where(e => e.isActiveAndEnabled).ToArray();

        public void AssertSingleActiveEventSystem()
        {
            Assert.That(ActiveEventSystems(), Is.EqualTo(new[] { events }),
                "Only the fixture's reused or owned EventSystem may process native UI frames.");
            Assert.That(EventSystem.current, Is.SameAs(events),
                "Raycast, modal focus and Submit must use the one active EventSystem.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // DestroyRace owns the root/components; only these extra resources survive it.
            // Constructor failure may dispose before DestroyRace; only remove an
            // EventSystem explicitly created by this scope, never the reused one.
            if (ownsEvents && events != null) Object.DestroyImmediate(events.gameObject);
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            if (font != null) Object.DestroyImmediate(font);
            if (material != null) Object.DestroyImmediate(material);
            Settings.SetValue(null, oldSettings); Audio.SetValue(null, oldAudio);
            if (previousEvents != null && previousEvents.isActiveAndEnabled)
            {
                EventSystem.current = previousEvents;
                previousEvents.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy
                    ? previousSelection : null);
            }
            Assert.That(ActiveEventSystems(), Is.EquivalentTo(previousActiveEvents),
                "Native UI cleanup must restore the exact pre-scope active EventSystem set.");
            if (previousEvents != null && previousEvents.isActiveAndEnabled)
            {
                Assert.That(EventSystem.current, Is.SameAs(previousEvents));
                GameObject expectedSelection = previousSelection != null && previousSelection.activeInHierarchy
                    ? previousSelection : null;
                if (expectedSelection != null)
                    Assert.That(previousEvents.currentSelectedGameObject, Is.SameAs(expectedSelection));
                else
                    // EventSystem may retain a destroyed selection wrapper after
                    // DestroyRace. Unity regards it as no focus although CLR
                    // reference identity differs from literal null.
                    Assert.That(previousEvents.currentSelectedGameObject == null, Is.True,
                        "Cleanup must leave no live selected object when the original focus was null or unavailable.");
            }
        }
    }
}
