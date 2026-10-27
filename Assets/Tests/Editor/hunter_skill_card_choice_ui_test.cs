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

/// <summary>Production modal handlers and real owned Canvas pointer geometry, without a match.</summary>
public sealed class HunterSkillCardChoiceUITests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Settings = typeof(GameSettingsRuntime).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
    private object previousSettings;
    private CareerUiPointerTestScope scope;
    private HUDUI hud;
    private EventSystem previousEventSystem;
    private EventSystem ownedFocusEventSystem;

    [SetUp]
    public void CreateOwnedHud()
    {
        previousSettings = Settings.GetValue(null);
        previousEventSystem = EventSystem.current;
        GameSettingsData settings = GameSettingsData.CreateDefault();
        settings.inRaceConfirmationMask = (int)InRaceConfirmationAction.All;
        settings.reduceMotion = true;
        Settings.SetValue(null, settings);
        try
        {
            scope = CareerUiPointerTestScope.Create(() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset"));
            hud = scope.Root.AddComponent<HUDUI>();
            typeof(HUDUI).GetMethod("Start", Private).Invoke(hud, null);
        }
        catch { scope?.Dispose(); Settings.SetValue(null, previousSettings); throw; }
    }

    [TearDown]
    public void RestoreOwnedHud()
    {
        try
        {
            if (ownedFocusEventSystem != null) InvokeHook(ownedFocusEventSystem, "OnDisable");
            scope?.Dispose();
        }
        finally
        {
            scope = null;
            Settings.SetValue(null, previousSettings);
            if (previousEventSystem != null && previousEventSystem.isActiveAndEnabled) EventSystem.current = previousEventSystem;
        }
    }

    [TestCase(0)]
    [TestCase((int)InRaceConfirmationAction.All)]
    public void RequiredChoiceCannotBeBypassedAndPointerConfirmsExactEqualValueInstanceOnce(int mask)
    {
        GameSettingsRuntime.Current.inRaceConfirmationMask = mask;
        List<CardData> cards = Cards();
        int confirmed = 0, cancelled = 0;
        CardData selected = null;
        hud.ShowDriverSkillCardChoice("决胜抉择", "目标前方三格；弃置所选速度牌，本回合移动 +2。", cards,
            card => { confirmed++; selected = card; }, () => cancelled++);
        Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
        Assert.That(confirmed, Is.Zero);
        Assert.That(Previous.interactable, Is.False);
        Assert.That(Next.interactable, Is.True);
        Assert.That(ChoiceLabel.text, Does.Contain("1 / 3"));
        scope.Click(Next);
        Assert.That(ChoiceLabel.text, Does.Contain("2 / 3"));
        Assert.That(Previous.interactable, Is.True);
        Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True, "Changing the card must not rebuild/hide the modal.");
        Assert.That(confirmed, Is.Zero);
        scope.Click(hud.confirmReturnToMenuButton);
        hud.ConfirmReturnToMenu();
        Assert.That(confirmed, Is.EqualTo(1));
        Assert.That(selected, Is.SameAs(cards[1]), "Equal face values cannot substitute a different runtime instance.");
        Assert.That(cancelled, Is.Zero);
        Assert.That(cards.Count, Is.EqualTo(3));
        Assert.That(cards[0].value, Is.EqualTo(2));
        Assert.That(cards[1].value, Is.EqualTo(2));
        Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
        AssertChoiceCleared();
    }

    [Test]
    public void PreviousNextStayBoundedAndSpaceSubmitsOnlyCurrentChoice()
    {
        List<CardData> cards = Cards();
        CardData selected = null;
        hud.ShowDriverSkillCardChoice("选择", "费用", cards, card => selected = card, null);
        scope.Click(Next);
        scope.Click(Next);
        Assert.That(Next.interactable, Is.False);
        Next.onClick.Invoke();
        Assert.That(ChoiceLabel.text, Does.Contain("3 / 3"));
        scope.Click(Previous);
        Assert.That(ChoiceLabel.text, Does.Contain("2 / 3"));
        Assert.That(hud.TryConfirmKeyboardAction(), Is.True);
        Assert.That(selected, Is.SameAs(cards[1]));
        Assert.That(hud.TryConfirmKeyboardAction(), Is.False);
    }

    [Test]
    public void CancelPointerReopensAtFirstCardWithoutSpendingOrLeakingOldCallbacks()
    {
        int confirmed = 0, cancelled = 0;
        List<CardData> cards = Cards();
        CardData selected = null;
        hud.ShowDriverSkillCardChoice("选择", "费用", cards, _ => confirmed++, () => cancelled++);
        scope.Click(Next);
        scope.Click(hud.cancelReturnToMenuButton);
        hud.CancelReturnToMenu();
        Assert.That(confirmed, Is.Zero);
        Assert.That(cancelled, Is.EqualTo(1));
        AssertChoiceCleared();
        hud.ShowDriverSkillCardChoice("重新选择", "费用", cards, card => selected = card, null);
        Assert.That(ChoiceLabel.text, Does.Contain("1 / 3"));
        Assert.That(hud.TryConfirmKeyboardAction(), Is.True);
        Assert.That(selected, Is.SameAs(cards[0]));
        Assert.That(confirmed, Is.Zero);
        Assert.That(cancelled, Is.EqualTo(1));
        Assert.That(cards.Count, Is.EqualTo(3));
    }

    [TestCase(0)]
    [TestCase((int)InRaceConfirmationAction.All)]
    public void NormalRequestCancelsChoiceAndRestoresOriginalLayoutEvenWithMaskBypass(int mask)
    {
        GameObject panel = hud.returnToMenuConfirmPanel;
        RectTransform card = panel.transform.Find("ReturnToMenuConfirmCard").GetComponent<RectTransform>();
        Vector4[] original = Geometry(card);
        int confirmed = 0, cancelled = 0, ordinary = 0;
        hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ => confirmed++, () => cancelled++);
        Button oldNext = Next;
        Assert.That(card.rect.size, Is.EqualTo(new Vector2(620f, 390f)));
        GameSettingsRuntime.Current.inRaceConfirmationMask = mask;
        hud.RequestInRaceAction(InRaceConfirmationAction.CardAction, "确认出牌", "普通确认正文", () => ordinary++);
        oldNext.onClick.Invoke();
        Assert.That(confirmed, Is.Zero);
        Assert.That(cancelled, Is.EqualTo(1));
        Assert.That(Geometry(card), Is.EqualTo(original));
        Assert.That(ChoiceLabel.text, Is.Empty);
        Assert.That(ChoiceRow.activeSelf, Is.False);
        if (mask != 0)
        {
            Assert.That(hud.returnToMenuConfirmText.text, Is.EqualTo("普通确认正文"));
            scope.Click(hud.confirmReturnToMenuButton);
        }
        Assert.That(ordinary, Is.EqualTo(1));
        Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
        AssertChoiceCleared();
    }

    [TestCase("panel-hidden")]
    [TestCase("row-hidden")]
    [TestCase("hud-disabled")]
    [TestCase("root-hidden")]
    [TestCase("ensure-called")]
    [TestCase("hud-destroyed")]
    [TestCase("panel-destroyed")]
    public void ExplicitEditModeLifecycleHooksCancelOnceWithoutCallingConfirm(string reason)
    {
        int confirmed = 0, cancelled = 0;
        hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ => confirmed++, () => cancelled++);
        DriverSkillCardChoiceUI row = ChoiceRow.GetComponent<DriverSkillCardChoiceUI>();
        // Ordinary MonoBehaviours do not receive native lifecycle messages in
        // EditMode. These are explicit wiring assertions; the native coroutine
        // below separately verifies Unity's actual dispatch in PlayMode.
        switch (reason)
        {
            case "panel-hidden": hud.returnToMenuConfirmPanel.SetActive(false); InvokeHook(row, "OnDisable"); break;
            case "row-hidden": ChoiceRow.SetActive(false); InvokeHook(row, "OnDisable"); break;
            case "hud-disabled": hud.enabled = false; InvokeHook(hud, "OnDisable"); break;
            case "root-hidden": scope.Root.SetActive(false); InvokeHook(hud, "OnDisable"); break;
            case "ensure-called": hud.EnsureReturnToMenuConfirmationUI(); break;
            case "hud-destroyed": InvokeHook(hud, "OnDestroy"); Object.DestroyImmediate(hud); break;
            case "panel-destroyed": InvokeHook(row, "OnDestroy"); Object.DestroyImmediate(hud.returnToMenuConfirmPanel); break;
        }
        Assert.That(confirmed, Is.Zero);
        Assert.That(cancelled, Is.EqualTo(1));
        if (hud != null)
        {
            Assert.That(Pending("pendingDriverSkillCardConfirm"), Is.Null);
            Assert.That(Pending("pendingDriverSkillCardCancel"), Is.Null);
            Assert.That(Pending("pendingConfirmationAction"), Is.Null);
        }
    }

    [Test]
    public void RebindingManagerCancelsOldUiIntentWithoutConfirming()
    {
        int confirmed = 0, cancelled = 0;
        hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ => confirmed++, () => cancelled++);
        var managerHost = new GameObject("Inactive alternate manager");
        managerHost.SetActive(false);
        managerHost.transform.SetParent(scope.Root.transform, false);
        MVPGameManager alternate = managerHost.AddComponent<MVPGameManager>();
        hud.SetGameManager(alternate);
        hud.SetGameManager(alternate);
        Assert.That(confirmed, Is.Zero);
        Assert.That(cancelled, Is.EqualTo(1));
        Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
        AssertChoiceCleared();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EmptyOrNullChoicesDisableCommitAndKeepCancelAvailable(bool nullList)
    {
        int confirmed = 0, cancelled = 0;
        hud.ShowDriverSkillCardChoice("选择", "没有可用费用", nullList ? null : new List<CardData>(),
            _ => confirmed++, () => cancelled++);
        Assert.That(hud.confirmReturnToMenuButton.interactable, Is.False);
        Assert.That(Previous.interactable, Is.False);
        Assert.That(Next.interactable, Is.False);
        Assert.That(ChoiceLabel.text, Does.Contain("0 / 0"));
        hud.ConfirmReturnToMenu();
        Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
        Assert.That(confirmed, Is.Zero);
        scope.Click(hud.cancelReturnToMenuButton);
        Assert.That(cancelled, Is.EqualTo(1));
    }

    [Test]
    public void NullConfirmCannotCommitButCanCancel()
    {
        int cancelled = 0;
        hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), null, () => cancelled++);
        Assert.That(hud.confirmReturnToMenuButton.interactable, Is.False);
        hud.ConfirmReturnToMenu();
        Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
        scope.Click(hud.cancelReturnToMenuButton);
        Assert.That(cancelled, Is.EqualTo(1));
    }

    [Test]
    public void ChoiceTakesReferenceSnapshotWithoutOwningOrMutatingCallerCollection()
    {
        List<CardData> cards = Cards();
        CardData exact = cards[1], selected = null;
        hud.ShowDriverSkillCardChoice("选择", "费用", cards, card => selected = card, null);
        cards.Clear();
        scope.Click(Next);
        hud.ConfirmReturnToMenu();
        Assert.That(selected, Is.SameAs(exact));
        Assert.That(cards, Is.Empty);
        Assert.That(exact.value, Is.EqualTo(2));
    }

    [Test]
    public void ReplacingChoiceCancelsOnlyOldRequestAndDoesNotGrowUiOrListeners()
    {
        int cancelled = 0, confirmed = 0;
        for (int i = 0; i < 5; i++)
        {
            hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ => confirmed++, () => cancelled++);
            if (i == 0) continue;
            Assert.That(cancelled, Is.EqualTo(i));
            scope.Click(Next);
            Assert.That(ChoiceLabel.text, Does.Contain("2 / 3"), "Exactly one navigation listener must run.");
        }
        Assert.That(hud.returnToMenuConfirmPanel.GetComponentsInChildren<DriverSkillCardChoiceUI>(true).Length, Is.EqualTo(1));
        Assert.That(confirmed, Is.Zero);
        hud.ConfirmReturnToMenu();
        Assert.That(confirmed, Is.EqualTo(1));
        Assert.That(cancelled, Is.EqualTo(4));
    }

    [Test]
    public void TemporaryChoiceLayoutRestoresAuthoredGeometryFontsMaterialsAndButtonSize()
    {
        Transform card = hud.returnToMenuConfirmPanel.transform.Find("ReturnToMenuConfirmCard");
        TMP_Text title = card.Find("ReturnToMenuConfirmTitle").GetComponent<TMP_Text>();
        var geometry = new Dictionary<RectTransform, Vector4[]>();
        foreach (RectTransform rect in hud.returnToMenuConfirmPanel.GetComponentsInChildren<RectTransform>(true))
            geometry.Add(rect, Geometry(rect));
        TMP_FontAsset font = title.font;
        Material material = title.fontSharedMaterial;
        float fontSize = title.fontSize;
        hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ => { }, null);
        Assert.That(title.font, Is.SameAs(font));
        Assert.That(title.fontSharedMaterial, Is.SameAs(material));
        hud.CancelReturnToMenu();
        foreach (var entry in geometry) Assert.That(Geometry(entry.Key), Is.EqualTo(entry.Value), entry.Key.name);
        Assert.That(title.fontSize, Is.EqualTo(fontSize));
        Assert.That(((RectTransform)card).rect.size, Is.EqualTo(new Vector2(620f, 300f)));
        Assert.That(hud.confirmReturnToMenuButton.interactable, Is.True);
    }

    [Test]
    public void FailedCommitStillConsumesOnlyUiCallbackAndCannotFallBackToMenuNavigation()
    {
        int confirmed = 0;
        string scene = SceneManager.GetActiveScene().path;
        hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ =>
        {
            confirmed++;
            AssertChoiceCleared();
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
            throw new InvalidOperationException("Recorded commit failure");
        }, null);
        Assert.Throws<InvalidOperationException>(() => hud.ConfirmReturnToMenu());
        hud.ConfirmReturnToMenu();
        // A stale external panel activation must not turn an absent action into navigation.
        hud.returnToMenuConfirmPanel.SetActive(true);
        hud.ConfirmReturnToMenu();
        Assert.That(confirmed, Is.EqualTo(1));
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(scene));
    }

    [TestCase(0, false)]
    [TestCase((int)InRaceConfirmationAction.All, false)]
    [TestCase(0, true)]
    [TestCase((int)InRaceConfirmationAction.All, true)]
    public void OpeningChoiceMovesFocusInsideAndDirectionalSubmitCannotReachUnderlyingAction(int mask, bool technology)
    {
        GameSettingsRuntime.Current.inRaceConfirmationMask = mask;
        EventSystem events = CreateActiveEventSystem();
        Button lower = CreateUnderlyingButton("Owned underlying submit", Vector2.zero);
        int lowerCalls = 0, confirmed = 0, cancelled = 0;
        lower.onClick.AddListener(() => lowerCalls++);
        events.SetSelectedGameObject(lower.gameObject);
        if (technology) hud.ShowTechnologyCardChoice("预备餐", "弃牌费用", Cards(), _ => confirmed++, () => cancelled++);
        else hud.ShowDriverSkillCardChoice("决胜抉择", "弃牌费用", Cards(), _ => confirmed++, () => cancelled++);
        Assert.That(events.currentSelectedGameObject, Is.SameAs(hud.cancelReturnToMenuButton.gameObject));
        foreach (MoveDirection direction in new[] { MoveDirection.Up, MoveDirection.Right, MoveDirection.Down, MoveDirection.Left })
        {
            MoveSelected(events, direction);
            AssertModalFocus(events);
        }
        hud.cancelReturnToMenuButton.Select();
        ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        Assert.That(cancelled, Is.EqualTo(1));
        Assert.That(confirmed, Is.Zero);
        Assert.That(lowerCalls, Is.Zero);
        Assert.That(events.currentSelectedGameObject, Is.SameAs(lower.gameObject));
        AssertChoiceCleared();
    }

    [Test]
    public void PagingAwayFromLastOrFirstCardMovesDisabledArrowFocusBackInside()
    {
        EventSystem events = CreateActiveEventSystem();
        hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ => { }, null);
        Next.Select();
        Next.onClick.Invoke();
        Next.onClick.Invoke();
        Assert.That(Next.interactable, Is.False);
        Assert.That(events.currentSelectedGameObject, Is.SameAs(hud.cancelReturnToMenuButton.gameObject));
        Previous.Select();
        Previous.onClick.Invoke();
        Previous.onClick.Invoke();
        Assert.That(Previous.interactable, Is.False);
        Assert.That(events.currentSelectedGameObject, Is.SameAs(hud.cancelReturnToMenuButton.gameObject));
        foreach (MoveDirection direction in new[] { MoveDirection.Left, MoveDirection.Up, MoveDirection.Right, MoveDirection.Down })
        {
            MoveSelected(events, direction);
            AssertModalFocus(events);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EmptyOrUnconfirmableChoiceNavigationKeepsCancelReachable(bool empty)
    {
        EventSystem events = CreateActiveEventSystem();
        hud.ShowDriverSkillCardChoice("选择", "费用", empty ? new List<CardData>() : Cards(), null, null);
        Assert.That(hud.confirmReturnToMenuButton.interactable, Is.False);
        foreach (MoveDirection direction in new[] { MoveDirection.Right, MoveDirection.Down, MoveDirection.Left, MoveDirection.Up })
        {
            MoveSelected(events, direction);
            AssertModalFocus(events);
        }
        hud.cancelReturnToMenuButton.Select();
        ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        AssertChoiceCleared();
    }

    [Test]
    public void ClearingAndReopeningRestoresSharedButtonsAuthoredNavigation()
    {
        CreateActiveEventSystem();
        Button lower = CreateUnderlyingButton("Owned authored navigation target", Vector2.zero);
        Navigation cancel = hud.cancelReturnToMenuButton.navigation;
        cancel.mode = Navigation.Mode.Explicit;
        cancel.selectOnLeft = lower;
        cancel.selectOnDown = lower;
        hud.cancelReturnToMenuButton.navigation = cancel;
        Navigation confirm = hud.confirmReturnToMenuButton.navigation;
        confirm.mode = Navigation.Mode.None;
        hud.confirmReturnToMenuButton.navigation = confirm;
        for (int i = 0; i < 2; i++)
        {
            hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ => { }, null);
            Assert.That(hud.cancelReturnToMenuButton.navigation.selectOnLeft, Is.Not.SameAs(lower));
            Assert.That(hud.confirmReturnToMenuButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            if (i == 0) hud.CancelReturnToMenu();
            else hud.ConfirmReturnToMenu();
            Assert.That(hud.cancelReturnToMenuButton.navigation, Is.EqualTo(cancel));
            Assert.That(hud.confirmReturnToMenuButton.navigation, Is.EqualTo(confirm));
        }
    }

    private static void MoveSelected(EventSystem events, MoveDirection direction)
    {
        ExecuteEvents.Execute(events.currentSelectedGameObject,
            new AxisEventData(events) { moveDir = direction }, ExecuteEvents.moveHandler);
    }

    private void AssertModalFocus(EventSystem events)
    {
        GameObject selected = events.currentSelectedGameObject;
        Assert.That(selected, Is.Not.Null);
        Assert.That(selected.transform.IsChildOf(hud.returnToMenuConfirmPanel.transform), Is.True);
        Selectable button = selected.GetComponent<Selectable>();
        Assert.That(button.IsInteractable() && button.isActiveAndEnabled, Is.True);
        Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
    }

    [TestCase("driver", false)]
    [TestCase("gear", false)]
    [TestCase("driver", true)]
    [TestCase("gear", true)]
    public void RealEventSystemRestoresOpeningFocusAfterChoiceNavigationAndCancelOrConfirm(string source, bool confirm)
    {
        EventSystem events = CreateActiveEventSystem();
        hud.driverSkillButton = CreateUnderlyingButton("Owned DriverSkillButton", new Vector2(-400f, 0f));
        Button original = source == "driver" ? hud.driverSkillButton : CreateUnderlyingButton("Owned Gear1Button", new Vector2(400f, 0f));
        events.SetSelectedGameObject(original.gameObject);
        int confirmed = 0, cancelled = 0;
        hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ => confirmed++, () => cancelled++);
        Next.Select();
        Assert.That(events.currentSelectedGameObject, Is.SameAs(Next.gameObject));
        scope.Click(Next);
        Button close = confirm ? hud.confirmReturnToMenuButton : hud.cancelReturnToMenuButton;
        close.Select();
        scope.Click(close);
        Assert.That(events.currentSelectedGameObject, Is.SameAs(original.gameObject));
        Assert.That(confirmed, Is.EqualTo(confirm ? 1 : 0));
        Assert.That(cancelled, Is.EqualTo(confirm ? 0 : 1));
        AssertChoiceCleared();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InvalidOpeningFocusFallsBackToAvailableDriverAfterCallback(bool destroyed)
    {
        EventSystem events = CreateActiveEventSystem();
        hud.driverSkillButton = CreateUnderlyingButton("Owned DriverSkillButton", new Vector2(-400f, 0f));
        Button original = CreateUnderlyingButton("Owned Gear1Button", new Vector2(400f, 0f));
        events.SetSelectedGameObject(original.gameObject);
        hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ =>
        {
            if (destroyed) Object.DestroyImmediate(original.gameObject);
            else original.interactable = false;
        }, null);
        Next.Select();
        scope.Click(Next);
        hud.ConfirmReturnToMenu();
        Assert.That(events.currentSelectedGameObject, Is.SameAs(hud.driverSkillButton.gameObject),
            "Focus eligibility must be checked after the gameplay callback, not before it.");
    }

    [Test]
    public void ReplacingChoiceWithNormalRequestRestoresOpeningFocus()
    {
        EventSystem events = CreateActiveEventSystem();
        Button original = CreateUnderlyingButton("Owned Gear1Button", new Vector2(400f, 0f));
        events.SetSelectedGameObject(original.gameObject);
        hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ => { }, null);
        Next.Select();
        scope.Click(Next);
        hud.RequestInRaceAction(InRaceConfirmationAction.CardAction, "确认出牌", "原操作", () => { });
        Assert.That(events.currentSelectedGameObject, Is.SameAs(original.gameObject));
        Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
        Assert.That(ChoiceRow.activeSelf, Is.False);
    }

    [Test]
    public void DisabledHudDoesNotSelectAnInvalidSourceOrKeepHiddenChoiceFocus()
    {
        EventSystem events = CreateActiveEventSystem();
        Button original = CreateUnderlyingButton("Owned Gear1Button", new Vector2(400f, 0f));
        events.SetSelectedGameObject(original.gameObject);
        hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ => { }, null);
        Next.Select();
        original.interactable = false;
        hud.enabled = false;
        InvokeHook(hud, "OnDisable");
        Assert.That(events.currentSelectedGameObject, Is.Null);
    }

    [Test]
    public void SuccessfulActivationReturnsToLegalGearWhenSkillButtonBecomesDisabled()
    {
        EventSystem events = CreateActiveEventSystem();
        hud.driverSkillButton = CreateUnderlyingButton("Owned DriverSkillButton", new Vector2(-400f, 0f));
        hud.gear1Button = CreateUnderlyingButton("Owned Gear1Button", new Vector2(400f, 0f));
        events.SetSelectedGameObject(hud.driverSkillButton.gameObject);
        hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ =>
        {
            hud.driverSkillButton.interactable = false;
            // As in the real manager callback, reopen legal gear controls only
            // after the modal closes. Attach starts them disabled without a race.
            hud.TeamGearPresentation.Refresh(new PlayerState("owned focus driver", false, 0, 1)
                { teamId = TeamId.UK }, 1, true, false, null, true);
        }, null);
        hud.confirmReturnToMenuButton.Select();
        scope.Click(hud.confirmReturnToMenuButton);
        Assert.That(events.currentSelectedGameObject, Is.SameAs(hud.gear1Button.gameObject));
    }

    [Test]
    public void OverlayIsTopRaycastAndUnderlyingButtonOnlyReceivesPointerAfterCancel()
    {
        Button underlying = CreateUnderlyingButton("Owned underlying race action", new Vector2(0f, 350f));
        int lowerCalls = 0;
        underlying.onClick.AddListener(() => lowerCalls++);
        hud.ShowDriverSkillCardChoice("选择", "费用", Cards(), _ => { }, null);
        RenderOwnedCanvas();
        Camera camera = scope.Root.GetComponent<Canvas>().worldCamera;
        RectTransform rect = underlying.GetComponent<RectTransform>();
        Vector2 center = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(scope.EventSystem) { position = center, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        scope.Root.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
        Assert.That(hits, Is.Not.Empty);
        Assert.That(hits[0].gameObject, Is.SameAs(hud.returnToMenuConfirmPanel));
        GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.That(receiver, Is.Not.SameAs(underlying.gameObject));
        if (receiver != null) ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
        Assert.That(lowerCalls, Is.Zero);
        scope.Click(hud.cancelReturnToMenuButton);
        scope.Click(underlying);
        Assert.That(lowerCalls, Is.EqualTo(1));
    }

    private EventSystem CreateActiveEventSystem()
    {
        var host = new GameObject("Owned active focus EventSystem");
        host.transform.SetParent(scope.Root.transform, false);
        EventSystem events = host.AddComponent<EventSystem>();
        // A normal MonoBehaviour is not registered by Unity in EditMode.
        // Exercise the real registry hook explicitly; the native test below
        // separately verifies automatic registration and focus restoration.
        InvokeHook(events, "OnEnable");
        ownedFocusEventSystem = events;
        EventSystem.current = events;
        Assert.That(EventSystem.current, Is.SameAs(events));
        return events;
    }

    private Button CreateUnderlyingButton(string name, Vector2 position)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        host.transform.SetParent(scope.Root.transform, false);
        RectTransform rect = host.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(200f, 64f);
        return host.GetComponent<Button>();
    }

    private void RenderOwnedCanvas()
    {
        foreach (Transform child in scope.Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in scope.Root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        RenderTexture previous = RenderTexture.active;
        try { scope.Root.GetComponent<Canvas>().worldCamera.Render(); }
        finally { RenderTexture.active = previous; }
    }

    private static void InvokeHook(Component target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);

    private Button Previous => Find("DriverSkillChoicePreviousButton").GetComponent<Button>();
    private Button Next => Find("DriverSkillChoiceNextButton").GetComponent<Button>();
    private TMP_Text ChoiceLabel => Find("DriverSkillChoiceLabel").GetComponent<TMP_Text>();
    private GameObject ChoiceRow => Find("DriverSkillCardChoiceRow").gameObject;

    private Transform Find(string name)
    {
        foreach (Transform node in hud.returnToMenuConfirmPanel.GetComponentsInChildren<Transform>(true))
            if (node.name == name) return node;
        throw new InvalidOperationException("Fixture UI missing: " + name);
    }

    private void AssertChoiceCleared()
    {
        Assert.That(ChoiceRow.activeSelf, Is.False);
        Assert.That(ChoiceLabel.text, Is.Empty);
        Assert.That(Pending("pendingDriverSkillCardConfirm"), Is.Null);
        Assert.That(Pending("pendingDriverSkillCardCancel"), Is.Null);
        Assert.That(Pending("pendingConfirmationType"), Is.Null);
        Assert.That(Pending("pendingConfirmationAction"), Is.Null);
    }

    private object Pending(string name) => typeof(HUDUI).GetField(name, Private).GetValue(hud);
    private static List<CardData> Cards() => new List<CardData>
    {
        new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 4)
    };
    private static Vector4[] Geometry(RectTransform rect) => new[]
    {
        new Vector4(rect.anchorMin.x, rect.anchorMin.y, rect.anchorMax.x, rect.anchorMax.y),
        new Vector4(rect.offsetMin.x, rect.offsetMin.y, rect.offsetMax.x, rect.offsetMax.y),
        new Vector4(rect.pivot.x, rect.pivot.y, rect.sizeDelta.x, rect.sizeDelta.y)
    };
}

