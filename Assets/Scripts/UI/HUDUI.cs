using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// HUD UI 管理器 — 显示比赛状态（档位、热量池、圈数、位置），
/// 提供档位按钮、出牌按钮、重置按钮，以及状态文本和日志输出。
/// </summary>
public class HUDUI : MonoBehaviour
{
    [Header("状态文本")]
    public TMP_Text statusText;
    public TMP_Text gearText;
    public TMP_Text heatText;
    public TMP_Text lapText;
    public TMP_Text positionText;
    public TMP_Text aiStatusText;

    [Header("5 系统接入显示 (可选，未赋值则跳过)")]
    public TMP_Text weatherText;
    public TMP_Text standingsText;

    [Header("日志")]
    public TMP_Text logText;
    public ScrollRect logScrollRect;
    public int maxLogLines = 80;

    [Header("档位按钮")]
    public UnityEngine.UI.Button gear1Button;
    public UnityEngine.UI.Button gear2Button;
    public UnityEngine.UI.Button gear3Button;
    public UnityEngine.UI.Button gear4Button;

    [Header("操作按钮")]
    public UnityEngine.UI.Button confirmGearButton;
    public UnityEngine.UI.Button driverSkillButton;
    public TMP_Text driverSkillLabel;
    public UnityEngine.UI.Button resetButton;
    public UnityEngine.UI.Button returnToMenuButton;
    public UnityEngine.UI.Button backToMenuButton;

    [Header("返回主菜单确认（可选，旧预制体会自动创建）")]
    public GameObject returnToMenuConfirmPanel;
    public TMP_Text returnToMenuConfirmText;
    public UnityEngine.UI.Button confirmReturnToMenuButton;
    public UnityEngine.UI.Button cancelReturnToMenuButton;

    [Header("弯道限速明细（点击赛道数字打开）")]
    public GameObject cornerLimitDetailsPanel;
    public TMP_Text cornerLimitDetailsTitle;
    public TMP_Text cornerLimitDetailsText;
    public UnityEngine.UI.Button closeCornerLimitDetailsButton;

    [Header("游戏结束面板")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;
    public TMP_Text gameOverTitle;
    public ScrollRect gameOverScrollRect;

    private MVPGameManager gameManager;
    private string logBuffer = "";
    private Action<string> logSink;
    private Action pendingConfirmationAction;
    private InRaceConfirmationAction? pendingConfirmationType;
    private TMP_Text returnToMenuConfirmTitle;
    private DriverSkillCardChoiceUI driverSkillCardChoice;
    private UnityEngine.Events.UnityAction<CardData> pendingDriverSkillCardConfirm;
    private UnityEngine.Events.UnityAction pendingDriverSkillCardCancel;
    private bool driverSkillCardChoicePending;
    private bool driverSkillChoiceRequiresCard = true;
    private CanvasGroup confirmationVisibility;
    private bool confirmationHiddenByLifecycle;
    private float confirmationPreviousAlpha;
    private bool confirmationPreviousInteraction, confirmationPreviousRaycasts;
    private DriverSkillChoiceFocus driverSkillChoiceFocus;
    private UkPrepMealActionUI ukPrepMealAction;
    private GermanFuelActionUI germanFuelAction;
    private GermanBurstActionUI germanBurstAction;
    [SerializeField] private HeatThermometerUI heatThermometer;
    [SerializeField] private GearDialPresentationUI gearDialPresentation;
    [SerializeField] private TeamGearPresentationUI teamGearPresentation;
    private bool teamGearControlsEnabled = true;
    private GameObject telemetryDetailsPanel;
    private TMP_Text telemetryDetailsText;
    private Button closeTelemetryDetailsButton;
    private PlayerState telemetryPlayer;

    public HeatThermometerUI HeatThermometer => heatThermometer;
    public GearDialPresentationUI GearDialPresentation => gearDialPresentation;
    public TeamGearPresentationUI TeamGearPresentation => teamGearPresentation;
    public Button UkPrepMealButton => ukPrepMealAction != null ? ukPrepMealAction.Button : null;
    public Button GermanFuelButton => germanFuelAction != null ? germanFuelAction.Button : null;
    public Button GermanBurstButton => germanBurstAction != null ? germanBurstAction.Button : null;
    public bool IsTelemetryDetailsOpen => telemetryDetailsPanel != null && telemetryDetailsPanel.activeSelf;
    public GameConfigSO GameConfigForPresentation => gameManager != null ? gameManager.Config : null;
    public bool IsReturnToMenuConfirmationVisible =>
        returnToMenuConfirmPanel != null && returnToMenuConfirmPanel.activeSelf && !confirmationHiddenByLifecycle;

    void Start()
    {
        // 绑定档位按钮
        gear1Button?.onClick.RemoveAllListeners();
        gear2Button?.onClick.RemoveAllListeners();
        gear3Button?.onClick.RemoveAllListeners();
        gear4Button?.onClick.RemoveAllListeners();
        BindGearButton(gear1Button, 1);
        BindGearButton(gear2Button, 2);
        BindGearButton(gear3Button, 3);
        BindGearButton(gear4Button, 4);

        if (confirmGearButton != null)
        {
            confirmGearButton.onClick.RemoveAllListeners();
            confirmGearButton.onClick.AddListener(OnConfirmGearClicked);
        }

        if (driverSkillButton != null)
        {
            driverSkillButton.onClick.RemoveAllListeners();
            driverSkillButton.onClick.AddListener(OnDriverSkillClicked);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(OnResetClicked);
        }

        if (returnToMenuButton != null)
        {
            returnToMenuButton.onClick.RemoveAllListeners();
            returnToMenuButton.onClick.AddListener(OnBackToMenuClicked);
        }

        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.RemoveAllListeners();
            backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
        }

        EnsureReturnToMenuConfirmationUI();

        if (confirmReturnToMenuButton != null)
        {
            confirmReturnToMenuButton.onClick.RemoveAllListeners();
            confirmReturnToMenuButton.onClick.AddListener(ConfirmReturnToMenu);
        }

        if (cancelReturnToMenuButton != null)
        {
            cancelReturnToMenuButton.onClick.RemoveAllListeners();
            cancelReturnToMenuButton.onClick.AddListener(CancelReturnToMenu);
        }

        ButtonClickAnimation.Attach(gear1Button);
        ButtonClickAnimation.Attach(gear2Button);
        ButtonClickAnimation.Attach(gear3Button);
        ButtonClickAnimation.Attach(gear4Button);
        ButtonClickAnimation.Attach(confirmGearButton);
        ButtonClickAnimation.Attach(driverSkillButton);
        ButtonClickAnimation.Attach(resetButton);
        ButtonClickAnimation.Attach(returnToMenuButton);
        ButtonClickAnimation.Attach(backToMenuButton);
        ButtonClickAnimation.Attach(confirmReturnToMenuButton);
        ButtonClickAnimation.Attach(cancelReturnToMenuButton);

        EnsureGameOverPresentation();
        HideGameOver();

        EnsurePresentation();
    }

