using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

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
    public int maxLogLines = 6;

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

    private MVPGameManager gameManager;
    private string logBuffer = "";
    private Action<string> logSink;
    private Action pendingConfirmationAction;
    private TMP_Text returnToMenuConfirmTitle;
    [SerializeField] private HeatThermometerUI heatThermometer;
    [SerializeField] private GearDialPresentationUI gearDialPresentation;

    public HeatThermometerUI HeatThermometer => heatThermometer;
    public GearDialPresentationUI GearDialPresentation => gearDialPresentation;
    public bool IsReturnToMenuConfirmationVisible =>
        returnToMenuConfirmPanel != null && returnToMenuConfirmPanel.activeSelf;

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

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

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
        gameManager = gm;
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
        gameManager = gm;

        RefreshPlayerResources(player);
        RefreshDriverSkill(gm, player);

        if (lapText != null)
            lapText.text = $"圈数: {player.lap}/{gm.Config.totalLaps}";

        if (positionText != null)
        {
            if (allPlayers != null && allPlayers.Count > 0)
            {
                int rank = RaceRanking.GetCurrentRank(player, new System.Collections.Generic.List<PlayerState>(allPlayers));
                positionText.text = $"{TrackPresentationRules.FormatCellPosition(player.position, gm.Track.TotalNodes)} | 排名: {rank}/{allPlayers.Count}";
            }
            else
            {
                positionText.text = TrackPresentationRules.FormatCellPosition(player.position, gm.Track.TotalNodes);
            }
        }

        if (aiStatusText != null && ai != null)
        {
            aiStatusText.text = ai.isBlown
                ? "<color=red>AI: 爆缸!</color>"
                : ai.hasFinished
                    ? "<color=green>AI: 完赛!</color>"
                    : $"AI: {TeamGearRules.GetDisplayName(ai.teamId, ai.gear)} | 引擎:{ai.deck.heatPool.remaining} | 圈{ai.lap} | 格{TrackPresentationRules.WrapNodeIndex(ai.position, gm.Track.TotalNodes) + 1}";
        }

        // 天气显示
        if (weatherText != null)
            weatherText.text = gm.WeatherLabel;

        // Corner numbers are live values, not authored constants. Refreshing
        // them with the same HUD pass keeps weather/driver/technology changes
        // immediately visible without touching the track geometry.
        gm.Track?.RefreshCornerLimitLabels();

        // 多车排行榜
        if (standingsText != null && allPlayers != null && allPlayers.Count > 1)
            standingsText.text = FormatStandings(allPlayers, player);
    }

    /// <summary>Immediately refreshes the local player's gear and heat resources.</summary>
    public void RefreshPlayerResources(PlayerState player)
    {
        if (player == null || player.deck == null) return;

        // Resolve the authored thermometer even when an older RaceCanvas
        // instance lost the private serialized field during prefab rebuild.
        // The gauge is a live view of the player's deck, not a static label.
        EnsurePresentation();
        HeatGaugeState gauge = HeatGaugeRules.Evaluate(player.deck);

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

        heatThermometer?.Refresh(player.deck);
    }

    /// <summary>Builds the authored visual wrappers without changing button ownership.</summary>
    public void EnsurePresentation()
    {
        if (heatThermometer == null)
            heatThermometer = GetComponentInChildren<HeatThermometerUI>(true);
        if (heatText != null && heatThermometer == null)
            heatThermometer = HeatThermometerUI.Attach(heatText);

        Transform gearContainer = gear1Button != null ? gear1Button.transform.parent : null;
        if (gearContainer != null && gearDialPresentation == null)
        {
            gearDialPresentation = GearDialPresentationUI.Attach(
                gearContainer, gear1Button, gear2Button, gear3Button, gear4Button);
        }
    }

    /// <summary>
    /// Ensures that every RaceCanvas, including older authored prefabs, has a
    /// modal confirmation surface before leaving the active race. The panel is
    /// runtime UI so the existing authored layout is never overwritten.
    /// </summary>
    public void EnsureReturnToMenuConfirmationUI()
    {
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
            returnToMenuConfirmPanel.SetActive(false);
    }

    /// <summary>Opens the return confirmation without changing any race state.</summary>
    public void ShowReturnToMenuConfirmation()
    {
        RequestInRaceAction(
            InRaceConfirmationAction.ReturnToMenu,
            "返回主菜单",
            "确定要退出当前比赛并返回主菜单吗？\n当前比赛进度不会保存。",
            () => SceneLoader.LoadMainMenu());
    }

    /// <summary>Dismisses the modal and leaves the race untouched.</summary>
    public void CancelReturnToMenu()
    {
        pendingConfirmationAction = null;
        if (returnToMenuConfirmPanel != null)
            returnToMenuConfirmPanel.SetActive(false);
    }

    /// <summary>Confirms the destructive navigation action.</summary>
    public void ConfirmReturnToMenu()
    {
        if (!IsReturnToMenuConfirmationVisible)
            return;

        Action action = pendingConfirmationAction;
        pendingConfirmationAction = null;
        returnToMenuConfirmPanel.SetActive(false);
        if (action != null)
            action.Invoke();
        else
            SceneLoader.LoadMainMenu();
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
        if (callback == null)
            return;

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
        if (returnToMenuConfirmTitle != null)
            returnToMenuConfirmTitle.text = title;
        if (returnToMenuConfirmText != null)
            returnToMenuConfirmText.text = message;
        SetConfirmationButtonLabels(action);
        returnToMenuConfirmPanel.transform.SetAsLastSibling();
        returnToMenuConfirmPanel.SetActive(true);
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
        string gearName = $"档位 {gear}";
        if (gameManager != null && gameManager.Player != null)
            gearName = TeamGearRules.GetDisplayName(gameManager.Player.teamId, gear);
        RequestInRaceAction(
            InRaceConfirmationAction.GearSelection,
            "确认换挡选择",
            $"确定选择 {gearName} 吗？",
            () => gameManager?.OnGearButtonClicked(gear));
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
    }

    public void HideCornerLimitDetails()
    {
        if (cornerLimitDetailsPanel != null)
            cornerLimitDetailsPanel.SetActive(false);
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
        EnsurePresentation();
        gearDialPresentation?.Configure(chinaMode, currentGear);
    }

    public void SelectGearPresentation(int gear)
    {
        EnsurePresentation();
        gearDialPresentation?.SetSelectedGear(gear);
    }

    public void RefreshDriverSkill(MVPGameManager gm, PlayerState player)
    {
        if (driverSkillButton == null) return;
        bool interactable = false;
        string label = gm != null ? gm.GetDriverSkillButtonLabel(player, out interactable) : "车手技能";
        driverSkillButton.interactable = interactable;
        TMP_Text target = driverSkillLabel != null
            ? driverSkillLabel
            : driverSkillButton.GetComponentInChildren<TMP_Text>(true);
        if (target != null) target.text = label;
    }

    /// <summary>生成多车排行榜文本（含自己的标记）。</summary>
    private string FormatStandings(System.Collections.Generic.IReadOnlyList<PlayerState> all, PlayerState self)
    {
        var rankings = RaceRanking.GetRankings(new System.Collections.Generic.List<PlayerState>(all));
        var sb = new System.Text.StringBuilder();
        foreach (var e in rankings)
        {
            string mark = e.player == self ? " ←你" : "";
            int totalNodes = gameManager != null && gameManager.Track != null
                ? gameManager.Track.TotalNodes
                : 0;
            string cell = totalNodes > 0
                ? (TrackPresentationRules.WrapNodeIndex(e.player.position, totalNodes) + 1).ToString()
                : "?";
            sb.AppendLine($"{e.rank}. {e.player.name} 圈{e.player.lap} 格{cell}{mark}");
        }
        return sb.ToString();
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
            logText.text = logBuffer;
    }

    // ====== 游戏结束 ======

    public void ShowGameOver(string result)
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverText != null)
            gameOverText.text = result;
    }

    // ====== 按钮回调 ======

    private void OnGearClicked(int gear)
    {
        RequestGearSelection(gear);
    }

    private void OnConfirmGearClicked()
    {
        RequestInRaceAction(
            InRaceConfirmationAction.GearCommit,
            "确认锁定档位",
            "确定锁定当前档位并进入出牌阶段吗？",
            () => gameManager?.OnConfirmGearClicked());
    }

    private void OnDriverSkillClicked()
    {
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
                if (gameOverPanel != null)
                    gameOverPanel.SetActive(false);
            });
    }

    private void OnBackToMenuClicked()
    {
        if (!IsReturnToMenuConfirmationVisible)
            ShowReturnToMenuConfirmation();
    }
}