/// <summary>Actual Unity lifecycle dispatch, separate from explicit EditMode hook assertions.</summary>
public sealed class HunterSkillCardChoiceNativeLifecycleTests
{
    private static readonly FieldInfo Settings = typeof(GameSettingsRuntime).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);

    [UnityTest]
    public IEnumerator NativeHideDisableAndDestroyCancelEachPendingSelectionOnce()
    {
        yield return new EnterPlayMode();
        object saved = Settings.GetValue(null);
        GameSettingsData settings = GameSettingsData.CreateDefault();
        settings.inRaceConfirmationMask = (int)InRaceConfirmationAction.All;
        settings.reduceMotion = true;
        Settings.SetValue(null, settings);
        Scene scene = SceneManager.CreateScene("Owned Hunter choice native lifecycle");
        GameObject root = null;
        TMP_FontAsset font = null;
        Material material = null;
        EventSystem priorEvents = EventSystem.current;
        try
        {
            TMP_FontAsset source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");
            bool sourceDirty = EditorUtility.IsDirty(source);
            font = Object.Instantiate(source);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            material = Object.Instantiate(source.material);
            font.material = material;
            root = new GameObject("Owned Hunter modal canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var seed = new GameObject("Owned Hunter font", typeof(RectTransform), typeof(TextMeshProUGUI));
            seed.transform.SetParent(root.transform, false);
            seed.GetComponent<TMP_Text>().font = font;
            seed.SetActive(false);
            HUDUI hud = root.AddComponent<HUDUI>();
            yield return null;
            Assert.That(hud.returnToMenuConfirmPanel, Is.Not.Null, "Unity Start must build the real modal.");
            int confirmed = 0, cancelled = 0;
            var cards = new List<CardData> { new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 4) };
            Action open = () => hud.ShowDriverSkillCardChoice("选择", "费用", cards, _ => confirmed++, () => cancelled++);

            var eventHost = new GameObject("Owned native choice EventSystem", typeof(EventSystem));
            eventHost.transform.SetParent(root.transform, false);
            EventSystem events = eventHost.GetComponent<EventSystem>();
            EventSystem.current = events;
            var focusHost = new GameObject("Owned native skill focus", typeof(RectTransform), typeof(Image), typeof(Button));
            focusHost.transform.SetParent(root.transform, false);
            hud.driverSkillButton = focusHost.GetComponent<Button>();
            events.SetSelectedGameObject(focusHost);
            int focusCancelled = 0;
            hud.ShowDriverSkillCardChoice("选择", "费用", cards, _ => { }, () => focusCancelled++);
            hud.returnToMenuConfirmPanel.GetComponentInChildren<DriverSkillCardChoiceUI>(true)
                .transform.Find("DriverSkillChoiceNextButton").GetComponent<Button>().Select();
            hud.CancelReturnToMenu();
            Assert.That(focusCancelled, Is.EqualTo(1));
            Assert.That(events.currentSelectedGameObject, Is.SameAs(focusHost), "Native EventSystem focus returns after cancel.");

            open();
            hud.returnToMenuConfirmPanel.SetActive(false);
            yield return null;
            Assert.That(cancelled, Is.EqualTo(1), "Native parent-panel OnDisable must cancel.");
            Assert.That(confirmed, Is.Zero);
            open();
            hud.returnToMenuConfirmPanel.GetComponentInChildren<DriverSkillCardChoiceUI>(true).gameObject.SetActive(false);
            yield return null;
            Assert.That(cancelled, Is.EqualTo(2), "Native row OnDisable must cancel.");
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
            Assert.That(hud.returnToMenuConfirmPanel.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(hud.returnToMenuConfirmPanel.GetComponent<CanvasGroup>().alpha, Is.Zero);
            open();
            Assert.That(hud.returnToMenuConfirmPanel.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            Assert.That(hud.returnToMenuConfirmPanel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            hud.enabled = false;
            yield return null;
            Assert.That(cancelled, Is.EqualTo(3), "Native HUD OnDisable must cancel.");
            hud.enabled = true;
            open();
            root.SetActive(false);
            yield return null;
            Assert.That(cancelled, Is.EqualTo(4), "Native root OnDisable must cancel once.");
            root.SetActive(true);
            open();
            Object.Destroy(hud.returnToMenuConfirmPanel);
            yield return null;
            Assert.That(cancelled, Is.EqualTo(5), "Native modal destruction must cancel once.");
            open();
            Object.Destroy(hud);
            yield return null;
            Assert.That(cancelled, Is.EqualTo(6), "Native HUD destruction must cancel once.");
            Assert.That(confirmed, Is.Zero);
            Assert.That(EditorUtility.IsDirty(source), Is.EqualTo(sourceDirty));
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            if (priorEvents != null && priorEvents.isActiveAndEnabled) EventSystem.current = priorEvents;
            if (font != null) Object.DestroyImmediate(font);
            if (material != null) Object.DestroyImmediate(material);
            Settings.SetValue(null, saved);
        }
        yield return SceneManager.UnloadSceneAsync(scene);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator EnsureEditMode() { if (EditorApplication.isPlaying) yield return new ExitPlayMode(); }
}