    private void BindGearButton(UnityEngine.UI.Button btn, int gear)
    {
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnGearClicked(gear));
        }
    }

    /// <summary>在游戏初始化后设置引用。</summary>
    public void SetGameManager(MVPGameManager gm)
    {
        if (gameManager != gm && driverSkillCardChoicePending) DismissConfirmation(false, true);
        if (gameManager != gm && telemetryDetailsPanel != null) telemetryDetailsPanel.SetActive(false);
        gameManager = gm;
        if (gm != null && gm.Player != null) RefreshTurnTelemetry(gm.Player);
        RefreshTeamGearPresentation();
    }

    /// <summary>Sets an optional sink for full-fidelity manual playtest logs.</summary>
    public void SetLogSink(Action<string> sink)
    {
        logSink = sink;
    }

    // ====== 刷新 ======

    /// <summary>
    /// 刷新所有 HUD 显示（2 人兼容重载）。
    /// </summary>
    public void Refresh(MVPGameManager gm, PlayerState player, PlayerState ai)
    {
        Refresh(gm, player, ai, null);
    }

    /// <summary>
    /// 刷新所有 HUD 显示 — 多车模式传入全部玩家以显示排名。
    /// </summary>
    public void Refresh(MVPGameManager gm, PlayerState player, PlayerState ai, System.Collections.Generic.IReadOnlyList<PlayerState> allPlayers)
    {
        if (gameManager != gm && driverSkillCardChoicePending) DismissConfirmation(false, true);
        gameManager = gm;

        RefreshPlayerResources(player);
        RefreshDriverSkill(gm, player);

        if (lapText != null)
            lapText.text = RaceHudTextRules.FormatLap(player, gm.Config.totalLaps);

        if (positionText != null)
            positionText.text = RaceHudTextRules.FormatPosition(
                player, allPlayers, gm.Track.TotalNodes);

        if (aiStatusText != null && ai != null)
            aiStatusText.text = RaceHudTextRules.FormatAiStatus(
                ai, gm.Track != null ? gm.Track.TotalNodes : 0);

        // 天气显示
        if (weatherText != null)
            weatherText.text = gm.WeatherLabel;

        // Corner numbers are live values, not authored constants. Refreshing
        // them with the same HUD pass keeps weather/driver/technology changes
        // immediately visible without touching the track geometry.
        gm.Track?.RefreshCornerLimitLabels();

        // 多车排行榜
        if (standingsText != null && allPlayers != null && allPlayers.Count > 1)
            standingsText.text = RaceHudTextRules.FormatStandings(
                allPlayers, player, gm.Track != null ? gm.Track.TotalNodes : 0);
    }

    /// <summary>Immediately refreshes the local player's gear and heat resources.</summary>
    public void RefreshPlayerResources(PlayerState player)
    {
        if (player == null) return;
        RefreshTurnTelemetry(player);
        RefreshTeamGearPresentation();
        if (player.deck == null) return;

        // Resolve the authored thermometer even when an older RaceCanvas
        // instance lost the private serialized field during prefab rebuild.
        // The gauge is a live view of the player's deck, not a static label.
        EnsurePresentation();
        HeatGaugeState gauge = HeatGaugeRules.Evaluate(player.deck, player.playedSpeedCardsThisTurn);

        if (gearText != null)
            gearText.text = $"档位: {TeamGearRules.GetDisplayName(player.teamId, player.gear)}";

        if (heatText != null)
        {
            int handHeat = player.deck.CountHeatInHand();
            int drawHeat = player.deck.CountHeatInDrawPile();
            int discardHeat = player.deck.CountHeatInDiscardPile();
            string heatColor = gauge.WarningLevel == HeatWarningLevel.Critical
                ? "#F74840"
                : gauge.WarningLevel == HeatWarningLevel.Elevated ? "#F79A3D" : "#58A6FF";
            string tempInfo = gauge.TemporaryHeat > 0 ? $" 临{gauge.TemporaryHeat}" : "";
            string spinInfo = player.spinCounter > 0 ? $" | 失控 {player.spinCounter}/3" : "";
            heatText.text = $"引擎 {gauge.EngineRemaining}/{gauge.Capacity}  <color={heatColor}>热量 {gauge.TotalHeat}</color>\n"
                + $"手{handHeat} 抽{drawHeat} 弃{discardHeat}{tempInfo}{spinInfo}";
        }

        if (heatThermometer != null) heatThermometer.RefreshSnapshot(gauge,
            teamGearPresentation != null && teamGearPresentation.Telemetry != null
                ? teamGearPresentation.Telemetry.LivePreview?.ProjectedHeat : null);
    }

    /// <summary>Builds the authored visual wrappers without changing button ownership.</summary>
    public void EnsurePresentation()
    {
        if (heatThermometer == null)
            heatThermometer = GetComponentInChildren<HeatThermometerUI>(true);
        if (heatText != null && heatThermometer == null)
            heatThermometer = HeatThermometerUI.Attach(heatText);

        Transform gearContainer = gear1Button != null ? gear1Button.transform.parent : null;
        if (gearContainer != null && teamGearPresentation == null)
        {
            // Keep authored dial components available to legacy tools, while
            // the team view is the only runtime writer of the gear layout.
            if (gearDialPresentation == null)
                gearDialPresentation = gearContainer.GetComponent<GearDialPresentationUI>();
            if (gearDialPresentation != null)
                gearDialPresentation.enabled = false;
            teamGearPresentation = TeamGearPresentationUI.Attach(
                gearContainer, this, gear1Button, gear2Button, gear3Button, gear4Button);
        }
    }

    /// <summary>
    /// Ensures that every RaceCanvas, including older authored prefabs, has a
    /// modal confirmation surface before leaving the active race. The panel is
    /// runtime UI so the existing authored layout is never overwritten.
    /// </summary>
    public void EnsureReturnToMenuConfirmationUI()
    {
        if (driverSkillCardChoicePending) DismissConfirmation(false, true);
        Transform canvasRoot = transform.root != null ? transform.root : transform;

        if (returnToMenuConfirmPanel == null)
            returnToMenuConfirmPanel = FindObjectByName(canvasRoot, "ReturnToMenuConfirmPanel")?.gameObject;

        if (returnToMenuConfirmPanel != null)
        {
            if (returnToMenuConfirmTitle == null)
                returnToMenuConfirmTitle = FindComponentByName<TMP_Text>(returnToMenuConfirmPanel.transform,
                    "ReturnToMenuConfirmTitle");
            if (returnToMenuConfirmText == null)
                returnToMenuConfirmText = FindComponentByName<TMP_Text>(returnToMenuConfirmPanel.transform,
                    "ReturnToMenuConfirmText");
            if (confirmReturnToMenuButton == null)
                confirmReturnToMenuButton = FindComponentByName<Button>(returnToMenuConfirmPanel.transform,
                    "ConfirmReturnToMenuButton");
            if (cancelReturnToMenuButton == null)
                cancelReturnToMenuButton = FindComponentByName<Button>(returnToMenuConfirmPanel.transform,
                    "CancelReturnToMenuButton");
        }

        if (returnToMenuConfirmPanel == null)
            BuildReturnToMenuConfirmationUI(canvasRoot);

        if (returnToMenuConfirmPanel != null)
        {
            if (confirmationVisibility == null)
            {
                confirmationVisibility = returnToMenuConfirmPanel.GetComponent<CanvasGroup>();
                if (confirmationVisibility == null)
                    confirmationVisibility = returnToMenuConfirmPanel.AddComponent<CanvasGroup>();
            }
            if (confirmationHiddenByLifecycle)
            {
                confirmationVisibility.alpha = confirmationPreviousAlpha;
                confirmationVisibility.interactable = confirmationPreviousInteraction;
                confirmationVisibility.blocksRaycasts = confirmationPreviousRaycasts;
                confirmationHiddenByLifecycle = false;
            }
            RacingTechConfirmationStyle.Apply(returnToMenuConfirmPanel, returnToMenuConfirmTitle,
                returnToMenuConfirmText, cancelReturnToMenuButton, confirmReturnToMenuButton);
            returnToMenuConfirmPanel.SetActive(false);
        }
    }

    /// <summary>Opens the return confirmation without changing any race state.</summary>
    public void ShowReturnToMenuConfirmation()
    {
        RequestReturnToMenu(SceneLoader.LoadMainMenu);
    }

    private void RequestReturnToMenu(UnityEngine.Events.UnityAction loadMainMenu)
    {
        RequestInRaceAction(
            InRaceConfirmationAction.ReturnToMenu,
            "返回主菜单",
            "确定要退出当前比赛并返回主菜单吗？\n当前比赛进度不会保存。",
            loadMainMenu);
    }

    /// <summary>Dismisses the modal and leaves the race untouched.</summary>
    public void CancelReturnToMenu()
    {
        DismissConfirmation(true, true);
    }

    private void DismissConfirmation(bool refresh, bool notifyCancel)
    {
        UnityEngine.Events.UnityAction cancel = ClearDriverSkillCardChoice(out DriverSkillChoiceFocus focus);
        pendingConfirmationAction = null;
        pendingConfirmationType = null;
        if (returnToMenuConfirmPanel != null)
            returnToMenuConfirmPanel.SetActive(false);
        try
        {
            if (refresh) RefreshTeamGearPresentation();
            if (notifyCancel) cancel?.Invoke();
        }
        finally { RestoreDriverSkillChoiceFocus(focus); }
    }

    /// <summary>
    /// Native OnDisable may run inside a child or ancestor's SetActive operation.
    /// Clear the intent and hide/block the surface without re-entering that hierarchy transition.
    /// The next ordinary Ensure restores its original CanvasGroup values.
    /// </summary>
    public void CancelDriverSkillChoiceForLifecycle()
    {
        UnityEngine.Events.UnityAction cancel = ClearDriverSkillCardChoice(out DriverSkillChoiceFocus focus, false);
        pendingConfirmationAction = null;
        pendingConfirmationType = null;
        if (confirmationVisibility != null && !confirmationHiddenByLifecycle)
        {
            confirmationPreviousAlpha = confirmationVisibility.alpha;
            confirmationPreviousInteraction = confirmationVisibility.interactable;
            confirmationPreviousRaycasts = confirmationVisibility.blocksRaycasts;
            confirmationHiddenByLifecycle = true;
            confirmationVisibility.alpha = 0f;
            confirmationVisibility.interactable = false;
            confirmationVisibility.blocksRaycasts = false;
        }
        try { cancel?.Invoke(); }
        finally { RestoreDriverSkillChoiceFocus(focus); }
    }

    /// <summary>Confirms the destructive navigation action.</summary>
    public void ConfirmReturnToMenu()
    {
        if (!IsReturnToMenuConfirmationVisible)
            return;

        Action action = pendingConfirmationAction;
        DriverSkillChoiceFocus focus = null;
        if (driverSkillCardChoicePending)
        {
            CardData selected = driverSkillCardChoice != null ? driverSkillCardChoice.Selected : null;
            if ((driverSkillChoiceRequiresCard && selected == null) || pendingDriverSkillCardConfirm == null ||
                confirmReturnToMenuButton == null || !confirmReturnToMenuButton.interactable)
                return;
            UnityEngine.Events.UnityAction<CardData> confirm = pendingDriverSkillCardConfirm;
            action = () => confirm.Invoke(selected);
            ClearDriverSkillCardChoice(out focus);
        }
        pendingConfirmationAction = null;
        pendingConfirmationType = null;
        returnToMenuConfirmPanel.SetActive(false);
        if (focus == null)
        {
            action?.Invoke();
            RefreshTeamGearPresentation();
        }
        else
        {
            try { action?.Invoke(); }
            finally
            {
                RefreshTeamGearPresentation();
                RestoreDriverSkillChoiceFocus(focus);
            }
        }
    }

    /// <summary>A required cost choice shares the existing single confirmation surface.</summary>
    public void ShowDriverSkillCardChoice(string title, string message, IReadOnlyList<CardData> choices,
        UnityEngine.Events.UnityAction<CardData> confirm, UnityEngine.Events.UnityAction cancel)
    {
        ShowRequiredCardChoice(title, message, choices, confirm, cancel,
            InRaceConfirmationAction.DriverSkill, "弃置并发动", null);
    }

    /// <summary>A technology cost choice uses the same required selection and lifecycle surface.</summary>
    public void ShowTechnologyCardChoice(string title, string message, IReadOnlyList<CardData> choices,
        UnityEngine.Events.UnityAction<CardData> confirm, UnityEngine.Events.UnityAction cancel)
    {
        CardHandUI hand = transform.root.GetComponentInChildren<CardHandUI>(true);
        GameObject continuation = hand != null && hand.playCardsButton != null ? hand.playCardsButton.gameObject : null;
        ShowRequiredCardChoice(title, message, choices, confirm, cancel,
            InRaceConfirmationAction.CardAction, "弃牌并回热", continuation);
    }

    /// <summary>Optional fixed-heat continuation shares cancellation/focus and honors the existing confirmation switch.</summary>
    public void ShowDriverSkillContinuation(string title, string message,
        UnityEngine.Events.UnityAction confirm, UnityEngine.Events.UnityAction cancel)
    {
        if (confirm == null || IsTelemetryDetailsOpen) { cancel?.Invoke(); return; }
        if (InRaceConfirmationRules.IsEnabled(GameSettingsRuntime.Current.inRaceConfirmationMask,
                InRaceConfirmationAction.DriverSkill))
        {
            ShowRequiredCardChoice(title, message, Array.Empty<CardData>(), _ => confirm.Invoke(), cancel,
                InRaceConfirmationAction.DriverSkill, "支付1热并追击", null, false);
            return;
        }
        DismissConfirmation(false, true);
        EventSystem events = EventSystem.current;
        GameObject previous = events != null ? events.currentSelectedGameObject : null;
        var focus = new DriverSkillChoiceFocus(events, previous, null, null);
        try { confirm.Invoke(); }
        finally { RefreshTeamGearPresentation(); RestoreDriverSkillChoiceFocus(focus); }
    }

    private void ShowRequiredCardChoice(string title, string message, IReadOnlyList<CardData> choices,
        UnityEngine.Events.UnityAction<CardData> confirm, UnityEngine.Events.UnityAction cancel,
        InRaceConfirmationAction confirmationType, string confirmLabel, GameObject continuationFocus, bool requiresCard = true)
    {
        DismissConfirmation(false, true);
        if (IsTelemetryDetailsOpen)
        {
            cancel?.Invoke();
            return;
        }
        EnsureReturnToMenuConfirmationUI();
        Transform card = returnToMenuConfirmPanel != null
            ? returnToMenuConfirmPanel.transform.Find("ReturnToMenuConfirmCard") : null;
        if (card == null || confirmReturnToMenuButton == null || cancelReturnToMenuButton == null)
        {
            cancel?.Invoke();
            return;
        }
        driverSkillCardChoice = DriverSkillCardChoiceUI.Attach(card, FindReferenceFont(card));
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        driverSkillChoiceFocus = new DriverSkillChoiceFocus(eventSystem,
            IsAvailableFocus(selected) ? selected : null, driverSkillCardChoice.gameObject, continuationFocus);
        if (returnToMenuConfirmTitle != null) returnToMenuConfirmTitle.text = title;
        if (returnToMenuConfirmText != null) returnToMenuConfirmText.text = message;
        SetConfirmationButtonLabels(confirmationType);
        TMP_Text commitLabel = confirmReturnToMenuButton.GetComponentInChildren<TMP_Text>(true);
        if (commitLabel != null) commitLabel.text = confirmLabel;
        pendingDriverSkillCardConfirm = confirm;
        pendingDriverSkillCardCancel = cancel;
        driverSkillCardChoicePending = true;
        driverSkillChoiceRequiresCard = requiresCard;
        pendingConfirmationType = confirmationType;
        driverSkillCardChoice.Show(choices, returnToMenuConfirmTitle, returnToMenuConfirmText,
            cancelReturnToMenuButton, confirmReturnToMenuButton, confirm != null,
            CancelDriverSkillChoiceForLifecycle, requiresCard);
        returnToMenuConfirmPanel.transform.SetAsLastSibling();
        returnToMenuConfirmPanel.SetActive(true);
        RefreshTeamGearPresentation();
        driverSkillCardChoice.FocusWithinModal();
    }

    private UnityEngine.Events.UnityAction ClearDriverSkillCardChoice(out DriverSkillChoiceFocus focus, bool hideRow = true)
    {
        focus = driverSkillChoiceFocus;
        driverSkillChoiceFocus = null;
        UnityEngine.Events.UnityAction cancel = pendingDriverSkillCardCancel;
        driverSkillCardChoicePending = false;
        driverSkillChoiceRequiresCard = true;
        pendingDriverSkillCardConfirm = null;
        pendingDriverSkillCardCancel = null;
        if (driverSkillCardChoice != null) driverSkillCardChoice.HideAndClear(hideRow);
        return cancel;
    }

    private sealed class DriverSkillChoiceFocus
    {
        internal readonly EventSystem EventSystem;
        internal readonly GameObject Previous, Row, Continuation;
        internal DriverSkillChoiceFocus(EventSystem eventSystem, GameObject previous, GameObject row, GameObject continuation)
        { EventSystem = eventSystem; Previous = previous; Row = row; Continuation = continuation; }
    }

    private static bool IsAvailableFocus(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return false;
        Selectable selectable = target.GetComponent<Selectable>();
        return selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable();
    }

    private void RestoreDriverSkillChoiceFocus(DriverSkillChoiceFocus focus)
    {
        if (focus == null || focus.EventSystem == null || !focus.EventSystem.isActiveAndEnabled ||
            EventSystem.current != focus.EventSystem)
            return;
        GameObject target = null;
        if (this != null && isActiveAndEnabled)
        {
            if (IsAvailableFocus(focus.Previous)) target = focus.Previous;
            else if (IsAvailableFocus(focus.Continuation)) target = focus.Continuation;
            else if (UkPrepMealButton != null && IsAvailableFocus(UkPrepMealButton.gameObject)) target = UkPrepMealButton.gameObject;
            else if (GermanFuelButton != null && IsAvailableFocus(GermanFuelButton.gameObject)) target = GermanFuelButton.gameObject;
            else if (GermanBurstButton != null && IsAvailableFocus(GermanBurstButton.gameObject)) target = GermanBurstButton.gameObject;
            else if (driverSkillButton != null && IsAvailableFocus(driverSkillButton.gameObject)) target = driverSkillButton.gameObject;
            else if (gear1Button != null && IsAvailableFocus(gear1Button.gameObject)) target = gear1Button.gameObject;
            else if (gear2Button != null && IsAvailableFocus(gear2Button.gameObject)) target = gear2Button.gameObject;
            else if (gear3Button != null && IsAvailableFocus(gear3Button.gameObject)) target = gear3Button.gameObject;
            else if (gear4Button != null && IsAvailableFocus(gear4Button.gameObject)) target = gear4Button.gameObject;
            else if (confirmGearButton != null && IsAvailableFocus(confirmGearButton.gameObject)) target = confirmGearButton.gameObject;
            else if (gameManager != null && gameManager.cardHandUI != null && gameManager.cardHandUI.playCardsButton != null &&
                IsAvailableFocus(gameManager.cardHandUI.playCardsButton.gameObject)) target = gameManager.cardHandUI.playCardsButton.gameObject;
        }
        if (target != null)
            focus.EventSystem.SetSelectedGameObject(target);
        else
        {
            GameObject selected = focus.EventSystem.currentSelectedGameObject;
            if (selected != null && ((focus.Row != null && selected.transform.IsChildOf(focus.Row.transform)) ||
                (returnToMenuConfirmPanel != null && selected.transform.IsChildOf(returnToMenuConfirmPanel.transform))))
                focus.EventSystem.SetSelectedGameObject(null);
        }
    }

    /// <summary>
    /// Requests a configurable in-race action confirmation. When the player
    /// disabled the selected gate, the callback executes immediately.
    /// </summary>
    public void RequestInRaceAction(
        InRaceConfirmationAction action,
        string title,
        string message,
        UnityEngine.Events.UnityAction callback)
    {
        if (callback == null || IsTelemetryDetailsOpen)
            return;

        if (driverSkillCardChoicePending) DismissConfirmation(false, true);

        if (!InRaceConfirmationRules.IsEnabled(
                GameSettingsRuntime.Current.inRaceConfirmationMask,
                action))
        {
            callback.Invoke();
            return;
        }

        EnsureReturnToMenuConfirmationUI();
        if (returnToMenuConfirmPanel == null)
        {
            SetStatus("无法打开操作确认，请继续比赛或重试。");
            return;
        }

        pendingConfirmationAction = () => callback.Invoke();
        pendingConfirmationType = action;
        if (returnToMenuConfirmTitle != null)
            returnToMenuConfirmTitle.text = title;
        if (returnToMenuConfirmText != null)
            returnToMenuConfirmText.text = message;
        SetConfirmationButtonLabels(action);
        returnToMenuConfirmPanel.transform.SetAsLastSibling();
        returnToMenuConfirmPanel.SetActive(true);
        RefreshTeamGearPresentation();
    }

    private void SetConfirmationButtonLabels(InRaceConfirmationAction action)
    {
        SetConfirmationButtonLabel(cancelReturnToMenuButton,
            InRaceConfirmationRules.GetCancelButtonLabel(action));
        SetConfirmationButtonLabel(confirmReturnToMenuButton,
            InRaceConfirmationRules.GetConfirmButtonLabel(action));
    }

    private static void SetConfirmationButtonLabel(Button button, string text)
    {
        if (button == null)
            return;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = text;
    }

    /// <summary>Public adapter used by procedural/fallback gear buttons.</summary>
    public void RequestGearSelection(int gear)
    {
        if (gameManager == null || !gameManager.CanSelectGearForPresentation ||
            !teamGearControlsEnabled || IsReturnToMenuConfirmationVisible || IsTelemetryDetailsOpen ||
            (cornerLimitDetailsPanel != null && cornerLimitDetailsPanel.activeSelf))
            return;
        PlayerState player = gameManager.Player;
        if (player == null || gear < 1 || gear > (player.teamId == TeamId.CN ? 2 : 4) ||
            (player.teamId != TeamId.CN && Mathf.Abs(gear - player.gear) > 2))
            return;
        gameManager?.DismissTutorialInteractionCallout();
        string gearName = $"档位 {gear}";
        if (gameManager != null && gameManager.Player != null)
            gearName = TeamGearRules.GetDisplayName(gameManager.Player.teamId, gear);
        RequestInRaceAction(
            InRaceConfirmationAction.GearSelection,
            "确认换挡选择",
            $"确定选择 {gearName} 吗？",
            () =>
            {
                gameManager?.OnGearButtonClicked(gear);
                RefreshTeamGearPresentation();
            });
    }

    /// <summary>Keyboard adapter that preserves the configured gear-confirmation gate.</summary>
    public void TriggerConfirmGearShortcut()
    {
        if (confirmGearButton == null || !confirmGearButton.gameObject.activeInHierarchy ||
            !confirmGearButton.interactable)
            return;
        OnConfirmGearClicked();
    }

    /// <summary>
    /// Confirms only normal race actions from the keyboard. Destructive reset/menu
    /// prompts intentionally remain pointer-only so Space cannot dismiss them by accident.
    /// </summary>
    public bool TryConfirmKeyboardAction()
    {
        if (!IsReturnToMenuConfirmationVisible || !pendingConfirmationType.HasValue)
            return false;
        if (pendingConfirmationType == InRaceConfirmationAction.ReturnToMenu ||
            pendingConfirmationType == InRaceConfirmationAction.ResetRace)
            return false;

        ConfirmReturnToMenu();
        return true;
    }

    /// <summary>Shows a live, signed breakdown for one corner limit.</summary>
    public void ShowCornerLimitDetails(string cornerName, CornerLimitBreakdown breakdown)
    {
        EnsureCornerLimitDetailsUI();
        if (cornerLimitDetailsPanel == null)
            return;

        if (cornerLimitDetailsTitle != null)
            cornerLimitDetailsTitle.text = $"弯道限速 · {cornerName}";
        if (cornerLimitDetailsText != null)
        {
            cornerLimitDetailsText.text =
                $"赛道原始限速    {breakdown.BaseLimit}\n" +
                $"天气影响        {FormatSignedModifier(breakdown.WeatherModifier)}\n" +
                $"车手技能影响    {FormatSignedModifier(breakdown.DriverModifier)}\n" +
                $"车队操控影响    {FormatSignedModifier(breakdown.TeamModifier)}\n" +
                $"科技树影响      {FormatSignedModifier(breakdown.TechnologyModifier)}\n" +
                $"当前有效限速    {breakdown.EffectiveLimit}";
        }
        cornerLimitDetailsPanel.transform.SetAsLastSibling();
        cornerLimitDetailsPanel.SetActive(true);
        RefreshTeamGearPresentation();
    }

    public void HideCornerLimitDetails()
    {
        if (cornerLimitDetailsPanel != null)
            cornerLimitDetailsPanel.SetActive(false);
        RefreshTeamGearPresentation();
    }

    private static string FormatSignedModifier(int modifier)
    {
        return modifier > 0 ? $"+{modifier}" : modifier.ToString();
    }

    private void EnsureCornerLimitDetailsUI()
    {
        Transform canvasRoot = transform.root != null ? transform.root : transform;
        if (cornerLimitDetailsPanel == null)
            cornerLimitDetailsPanel = FindObjectByName(canvasRoot, "CornerLimitDetailsPanel")?.gameObject;

        if (cornerLimitDetailsPanel != null)
        {
            if (cornerLimitDetailsTitle == null)
                cornerLimitDetailsTitle = FindComponentByName<TMP_Text>(cornerLimitDetailsPanel.transform,
                    "CornerLimitDetailsTitle");
            if (cornerLimitDetailsText == null)
                cornerLimitDetailsText = FindComponentByName<TMP_Text>(cornerLimitDetailsPanel.transform,
                    "CornerLimitDetailsText");
            if (closeCornerLimitDetailsButton == null)
                closeCornerLimitDetailsButton = FindComponentByName<Button>(cornerLimitDetailsPanel.transform,
                    "CloseCornerLimitDetailsButton");
        }

        if (cornerLimitDetailsPanel == null)
            BuildCornerLimitDetailsUI(canvasRoot);
        if (cornerLimitDetailsPanel != null && closeCornerLimitDetailsButton != null)
        {
            closeCornerLimitDetailsButton.onClick.RemoveListener(HideCornerLimitDetails);
            closeCornerLimitDetailsButton.onClick.AddListener(HideCornerLimitDetails);
        }
    }

    private void BuildCornerLimitDetailsUI(Transform canvasRoot)
    {
        if (canvasRoot == null)
            return;

        GameObject overlay = new GameObject("CornerLimitDetailsPanel", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvasRoot, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0.005f, 0.012f, 0.025f, 0.74f);
        overlayImage.raycastTarget = true;

        GameObject card = new GameObject("CornerLimitDetailsCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(overlay.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(620f, 390f);
        ModernUIStyle.ApplyPanel(card, true);

        TMP_FontAsset font = FindReferenceFont(canvasRoot);
        cornerLimitDetailsTitle = CreateConfirmationText(card.transform, "CornerLimitDetailsTitle",
            "弯道限速", font, 28, TextAlignmentOptions.Center,
            new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.93f), ModernUIStyle.TextPrimary);
        cornerLimitDetailsText = CreateConfirmationText(card.transform, "CornerLimitDetailsText",
            "", font, 19, TextAlignmentOptions.Left,
            new Vector2(0.13f, 0.22f), new Vector2(0.87f, 0.75f), ModernUIStyle.TextSecondary);
        closeCornerLimitDetailsButton = CreateConfirmationButton(card.transform,
            "CloseCornerLimitDetailsButton", "关闭", new Vector2(0f, -132f),
            ModernUIStyle.AccentBlue, true, font);
        closeCornerLimitDetailsButton.onClick.AddListener(HideCornerLimitDetails);
        overlay.SetActive(false);
        cornerLimitDetailsPanel = overlay;
    }

    private void BuildReturnToMenuConfirmationUI(Transform canvasRoot)
    {
        if (canvasRoot == null)
            return;

        GameObject overlay = new GameObject("ReturnToMenuConfirmPanel", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvasRoot, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        ModernUIStyle.ApplyOverlay(overlay.GetComponent<Image>());

        GameObject card = new GameObject("ReturnToMenuConfirmCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(overlay.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(620f, 300f);
        ModernUIStyle.ApplyPanel(card, true);

        TMP_FontAsset font = FindReferenceFont(canvasRoot);
        returnToMenuConfirmTitle = CreateConfirmationText(card.transform, "ReturnToMenuConfirmTitle", "返回主菜单", font, 28,
            TextAlignmentOptions.Center, new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.88f),
            ModernUIStyle.TextPrimary);
        returnToMenuConfirmText = CreateConfirmationText(card.transform, "ReturnToMenuConfirmText",
            "确定要退出当前比赛并返回主菜单吗？\n当前比赛进度不会保存。", font, 19,
            TextAlignmentOptions.Center, new Vector2(0.10f, 0.38f), new Vector2(0.90f, 0.68f),
            ModernUIStyle.TextSecondary);

        cancelReturnToMenuButton = CreateConfirmationButton(card.transform, "CancelReturnToMenuButton",
            "留在比赛", new Vector2(-112f, -82f), ModernUIStyle.AccentBlue, false, font);
        confirmReturnToMenuButton = CreateConfirmationButton(card.transform, "ConfirmReturnToMenuButton",
            "返回主菜单", new Vector2(112f, -82f), ModernUIStyle.AccentRed, true, font);

        returnToMenuConfirmPanel = overlay;
    }

    private static TMP_Text CreateConfirmationText(Transform parent, string objectName, string text,
        TMP_FontAsset font, float fontSize, TextAlignmentOptions alignment, Vector2 anchorMin,
        Vector2 anchorMax, Color color)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.enableWordWrapping = true;
        label.color = color;
        if (font != null)
            label.font = font;
        ModernUIStyle.ApplyText(label);
        label.color = color;
        return label;
    }

    private static Button CreateConfirmationButton(Transform parent, string objectName, string text,
        Vector2 anchoredPosition, Color accent, bool primary, TMP_FontAsset font)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(190f, 52f);

        Button button = buttonObject.GetComponent<Button>();
        ModernUIStyle.ApplyButton(button, accent, primary);
        TMP_Text label = CreateConfirmationText(buttonObject.transform, "Label", text, font, 20,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, ModernUIStyle.TextPrimary);
        label.fontStyle = FontStyles.Bold;
        return button;
    }

    private static TMP_FontAsset FindReferenceFont(Transform root)
    {
        TMP_Text reference = root != null ? root.GetComponentInChildren<TMP_Text>(true) : null;
        return reference != null && reference.font != null ? reference.font : TMP_Settings.defaultFontAsset;
    }

    private static Transform FindObjectByName(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (root.name == objectName)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindObjectByName(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }
        return null;
    }

    private static T FindComponentByName<T>(Transform root, string objectName) where T : Component
    {
        Transform found = FindObjectByName(root, objectName);
        return found != null ? found.GetComponent<T>() : null;
    }

    public void ConfigureGearPresentation(bool chinaMode, int currentGear)
    {
        ConfigureGearPresentation(chinaMode ? TeamId.CN : TeamId.UK, currentGear);
    }

    public void ConfigureGearPresentation(TeamId team, int currentGear)
    {
        if (telemetryDetailsPanel != null) telemetryDetailsPanel.SetActive(false);
        EnsurePresentation();
        teamGearPresentation?.Configure(team, currentGear);
        if (gameManager != null && gameManager.Player != null) RefreshTurnTelemetry(gameManager.Player);
        RefreshTeamGearPresentation();
    }

    public void SetTeamGearControlsInteractable(bool interactable)
    {
        teamGearControlsEnabled = interactable;
        RefreshTeamGearPresentation();
    }

    /// <summary>Reads pending and actual state separately; confirmation never fakes a successful shift.</summary>
    public void RefreshTeamGearPresentation()
    {
        EnsurePresentation();
        RefreshUkPrepMeal(gameManager, gameManager != null ? gameManager.Player : null);
        RefreshGermanFuel(gameManager, gameManager != null ? gameManager.Player : null);
        if (gameManager == null || gameManager.Player == null || teamGearPresentation == null)
            return;
        bool modal = IsReturnToMenuConfirmationVisible || IsTelemetryDetailsOpen ||
            (cornerLimitDetailsPanel != null && cornerLimitDetailsPanel.activeSelf);
        teamGearPresentation.Refresh(gameManager.Player, gameManager.PendingGearForPresentation,
            teamGearControlsEnabled && gameManager.CanSelectGearForPresentation && !modal,
            gameManager.TutorialInputPhase != "gear", gameManager.Config,
            gameManager.CurrentPhase == GamePhase.WaitingForGear);
    }

    public void SetEnginePresentation(float intensity)
    {
        // Audio cleanup may arrive after the HUD or its visual children are destroyed.
        // Startup owns creation; an intensity update must never rebuild UI during teardown.
        if (this == null || teamGearPresentation == null) return;
        teamGearPresentation.SetEngineIntensity(intensity);
    }

    public void RefreshTurnTelemetry(PlayerState player) => RefreshTurnTelemetryInternal(player, null);

    public void RefreshGearTelemetryPreview(PlayerState player, int gear) => RefreshTurnTelemetryInternal(player, gear);

    private void RefreshTurnTelemetryInternal(PlayerState player, int? gearOverride)
    {
        if (this == null || player == null) return;
        EnsurePresentation();
        if (telemetryPlayer != null && telemetryPlayer != player && telemetryDetailsPanel != null)
            telemetryDetailsPanel.SetActive(false);
        telemetryPlayer = player;
        RaceTelemetryStripUI strip = teamGearPresentation != null ? teamGearPresentation.Telemetry : null;
        if (strip == null) return;
        RaceTurnPreview preview = gameManager != null ? gameManager.BuildTurnTelemetryPreview(player, gearOverride) : null;
        HeatGaugeState realHeat = HeatGaugeRules.Evaluate(player.deck, player.playedSpeedCardsThisTurn);
        strip.Refresh(player.turnMovement, realHeat,
            CardPlayRules.SumCommittedSpeedCardValues(player.playedSpeedCardsThisTurn), preview);
        if (heatThermometer != null) heatThermometer.RefreshSnapshot(realHeat, preview?.ProjectedHeat);
        if (IsTelemetryDetailsOpen && telemetryDetailsText != null)
            telemetryDetailsText.text = strip.FormatDetails();
    }

    // Check geometry before EventSystem dispatch: the opening click cannot skip movement.
    public bool IsPointerOverTelemetry(Vector2 screenPosition)
    {
        if (IsTelemetryDetailsOpen) return true;
        Button target = teamGearPresentation != null && teamGearPresentation.Telemetry != null
            ? teamGearPresentation.Telemetry.MovementButton : null;
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        Canvas canvas = target.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(target.transform as RectTransform, screenPosition, camera);
    }

    public void ShowTelemetryDetails()
    {
        if ((gameManager != null && gameManager.CurrentPhase == GamePhase.GameOver) || IsReturnToMenuConfirmationVisible ||
            (cornerLimitDetailsPanel != null && cornerLimitDetailsPanel.activeSelf)) return;
        EnsureTelemetryDetails();
        PlayerState player = gameManager != null ? gameManager.Player : telemetryPlayer;
        if (player != null) RefreshTurnTelemetry(player);
        if (telemetryDetailsText != null && teamGearPresentation != null && teamGearPresentation.Telemetry != null)
            telemetryDetailsText.text = teamGearPresentation.Telemetry.FormatDetails();
        telemetryDetailsPanel.transform.SetAsLastSibling();
        telemetryDetailsPanel.SetActive(true);
        RefreshTeamGearPresentation();
    }

    public void HideTelemetryDetails()
    {
        if (telemetryDetailsPanel != null) telemetryDetailsPanel.SetActive(false);
        RefreshTeamGearPresentation();
    }

    private void EnsureTelemetryDetails()
    {
        if (telemetryDetailsPanel != null) return;
        TMP_FontAsset font = FindReferenceFont(transform.root);
        telemetryDetailsPanel = new GameObject("TurnTelemetryDetails", typeof(RectTransform), typeof(Image));
        telemetryDetailsPanel.transform.SetParent(transform.root, false);
        RectTransform overlay = telemetryDetailsPanel.GetComponent<RectTransform>();
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        Image overlayImage = telemetryDetailsPanel.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.82f);
        overlayImage.raycastTarget = true;

        GameObject card = new GameObject("DetailsCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(overlay, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.25f, 0.15f); cardRect.anchorMax = new Vector2(0.75f, 0.85f);
        cardRect.offsetMin = cardRect.offsetMax = Vector2.zero;
        card.GetComponent<Image>().color = ModernUIStyle.Panel;
        TMP_Text title = CreateConfirmationText(card.transform, "Title", "本回合位移来源", font, 24,
            TextAlignmentOptions.Center, new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f), ModernUIStyle.TextPrimary);
        title.raycastTarget = false;

        GameObject viewport = new GameObject("DetailsViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.transform.SetParent(card.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.06f, 0.17f); viewportRect.anchorMax = new Vector2(0.94f, 0.87f);
        viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        telemetryDetailsText = CreateConfirmationText(viewport.transform, "DetailsText", "", font, 18,
            TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), Vector2.one, ModernUIStyle.TextPrimary);
        telemetryDetailsText.raycastTarget = false;
        RectTransform content = telemetryDetailsText.rectTransform;
        content.pivot = new Vector2(0.5f, 1f);
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        closeTelemetryDetailsButton = CreateConfirmationButton(card.transform, "CloseTelemetryDetailsButton", "关闭详情",
            Vector2.zero, ModernUIStyle.AccentBlue, true, font);
        RectTransform closeRect = closeTelemetryDetailsButton.transform as RectTransform;
        closeRect.anchorMin = new Vector2(0.35f, 0.035f); closeRect.anchorMax = new Vector2(0.65f, 0.13f);
        closeRect.offsetMin = closeRect.offsetMax = Vector2.zero;
        closeTelemetryDetailsButton.onClick.AddListener(HideTelemetryDetails);
        telemetryDetailsPanel.SetActive(false);
    }

    private void OnDisable()
    {
        if (ukPrepMealAction != null) ukPrepMealAction.SuspendInteraction();
        if (germanFuelAction != null) germanFuelAction.SuspendInteraction();
        if (germanBurstAction != null) germanBurstAction.SuspendInteraction();
        CancelDriverSkillChoiceForLifecycle();
        if (telemetryDetailsPanel != null) telemetryDetailsPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (germanBurstAction != null) germanBurstAction.RestoreSlot();
        CancelDriverSkillChoiceForLifecycle();
        if (telemetryDetailsPanel == null || telemetryDetailsPanel.transform.IsChildOf(transform)) return;
        // A HUD nested below its canvas owns a sibling overlay; clean that up too.
        if (Application.isPlaying) Destroy(telemetryDetailsPanel);
        else DestroyImmediate(telemetryDetailsPanel);
    }

    public void SelectGearPresentation(int gear)
    {
        RefreshTeamGearPresentation();
    }

    public void RefreshDriverSkill(MVPGameManager gm, PlayerState player)
    {
        RefreshUkPrepMeal(gm, player);
        RefreshGermanFuel(gm, player);
        if (driverSkillButton == null) return;
        bool interactable = false;
        string label = gm != null ? gm.GetDriverSkillButtonLabel(player, out interactable) : "车手技能";
        driverSkillButton.interactable = interactable;
        TMP_Text target = driverSkillLabel != null
            ? driverSkillLabel
            : driverSkillButton.GetComponentInChildren<TMP_Text>(true);
        if (target != null) target.text = label;
    }

    /// <summary>Displays manager-owned availability; A4 technology and A1 driver share space only.</summary>
    public void RefreshUkPrepMeal(MVPGameManager gm, PlayerState player)
    {
        germanBurstAction?.RestoreSlot();
        bool visible = gm != null && gm.IsUkPrepMealVisible(player);
        if (ukPrepMealAction == null && !visible) { RefreshGermanBurst(gm, player); return; }
        if (ukPrepMealAction == null) ukPrepMealAction = UkPrepMealActionUI.Attach(this, OnUkPrepMealClicked);
        if (ukPrepMealAction == null) { RefreshGermanBurst(gm, player); return; }
        bool interactable = false;
        string label = gm != null ? gm.GetUkPrepMealButtonLabel(player, out interactable) : "科技·预备餐";
        bool modal = IsReturnToMenuConfirmationVisible || IsTelemetryDetailsOpen ||
            (cornerLimitDetailsPanel != null && cornerLimitDetailsPanel.activeSelf);
        ukPrepMealAction.Refresh(visible, interactable && !modal && isActiveAndEnabled, label);
        RefreshGermanBurst(gm, player);
    }

    /// <summary>Phase-exclusive technology presentation; resource authority stays in the manager plan.</summary>
    public void RefreshGermanFuel(MVPGameManager gm, PlayerState player)
    {
        germanBurstAction?.RestoreSlot();
        bool visible = gm != null && gm.IsGermanFuelVisible(player);
        if (germanFuelAction == null && !visible) { RefreshGermanBurst(gm, player); return; }
        if (germanFuelAction == null) germanFuelAction = GermanFuelActionUI.Attach(this, OnGermanFuelClicked);
        if (germanFuelAction == null) { RefreshGermanBurst(gm, player); return; }
        bool interactable = false;
        string label = gm != null ? gm.GetGermanFuelButtonLabel(player, out interactable) : "科技·黑啤燃料";
        bool modal = IsReturnToMenuConfirmationVisible || IsTelemetryDetailsOpen ||
            (cornerLimitDetailsPanel != null && cornerLimitDetailsPanel.activeSelf);
        germanFuelAction.Refresh(visible, interactable && !modal && isActiveAndEnabled, label);
        RefreshGermanBurst(gm, player);
    }

    /// <summary>Displays the manager-owned DE declaration beside the current driver or technology action.</summary>
    public void RefreshGermanBurst(MVPGameManager gm, PlayerState player)
    {
        bool visible = gm != null && gm.IsGermanBurstVisible(player);
        if (germanBurstAction == null && !visible) return;
        if (germanBurstAction == null) germanBurstAction = GermanBurstActionUI.Attach(this, OnGermanBurstClicked);
        if (germanBurstAction == null) return;
        bool interactable = false;
        string label = gm != null ? gm.GetGermanBurstButtonLabel(player, out interactable) : "科技·烤肉拼盘";
        bool modal = IsReturnToMenuConfirmationVisible || IsTelemetryDetailsOpen ||
            (cornerLimitDetailsPanel != null && cornerLimitDetailsPanel.activeSelf);
        Button source = GermanFuelButton != null && GermanFuelButton.gameObject.activeSelf ? GermanFuelButton :
            UkPrepMealButton != null && UkPrepMealButton.gameObject.activeSelf ? UkPrepMealButton : driverSkillButton;
        germanBurstAction.Refresh(source, visible, interactable && !modal && isActiveAndEnabled, label);
    }

    private void OnGermanBurstClicked()
    {
        if (!isActiveAndEnabled || gameManager == null || GermanBurstButton == null ||
            !GermanBurstButton.isActiveAndEnabled || !GermanBurstButton.IsInteractable() ||
            IsReturnToMenuConfirmationVisible || IsTelemetryDetailsOpen ||
            (cornerLimitDetailsPanel != null && cornerLimitDetailsPanel.activeSelf)) return;
        gameManager.OnGermanBurstButtonClicked();
    }

    private void OnGermanFuelClicked()
    {
        if (!isActiveAndEnabled || gameManager == null || GermanFuelButton == null ||
            !GermanFuelButton.isActiveAndEnabled || !GermanFuelButton.IsInteractable() ||
            IsReturnToMenuConfirmationVisible || IsTelemetryDetailsOpen ||
            (cornerLimitDetailsPanel != null && cornerLimitDetailsPanel.activeSelf)) return;
        gameManager.OnGermanFuelButtonClicked();
    }

    /// <summary>Optional fixed technology fee uses the existing card-action confirmation, cancel and focus surface.</summary>
    public void ShowGermanFuelConfirmation(string title, string message,
        UnityEngine.Events.UnityAction confirm, UnityEngine.Events.UnityAction cancel)
    {
        if (confirm == null || IsTelemetryDetailsOpen) { cancel?.Invoke(); return; }
        if (InRaceConfirmationRules.IsEnabled(GameSettingsRuntime.Current.inRaceConfirmationMask, InRaceConfirmationAction.CardAction))
        {
            ShowRequiredCardChoice(title, message, Array.Empty<CardData>(), _ => confirm.Invoke(), cancel,
                InRaceConfirmationAction.CardAction, "支付1热 · 移动+2", null, false);
            driverSkillCardChoice?.SetNoCardDescription("无需弃置速度牌\n仅支付1张永久热");
            return;
        }
        DismissConfirmation(false, true);
        EventSystem events = EventSystem.current;
        var focus = new DriverSkillChoiceFocus(events, events != null ? events.currentSelectedGameObject : null, null, null);
        try { confirm.Invoke(); }
        finally { RefreshTeamGearPresentation(); RestoreDriverSkillChoiceFocus(focus); }
    }

    private void OnUkPrepMealClicked()
    {
        if (!isActiveAndEnabled || gameManager == null || UkPrepMealButton == null ||
            !UkPrepMealButton.isActiveAndEnabled || !UkPrepMealButton.IsInteractable() ||
            IsReturnToMenuConfirmationVisible || IsTelemetryDetailsOpen)
            return;
        gameManager.OnUkPrepMealButtonClicked();
    }

    public void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
    }

    // ====== 日志 ======

    public void AppendLog(string msg)
    {
        logSink?.Invoke(msg);
        logBuffer = msg + "\n" + logBuffer;

        // 限制行数
        string[] lines = logBuffer.Split('\n');
        if (lines.Length > maxLogLines)
        {
            logBuffer = string.Join("\n", lines, 0, maxLogLines);
        }

        if (logText != null)
        {
            logText.text = logBuffer;
            RefreshLogScrollWindow();
        }
    }

    /// <summary>Connects the fixed race-log viewport created by the layout controller.</summary>
    public void AttachLogScrollRect(ScrollRect scrollRect)
    {
        logScrollRect = scrollRect;
        RefreshLogScrollWindow();
    }

    private void RefreshLogScrollWindow()
    {
        if (logText == null || logScrollRect == null || logScrollRect.viewport == null)
            return;

        Canvas.ForceUpdateCanvases();
        RectTransform content = logText.rectTransform;
        float height = Mathf.Max(logScrollRect.viewport.rect.height, logText.preferredHeight + 8f);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        logScrollRect.verticalNormalizedPosition = 1f;
    }

    // ====== 游戏结束 ======

    /// <summary>Hide the result without clearing data or rebuilding cached presentation controls.</summary>
    public void HideGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void ShowGameOver(string result)
    {
        if (telemetryDetailsPanel != null) telemetryDetailsPanel.SetActive(false);
        EnsureGameOverPresentation();
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
        }

        if (gameOverText != null)
            gameOverText.text = RaceResultPresentationRules.FormatForRichText(result);

        RefreshGameOverScrollWindow();
    }

    /// <summary>
    /// Upgrades authored and fallback result panels to the same fixed-size,
    /// masked scroll presentation without changing race-result data.
    /// </summary>
    public void EnsureGameOverPresentation()
    {
        if (gameOverPanel == null || gameOverText == null)
            return;

        RectTransform panelRect = gameOverPanel.GetComponent<RectTransform>();
        if (panelRect == null)
            return;

        Image panelImage = gameOverPanel.GetComponent<Image>();
        if (panelImage == null)
            panelImage = gameOverPanel.AddComponent<Image>();
        ModernUIStyle.ApplyPanel(gameOverPanel, true);
        panelImage.raycastTarget = true;

        TMP_FontAsset font = gameOverText.font != null
            ? gameOverText.font
            : FindReferenceFont(gameOverPanel.transform);

        if (gameOverTitle == null)
            gameOverTitle = FindComponentByName<TMP_Text>(gameOverPanel.transform, "GameOverTitle");
        if (gameOverTitle == null)
        {
            gameOverTitle = CreateConfirmationText(gameOverPanel.transform, "GameOverTitle",
                "赛事结算", font, 30, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.85f), new Vector2(0.92f, 0.97f),
                new Color(1f, 0.78f, 0.34f, 1f));
            gameOverTitle.fontStyle = FontStyles.Bold;
        }

        if (gameOverScrollRect == null)
            gameOverScrollRect = FindComponentByName<ScrollRect>(gameOverPanel.transform,
                "GameOverScrollView");
        if (gameOverScrollRect == null)
            BuildGameOverScrollView(font);

        if (backToMenuButton != null)
        {
            RectTransform buttonRect = backToMenuButton.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                buttonRect.SetParent(gameOverPanel.transform, false);
                buttonRect.anchorMin = new Vector2(0.36f, 0.035f);
                buttonRect.anchorMax = new Vector2(0.64f, 0.15f);
                buttonRect.offsetMin = Vector2.zero;
                buttonRect.offsetMax = Vector2.zero;
            }
            ModernUIStyle.ApplyButton(backToMenuButton, ModernUIStyle.AccentBlue, true);
        }
    }

    private void BuildGameOverScrollView(TMP_FontAsset font)
    {
        GameObject scrollObject = new GameObject("GameOverScrollView", typeof(RectTransform), typeof(ScrollRect));
        scrollObject.transform.SetParent(gameOverPanel.transform, false);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0.07f, 0.19f);
        scrollRectTransform.anchorMax = new Vector2(0.93f, 0.83f);
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = Vector2.zero;

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = new Vector2(0.965f, 1f);
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.018f, 0.028f, 0.05f, 0.76f);
        viewportImage.raycastTarget = true;

        RectTransform content = gameOverText.rectTransform;
        content.SetParent(viewportRect, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.offsetMin = new Vector2(18f, content.offsetMin.y);
        content.offsetMax = new Vector2(-18f, content.offsetMax.y);
        gameOverText.font = font;
        gameOverText.fontSize = 20f;
        gameOverText.fontStyle = FontStyles.Normal;
        gameOverText.alignment = TextAlignmentOptions.TopLeft;
        gameOverText.enableWordWrapping = true;
        gameOverText.overflowMode = TextOverflowModes.Overflow;
        gameOverText.raycastTarget = false;
        gameOverText.color = ModernUIStyle.TextPrimary;

        Scrollbar scrollbar = BuildGameOverScrollbar(scrollObject.transform);
        gameOverScrollRect = scrollObject.GetComponent<ScrollRect>();
        gameOverScrollRect.content = content;
        gameOverScrollRect.viewport = viewportRect;
        gameOverScrollRect.horizontal = false;
        gameOverScrollRect.vertical = true;
        gameOverScrollRect.movementType = ScrollRect.MovementType.Clamped;
        gameOverScrollRect.scrollSensitivity = 28f;
        gameOverScrollRect.verticalScrollbar = scrollbar;
        gameOverScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        gameOverScrollRect.verticalNormalizedPosition = 1f;
    }

    private static Scrollbar BuildGameOverScrollbar(Transform parent)
    {
        GameObject barObject = new GameObject("Scrollbar Vertical", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barObject.transform.SetParent(parent, false);
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.975f, 0f);
        barRect.anchorMax = Vector2.one;
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;
        Image barImage = barObject.GetComponent<Image>();
        barImage.color = new Color(0.16f, 0.22f, 0.32f, 0.65f);

        GameObject slidingObject = new GameObject("Sliding Area", typeof(RectTransform));
        slidingObject.transform.SetParent(barObject.transform, false);
        RectTransform slidingRect = slidingObject.GetComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.offsetMin = new Vector2(2f, 2f);
        slidingRect.offsetMax = new Vector2(-2f, -2f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(slidingObject.transform, false);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(0.25f, 0.72f, 1f, 0.92f);

        Scrollbar scrollbar = barObject.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }

    private void RefreshGameOverScrollWindow()
    {
        if (gameOverText == null || gameOverScrollRect == null || gameOverScrollRect.viewport == null)
            return;

        Canvas.ForceUpdateCanvases();
        RectTransform content = gameOverText.rectTransform;
        float viewportHeight = gameOverScrollRect.viewport.rect.height;
        float contentHeight = Mathf.Max(viewportHeight, gameOverText.preferredHeight + 28f);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        Canvas.ForceUpdateCanvases();
        gameOverScrollRect.verticalNormalizedPosition = 1f;
    }

    // ====== 按钮回调 ======

    private void OnGearClicked(int gear)
    {
        RequestGearSelection(gear);
    }

    private void OnConfirmGearClicked()
    {
        if (gameManager == null || !gameManager.CanSelectGearForPresentation ||
            !teamGearControlsEnabled || IsReturnToMenuConfirmationVisible || IsTelemetryDetailsOpen ||
            (cornerLimitDetailsPanel != null && cornerLimitDetailsPanel.activeSelf))
            return;
        gameManager?.DismissTutorialInteractionCallout();
        RequestInRaceAction(
            InRaceConfirmationAction.GearCommit,
            "确认锁定档位",
            "确定锁定当前档位并进入出牌阶段吗？",
            () =>
            {
                gameManager?.OnConfirmGearClicked();
                RefreshTeamGearPresentation();
            });
    }

    private void OnDriverSkillClicked()
    {
        if (gameManager != null &&
            (gameManager.TryRequestHunterSkillSelection() || gameManager.TryRequestMansellChargeSelection())) return;
        RequestInRaceAction(
            InRaceConfirmationAction.DriverSkill,
            "确认发动技能",
            "车手技能发动后会消耗本场次数，确定继续吗？",
            () => gameManager?.OnDriverSkillButtonClicked());
    }

    private void OnResetClicked()
    {
        RequestInRaceAction(
            InRaceConfirmationAction.ResetRace,
            "确认重新开始",
            "当前比赛进度会丢失，确定重新开始吗？",
            () =>
            {
                gameManager?.ResetGame();
                HideGameOver();
            });
    }

    private void OnBackToMenuClicked()
    {
        if (!IsReturnToMenuConfirmationVisible)
            ShowReturnToMenuConfirmation();
    }
}
