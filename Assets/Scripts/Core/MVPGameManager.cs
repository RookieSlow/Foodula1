using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 比赛主管理器 — 协程驱动的回合制 HEAT 核心循环。
/// 挂载到场景中的 GameManager GameObject 上。
///
/// 5 大核心系统接入（2026-08-03）：
/// - 多车：RaceSession.Players + RaceRanking 排名/回合顺序（末位先行）
/// - 天气：比赛开始抽取 + 每圈掷骰换天，五种赛道天气画像由 WeatherRules 统一处理
/// - 维修区：经过 pit_entry 时选择进站，冷却全部热量、停 1 回合
/// - 特技牌：4 张洗入普通牌库，每回合限 1，单张确认后即时结算；速度牌支持多选确认
/// - 科技树：demo 预算解锁 L1，修正手牌/热量池/弯速/失控阈值等
/// 所有规则计算均在纯函数层（RaceSession / RaceRules / *Rules），本类只做编排。
/// </summary>
public class MVPGameManager : MonoBehaviour
{
    [Header("配置")]
    public GameConfigSO config;

    [Header("引用")]
    public TrackManager trackManager;
    public AIController aiController;
    public CardHandUI cardHandUI;
    public HUDUI hudUI;

    [Header("卡牌精灵图")]
    [Tooltip("卡面底图、选中叠加、数字图标、热量图标。Inspector 中拖入。")]
    public Sprite speedBgSprite;
    public Sprite heatBgSprite;
    public Sprite selectedOverlaySprite;
    public Sprite[] numberSprites = new Sprite[4];
    public Sprite heatIconSprite;

    [Header("赛车 Prefab")]
    public GameObject carPrefab;

    [Header("赛车精灵图")]
    [Tooltip("6 辆赛车精灵，按车队索引: 0=UK, 1=DE, 2=IT, 3=US, 4=CN, 5=JP。留空则回退到颜色区分。")]
    public Sprite[] carSprites = new Sprite[TeamCarPresentationRules.TeamCount];

    [Header("比赛事件特效")]
    [Tooltip("超车慢放、失控旋转和爆缸提示的运行时表现组件。留空时自动创建。")]
    public RaceEventFX raceEventFX;

    [Header("UI Prefab (Demo模式)")]
    [Tooltip("拖入 RaceCanvas Prefab 以使用预制 UI；留空则回退到硬编码 MVP UI。")]
    public GameObject raceCanvasPrefab;
    [Tooltip("卡牌预制体引用，Prefab 模式下会自动传给 CardHandUI。")]
    public GameObject cardUIPrefab;

    // --- 运行时状态 ---
    private RaceSession session;
    private TutorialScenarioDefinition tutorialScenario;
    private CareerRaceLaunchRequest careerRaceLaunch;
    private FreeRaceRosterEntry[] freeRaceRoster;
    private bool careerResultRecorded;
    private string careerInitializationFailure;
    private TutorialRuntimeDirector tutorialDirector;
    private TutorialGuideUI tutorialGuideUI;
    private TutorialOpponentCue pendingTutorialOpponentCue;
    private TutorialWeatherCue pendingTutorialWeatherCue;
    private TutorialOpponentCue activeTutorialOpponentCue;
    private TutorialPlayerCheckpoint pendingTutorialPlayerCheckpoint;
    private bool pendingTutorialGuideRefreshAtTurnStart;
    private int pendingTutorialGuideEarliestTurn;
    private IReadOnlyList<TrackNode> tutorialPitRuleNodes;
    private bool initializeTutorialInPractice;
    private readonly RacePhaseState phaseState = new RacePhaseState();
    private RaceTestLogWriter raceLogWriter;
    private int raceTurnNumber;
    // Storage/display defaults stay at the coordinator boundary; startup/coroutine
    // regressions replace these dependencies without touching player profiles or resolution.
    private System.Action<TechTreeState> saveRaceTechState = TechTreeProfileStore.Save;
    private System.Action<string, int> saveRaceDriverXp = DriverProgressStore.Save;
    private System.Func<CareerRepository> createRaceCareerRepository = CareerRuntimeRepository.CreateDefault;
    private System.Action prepareRaceDisplay = GameSettingsRuntime.EnsureLoadedAndApplyDisplay;
    private System.Func<string, int> loadRaceDriverXp = DriverProgressStore.Load;
    private System.Func<TeamId, TechTreeDatabase, TechTreeState> loadRaceTechProfile = TechTreeProfileStore.GetOrCreate;

    private List<GameObject> carInstances = new List<GameObject>();
    private List<int> laneIndices = new List<int>();
    private Dictionary<PlayerState, AIController> aiControllers = new Dictionary<PlayerState, AIController>();
    private Dictionary<PlayerState, int> overtakesThisTurn = new Dictionary<PlayerState, int>();
    private readonly Dictionary<PlayerState, SlipstreamChainResult> slipstreamsThisTurn = new Dictionary<PlayerState, SlipstreamChainResult>();
    private readonly RaceWeatherState weatherState = new RaceWeatherState();
    private RaceCameraController raceCameraController;
    private CarOrientationController carOrientationController;
    private ICarMovementAnimator carMovementAnimator;

    private float nodeWaitDuration;
    private readonly RacePresentationSkipState presentationSkipState = new RacePresentationSkipState();
    private readonly RaceInputState inputState = new RaceInputState();
    private Dictionary<int, Button> gearButtons = new Dictionary<int, Button>();
    private Button confirmGearControl;

    // 印地安纳波利斯起点换道
    private GameObject laneChangePanel;
    private Button laneInButton;
    private Button laneKeepButton;
    private Button laneOutButton;

    // 维修区
    private GameObject pitChoicePanel;
    private TMP_Text pitPromptText;
    private Button pitEnterButton;
    private Button pitSkipButton;
    private PlayerState pitWaitingPlayer;

    // --- 属性 ---
    public PlayerState Player => session != null ? session.Human : null;
    public PlayerState AI => session != null && session.Players.Count > 1 ? session.Players[1] : null;
    /// <summary>当前比赛的完整会话（多车/天气/特技/科技状态）。</summary>
    public RaceSession Session => session;
    public bool IsTutorialMode => tutorialScenario != null;
    public bool IsCareerMode => careerRaceLaunch != null;
    public TutorialScenarioDefinition TutorialScenario => tutorialScenario;
    public TutorialRuntimeDirector TutorialDirector => tutorialDirector;
    /// <summary>Actual open input gate, in modal priority order, for tutorial focus.</summary>
    public string TutorialInputPhase => inputState.WaitingForPitChoice ? "pit"
        : inputState.WaitingForLaneChange ? "lane"
        : inputState.WaitingForDiscard ? "discard"
        : inputState.WaitingForGear ? "gear"
        : inputState.WaitingForCards ? "cards" : "none";
    public bool IsTutorialActionInputBlocked =>
        !pendingTutorialGuideRefreshAtTurnStart &&
        tutorialDirector != null && tutorialDirector.BlocksRaceInput;
    public bool IsTutorialFocusInputBlocked =>
        tutorialDirector != null &&
        (pendingTutorialGuideRefreshAtTurnStart || tutorialDirector.BlocksRaceInput);
    public void DismissTutorialInteractionCallout()
    {
        tutorialGuideUI?.DismissCurrentCallout();
    }
    public GamePhase CurrentPhase => phaseState.Current;
    public bool IsPresentationSkipActive => presentationSkipState.IsActive;
    public bool IsPresentationSkipRequested => presentationSkipState.IsSkipRequested;
    public GameConfigSO Config => config;
    public TrackManager Track => trackManager;
    /// <summary>当前天气显示名。</summary>
    public string WeatherLabel => session != null ? session.WeatherLabel : "晴天";
    /// <summary>Absolute path of the current or most recent manual playtest log.</summary>
    public string LastRaceLogPath => raceLogWriter != null ? raceLogWriter.FilePath : null;
    /// <summary>The currently rendered human car, if it has been spawned.</summary>
    public Transform PlayerCarTransform => carInstances.Count > 0 ? carInstances[0].transform : null;
    /// <summary>The currently rendered first AI car, if it has been spawned.</summary>
    public Transform AICarTransform => carInstances.Count > 1 ? carInstances[1].transform : null;

    /// <summary>
    /// Requests the active movement/tailwind presentation to settle immediately.
    /// This is also useful for an authored UI skip affordance; normal gameplay
    /// input remains untouched outside the presentation window.
    /// </summary>
    public bool RequestPresentationSkip()
    {
        return presentationSkipState.RequestSkip();
    }

    private bool IsPresentationSkipRequestedNow()
    {
        if (!presentationSkipState.IsActive)
            return false;

        if (Input.GetMouseButtonDown(0))
            presentationSkipState.RequestSkip();
        return presentationSkipState.IsSkipRequested;
    }

    void Update()
    {
        // Dismiss tutorial explanatory copy at the authoritative input boundary.
        // This runs before uGUI click callbacks, so a click closes the current
        // lesson's callout while a Next-button callback may still open the next
        // lesson's fresh callout later in the same frame.
        if (Input.GetMouseButtonDown(0))
            DismissTutorialInteractionCallout();

        bool spacePressed = Input.GetKeyDown(KeyCode.Space);

        // Presentation skip has priority over phase actions so one press cannot
        // both settle an animation and accidentally confirm the following phase.
        if (spacePressed && presentationSkipState.IsActive)
        {
            RequestPresentationSkip();
            return;
        }

        if (hudUI != null && hudUI.IsReturnToMenuConfirmationVisible)
        {
            if (spacePressed)
                hudUI.TryConfirmKeyboardAction();
            return;
        }

        if (IsTutorialActionInputBlocked)
            return;

        if (phaseState.CanAcceptGear(inputState))
        {
            int gear = ReadGearShortcut();
            if (gear > 0)
                hudUI?.RequestGearSelection(gear);
            if (spacePressed)
                hudUI?.TriggerConfirmGearShortcut();
            return;
        }

        if (phaseState.CanAcceptCards(inputState) || inputState.WaitingForDiscard)
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) ||
                Input.GetKeyDown(KeyCode.UpArrow))
                cardHandUI?.MoveKeyboardHighlight(-1);
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) ||
                     Input.GetKeyDown(KeyCode.DownArrow))
                cardHandUI?.MoveKeyboardHighlight(1);

            if (Input.GetKeyDown(KeyCode.F))
                cardHandUI?.ToggleKeyboardHighlightedCard();
            if (spacePressed)
                cardHandUI?.TriggerActionShortcut();
        }
    }

    private static int ReadGearShortcut()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 3;
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) return 4;
        return 0;
    }

    void Awake()
    {
    }

    void OnDestroy()
    {
        presentationSkipState.End();
        if (raceLogWriter != null && raceLogWriter.IsActive)
            raceLogWriter.End("scene destroyed", RaceLogTermination.SceneDestroyed);
    }

    void Start()
    {
        prepareRaceDisplay();
        // 自动创建默认配置
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GameConfigSO>();
            Debug.LogWarning("MVPGameManager: GameConfigSO not set. Using defaults. Create one via Create > Foodula1 > MVP Game Config for better control.");
        }
        carOrientationController = new CarOrientationController(config);
        carMovementAnimator = new CarMovementAnimator(
            config,
            carOrientationController,
            null,
            () => IsPresentationSkipRequestedNow());

        // 自动创建缺失的引用
        if (trackManager == null)
            trackManager = GetComponent<TrackManager>();
        if (aiController == null)
            aiController = GetComponent<AIController>();

        // Scene references can become stale when the RaceCanvas prefab is
        // rebuilt (its child file IDs change). Resolve the live scene UI before
        // falling back to the procedural HUD; otherwise AutoCreateUI creates a
        // second, empty HUD on top of the authored one.
        ResolveSceneUIReferences();

        // UI 初始化：Prefab 优先，硬编码回退
        if (raceCanvasPrefab != null && hudUI == null && cardHandUI == null)
        {
            InstantiateUIFromPrefab();
        }
        else if (hudUI == null || cardHandUI == null)
        {
            AutoCreateUI();
        }

        ApplyRaceUILayout();

        // 同时支持 Prefab 与硬编码回退 UI：统一收集档位控件用于高亮和阶段门控。
        CollectGearButtonImages();

        CreateLaneChangeUI();
        CreatePitChoiceUI();

        nodeWaitDuration = GameSettingsRuntime.ScaleAnimationDuration(config.nodeDelay);
        InitializeGame();
        if (!string.IsNullOrEmpty(careerInitializationFailure))
        {
            string message = $"生涯比赛无法启动：{careerInitializationFailure}\n进度未改变，请返回主菜单重试。";
            hudUI?.SetStatus($"<color=red>{message}</color>");
            hudUI?.ShowGameOver(message);
            Debug.LogError($"[CAREER] {message}");
            return;
        }
        InitializeRaceCamera();
        InitializeTutorialGuideUI();
        // Event visuals are optional presentation. If a stale runtime UI
        // object survives an editor scene reload, it must not prevent the
        // gameplay loop from starting.
        try
        {
            InitializeRaceEventFX();
        }
        catch (System.Exception exception)
        {
            raceEventFX = null;
            Debug.LogWarning($"[MVPGameManager] Race event visuals disabled: {exception.Message}");
        }
        StartCoroutine(GameLoop());
    }

    /// <summary>
    /// Recovers references to UI already present in the scene. This is
    /// intentionally called before AutoCreateUI so a stale serialized
    /// reference cannot produce a duplicate runtime HUD.
    /// </summary>
    private void ResolveSceneUIReferences()
    {
        if (hudUI == null)
        {
            HUDUI[] candidates = FindObjectsOfType<HUDUI>(true);
            HUDUI fallback = null;
            for (int i = 0; i < candidates.Length; i++)
            {
                HUDUI candidate = candidates[i];
                if (candidate == null)
                    continue;

                // Prefer the authored prefab HUD: a valid gear button and the
                // permanent return-to-menu button distinguish it from the
                // minimal procedural fallback.
                if (candidate.returnToMenuButton != null && candidate.gear1Button != null)
                {
                    hudUI = candidate;
                    break;
                }

                if (fallback == null)
                    fallback = candidate;
            }

            if (hudUI == null)
                hudUI = fallback;
        }

        if (cardHandUI == null)
        {
            CardHandUI[] candidates = FindObjectsOfType<CardHandUI>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                CardHandUI candidate = candidates[i];
                if (candidate != null)
                {
                    cardHandUI = candidate;
                    break;
                }
            }
        }
    }

    private void InitializeRaceCamera()
    {
        Camera raceCamera = Camera.main;
        if (raceCamera == null)
        {
            Debug.LogWarning("[MVPGameManager] Main camera not found; race camera setup skipped.");
            return;
        }

        raceCameraController = raceCamera.GetComponent<RaceCameraController>();
        if (raceCameraController == null)
        {
            raceCameraController = raceCamera.gameObject.AddComponent<RaceCameraController>();
        }

        Canvas raceCanvas = hudUI != null
            ? hudUI.GetComponentInParent<Canvas>()
            : FindObjectOfType<Canvas>();
        raceCameraController.Initialize(this, raceCanvas);
    }

    private void InitializeRaceEventFX()
    {
        Canvas raceCanvas = hudUI != null
            ? hudUI.GetComponentInParent<Canvas>()
            : FindObjectOfType<Canvas>();
        if (raceCanvas == null)
            return;

        if (raceEventFX == null)
            raceEventFX = raceCanvas.GetComponentInChildren<RaceEventFX>(true);
        if (raceEventFX == null)
        {
            GameObject fxObject = new GameObject("RaceEventFX", typeof(RectTransform));
            fxObject.transform.SetParent(raceCanvas.transform, false);
            raceEventFX = fxObject.AddComponent<RaceEventFX>();
        }

        TMP_FontAsset font = FindObjectOfType<TMP_Text>()?.font;
        raceEventFX.Initialize(raceCanvas, font);
    }

    /// <summary>
    /// 从 Prefab 实例化 UI — Demo 模式的主路径。
    /// 实例化后自动查找 HUDUI 和 CardHandUI 组件。
    /// </summary>
    private void InstantiateUIFromPrefab()
    {
        HideStaleMenuUI();

        // 禁用场景中已有的所有 Canvas（避免双份 UI）
        foreach (var oldCanvas in FindObjectsOfType<Canvas>(true))
            oldCanvas.gameObject.SetActive(false);

        GameObject instance = Instantiate(raceCanvasPrefab);
        instance.name = "RaceCanvas";
        instance.SetActive(true);
        // 修复 Prefab 序列化导致的 scale 归零问题
        instance.transform.localScale = Vector3.one;

        hudUI = instance.GetComponentInChildren<HUDUI>();
        cardHandUI = instance.GetComponentInChildren<CardHandUI>();

        // 将 CardPrefab 引用从 Manager 传给 CardHandUI（Editor 脚本赋值可能在运行时丢失）
        if (cardHandUI != null && cardHandUI.cardPrefab == null && cardUIPrefab != null)
            cardHandUI.cardPrefab = cardUIPrefab;

        // 注入卡牌精灵图引用
        if (cardHandUI != null)
        {
            if (cardHandUI.speedBgSprite == null) cardHandUI.speedBgSprite = speedBgSprite;
            if (cardHandUI.heatBgSprite == null) cardHandUI.heatBgSprite = heatBgSprite;
            if (cardHandUI.selectedOverlaySprite == null) cardHandUI.selectedOverlaySprite = selectedOverlaySprite;
            if (cardHandUI.numberSprites == null || cardHandUI.numberSprites.Length == 0) cardHandUI.numberSprites = numberSprites;
            if (cardHandUI.heatIconSprite == null) cardHandUI.heatIconSprite = heatIconSprite;
        }

        if (hudUI == null)
            Debug.LogError("MVPGameManager: RaceCanvas Prefab has no HUDUI in children!");
        if (cardHandUI == null)
            Debug.LogError("MVPGameManager: RaceCanvas Prefab has no CardHandUI in children!");
    }

    /// <summary>
    /// Scene transitions normally unload the menu, but editor play sessions or
    /// persistent menu overlays can leave a canvas behind. Race UI must be the
    /// only menu-facing canvas once the race scene starts.
    /// </summary>
    private static void HideStaleMenuUI()
    {
        foreach (MainMenuUI menu in FindObjectsOfType<MainMenuUI>(true))
            menu.gameObject.SetActive(false);
        foreach (TrackSelectionUI trackSelection in FindObjectsOfType<TrackSelectionUI>(true))
            trackSelection.gameObject.SetActive(false);
        foreach (DriverSelectionUI driverSelection in FindObjectsOfType<DriverSelectionUI>(true))
            driverSelection.gameObject.SetActive(false);

        foreach (GameObject candidate in FindObjectsOfType<GameObject>(true))
        {
            if (candidate == null)
                continue;

            switch (candidate.name)
            {
                case "MainMenuCanvas":
                case "TrackSelectionOverlay":
                case "TrackSelectionPanel":
                case "DriverSelectionOverlay":
                case "DriverSelectionPanel":
                    candidate.SetActive(false);
                    break;
            }
        }
    }

    private void ApplyRaceUILayout()
    {
        Canvas canvas = hudUI != null
            ? hudUI.GetComponentInParent<Canvas>()
            : cardHandUI != null ? cardHandUI.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        RaceUILayoutController layout = canvas.GetComponent<RaceUILayoutController>();
        if (layout == null)
            layout = canvas.gameObject.AddComponent<RaceUILayoutController>();
        layout.ApplyLayout(hudUI, cardHandUI);
    }

    /// <summary>
    /// 从 HUDUI 的 public 字段收集档位按钮引用，并接入旋钮表现。
    /// </summary>
    private void CollectGearButtonImages()
    {
        gearButtons.Clear();
        confirmGearControl = null;

        RegisterGearButton(1, hudUI != null ? hudUI.gear1Button : null, "Gear1Btn");
        RegisterGearButton(2, hudUI != null ? hudUI.gear2Button : null, "Gear2Btn");
        RegisterGearButton(3, hudUI != null ? hudUI.gear3Button : null, "Gear3Btn");
        RegisterGearButton(4, hudUI != null ? hudUI.gear4Button : null, "Gear4Btn");

        hudUI?.EnsurePresentation();

        confirmGearControl = hudUI != null ? hudUI.confirmGearButton : null;
        if (confirmGearControl == null)
        {
            GameObject confirmObject = GameObject.Find("ConfirmGearBtn");
            if (confirmObject != null)
                confirmGearControl = confirmObject.GetComponent<Button>();
        }
    }

    private void RegisterGearButton(int gear, Button button, string fallbackName)
    {
        if (button == null)
        {
            GameObject buttonObject = GameObject.Find(fallbackName);
            if (buttonObject != null)
                button = buttonObject.GetComponent<Button>();
        }

        if (button == null) return;
        gearButtons[gear] = button;
    }

    private void SetGearControlsInteractable(bool interactable)
    {
        foreach (Button button in gearButtons.Values)
        {
            if (button != null)
                button.interactable = interactable;
        }

        if (confirmGearControl != null)
            confirmGearControl.interactable = interactable;
    }

    /// <summary>
    /// Standard cars expose G1-G4; the Chinese electric car exposes only
    /// Recover and Go. The existing prefab buttons are reused for both modes.
    /// </summary>
    private void ConfigureGearControls(PlayerState player)
    {
        bool china = player != null && TeamGearRules.IsChina(player.teamId);
        SetGearButtonVisible(3, !china);
        SetGearButtonVisible(4, !china);
        SetGearButtonLabel(1, china ? "Recover" : "G1");
        SetGearButtonLabel(2, china ? "Go" : "G2");
        SetGearButtonLabel(3, "G3");
        SetGearButtonLabel(4, "G4");
        hudUI?.ConfigureGearPresentation(china, player != null ? player.gear : 1);
    }

    private void SetGearButtonVisible(int gear, bool visible)
    {
        if (gearButtons.TryGetValue(gear, out Button button) && button != null)
            button.gameObject.SetActive(visible);
    }

    private void SetGearButtonLabel(int gear, string label)
    {
        if (!gearButtons.TryGetValue(gear, out Button button) || button == null) return;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
    }

    /// <summary>
    /// 当 Inspector 中未设置 UI 引用时，自动在 Canvas 上创建基础 UI。
    /// </summary>
    private void AutoCreateUI()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("MVPGameManager: No Canvas found in scene!");
            return;
        }

        // Keep the fallback path isolated from race orchestration. The factory
        // only builds controls and wires callbacks; gameplay state stays here.
        TMP_Text existingTmp = FindObjectOfType<TMP_Text>();
        TMP_FontAsset fontAsset = existingTmp != null ? existingTmp.font : null;
        RaceUIFactory factory = new RaceUIFactory(fontAsset);
        factory.Build(
            canvas,
            ref hudUI,
            ref cardHandUI,
            trackManager != null ? trackManager.TotalNodes : 0,
            cardUIPrefab,
            OnGearButtonClicked,
            OnConfirmGearClicked,
            ResetGame);

        // Authored and procedural buttons use the same binding path. This
        // also repairs older scenes whose serialized listeners were lost.
        BindGearButton("Gear1Btn", 1);
        BindGearButton("Gear2Btn", 2);
        BindGearButton("Gear3Btn", 3);
        BindGearButton("Gear4Btn", 4);
    }

    private void CreateLaneChangeUI()
    {
        if (trackManager == null || !trackManager.AllowsStartFinishLaneChange)
            return;

        Canvas canvas = hudUI != null
            ? hudUI.GetComponentInParent<Canvas>()
            : FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        laneChangePanel = new GameObject("IndianapolisLaneChangePanel", typeof(RectTransform));
        laneChangePanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = laneChangePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 135f);
        panelRect.sizeDelta = new Vector2(520f, 105f);

        CreateTMPText(panelRect, "LaneChangePrompt", "通过起点：选择车道", 18,
            new Vector2(0f, 32f), new Vector2(500f, 28f),
            FindObjectOfType<TMP_Text>()?.font);

        laneInButton = CreateActionButton(panelRect, "LaneInButton", LaneChoicePresentationRules.GetChoiceLabel(1),
            new Vector2(-150f, -15f), new Color(0.55f, 0.85f, 1f),
            () => RequestLaneChange(1));
        laneKeepButton = CreateActionButton(panelRect, "LaneKeepButton", LaneChoicePresentationRules.GetChoiceLabel(0),
            new Vector2(0f, -15f), new Color(0.8f, 0.8f, 0.8f),
            () => RequestLaneChange(0));
        laneOutButton = CreateActionButton(panelRect, "LaneOutButton", LaneChoicePresentationRules.GetChoiceLabel(-1),
            new Vector2(150f, -15f), new Color(1f, 0.75f, 0.45f),
            () => RequestLaneChange(-1));

        laneChangePanel.SetActive(false);
    }

    private void CreatePitChoiceUI()
    {
        Canvas canvas = hudUI != null
            ? hudUI.GetComponentInParent<Canvas>()
            : FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        pitChoicePanel = new GameObject(
            "PitChoicePanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(Outline));
        pitChoicePanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = pitChoicePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -112f);
        panelRect.sizeDelta = new Vector2(720f, 170f);

        Image background = pitChoicePanel.GetComponent<Image>();
        background.color = new Color(0.035f, 0.055f, 0.09f, 0.97f);
        background.raycastTarget = true;
        Outline outline = pitChoicePanel.GetComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.67f, 0.2f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_FontAsset font = FindObjectOfType<TMP_Text>()?.font;
        TMP_Text header = CreateTMPText(panelRect, "PitChoiceTitle", "维修区策略", 22,
            new Vector2(0f, 56f), new Vector2(660f, 30f), font);
        header.alignment = TextAlignmentOptions.Center;
        header.fontStyle = FontStyles.Bold;
        header.color = new Color(1f, 0.78f, 0.32f);

        pitPromptText = CreateTMPText(panelRect, "PitPrompt",
            PitChoicePresentationRules.BuildPrompt(PitLaneRules.DEFAULT_APPROACH_WINDOW), 17,
            new Vector2(0f, 18f), new Vector2(660f, 42f), font);
        pitPromptText.alignment = TextAlignmentOptions.Center;
        pitPromptText.color = new Color(0.84f, 0.9f, 0.97f);

        pitEnterButton = CreateActionButton(panelRect, "PitEnterButton", "预定进站",
            new Vector2(-145f, -45f), new Color(0.18f, 0.62f, 0.38f),
            () => RequestPitDecision(true));
        pitEnterButton.GetComponent<RectTransform>().sizeDelta = new Vector2(230f, 48f);
        pitSkipButton = CreateActionButton(panelRect, "PitSkipButton", "本圈不进站",
            new Vector2(145f, -45f), new Color(0.25f, 0.34f, 0.46f),
            () => RequestPitDecision(false));
        pitSkipButton.GetComponent<RectTransform>().sizeDelta = new Vector2(230f, 48f);

        pitChoicePanel.SetActive(false);
    }

    private void RequestLaneChange(int direction)
    {
        DismissTutorialInteractionCallout();
        if (hudUI == null)
        {
            ChooseIndianapolisLaneChange(direction);
            return;
        }

        string choice = LaneChoicePresentationRules.GetChoiceLabel(direction);
        hudUI.RequestInRaceAction(
            InRaceConfirmationAction.LaneChange,
            "确认起点换道",
            $"确定选择“{choice}”并完成本次车道决定吗？",
            () => ChooseIndianapolisLaneChange(direction));
    }

    private void RequestPitDecision(bool enter)
    {
        DismissTutorialInteractionCallout();
        if (hudUI == null)
        {
            ChoosePit(enter);
            return;
        }

        hudUI.RequestInRaceAction(
            InRaceConfirmationAction.PitDecision,
            PitChoicePresentationRules.GetConfirmationTitle(enter),
            PitChoicePresentationRules.GetConfirmationMessage(enter),
            () => ChoosePit(enter));
    }

    // Auxiliary overlays share the same typography/button construction as the
    // procedural HUD. These adapters keep the manager-specific callbacks local
    // while RaceUIFactory owns the actual UI construction.
    private RaceUIFactory GetRaceUIFactory()
    {
        return new RaceUIFactory(FindObjectOfType<TMP_Text>()?.font);
    }

    private TMP_Text CreateTMPText(Transform parent, string name, string text, int fontSize,
        Vector2 anchoredPos, Vector2 size, TMP_FontAsset font = null)
    {
        return new RaceUIFactory(font != null ? font : FindObjectOfType<TMP_Text>()?.font)
            .CreateText(parent, name, text, fontSize, anchoredPos, size);
    }

    private Button CreateActionButton(Transform parent, string name, string label, Vector2 pos, Color color,
        UnityEngine.Events.UnityAction callback)
    {
        return GetRaceUIFactory().CreateActionButton(parent, name, label, pos, color, callback);
    }

    private void BindGearButton(string name, int gear)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
        {
            UnityEngine.UI.Button btn = go.GetComponent<UnityEngine.UI.Button>();
            if (btn != null)
            {
                int capturedGear = gear;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    if (hudUI != null)
                        hudUI.RequestGearSelection(capturedGear);
                    else
                        OnGearButtonClicked(capturedGear);
                });
            }
        }
    }

    // ====== 初始化 ======

    // Reused by initial entry and ResetRaceRuntime. Keep mode activation separate
    // from scene/participant setup so retries cannot inherit prior result flags.
    private void ActivateRaceLaunchState()
    {
        tutorialScenario = TutorialLaunchState.ActivateRequested();
        careerRaceLaunch = tutorialScenario == null
            ? CareerRaceLaunchState.ActivateRequested()
            : null;
        if (tutorialScenario != null)
            CareerRaceLaunchState.Clear();
        careerResultRecorded = false;
        careerInitializationFailure = string.Empty;
        freeRaceRoster = null;
    }

    private void InitializeGame()
    {
        int startFinishNodeIndex = trackManager.StartFinishNodeIndex;

        ActivateRaceLaunchState();
        if (careerRaceLaunch != null && !ValidateCareerRaceLaunch(out careerInitializationFailure))
            return;
        if (tutorialScenario == null && careerRaceLaunch == null &&
            FreeRaceRosterState.IsConfigured &&
            !FreeRaceRosterState.TryBuildRoster(out freeRaceRoster, out string rosterError))
        {
            Debug.LogWarning($"[MVPGameManager] 自由赛事阵容无效，回退到配置默认阵容：{rosterError}");
            freeRaceRoster = null;
        }
        DriverProfile quickRaceDriver = DriverSelectionState.ResolveDriver(
            config.playerDriverId, config.playerTeam);
        List<RaceParticipantPlan> participantPlans = RaceParticipantPlanBuilder.Build(
            tutorialScenario,
            careerRaceLaunch,
            freeRaceRoster,
            quickRaceDriver,
            config.aiTeams,
            config.aiOpponentCount,
            loadRaceDriverXp);
        tutorialDirector = tutorialScenario != null
            ? new TutorialRuntimeDirector(tutorialScenario, initializeTutorialInPractice)
            : null;
        initializeTutorialInPractice = false;
        pendingTutorialOpponentCue = null;
        pendingTutorialWeatherCue = null;
        activeTutorialOpponentCue = null;
        pendingTutorialPlayerCheckpoint = null;
        pendingTutorialGuideRefreshAtTurnStart = false;
        pendingTutorialGuideEarliestTurn = 0;
        tutorialPitRuleNodes = tutorialScenario != null && tutorialScenario.tutorialPitLane != null
            ? TutorialCheckpointRules.CreateVirtualPitRuleNodes(
                trackManager.TotalNodes,
                tutorialScenario.tutorialPitLane)
            : null;
        session = tutorialScenario != null
            ? new RaceSession(new SystemRandomSource(TutorialScenarioDefinition.RuntimeSeed))
            : new RaceSession();
        session.TeamVehicleBonusesEnabled = tutorialScenario == null ||
            tutorialScenario.teamVehicleBonusesEnabled;
        if (tutorialScenario != null && tutorialScenario.opponentScript.Count > 0)
            session.SlipstreamRangeOverride = tutorialScenario.opponentScript[0].expectedSlipstreamDistance;
        aiControllers.Clear();
        weatherState.Reset();
        raceTurnNumber = 0;

        // 人类玩家（Players[0]）
        RaceParticipantPlan humanPlan = participantPlans[0];
        DriverProfile humanDriver = humanPlan.Driver;
        PlayerState human = CreateParticipantForRace(humanPlan, startFinishNodeIndex);

        // AI 对手
        for (int i = 0; i < participantPlans.Count - 1; i++)
        {
            RaceParticipantPlan aiPlan = participantPlans[i + 1];
            PlayerState aiState = CreateParticipantForRace(aiPlan, startFinishNodeIndex);
            BindAiControllerForRace(aiState, i);
        }

        SpawnCars();
        BeginRaceTestLog(humanDriver, human);

        if (hudUI != null)
        {
            hudUI.AppendLog(tutorialScenario != null
                ? $"教程车辆: {tutorialScenario.playerTeam} / {humanDriver.DisplayName}（车手增益关闭）"
                : careerRaceLaunch != null
                    ? $"生涯第 {careerRaceLaunch.RaceIndex + 1}/8 站：锁定 {careerRaceLaunch.PlayerTeam} 车队"
                    : $"车手: {humanDriver.DisplayName}（{humanDriver.Style}，XP {humanDriver.TalentMultiplier:0.0}x）");
        }

        // 天气：比赛开始时从赛道天气池抽取
        if (tutorialScenario != null)
        {
            string tutorialWeatherId = tutorialDirector != null &&
                                       tutorialDirector.Phase == TutorialRunPhase.Practice
                ? tutorialScenario.practiceWeatherId
                : tutorialScenario.guidedStartWeatherId;
            session.InitializeWeather(
                new[] { tutorialWeatherId },
                tutorialWeatherId);
            if (hudUI != null)
                hudUI.AppendLog($"教程脚本天气: {session.WeatherLabel}");
            if (tutorialDirector != null && tutorialDirector.Phase == TutorialRunPhase.Practice)
            {
                raceLogWriter?.Append(
                    $"[TUTORIAL_PRACTICE] event=started laps=1 weather={tutorialWeatherId} " +
                    $"deck=exact rewards=false progression=false");
            }
        }
        else if (config.enableWeather)
        {
            var trackCfg = trackManager.LoadedTrackConfig;
            session.InitializeWeather(
                trackCfg != null ? trackCfg.weatherPool : null,
                trackCfg != null ? trackCfg.defaultWeather : null);
            if (hudUI != null)
                hudUI.AppendLog($"今日天气: {session.WeatherLabel}");
        }

        // Corner speed labels are clickable and show the exact current
        // player-specific formula. Configure this after weather and player
        // setup, before the first HUD refresh renders the live numbers.
        ConfigureCornerLimitPresentation();

        phaseState.ResetForRace();
        inputState.Reset();
        inputState.BeginGearSelection(Player != null ? Player.gear : config.minGear);
        pitWaitingPlayer = null;
        SetGearControlsInteractable(true);
        ConfigureGearControls(Player);
        if (laneChangePanel != null) laneChangePanel.SetActive(false);
        if (pitChoicePanel != null) pitChoicePanel.SetActive(false);

        if (hudUI != null)
        {
            hudUI.SetGameManager(this);
            hudUI.Refresh(this, Player, AI, session.Players);
        }
        if (cardHandUI != null)
        {
            cardHandUI.SetDiscardMode(false);
            cardHandUI.SetGearSelectionMode(true);
            cardHandUI.ShowHand(this, Player);
            cardHandUI.UpdateDeckInfo(Player);
        }

        ApplyTutorialPendingCue();
        FlushTutorialEvents();
    }

    private void InitializeTutorialGuideUI()
    {
        if (tutorialDirector == null)
        {
            if (tutorialGuideUI != null)
                tutorialGuideUI.gameObject.SetActive(false);
            return;
        }

        bool presentationReady = TutorialGuideTimingRules.IsInitialPresentationReady(
            gameObject.scene.IsValid() && gameObject.scene.isLoaded,
            raceCameraController != null && raceCameraController.IsInitialized,
            phaseState.Current);
        if (!presentationReady)
        {
            if (tutorialGuideUI != null)
                tutorialGuideUI.gameObject.SetActive(false);
            Debug.LogWarning(
                "[TUTORIAL_GUIDE] Initial guide held until the Race scene, camera and input HUD are ready.");
            return;
        }

        raceCameraController.SnapToPlayer();
        Canvas.ForceUpdateCanvases();

        if (tutorialGuideUI == null)
        {
            Canvas canvas = hudUI != null
                ? hudUI.GetComponentInParent<Canvas>()
                : FindObjectOfType<Canvas>();
            TMP_FontAsset font = hudUI != null && hudUI.statusText != null
                ? hudUI.statusText.font
                : TMP_Settings.defaultFontAsset;
            tutorialGuideUI = TutorialGuideUI.Create(this, canvas, font);
        }
        else
        {
            tutorialGuideUI.Refresh();
        }
    }

    private bool TryAdvanceTutorialIfExpected(TutorialAction action, string detail)
    {
        if (pendingTutorialGuideRefreshAtTurnStart ||
            tutorialDirector == null || !tutorialDirector.IsExpecting(action))
            return false;

        bool accepted = tutorialDirector.TryPerform(action, out string failureReason);
        FlushTutorialEvents();
        if (!accepted)
        {
            raceLogWriter?.Append(
                $"[TUTORIAL_GATE] action={action} accepted=false reason={failureReason} detail={detail}");
            tutorialGuideUI?.Refresh();
            return false;
        }

        raceLogWriter?.Append(
            $"[TUTORIAL_GATE] action={action} accepted=true detail={detail}");
        tutorialGuideUI?.Refresh();
        return true;
    }

    private void PresentNextTutorialLesson(TutorialStepDefinition completedStep)
    {
        ApplyTutorialPendingCue();
        TutorialStepDefinition nextStep = tutorialDirector.CurrentStep;
        if (nextStep != null && pendingTutorialPlayerCheckpoint != null &&
            completedStep != null &&
            (completedStep.requiredAction == TutorialAction.PlayChinaHotpot ||
             completedStep.requiredAction == TutorialAction.PlayChinaIceJelly ||
             completedStep.requiredAction == TutorialAction.PlayUsFries ||
             completedStep.requiredAction == TutorialAction.PlayUsCola ||
             completedStep.requiredAction == TutorialAction.ResolveDeSauerkraut ||
             completedStep.requiredAction == TutorialAction.ResolveDeSchwarzbrot ||
             completedStep.requiredAction == TutorialAction.PlayUkScone ||
             completedStep.requiredAction == TutorialAction.PlayUkEnglishBreakfastTea ||
             completedStep.requiredAction == TutorialAction.ResolveUkSpecialtyScone ||
             completedStep.requiredAction == TutorialAction.ResolveUkSpecialtyTea ||
             completedStep.requiredAction == TutorialAction.ResolveJpKantoSkip) &&
            inputState.WaitingForCards)
        {
            // The next lesson rebuilds every card/heat zone at a safe turn boundary.
            // Finish the demonstration card phase without an artificial missing-card
            // penalty; otherwise a completed trick can strand the guide behind its
            // own pending-presentation input gate.
            cardHandUI?.ClearPendingPlaySelection();
            inputState.EndCardSelection();
            cardHandUI?.HideAll();
            raceLogWriter?.Append("[TUTORIAL_GUIDE] card_phase=closed_for_next_checkpoint");
        }
        if (nextStep != null &&
            (TutorialGuideTimingRules.StartsAtNextTurn(nextStep.id) ||
             pendingTutorialPlayerCheckpoint != null))
        {
            pendingTutorialGuideRefreshAtTurnStart = true;
            pendingTutorialGuideEarliestTurn =
                TutorialGuideTimingRules.EarliestPresentationTurn(
                    nextStep.id,
                    raceTurnNumber);
            tutorialGuideUI?.SuspendPresentation();
            raceLogWriter?.Append(
                $"[TUTORIAL_GUIDE] step={nextStep.id} refresh=deferred " +
                $"boundary=prepared_input_state earliest_turn={pendingTutorialGuideEarliestTurn}");
            if (phaseState.Current == GamePhase.WaitingForGear && inputState.WaitingForGear &&
                raceTurnNumber >= pendingTutorialGuideEarliestTurn)
            {
                ApplyPendingTutorialPlayerCheckpoint(force: true);
                TutorialPlayerCheckpoint checkpoint = FindTutorialPlayerCheckpoint(nextStep.id);
                if (TutorialGuideTimingRules.BeginsAtCardSelection(checkpoint))
                {
                    inputState.ConfirmGear();
                }
                else
                {
                    pendingTutorialGuideRefreshAtTurnStart = false;
                    PrepareTutorialTurnPresentation();
                    tutorialGuideUI?.Refresh();
                }
            }
        }
        else
        {
            tutorialGuideUI?.Refresh();
        }
        if (tutorialDirector.Phase == TutorialRunPhase.Practice)
        {
            raceLogWriter?.Append(
                "[TUTORIAL_PRACTICE] event=guided_complete reset=full_lap weather=" +
                tutorialScenario.practiceWeatherId);
            ResetTutorialRace(true, "guided_complete");
        }
    }

    private void FlushTutorialEvents()
    {
        if (tutorialDirector == null || raceLogWriter == null)
            return;

        IReadOnlyList<TutorialEventRecord> newEvents = tutorialDirector.DrainNewEvents();
        for (int i = 0; i < newEvents.Count; i++)
            raceLogWriter.Append(newEvents[i].ToString());
    }

    private void ApplyTutorialPendingCue()
    {
        TutorialCheckpointCue cue = tutorialDirector?.TakePendingCue();
        if (cue == null)
            return;

        if (TutorialGuideTimingRules.DefersWeatherUntilCheckpoint(cue))
        {
            pendingTutorialWeatherCue = cue.Weather;
            raceLogWriter?.Append(
                $"[TUTORIAL_CUE] type=weather step={cue.Weather.step} " +
                $"weather={cue.Weather.weatherId} queued=checkpoint");
        }
        else if (cue.Weather != null)
        {
            ApplyTutorialWeather(cue.Weather);
        }

        if (cue.Opponent != null)
        {
            // Positioning is deferred until every racer has finished its base
            // movement. Applying it immediately would let later movement alter
            // the authored end-of-turn distance before normal tailwind rules run.
            pendingTutorialOpponentCue = cue.Opponent;
            raceLogWriter?.Append(
                $"[TUTORIAL_CUE] type=opponent step={cue.Opponent.step} queued=true " +
                $"leader={cue.Opponent.leaderCell} player={cue.Opponent.playerCell}");
        }

        if (cue.Player != null)
        {
            pendingTutorialPlayerCheckpoint = cue.Player;
            raceLogWriter?.Append(
                $"[TUTORIAL_CHECKPOINT] step={cue.Player.step} queued=true " +
                $"cell={cue.Player.playerCell} gear={cue.Player.gear} " +
                $"normal_hand={cue.Player.normalHandSize} heat_hand={cue.Player.heatInHand} " +
                $"heat_discard={cue.Player.heatInDiscard}");
            // Navigation may arrive during an unfinished turn. Apply only at
            // the prepared gear boundary or the next turn, never in the event.
        }
    }

    private void ApplyTutorialWeather(TutorialWeatherCue weather)
    {
        if (weather != null && session != null)
        {
            WeatherType? scriptedWeather = WeatherRules.ParseWeather(weather.weatherId);
            if (scriptedWeather.HasValue)
            {
                session.Weather = scriptedWeather.Value;
                raceLogWriter?.Append(
                    $"[TUTORIAL_CUE] type=weather step={weather.step} " +
                    $"weather={weather.weatherId} applied=true");
                hudUI?.AppendLog($"<color=cyan>教程脚本天气：{session.WeatherLabel}</color>");
                hudUI?.Refresh(this, Player, AI, session.Players);
            }
            else
            {
                raceLogWriter?.Append(
                    $"[TUTORIAL_CUE] type=weather step={weather.step} " +
                    $"weather={weather.weatherId} applied=false reason=unknown_weather");
            }
        }
    }

    private void ApplyPendingTutorialPlayerCheckpoint(bool force)
    {
        TutorialPlayerCheckpoint checkpoint = pendingTutorialPlayerCheckpoint;
        if (checkpoint == null || tutorialScenario == null || Player == null)
            return;
        if (!force &&
            (phaseState.Current != GamePhase.WaitingForGear || !inputState.WaitingForGear))
            return;

        pendingTutorialPlayerCheckpoint = null;
        TutorialCheckpointApplyResult result = TutorialCheckpointRules.ApplyPlayerCheckpoint(
            tutorialScenario,
            checkpoint,
            Player);
        if (!result.success)
        {
            raceLogWriter?.Append(
                $"[TUTORIAL_CHECKPOINT] step={checkpoint.step} applied=false " +
                $"reason={result.failureReason}");
            pendingTutorialPlayerCheckpoint = checkpoint;
            return;
        }

        if (pendingTutorialWeatherCue != null &&
            pendingTutorialWeatherCue.step == checkpoint.step)
        {
            ApplyTutorialWeather(pendingTutorialWeatherCue);
            pendingTutorialWeatherCue = null;
        }

        // A paired leader must already be in place when the lesson is shown.
        // The normal turn-boundary path also calls this, but an immediate
        // prepared-gear transition can present the guide before that path.
        if (TutorialOpponentCueRules.AppliesWithPlayerCheckpoint(
                pendingTutorialOpponentCue, checkpoint))
            ApplyPendingTutorialOpponentCue();

        MoveCarTo(Player, Player.position);
        RefreshVisualCarLanes();
        if (phaseState.Current == GamePhase.WaitingForGear && inputState.WaitingForGear)
        {
            inputState.BeginGearSelection(Player.gear);
            ConfigureGearControls(Player);
            hudUI?.SelectGearPresentation(Player.gear);
        }
        if (cardHandUI != null)
        {
            cardHandUI.SetDiscardMode(false);
            cardHandUI.SetGearSelectionMode(true);
            cardHandUI.ShowHand(this, Player);
            cardHandUI.UpdateDeckInfo(Player);
        }
        hudUI?.Refresh(this, Player, AI, session.Players);
        // Tutorial checkpoints change the authoritative race position outside
        // normal movement animation. Synchronize every visible consumer now,
        // before a guide panel can describe the newly prepared state.
        raceCameraController?.SnapToPlayer();
        Canvas.ForceUpdateCanvases();
        raceLogWriter?.Append(
            $"[TUTORIAL_CHECKPOINT] step={checkpoint.step} applied=true " +
            $"cell={Player.position} gear={Player.gear} hand={result.handCount} " +
            $"draw={result.drawCount} discard={result.discardCount} engine={result.engineHeat} " +
            "visual_sync=true");
    }

    private void PrepareTutorialTurnPresentation()
    {
        TutorialStepDefinition step = tutorialDirector?.CurrentStep;
        PlayerState player = Player;
        if (step == null || player == null || session == null ||
            !TutorialGuideTimingRules.RequiresFullHandPresentation(step.id))
            return;

        // The normal draw phase follows gear selection. This lesson is shown
        // before gear input, so fill the hand once at the presentation boundary;
        // the regular draw phase will then be a harmless no-op for this player.
        int handSize = session.EffectiveHandSize(player, config.handSize) +
                       player.extraCardSlotsThisTurn;
        bool fullyDrawn = player.deck.DrawToHand(handSize);
        if (cardHandUI != null)
        {
            cardHandUI.SetDiscardMode(false);
            cardHandUI.SetGearSelectionMode(true);
            cardHandUI.ShowHand(this, player);
            cardHandUI.UpdateDeckInfo(player);
        }
        hudUI?.Refresh(this, Player, AI, session.Players);
        raceLogWriter?.Append(
            $"[TUTORIAL_GUIDE] step={step.id} hand_presentation=true " +
            $"target={handSize} hand={player.deck.HandCount} " +
            $"draw={player.deck.DrawPileCount} discard={player.deck.DiscardPileCount} " +
            $"fully_drawn={fullyDrawn}");
    }

    private TutorialPlayerCheckpoint FindTutorialPlayerCheckpoint(TutorialStepId? stepId)
    {
        if (!stepId.HasValue || tutorialScenario == null)
            return null;

        for (int i = 0; i < tutorialScenario.playerCheckpoints.Count; i++)
        {
            TutorialPlayerCheckpoint checkpoint = tutorialScenario.playerCheckpoints[i];
            if (checkpoint.step == stepId.Value)
                return checkpoint;
        }
        return null;
    }

    private void ApplyPendingTutorialOpponentCue()
    {
        TutorialOpponentCue cue = pendingTutorialOpponentCue;
        if (cue == null || session == null || trackManager == null ||
            Player == null || AI == null || trackManager.TotalNodes <= 0)
            return;

        pendingTutorialOpponentCue = null;
        activeTutorialOpponentCue = cue;
        Player.position = Mathf.Clamp(cue.playerCell, 0, trackManager.TotalNodes - 1);
        AI.position = Mathf.Clamp(cue.leaderCell, 0, trackManager.TotalNodes - 1);
        Player.lap = AI.lap;
        MoveCarTo(Player, Player.position);
        MoveCarTo(AI, AI.position);
        RefreshVisualCarLanes();

        int distance = RaceSession.ForwardDistance(
            Player.position,
            AI.position,
            trackManager.TotalNodes);
        raceLogWriter?.Append(
            $"[TUTORIAL_CUE] type=opponent step={cue.step} applied=true " +
            $"leader={AI.position} player={Player.position} distance={distance}");
    }

    public void OnTutorialContinueClicked()
    {
        if (tutorialDirector == null || pendingTutorialGuideRefreshAtTurnStart ||
            phaseState.Current == GamePhase.GameOver)
            return;
        TutorialStepDefinition previous = tutorialDirector.ActiveStep;
        if (!tutorialDirector.TryNext(out _)) return;
        FlushTutorialEvents();
        if (previous == tutorialDirector.ActiveStep)
            tutorialGuideUI?.Refresh();
        else
            PresentNextTutorialLesson(previous);
    }

    /// <summary>Reviews earlier text without restoring cards, cars, weather or checkpoints.</summary>
    public void OnTutorialPreviousClicked()
    {
        if (pendingTutorialGuideRefreshAtTurnStart || tutorialDirector == null ||
            !tutorialDirector.TryPrevious()) return;
        FlushTutorialEvents();
        tutorialGuideUI?.Refresh();
    }

    private IEnumerator WaitForTutorialNavigation()
    {
        yield return new WaitWhile(() => IsTutorialActionInputBlocked);
    }

    /// <summary>
    /// Reading/acknowledgement steps may appear while the previous movement
    /// animation is still finishing. Accepting their button at that point is
    /// safe: authored checkpoint state is already queued and is only applied
    /// by ApplyPendingTutorialPlayerCheckpoint at a turn boundary.
    /// </summary>
    public static bool CanRequestTutorialManualAdvance(
        TutorialStepDefinition step,
        GamePhase currentPhase)
    {
        if (step == null || !step.allowManualAdvance)
            return false;

        // Keep the phase in this contract so the regression explicitly covers
        // Animating. Manual acknowledgement never mutates race state directly.
        return currentPhase != GamePhase.GameOver;
    }

    public void SkipTutorialGuidedSection()
    {
        if (tutorialDirector == null || tutorialDirector.Phase != TutorialRunPhase.Guided)
            return;

        tutorialDirector.SkipGuidedSection();
        FlushTutorialEvents();
        raceLogWriter?.Append(
            "[TUTORIAL_PRACTICE] event=guided_skipped reset=full_lap weather=" +
            tutorialScenario.practiceWeatherId);
        ResetTutorialRace(true, "guided_skipped");
    }

    public void RestartTutorialGuidedSection()
    {
        if (tutorialDirector == null)
            return;

        tutorialDirector.RestartGuidedSection();
        FlushTutorialEvents();
        ResetTutorialRace(false, "guided_restarted");
    }

    public void RestartTutorialPracticeLap()
    {
        if (tutorialDirector == null ||
            (tutorialDirector.Phase != TutorialRunPhase.Practice &&
             tutorialDirector.Phase != TutorialRunPhase.Completed))
            return;

        tutorialDirector.RestartPracticeLap();
        FlushTutorialEvents();
        raceLogWriter?.Append(
            "[TUTORIAL_PRACTICE] event=restart_requested reset=full_lap");
        ResetTutorialRace(true, "practice_restarted");
    }

    public void ExitTutorialToMainMenu()
    {
        ExitTutorialRuntime(SceneLoader.LoadMainMenu);
    }

    // Close evidence before navigation; keep scene I/O at the public entry point.
    private void ExitTutorialRuntime(System.Action loadMainMenu)
    {
        if (tutorialDirector != null)
        {
            tutorialDirector.ExitTutorial();
            FlushTutorialEvents();
            raceLogWriter?.Append("[TUTORIAL_PRACTICE] event=exit_requested");
        }

        StopAllCoroutines();
        if (raceLogWriter != null && raceLogWriter.IsActive)
            raceLogWriter.End("tutorial exited without normal rewards", RaceLogTermination.Exited);
        loadMainMenu();
    }

    private void BeginRaceTestLog(DriverProfile humanDriver, PlayerState human)
    {
        if (raceLogWriter == null)
            raceLogWriter = new RaceTestLogWriter();
        else if (raceLogWriter.IsActive)
            raceLogWriter.End("race reset", RaceLogTermination.Restarted);

        // The menu selection is resolved by TrackManager before the race
        // starts.  The config asset can still contain its default track ID, so
        // log the track that was actually loaded rather than the asset value.
        RaceLogStartupRules.TrackIdentity track = RaceLogStartupRules.ResolveTrack(
            trackManager != null,
            trackManager != null ? trackManager.TrackId : null,
            trackManager != null && trackManager.LoadedTrackConfig != null,
            trackManager != null && trackManager.LoadedTrackConfig != null
                ? trackManager.LoadedTrackConfig.trackName : null,
            trackManager == null && config != null
                ? TrackSelectionState.ResolveTrackId(config.trackId) : "");
        raceLogWriter.BeginRace(track.Id, track.Name, human.name, human.teamId, session.Players.Count - 1);
        foreach (string line in RaceLogStartupRules.BuildSetupLines(
            tutorialScenario,
            tutorialScenario != null ? tutorialDirector.Phase : default,
            careerRaceLaunch, freeRaceRoster))
            raceLogWriter.Append(line);
        if (hudUI != null)
            hudUI.SetLogSink(raceLogWriter.Append);
        Debug.Log($"[RaceTestLog] Started: {raceLogWriter.FilePath}");
    }

    private void LogPlayerSnapshots()
    {
        if (raceLogWriter == null || session == null)
            return;

        foreach (PlayerState p in session.Players)
        {
            if (p == null || p.deck == null || p.deck.heatPool == null)
                continue;

            raceLogWriter.Append(RaceStateLogFormatter.BuildPlayerSnapshot(p));
        }
    }

    private void LogPlayedCards(PlayerState player, string source)
    {
        if (raceLogWriter == null || player == null)
            return;

        raceLogWriter.Append(RaceStateLogFormatter.BuildPlayedCards(player, source));
    }

    /// <summary>Creates and registers one participant from its resolved launch plan.</summary>
    // Do not load launch state or progression again here.
    // Scene objects, AI controllers and race-loop scheduling stay in InitializeGame.
    private PlayerState CreateParticipantForRace(RaceParticipantPlan plan, int startPosition)
    {
        var player = new PlayerState(plan.Name, !plan.IsHuman, startPosition, config.minGear);
        player.driverId = plan.Driver.Id;
        player.driverXp = plan.InitialXp;
        SetupPlayerForRace(player, plan.Team, plan.CareerTechSnapshot);

        // Preserve the original registration order on each role's setup path.
        if (plan.IsHuman) session.Players.Add(player);
        player.driverSkill.Initialize(
            plan.Driver, player.DriverLevel, plan.IsHuman && tutorialScenario == null);
        if (!plan.IsHuman) session.Players.Add(player);
        return player;
    }

    /// <summary>Binds one scene controller to an already registered opponent.</summary>
    private AIController BindAiControllerForRace(PlayerState opponent, int opponentIndex)
    {
        var controller = gameObject.AddComponent<AIController>();
        controller.Initialize(
            this,
            opponent,
            tutorialScenario != null
                ? new SystemRandomSource(TutorialScenarioDefinition.RuntimeSeed + opponentIndex + 1)
                : null);
        aiControllers[opponent] = controller;
        return controller;
    }

    /// <summary>单个玩家的车队、科技、热量池与普通牌组初始化。</summary>
    private void SetupPlayerForRace(
        PlayerState p,
        TeamId teamId,
        CareerTechSnapshot careerTechSnapshot = null)
    {
        int poolSize = RaceParticipantTechnologySetup.Prepare(
            p, teamId, tutorialScenario, careerTechSnapshot,
            config.enableTechTree, config.heatPoolPerPlayer, config.jpDemoBroth,
            session, trackManager?.LoadedTrackConfig?.country,
            team => loadRaceTechProfile(team, session.TechDb),
            () =>
            {
                Debug.LogError("[CAREER] Invalid technology snapshot; career race has no active technology.");
                careerInitializationFailure = "科技快照无法映射到当前科技数据库";
            });

        session.InitializeRaceDeck(p, config, poolSize, tutorialScenario);
        SyncRegionalHeatCapacity(p);

        if (tutorialScenario == null && !p.isAI && teamId == TeamId.CN && config.ensurePlayerAttackTrickInOpeningHand)
        {
            string attackId = session.TrickDb.GetAttackId(teamId);
            if (!p.deck.EnsureTrickCardInHand(attackId))
                Debug.LogWarning($"[MVPGameManager] 无法保证中国队 ATTACK 牌 {attackId} 进入开局手牌。");
        }
    }

    private bool ValidateCareerRaceLaunch(out string failureReason)
    {
        CareerLoadResult loaded = createRaceCareerRepository().Load();
        string loadedTrackId = trackManager != null && trackManager.LoadedTrackConfig != null
            ? trackManager.LoadedTrackConfig.trackId
            : string.Empty;
        return CareerRaceLaunchValidation.TryValidate(
            careerRaceLaunch, loaded, loadedTrackId, out failureReason);
    }

    private void SpawnCars()
    {
        foreach (var c in carInstances)
            if (c != null) Destroy(c);
        carInstances.Clear();
        laneIndices.Clear();
        if (carPrefab == null) return;

        for (int i = 0; i < session.Players.Count; i++)
        {
            var p = session.Players[i];
            int lane = trackManager.GetDefaultLaneIndex(p.isAI);
            laneIndices.Add(lane);

            int visualLane = GetVisualLaneIndex(p);
            laneIndices[i] = visualLane;
            Vector3 startPos = trackManager.GetNodePosition(trackManager.StartFinishNodeIndex, visualLane);
            // 出生即朝向赛道前进方向（P2 #17 赛车随赛道方向旋转）
            int nextIdx = (trackManager.StartFinishNodeIndex + 1) % trackManager.TotalNodes;
            Quaternion startRot = GetCarOrientationController().GetFacingRotation(
                trackManager.GetNodePosition(nextIdx, visualLane) - startPos);
            GameObject instance = Instantiate(carPrefab, startPos, startRot);
            instance.name = $"Car_{p.name}";
            float carScale = config != null ? Mathf.Max(0.01f, config.carSpriteScale) : 0.28f;
            instance.transform.localScale = new Vector3(carScale, carScale, 1f);

            SpriteRenderer sr = instance.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Sprite teamSprite;
                if (TeamCarPresentationRules.TryGetSprite(carSprites, p.teamId, out teamSprite))
                    sr.sprite = teamSprite;
                else
                    sr.color = TeamCarPresentationRules.GetFallbackColor(p.teamId);
            }

            RaceCarBadgeUI badge = instance.GetComponent<RaceCarBadgeUI>();
            if (badge == null)
                badge = instance.AddComponent<RaceCarBadgeUI>();
            badge.Initialize(p.teamId);

            carInstances.Add(instance);
        }

        RefreshVisualCarLanes();
        bool unitedStatesTeamPresent = TeamLandmarkPresentationRules.HasUnitedStatesTeam(session?.Players);
        trackManager.BindPlayerReadability(PlayerCarTransform, unitedStatesTeamPresent);
    }

    // ====== 主游戏循环 ======

    private IEnumerator GameLoop()
    {
        while (phaseState.IsRunning)
        {
            // Reading steps, completed action steps and history review all wait
            // for an explicit guide-panel navigation click. This includes the
            // very first lesson before turn one opens the gear controls.
            yield return WaitForTutorialNavigation();
            // Step transitions often occur during movement/reaction resolution.
            // Apply the authored recovery state only at the next turn boundary.
            ApplyPendingTutorialPlayerCheckpoint(force: true);
            ApplyPendingTutorialOpponentCue();
            raceTurnNumber++;
            raceLogWriter?.Append($"[TURN_START] turn={raceTurnNumber} weather={WeatherLabel}");
            LogPlayerSnapshots();
            // ──── 回合开始 ────
            raceCameraController?.BeginTurn();
            foreach (var p in session.Players)
                session.BeginTurn(p);
            // JP L3 番狂わせ：末位/次末位自动激活，逐回合递减
            TickBankuruwaseForAll();

            var turnOrder = session.GetTurnOrder(); // 末位先行（追赶优势）
            var turnSkipped = new HashSet<PlayerState>(); // 本回合被跳过的玩家（失控/维修区）

            // ====== PHASE A1: 档位决策 ======
            foreach (var p in turnOrder)
            {
                RaceTurnStartAction startAction = RaceTurnRules.GetStartAction(p);
                // 已完赛或已爆缸的赛车不再参与后续回合；否则在玩家 DNF
                // 后比赛继续时，A1 仍会向玩家请求档位输入。
                if (startAction == RaceTurnStartAction.ExcludeTerminal)
                {
                    turnSkipped.Add(p);
                    continue;
                }

                // 维修区预选在上一回合越过入口时登记；本回合开始才真正执行，
                // 因此“停一回合 + 出口后前移”不会发生在入口提示的同一回合。
                if (startAction == RaceTurnStartAction.ExecuteScheduledPitStop)
                {
                    ExecuteScheduledPitStop(p);
                    yield return WaitForTutorialNavigation();
                    turnSkipped.Add(p);
                    continue;
                }

                if (startAction == RaceTurnStartAction.ResolveSkip)
                {
                    ResolveSkip(p);
                    turnSkipped.Add(p);
                    continue;
                }

                if (p.isAI)
                {
                    ApplyGearShift(p, GetAIController(p).DecideGear());
                }
                else
                {
                    phaseState.BeginGearSelection();
                    inputState.BeginGearSelection(p.gear);
                    SetGearControlsInteractable(true);
                    ConfigureGearControls(p);
                    hudUI?.SelectGearPresentation(p.gear);
                    hudUI?.RefreshDriverSkill(this, p);
                    if (hudUI != null)
                        hudUI.SetStatus(
                            $"选择档位 (当前: {TeamGearRules.GetDisplayName(p.teamId, p.gear)}) · 1-4 选择 / 空格确认");
                    if (cardHandUI != null) { cardHandUI.SetGearSelectionMode(true); cardHandUI.UpdateDeckInfo(p); }

                    if (pendingTutorialGuideRefreshAtTurnStart &&
                        raceTurnNumber >= pendingTutorialGuideEarliestTurn &&
                        TutorialGuideTimingRules.IsTurnPresentationReady(phaseState.Current) &&
                        !TutorialGuideTimingRules.BeginsAtCardSelection(
                            FindTutorialPlayerCheckpoint(tutorialDirector?.CurrentStep?.id)))
                    {
                        // Checkpoint, camera and input HUD must all be visible
                        // before the next lesson panel and spotlight appear.
                        pendingTutorialGuideRefreshAtTurnStart = false;
                        PrepareTutorialTurnPresentation();
                        raceCameraController?.SnapToPlayer();
                        Canvas.ForceUpdateCanvases();
                        tutorialGuideUI?.Refresh();
                        raceLogWriter?.Append(
                            $"[TUTORIAL_GUIDE] step={tutorialDirector?.CurrentStep?.id} " +
                            "refresh=applied boundary=player_input_ready");
                    }

                    TutorialPlayerCheckpoint cardStartCheckpoint =
                        FindTutorialPlayerCheckpoint(tutorialDirector?.CurrentStep?.id);
                    if (pendingTutorialGuideRefreshAtTurnStart &&
                        raceTurnNumber >= pendingTutorialGuideEarliestTurn &&
                        TutorialGuideTimingRules.BeginsAtCardSelection(cardStartCheckpoint))
                    {
                        inputState.SelectGear(p.gear);
                        inputState.ConfirmGear();
                        SetGearControlsInteractable(false);
                        hudUI?.SelectGearPresentation(p.gear);
                        raceLogWriter?.Append(
                            $"[TUTORIAL_GUIDE] step={tutorialDirector?.CurrentStep?.id} " +
                            $"gear_preselected={p.gear} boundary=card_selection");
                    }

                    yield return new WaitWhile(() => inputState.WaitingForGear);
                    SetGearControlsInteractable(false);
                    ApplyGearShift(p, inputState.PlayerGearChoice);
                    if (hudUI != null)
                        hudUI.RefreshPlayerResources(p);
                    yield return WaitForTutorialNavigation();
                }
            }

            // ====== PHASE A2: 抽牌 ======
            foreach (var p in turnOrder)
            {
                if (turnSkipped.Contains(p)) continue;

                int handSize = session.EffectiveHandSize(p, config.handSize) + p.extraCardSlotsThisTurn;
                int handCountBeforeDraw = p.deck.HandCount;
                if (p.driverSkill != null &&
                    p.driverSkill.TryConsumePassiveDeckLookahead(out int lookahead))
                {
                    p.deck.DrawBestOfTopPlayableCardsToHand(lookahead);
                    if (hudUI != null)
                        hudUI.AppendLog($"{p.name} 信息海绵：从牌库顶 {lookahead} 张中优先抽取高值牌。");
                }
                bool canDraw = p.deck.DrawToHand(handSize);
                if (!p.isAI && p.deck.HandCount > handCountBeforeDraw)
                    AudioService.PlaySfx(AudioEventNames.CardDraw);
                if (!canDraw && hudUI != null)
                    hudUI.AppendLog($"<color=orange>{p.name}: 牌库耗尽! 以 {p.deck.HandCount} 张手牌继续。</color>");

                // 全热手牌 → 强制 1 档
                if (!p.isAI && p.deck.CountSpeedInHand() == 0 && p.gear > config.minGear)
                {
                    p.gear = config.minGear;
                    p.chinaConsecutiveGearCount = 0;
                    if (hudUI != null)
                        hudUI.AppendLog("<color=orange>No speed cards! Forced to Gear 1.</color>");
                }

                // UK L2 英式全餐；L3 在英国主场额外结算一次。
                if (config.enableTechTree)
                {
                    int triggers = session.ResolveFullEnglishOnDraw(
                        p, trackManager?.LoadedTrackConfig?.country);
                    if (triggers > 0 && hudUI != null)
                        hudUI.AppendLog($"{p.name} 英式全餐：尾流距离+{triggers}，获得 {triggers} 张限时热量牌。");
                }
            }

            // ====== PHASE A3: AI 特技牌 ======
            foreach (var p in turnOrder)
            {
                if (p.isAI && !turnSkipped.Contains(p))
                    DecideAITrick(p);
            }

            // ====== PHASE A4: 速度牌可多选确认，特技牌单张即时确认 ======
            foreach (var p in turnOrder)
            {
                // 含 RaceTurnRules.ShouldSkip：AI 在 A3 打出关东慢煮后本回合不再选牌
                if (turnSkipped.Contains(p) || RaceTurnRules.ShouldSkip(p)) continue;

                if (p.isAI)
                {
                    GetAIController(p).SelectCards();
                    LogPlayedCards(p, "AI");
                }
                else
                {
                    phaseState.BeginCardSelection();
                    inputState.BeginCardSelection();
                    if (cardHandUI != null)
                    {
                        cardHandUI.SetGearSelectionMode(false);
                        cardHandUI.ShowHand(this, p);
                        cardHandUI.UpdateDeckInfo(p);
                    }
                    if (hudUI != null)
                    {
                        hudUI.RefreshPlayerResources(p);
                        hudUI.SetStatus($"{GetSpeedCardRequirementLabel(p)} - 可多选速度牌后确认（最多 {GetMaxSpeedCardsThisTurn(p)} 张；特技牌单张确认）");
                    }
                    if (cardHandUI != null)
                        cardHandUI.RefreshRequirementFeedback(p);

                    TutorialPlayerCheckpoint cardStartCheckpoint =
                        FindTutorialPlayerCheckpoint(tutorialDirector?.CurrentStep?.id);
                    if (pendingTutorialGuideRefreshAtTurnStart &&
                        raceTurnNumber >= pendingTutorialGuideEarliestTurn &&
                        TutorialGuideTimingRules.BeginsAtCardSelection(cardStartCheckpoint))
                    {
                        pendingTutorialGuideRefreshAtTurnStart = false;
                        raceCameraController?.SnapToPlayer();
                        Canvas.ForceUpdateCanvases();
                        tutorialGuideUI?.Refresh();
                        raceLogWriter?.Append(
                            $"[TUTORIAL_GUIDE] step={tutorialDirector?.CurrentStep?.id} " +
                            "refresh=applied boundary=card_input_ready");
                    }

                    yield return new WaitWhile(() => inputState.WaitingForCards);
                    yield return WaitForTutorialNavigation();
                    raceCameraController?.FocusPlayerAfterCardPlay();
                }

                // AI 的特技阶段固定早于速度选牌；人类在每张速度牌确认时记录真实顺序。
                if (p.isAI && p.techState != null && p.playedSpeedCardsThisTurn.Count > 0)
                    TechTreeRules.TrackDimSumCombo(p.techState, false, true, false);
            }
            raceLogWriter?.Append("[CARD_PHASE] end");

            // ====== 维修区预选 ======
            // 在车辆本回合移动前检查入口前十格窗口；选择只登记意图，
            // 真正的停靠要等车辆越过入口后的下一回合开始。
            foreach (var p in turnOrder)
            {
                if (RaceTurnRules.IsInactive(p, turnSkipped)) continue;
                yield return StartCoroutine(ResolvePitApproachChoice(p));
                yield return WaitForTutorialNavigation();
            }

            // ====== 计算移动力（科技 + 特技加成） ======
            ComputeMovements(turnOrder, turnSkipped);
            // ====== PHASE B：先完成所有车辆的基础移动结算 ======
            presentationSkipState.Begin();
            phaseState.BeginAnimation();
            raceLogWriter?.Append("[MOVE_PHASE] begin");
            if (hudUI != null)
                hudUI.SetStatus("移动阶段 · 点击任意位置跳过动画");
            foreach (var p in turnOrder)
            {
                if (RaceTurnRules.IsInactive(p, turnSkipped)) continue;

                int oldPos = p.position;
                int rawEnd = oldPos + p.cornerTotalThisTurn;

                // 移动 → 反应(冷却) → 弯道判定
                yield return StartCoroutine(AnimateMovement(p, GetCarIndex(p)));
                ReactStep(p);
                int engineBeforeCorners = p.deck.heatPool.remaining;
                bool completedCorner = ResolveCorners(p, oldPos, rawEnd);
                session.ArmItalyCornerExitBonus(p, completedCorner);

                if (!p.isAI)
                {
                    TryAdvanceTutorialIfExpected(
                        TutorialAction.ResolveSpeedMovement,
                        $"from:{oldPos},to:{p.position},movement:{p.totalMovementThisTurn}");
                    if (p.teamId == TeamId.US)
                    {
                        if (p.totalMovementThisTurn > p.cornerTotalThisTurn &&
                            !completedCorner)
                            TryAdvanceTutorialIfExpected(TutorialAction.ResolveUsStraight,
                                $"base:{p.cornerTotalThisTurn},movement:{p.totalMovementThisTurn}");
                        if (completedCorner && p.deck.heatPool.remaining < engineBeforeCorners)
                            TryAdvanceTutorialIfExpected(TutorialAction.ResolveUsCorner,
                                $"corner_speed:{p.cornerTotalThisTurn},heat_paid:{engineBeforeCorners - p.deck.heatPool.remaining}");
                    }
                    else if (p.teamId == TeamId.DE && !completedCorner &&
                             p.playedSpeedCardsThisTurn != null &&
                             p.playedSpeedCardsThisTurn.Count == 1 &&
                             p.playedSpeedCardsThisTurn[0].value == 1 &&
                             p.totalMovementThisTurn == 3)
                    {
                        TryAdvanceTutorialIfExpected(TutorialAction.ResolveDeStraight,
                            $"base:1,straight_card:1,chassis:1,movement:{p.totalMovementThisTurn}");
                    }
                    if (p.teamId == TeamId.DE && completedCorner &&
                        p.trickState.sauerkrautPlayed &&
                        p.cornerTotalThisTurn == 1 && p.totalMovementThisTurn == 3)
                    {
                        TryAdvanceTutorialIfExpected(TutorialAction.ResolveDeSauerkraut,
                            $"base:1,sauerkraut:2,movement:{p.totalMovementThisTurn}");
                    }
                    if (p.teamId == TeamId.IT && completedCorner &&
                        p.cornerTotalThisTurn == 1 &&
                        TeamVehicleRules.GetHandling(TeamId.IT) == 2)
                    {
                        TryAdvanceTutorialIfExpected(TutorialAction.ResolveItCorner,
                            $"corner_speed:1,handling:2,from:{oldPos},to:{p.position}");
                    }
                    if (p.teamId == TeamId.IT && p.italyCornerExitBonusAppliedThisTurn &&
                        !completedCorner && p.cornerTotalThisTurn == 1 &&
                        p.position == oldPos + 2)
                    {
                        TryAdvanceTutorialIfExpected(TutorialAction.ResolveItCornerExit,
                            $"base:1,corner_exit:1,from:{oldPos},to:{p.position}");
                    }
                    if (p.teamId == TeamId.JP &&
                        tutorialDirector?.CurrentStep?.id == TutorialStepId.JpTorpedo &&
                        p.trickState.torpedoTempuraActive &&
                        overtakesThisTurn.TryGetValue(p, out int jpOvertakes) &&
                        jpOvertakes == 1 && p.cornerTotalThisTurn == 3 &&
                        p.totalMovementThisTurn == 4 && p.position == oldPos + 4)
                    {
                        TryAdvanceTutorialIfExpected(TutorialAction.ResolveJpTorpedo,
                            $"overtakes:1,card_speed:3,torpedo_bonus:1,from:{oldPos},to:{p.position}");
                    }
                }

                ResolveLandmarkPasses(p, oldPos, oldPos + p.totalMovementThisTurn);
                RegisterPitEntryCrossing(p, oldPos, oldPos + p.totalMovementThisTurn);
                yield return WaitForTutorialNavigation();
            }
            raceLogWriter?.Append("[MOVE_PHASE] end");

            // ====== PHASE C：回合结束时按实际落位结算尾流 ======
            // 尾流不能在回合开始或基础移动前触发；此处所有车辆都已完成
            // 移动、反应和弯道判定，规则层读取的是本回合结束时的实际位置。
            ResolveSlipstreamsAtTurnEnd(turnOrder, turnSkipped);
            if (Player != null &&
                slipstreamsThisTurn.TryGetValue(Player, out SlipstreamChainResult tutorialSlipstream) &&
                tutorialSlipstream.Triggered)
            {
                TryAdvanceTutorialIfExpected(
                    TutorialAction.ResolveSlipstream,
                    $"leader:{tutorialSlipstream.Steps[0].Leader.position},bonus:{tutorialSlipstream.TotalBonus}");
                if (Player.teamId == TeamId.US)
                    TryAdvanceTutorialIfExpected(TutorialAction.ResolveUsSlipstream,
                        $"bonus:{tutorialSlipstream.TotalBonus}");
                if (Player.teamId == TeamId.IT && Player.trickState.parmigianoActive &&
                    tutorialSlipstream.TotalBonus == 4)
                    TryAdvanceTutorialIfExpected(TutorialAction.ResolveItParmigiano,
                        $"bonus:4,leader:{tutorialSlipstream.Steps[0].Leader.position}");
            }
            yield return StartCoroutine(PlaySlipstreamPhase(turnOrder, turnSkipped));
            yield return StartCoroutine(ApplySlipstreamMovement(turnOrder, turnSkipped));
            activeTutorialOpponentCue = null;
            presentationSkipState.End();
            yield return WaitForTutorialNavigation();

            // ====== 弃牌（可选，仅玩家） ======
            var human = Player;
            if (human != null && !human.hasFinished && !human.isBlown &&
                !turnSkipped.Contains(human) && !RaceTurnRules.ShouldSkip(human))
            {
                TutorialStepDefinition tutorialStep = tutorialDirector?.CurrentStep;
                bool skipOptionalDiscard = tutorialStep != null &&
                    TutorialGuideTimingRules.SkipsOptionalDiscardBeforePresentation(
                        tutorialStep.id);
                if (skipOptionalDiscard)
                {
                    raceLogWriter?.Append(
                        $"[TUTORIAL_GUIDE] step={tutorialStep.id} " +
                        "optional_discard=skipped reason=next_turn_card_zone_presentation");
                }
                else
                {
                    yield return StartCoroutine(DiscardStep());
                }
            }

            // ====== 收尾 + 补牌 ======
            foreach (var p in session.Players)
                CleanupTurn(p);

            TryAdvanceTutorialIfExpected(
                TutorialAction.CompleteTurnFlow,
                $"turn:{raceTurnNumber}");

            // 检查游戏是否结束
            if (CheckGameEnd()) break;

            // 刷新 UI
            if (hudUI != null) hudUI.Refresh(this, Player, AI, session.Players);
        }

        // ──── 游戏结束 ────
        phaseState.CompleteGame();
        ShowGameOver();
    }

    // ====== 回合跳过（失控 / 维修区 / 关东慢煮） ======

    private void ResolveSkip(PlayerState p)
    {
        // 失控恢复 / 进站停靠 — 跳过本回合，G1 冷却仍生效
        if (session.ConsumeRecoverySkip(p, config.minGear))
        {
            if (hudUI != null)
                hudUI.AppendLog($"{p.name} sits out this turn (recovery / pit stop).");
        }
    }

    // ====== 失控处理 ======

    /// <summary>
    /// 统一失控处理 — 引擎不足支付时触发。
    /// 弯心超速/引擎故障/急刹→引擎干了→失控。
    /// 计数器达到有效上限（基础 3 + 科技加成）→ 退赛淘汰。
    /// </summary>
    public void HandleSpin(PlayerState p, int rewindPos, string reason)
    {
        if (p.isBlown) return;

        int spinMax = session != null ? session.EffectiveSpinMax(p) : 3;
        RaceSpinOutcome outcome = RaceSpinRules.Evaluate(
            p.spinCounter, session != null ? session.Weather : WeatherType.Sunny,
            spinMax);
        p.spinCounter = outcome.Counter;
        bool eliminated = outcome.Eliminated;

        // 回收全部热量回引擎
        RecoverAllHeatWithPresentation(p);

        // 回退位置
        p.position = rewindPos;
        SyncRegionalHeatCapacity(p);

        // 强制 1 档
        p.gear = RaceSpinRules.GetRecoveryGear(p.teamId,
            TeamGearRules.IsChina(p.teamId) ? ChinaGearShiftRules.RecoverGear : config.minGear);
        p.chinaConsecutiveGearCount = 0;

        // 跳过下回合
        p.skipNextTurn = true;

        // 移动赛车回退位置
        MoveCarTo(p, rewindPos);

        string tag = eliminated ? "<color=red>ELIMINATED!</color>" : $"<color=orange>[{p.spinCounter}/{spinMax}]</color>";
        if (hudUI != null)
        {
            hudUI.AppendLog($"{p.name} <color=red>SPINS OUT!</color> Reason: {reason}. {tag}");
            hudUI.SetStatus(eliminated
                ? $"<color=red>{p.name} 爆缸！赛车退赛</color>"
                : $"<color=orange>{p.name} 失控！回退并跳过下回合</color>");
        }

        Transform spunCar = GetCarTransform(p);
        if (raceEventFX != null && spunCar != null)
            StartCoroutine(PlaySpinPresentation(spunCar, reason, eliminated));

        if (eliminated)
        {
            p.isBlown = true;
            if (hudUI != null)
                hudUI.AppendLog($"<color=red><b>{p.name} has retired from the race!</b></color>");
        }

        if (!p.isAI)
        {
            TryAdvanceTutorialIfExpected(
                TutorialAction.ResolveCornerSpin,
                $"reason:{reason},rewind:{rewindPos},eliminated:{eliminated}");
        }
    }

    private IEnumerator PlaySpinPresentation(Transform car, string reason, bool blown)
    {
        raceCameraController?.BeginVehicleMovement(car);
        yield return StartCoroutine(raceEventFX.PlaySpin(car, reason, blown));
        raceCameraController?.EndVehicleMovement();
    }

    /// <summary>
    /// 尝试从引擎支付热量。若引擎不足 → 触发失控。
    /// 默认将支付的永久热量放入手牌，让热量实际占用手牌；明确指定弃牌堆的效果
    /// （例如中国队阴阳茶 Go）仍可使用 Discard 目的地。
    /// 支持特技牌黑面包垫底（-1，最少 1）与英国 L1 炸鱼薯条（每场 1 次免单）。
    /// 返回 true 表示支付成功，false 表示已触发失控。
    /// </summary>
    public bool TryPayHeat(
        PlayerState p,
        int amount,
        int rewindPos,
        string reason,
        HeatPaymentDestination destination = HeatPaymentDestination.Hand)
    {
        if (amount <= 0) return true;

        HeatPaymentCost cost = HeatPaymentCostRules.Resolve(p, amount);
        amount = cost.Amount;
        if (amount <= 0) return true;

        int drawn = p.deck.DrawHeatFromPool(amount, destination, p.heatPaidCardsThisTurn);
        // Record actual payments even if the engine can only pay part of the request.
        if (session != null)
            session.TrackHeatPaid(p, drawn);
        if (drawn < amount)
        {
            // UK L1 炸鱼薯条：每场限 1 次 — 忽略本次热量判定，回收 1 张热量牌至引擎
            if (p.techState != null && session != null &&
                TechTreeRules.CanUseFishAndChips(p.techState, session.TechDb))
            {
                TechTreeRules.UseFishAndChips(p.techState);
                p.deck.RemoveOneHeatFromDeck();
                RefreshHumanHeatPresentation(p);
                if (hudUI != null)
                    hudUI.AppendLog($"<color=green>{p.name} 炸鱼薯条！忽略本次热量判定，回收 1 张热量牌至引擎。</color>");
                return true;
            }

            HandleSpin(p, rewindPos, reason);
            return false;
        }

        // DE L3 烤肉拼盘跟踪 + CN L2 连击追踪（付热）
        if (p.techState != null)
            TechTreeRules.TrackDimSumCombo(p.techState, false, false, true);

        if (!p.isAI && cardHandUI != null)
        {
            CardVisualZone target = destination == HeatPaymentDestination.Discard
                ? CardVisualZone.DiscardPile
                : CardVisualZone.Hand;
            cardHandUI.PlayHeatTransitions(drawn, CardVisualZone.Engine, target);
        }
        RefreshHumanHeatPresentation(p);
        if (!p.isAI)
        {
            AudioService.PlaySfx(AudioEventNames.HeatPay);
            TutorialHeatFeedbackRules.ReportPayment(
                p.teamId, cost, drawn, destination, reason, TryAdvanceTutorialIfExpected);
        }
        return true;
    }

    // ====== 档位处理 ======

    private void ApplyGearShift(PlayerState p, int targetGear)
    {
        TeamGearRules.Resolution shift = TeamGearRules.Resolve(
            p.teamId,
            p.gear,
            p.chinaConsecutiveGearCount,
            targetGear,
            config.minGear,
            config.maxGear,
            config.twoGearShiftHeatCost,
            config.gearOneCooldown,
            config.gearTwoCooldown);

        RaceGearShiftResolver.Apply(
            p, shift,
            (amount, reason) => TryPayHeat(p, amount, p.position, reason),
            () => AudioService.PlaySfx(AudioEventNames.GearShift),
            () => AudioService.PlaySfx(AudioEventNames.GearFailure));
        raceLogWriter?.Append(
            $"[GEAR] {p.name} role={(p.isAI ? "AI" : "PLAYER")} requested={targetGear} " +
            $"selected={p.gear} engine_heat={p.deck.heatPool.remaining}");
    }

    // ====== 步骤 5：反应（冷却） ======

    /// <summary>
    /// HEAT 规则书步骤 5：根据档位执行冷却。
    /// G1 = 冷却 3，G2 = 冷却 1，G3/G4 = 无冷却。
    /// 科技加成：JP 盐味汤底（含番狂わせ临时效果）/ 转子引擎额外冷却。
    /// </summary>
    private void ReactStep(PlayerState p)
    {
        if (p.isBlown || p.hasFinished) return;

        int cooldown = session.ComputeReactionCooldown(
            p, config.gearOneCooldown, config.gearTwoCooldown);

        if (p.driverSkill != null && p.driverSkill.IsActive &&
            p.driverSkill.Skill == DriverActiveSkillId.TrueSelf)
            TryPayHeat(p, 1, p.positionAtTurnStart, "driver skill: true self");

        if (cooldown > 0)
        {
            int removed = CoolHeatWithPresentation(p, cooldown);
            if (removed > 0 && hudUI != null)
                hudUI.AppendLog($"{p.name} ({TeamGearRules.GetDisplayName(p.teamId, p.gear)}): cools {removed} Heat → engine.");
        }
    }

    private int CoolHeatWithPresentation(PlayerState p, int amount)
    {
        if (p == null || p.deck == null || amount <= 0)
            return 0;

        HeatCoolingResult result = p.deck.CoolHeatWithSources(amount);
        int cooled = result.Total;
        if (!p.isAI && cooled > 0 && cardHandUI != null)
        {
            cardHandUI.PlayHeatTransitions(result.FromHand, CardVisualZone.Hand, CardVisualZone.Engine);
            cardHandUI.PlayHeatTransitions(result.FromDraw, CardVisualZone.DrawPile, CardVisualZone.Engine);
            cardHandUI.PlayHeatTransitions(result.FromDiscard, CardVisualZone.DiscardPile, CardVisualZone.Engine);
        }
        if (cooled > 0)
            RefreshHumanHeatPresentation(p);
        if (!p.isAI && cooled > 0)
        {
            AudioService.PlaySfx(AudioEventNames.HeatCool);
            TutorialHeatFeedbackRules.ReportCooling(
                p, cooled, amount, TryAdvanceTutorialIfExpected);
        }
        return cooled;
    }

    private int RemoveHandHeatWithPresentation(PlayerState p, int amount)
    {
        if (p == null || p.deck == null || amount <= 0)
            return 0;

        int cooled = p.deck.RemoveHeatFromHand(amount);
        if (!p.isAI && cooled > 0 && cardHandUI != null)
        {
            cardHandUI.PlayHeatTransitions(cooled, CardVisualZone.Hand, CardVisualZone.Engine);
        }
        if (cooled > 0)
            RefreshHumanHeatPresentation(p);
        if (!p.isAI && cooled > 0)
            AudioService.PlaySfx(AudioEventNames.HeatCool);
        return cooled;
    }

    private void RecoverAllHeatWithPresentation(PlayerState p)
    {
        if (p == null || p.deck == null)
            return;

        CardPlayRules.RetireCommittedHeatForRecovery(p);
        HeatCoolingResult result = p.deck.RecoverAllHeatWithSources();

        if (!p.isAI && cardHandUI != null)
        {
            cardHandUI.PlayHeatTransitions(result.FromHand, CardVisualZone.Hand, CardVisualZone.Engine);
            cardHandUI.PlayHeatTransitions(result.FromDraw, CardVisualZone.DrawPile, CardVisualZone.Engine);
            cardHandUI.PlayHeatTransitions(result.FromDiscard, CardVisualZone.DiscardPile, CardVisualZone.Engine);
        }
        RefreshHumanHeatPresentation(p);
        if (!p.isAI && result.Total > 0)
            AudioService.PlaySfx(AudioEventNames.HeatCool);
    }

    private void RefreshHumanHeatPresentation(PlayerState player)
    {
        if (player == null || player.isAI)
            return;
        cardHandUI?.UpdateDeckInfo(player);
        hudUI?.RefreshPlayerResources(player);
    }

    // ====== 移动动画（含圈数检测） ======

    private IEnumerator AnimateMovement(PlayerState p, int carIndex)
    {
        yield return StartCoroutine(AnimateMovementByAmount(
            p,
            carIndex,
            p != null ? p.totalMovementThisTurn : 0,
            true));
    }

    private IEnumerator AnimateMovementByAmount(
        PlayerState p,
        int carIndex,
        int totalMove,
        bool playOvertake)
    {
        if (p == null || trackManager == null || trackManager.TotalNodes <= 0)
            yield break;

        totalMove = Mathf.Max(0, totalMove);

        // Keep the pure game state correct even when a car presentation is
        // unavailable (for example in a headless/editor validation run).
        if (carIndex < 0 || carIndex >= carInstances.Count || carInstances[carIndex] == null)
        {
            // Missing visuals must not suppress lap/weather/finish or lane gates.
            // Reuse the same ordered node traversal, omitting only car animation.
            int rawEnd = p.position + totalMove;
            yield return TraverseMovementNodes(p, null, rawEnd, GetVisualLaneIndex(p));
            p.position = rawEnd % trackManager.TotalNodes;
            SyncRegionalHeatCapacity(p);
            yield break;
        }

        GameObject car = carInstances[carIndex];
        int laneIndex = GetVisualLaneIndex(p);
        laneIndices[carIndex] = laneIndex;
        int totalNodes = trackManager.TotalNodes;
        int targetPos = p.position + totalMove;

        if (totalMove > 0)
        {
            raceCameraController?.BeginVehicleMovement(car.transform);
            float leadDelay = GameSettingsRuntime.ScaleAnimationDuration(
                config.movementFocusLeadDelay);
            if (leadDelay > 0f)
                yield return WaitForPresentationDelay(leadDelay);
        }

        yield return StartCoroutine(TraverseMovementNodes(p, car, targetPos, laneIndex));

        p.position = targetPos % totalNodes;
        SyncRegionalHeatCapacity(p);
        RefreshVisualCarLanes();

        int overtakeCount = 0;
        overtakesThisTurn.TryGetValue(p, out overtakeCount);
        if (playOvertake && overtakeCount > 0 && raceEventFX != null)
        {
            // Keep the movement camera on the winner for a dedicated
            // slow-motion close-up before returning to normal race pacing.
            raceCameraController?.BeginVehicleMovement(car.transform);
            yield return StartCoroutine(raceEventFX.PlayOvertake(
                car.transform,
                overtakeCount,
                () => IsPresentationSkipRequestedNow()));
        }

        if (totalMove > 0)
        {
            float trailDelay = GameSettingsRuntime.ScaleAnimationDuration(
                config.movementFocusTrailDelay);
            if (trailDelay > 0f)
                yield return WaitForPresentationDelay(trailDelay);
            raceCameraController?.EndVehicleMovement();
        }
    }

    /// <summary>Visits movement nodes in order, settling each crossing before continuing.</summary>
    private IEnumerator TraverseMovementNodes(PlayerState p, GameObject car, int targetPos, int laneIndex)
    {
        int totalNodes = trackManager.TotalNodes;
        for (int i = p.position + 1; i <= targetPos; i++)
        {
            int nodeIdx = i % totalNodes;
            if (car != null)
            {
                Vector3 target = trackManager.GetNodePosition(nodeIdx, laneIndex);
                yield return GetCarMovementAnimator().MoveToNode(car, target);
            }

            if (trackManager.GetNode(nodeIdx).isStartFinish && RegisterStartFinishCrossing(p))
            {
                yield return WaitForIndianapolisLaneChoice();
                // The choice can change the lane while this traversal is suspended.
                laneIndex = GetVisualLaneIndex(p);
            }

            yield return WaitForPresentationDelay(nodeWaitDuration);
        }
    }

    /// <summary>
    /// Waits for a presentation-only delay while allowing a global click to
    /// settle the remaining visual work immediately.
    /// </summary>
    private IEnumerator WaitForPresentationDelay(float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0f, duration);
        while (elapsed < safeDuration)
        {
            if (IsPresentationSkipRequestedNow())
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // ====== 移动力计算（科技 + 特技加成） ======

    /// <summary>Returns the base and extra speed-card slots for the current turn.</summary>
    public TeamGearRules.SpeedCardRequirement GetSpeedCardRequirement(PlayerState p)
    {
        return CardPlayRules.GetSpeedCardRequirement(p);
    }

    /// <summary>Formats the base-versus-extra slot breakdown for player feedback.</summary>
    public string GetSpeedCardRequirementLabel(PlayerState p)
    {
        return GearRequirementFeedbackRules.FormatRequirementLabel(
            p.teamId, p.gear, GetSpeedCardRequirement(p));
    }

    /// <summary>本回合最大可出速度牌数 = 档位基础要求 + 额外槽。</summary>
    public int GetMaxSpeedCardsThisTurn(PlayerState p)
    {
        return GetSpeedCardRequirement(p).TotalCardCount;
    }

    public bool CanUseBBQHeatCards(PlayerState player)
    {
        return config != null && config.enableTechTree &&
            CardPlayRules.CanUseHeatAsSpeed(player, session, trackManager != null ? trackManager.TotalNodes : 0);
    }

    /// <summary>Synchronizes the regional engine loan after each real position change.</summary>
    private void SyncRegionalHeatCapacity(PlayerState player)
    {
        if (session == null || player == null || player.deck == null || player.deck.heatPool == null)
            return;
        session.SyncRegionalHeatCapacity(player, trackManager != null ? trackManager.TotalNodes : 0,
            config != null && config.enableTechTree && trackManager != null);
        if (session.Players.Contains(player))
            RefreshHumanHeatPresentation(player);
    }

    public bool IsPlayableSpeedCard(PlayerState player, CardData card)
    {
        return card != null && (card.IsSpeed || (card.IsHeat && CanUseBBQHeatCards(player)));
    }

    public int CountPlayableSpeedCardsInHand(PlayerState player)
    {
        return player.deck.CountSpeedInHand() + (CanUseBBQHeatCards(player) ? player.deck.CountHeatInHand() : 0);
    }

    /// <summary>Required cards exclude optional slots granted by tricks or technology.</summary>
    public int GetRequiredSpeedCardsThisTurn(PlayerState p)
    {
        return GetSpeedCardRequirement(p).RequiredCardCount;
    }

    /// <summary>Returns the lane currently used to render and judge a racer.</summary>
    public int GetLaneIndexForPlayer(PlayerState p)
    {
        return GetVisualLaneIndex(p);
    }

    private void ComputeMovements(List<PlayerState> turnOrder, HashSet<PlayerState> turnSkipped)
    {
        overtakesThisTurn.Clear();
        slipstreamsThisTurn.Clear();

        // 第一轮：基础速度总和（弯道判定用，不含特技/科技加成）
        foreach (var p in turnOrder)
        {
            if (RaceTurnRules.IsInactive(p, turnSkipped))
            {
                p.totalMovementThisTurn = 0;
                p.cornerTotalThisTurn = 0;
                slipstreamsThisTurn[p] = default;
                continue;
            }
            p.cornerTotalThisTurn = RaceMovementRules.ComputeCornerSpeed(
                p, activeTutorialOpponentCue, p == AI);
        }

        // 第二轮：加成（需要弯道信息与对手移动）
        foreach (var p in turnOrder)
        {
            if (RaceTurnRules.IsInactive(p, turnSkipped)) continue;

            int rawEnd = p.position + p.cornerTotalThisTurn;
            int lane = GetLane(p);
            bool crossedCorner = trackManager.GetUniqueCornersCrossed(p.position, rawEnd).Count > 0;

            int bonus = session.ComputeMovementBonus(p, crossedCorner);
            int italyCornerExitBonus = session.ConsumeItalyCornerExitBonus(p);
            p.italyCornerExitBonusAppliedThisTurn = italyCornerExitBonus > 0;
            bonus += italyCornerExitBonus;
            // DE L2 香肠拼盘：额外 +1 不计限速，原速度仍在 ResolveCorners 判定。
            if (p.techState != null && TechTreeRules.ShouldTriggerWurstplatte(p.techState, session.TechDb, crossedCorner))
                bonus += 1;
            // JP L1 寿司：速度精确等于弯道限速 → 每弯 +2
            bonus += GetNigiriBonus(p, crossedCorner, rawEnd, lane);
            // JP 特技牌 鱼雷天妇罗：超车 +1
            bonus += GetTorpedoBonus(p, turnOrder);
            // CN 特技牌 火锅底料：下一张正常速度牌整体移出弯道速度，并获得 +1。
            int speedPerCardBonus = DriverSkillRules.GetSpeedPerCardBonus(p.driverSkill);
            bonus += CardPlayRules.GetHotpotMovementContribution(p, speedPerCardBonus);
            // 特技牌即时移动（司康 +2 等）
            bonus += p.trickMoveBonusThisTurn;
            bonus += DriverSkillRules.GetMovementBonus(p.driverSkill, crossedCorner);
            bonus -= DriverSkillRules.GetGutterMovementPenalty(p.driverSkill);
            if (p.driverSkill != null)
                bonus += p.driverSkill.PassiveMovementBonusThisTurn;

            // DE L1 黑啤酒燃料：每圈一次，付 1 热 → +2 移动
            // 引擎支付至弃牌堆；不额外要求保留一张热量。
            if (p.techState != null && config.enableTechTree &&
                session.GetModifiers(p).hasSchwarzbierFuel &&
                p.deck.heatPool != null && p.deck.heatPool.remaining > 0 &&
                TechTreeRules.CanTriggerSchwarzbierFuelThisLap(p.techState, p.lap))
            {
                if (TryPayHeat(p, 1, p.positionAtTurnStart, "schwarzbier fuel", HeatPaymentDestination.Discard))
                {
                    TechTreeRules.UseSchwarzbierFuel(p.techState, p.lap);
                    bonus += 2;
                    if (hudUI != null)
                        hudUI.AppendLog($"{p.name} 黑啤酒燃料（本圈一次）：付 1 热 → +2 移动。");
                }
            }

            // US L1 得来速：基础移动每经过一处地标（起点线/中点）→ +1 移动。
            // 奖励不递归触发本次行动的另一轮得来速判定。
            if (p.techState != null && config.enableTechTree &&
                session.GetModifiers(p).hasDriveThru)
            {
                int crossedLandmarks = RaceSession.CountCrossedLandmarks(
                    p.position, rawEnd, trackManager.TotalNodes);
                if (crossedLandmarks > 0)
                {
                    bonus += crossedLandmarks;
                    if (hudUI != null)
                        hudUI.AppendLog($"{p.name} 得来速：经过 {crossedLandmarks} 处地标 +{crossedLandmarks} 移动。");
                }
            }

            // 先保存不含尾流的基础移动。尾流必须等所有车辆完成这段移动、
            // 反应和弯道判定后，依据实际落位在回合末结算。
            (p.totalMovementThisTurn, p.cornerTotalThisTurn) =
                RaceMovementRules.ResolveBaseMovement(
                    p.cornerTotalThisTurn, bonus, activeTutorialOpponentCue, p == AI);
            raceLogWriter?.Append(
                $"[MOVE_PLAN] {p.name} role={(p.isAI ? "AI" : "PLAYER")} position={p.position} " +
                $"corner_speed={p.cornerTotalThisTurn} non_slipstream_bonus={bonus} " +
                $"base_total={p.totalMovementThisTurn}");
        }


        // Presentation overtake counts still belong to the base movement pass;
        // settled slipstream gets its own phase after everyone has moved.
        RaceOvertakeResolution.Execute(
            turnOrder, turnSkipped, trackManager.TotalNodes, overtakesThisTurn);
    }

    /// <summary>
    /// Resolves slipstream only after every racer has completed the base
    /// movement pass. A zero additional-movement map tells the pure rules
    /// layer to compare the actual settled positions rather than simulating
    /// the turn a second time.
    /// </summary>
    private void ResolveSlipstreamsAtTurnEnd(List<PlayerState> turnOrder, HashSet<PlayerState> turnSkipped)
    {
        if (session == null || trackManager == null || turnOrder == null)
            return;
        RaceSettledSlipstreamResolution.Execute(
            session, turnOrder, turnSkipped, trackManager.TotalNodes,
            Player, activeTutorialOpponentCue, slipstreamsThisTurn,
            message => raceLogWriter?.Append(message));
    }

    /// <summary>
    /// Applies the extra movement awarded by the already-resolved end-of-turn
    /// slipstream. It deliberately skips another reaction/corner check: those
    /// checks belong to the speed-card movement pass, while this is a bonus
    /// movement performed after the tailwind presentation.
    /// </summary>
    private IEnumerator ApplySlipstreamMovement(List<PlayerState> turnOrder, HashSet<PlayerState> turnSkipped)
    {
        bool hasMovement = false;
        foreach (PlayerState follower in turnOrder)
        {
            if (RaceTurnRules.IsInactive(follower, turnSkipped))
                continue;
            if (slipstreamsThisTurn.TryGetValue(follower, out SlipstreamChainResult chain) &&
                chain.TotalBonus > 0)
            {
                hasMovement = true;
                break;
            }
        }

        if (!hasMovement)
            yield break;

        AudioService.PlaySfx(AudioEventNames.SlipstreamMove);

        raceLogWriter?.Append(
            $"[SLIPSTREAM_MOVE_PHASE] begin time_scale_before={Time.timeScale:F2}");
        if (hudUI != null)
            hudUI.SetStatus("尾流阶段结束 · 执行额外移动（点击任意位置跳过动画）");

        if (raceEventFX != null && !IsPresentationSkipRequestedNow())
        {
            raceEventFX.BeginSlipstreamBonusMovementSlowMotion();
            raceLogWriter?.Append(
                $"[SLIPSTREAM_MOVE_PHASE] slow_motion time_scale={Time.timeScale:F2}");
        }

        try
        {
            foreach (PlayerState follower in turnOrder)
            {
                if (RaceTurnRules.IsInactive(follower, turnSkipped))
                    continue;
                if (!slipstreamsThisTurn.TryGetValue(follower, out SlipstreamChainResult chain) ||
                    chain.TotalBonus <= 0)
                    continue;

                int oldPos = follower.position;
                int tailwindMovement = chain.TotalBonus;
                raceLogWriter?.Append(
                    $"[SLIPSTREAM_MOVE] {follower.name} from={oldPos} " +
                    $"bonus={tailwindMovement} to={(oldPos + tailwindMovement) % trackManager.TotalNodes}");
                yield return StartCoroutine(AnimateMovementByAmount(
                    follower, GetCarIndex(follower), tailwindMovement, false));

                ResolveLandmarkPasses(follower, oldPos, oldPos + tailwindMovement);
                RegisterPitEntryCrossing(follower, oldPos, oldPos + tailwindMovement);
            }
        }
        finally
        {
            if (raceEventFX != null)
                raceEventFX.EndSlipstreamBonusMovementSlowMotion();
        }

        raceLogWriter?.Append($"[SLIPSTREAM_MOVE_PHASE] end time_scale_after={Time.timeScale:F2}");
        if (hudUI != null)
            hudUI.SetStatus("尾流加成已执行 · 正在弃牌");
    }

    private void ResolveLandmarkPasses(PlayerState p, int oldPos, int rawMovementEnd)
    {
        if (p == null || p.hasFinished || p.techState == null ||
            !config.enableTechTree || session == null || trackManager == null ||
            !TechTreeRules.HasUniqueTech(p.techState, session.TechDb, TechEffectType.MotherRoad))
            return;

        foreach (int landmarkIndex in RaceSession.CrossedLandmarkIndicesInOrder(
                     oldPos, rawMovementEnd, trackManager.TotalNodes))
        {
            if (p.hasFinished) break;
            ResolveMotherRoadPass(p, landmarkIndex, oldPos);
        }
    }

    private void RegisterPitEntryCrossing(PlayerState p, int oldPos, int rawMovementEnd)
    {
        IReadOnlyList<TrackNode> pitNodes = GetPitRuleNodes();
        if (p == null || trackManager == null || !IsPitLaneEnabledForCurrentSession() ||
            !PitLaneRules.HasPitLane(pitNodes) ||
            !PitLaneRules.CrossedPitEntry(oldPos, rawMovementEnd, pitNodes))
            return;

        // The approach-window choice is only a reservation. Crossing the entry
        // turns it into a stop scheduled for the next turn; no teleport or
        // skipped movement is performed in the current movement phase.
        if (PitLaneRules.ApplyEntryCrossingReservation(p))
        {
            if (hudUI != null)
                hudUI.AppendLog($"{p.name} 已越过维修区入口，下一回合执行进站。");
        }
    }

    /// <summary>
    /// 合并播放本回合全部尾流事件：所有车辆完成基础移动后，
    /// 进入独立的回合末尾流表现阶段，再执行额外移动。
    /// </summary>
    private IEnumerator PlaySlipstreamPhase(List<PlayerState> turnOrder, HashSet<PlayerState> turnSkipped)
    {
        var events = new List<RaceEventFX.SlipstreamVisualEvent>();
        foreach (PlayerState follower in turnOrder)
        {
            if (RaceTurnRules.IsInactive(follower, turnSkipped))
                continue;
            if (!slipstreamsThisTurn.TryGetValue(follower, out SlipstreamChainResult chain) || !chain.Triggered)
                continue;

            for (int stepIndex = 0; stepIndex < chain.Steps.Count; stepIndex++)
            {
                SlipstreamResult step = chain.Steps[stepIndex];
                raceLogWriter?.Append(
                    $"[SLIPSTREAM] {follower.name} chain={stepIndex + 1}/{chain.Steps.Count} " +
                    $"follows={step.Leader.name} bonus={step.Bonus} total_bonus={chain.TotalBonus}");

                Transform followerCar = GetCarTransform(follower);
                Transform leaderCar = GetCarTransform(step.Leader);
                if (followerCar != null && leaderCar != null && step.Bonus > 0)
                    events.Add(new RaceEventFX.SlipstreamVisualEvent(followerCar, leaderCar, step.Bonus));
            }
        }

        if (events.Count == 0)
            yield break;

        // Card flights are deliberately non-blocking for rule resolution, but the
        // tailwind close-up still waits for them before the end-of-turn reveal.
        if (hudUI != null)
            hudUI.SetStatus("移动阶段结束 · 正在结算尾流（点击任意位置跳过动画）");
        if (cardHandUI != null)
            yield return StartCoroutine(cardHandUI.WaitForCardTransitions(
                () => IsPresentationSkipRequestedNow()));
        yield return WaitForPresentationDelay(0.12f);

        raceLogWriter?.Append(
            $"[SLIPSTREAM_PHASE] begin events={events.Count} " +
            $"visuals={(raceEventFX != null ? "enabled" : "disabled")} " +
            $"time_scale_before={Time.timeScale:F2}");
        if (hudUI != null)
            hudUI.SetStatus($"尾流阶段：{events.Count} 段气流，点击任意位置跳过动画");

        if (raceEventFX != null)
        {
            Transform focus = events[0].Follower != null
                ? events[0].Follower
                : events[0].Leader;
            raceCameraController?.BeginVehicleMovement(focus);
            try
            {
                yield return StartCoroutine(raceEventFX.PlaySlipstreams(
                    events,
                    () => IsPresentationSkipRequestedNow()));
            }
            finally
            {
                raceCameraController?.EndVehicleMovement();
            }
        }

        raceLogWriter?.Append($"[SLIPSTREAM_PHASE] end time_scale_after={Time.timeScale:F2}");
        if (hudUI != null)
            hudUI.SetStatus("尾流阶段结束 · 即将执行额外移动");
        float postGap = raceEventFX != null
            ? Mathf.Max(0f, raceEventFX.slipstreamPostGapDuration)
            : 0.16f;
        if (postGap > 0f)
            yield return WaitForPresentationDelay(postGap);
        if (hudUI != null)
            hudUI.SetStatus("尾流加成阶段：按奖励推进");
    }

    private int GetNigiriBonus(PlayerState p, bool crossedCorner, int rawEnd, int lane)
    {
        if (!crossedCorner || p.techState == null) return 0;
        if (!TechTreeRules.HasUniqueTech(p.techState, session.TechDb, TechEffectType.Nigiri)) return 0;

        return RaceMovementRules.ComputeNigiriBonus(
            p.cornerTotalThisTurn,
            trackManager.GetUniqueCornersCrossed(p.position, rawEnd),
            cornerId => session.EffectiveCornerLimit(
                p, trackManager.GetCornerSpeedLimit(cornerId, lane), false));
    }

    private int GetTorpedoBonus(PlayerState p, List<PlayerState> turnOrder)
    {
        return RaceMovementRules.ComputeTorpedoBonus(
            p.trickState, p, turnOrder, trackManager.TotalNodes, RaceTurnRules.ShouldSkip);
    }

    // ====== 弯道判定（per-corner-segment，含天气/科技修正） ======

    private bool ResolveCorners(PlayerState p, int oldPos, int rawEndPos)
    {
        if (p.cornerTotalThisTurn <= 0) return false;
        if (p.isBlown) return false;

        HashSet<int> corners = trackManager.GetUniqueCornersCrossed(oldPos, rawEndPos);
        if (corners.Count == 0) return false;
        int laneIndex = GetLane(p);
        var result = RaceCornerResolver.Resolve(
            p, laneIndex, corners,
            cornerId => session.EffectiveCornerLimit(
                p, trackManager.GetCornerSpeedLimit(cornerId, laneIndex)),
            trackManager.GetCornerName,
            overspeed => session.ResolveOverspeedHeatCost(p, overspeed),
            (heat, reason) => TryPayHeat(p, heat, oldPos, reason),
            over => AudioService.PlaySfx(over ? AudioEventNames.CornerOver : AudioEventNames.CornerSafe));
        if (hudUI != null) hudUI.AppendLog(result.log);
        return result.completed;
    }

    // ====== 维修区 ======

    private IEnumerator ResolvePitApproachChoice(PlayerState p)
    {
        IReadOnlyList<TrackNode> pitNodes = GetPitRuleNodes();
        if (!PitLaneRules.TryGetApproachChoiceDistance(
                p, pitNodes, IsPitLaneEnabledForCurrentSession(), out int distance))
            yield break;

        if (p.isAI)
        {
            DecideAIPit(p, distance);
            yield break;
        }

        pitWaitingPlayer = p;
        yield return StartCoroutine(WaitForPitChoice(distance));
        pitWaitingPlayer = null;
    }

    private IEnumerator WaitForPitChoice(int distance)
    {
        PlayerState waitingPlayer = pitWaitingPlayer;
        if (waitingPlayer == null) yield break;

        // 无 Canvas 时安全地默认继续比赛，避免输入门永远保持打开。
        if (pitChoicePanel == null)
        {
            ApplyPitChoice(waitingPlayer, false);
            yield break;
        }

        inputState.BeginPitChoice();
        if (pitPromptText != null)
            pitPromptText.text = PitChoicePresentationRules.BuildPrompt(distance);
        pitChoicePanel.transform.SetAsLastSibling();
        pitChoicePanel.SetActive(true);
        if (hudUI != null)
            hudUI.SetStatus(
                $"距维修区入口还有 {distance} 格：可预定进站（越过入口后的下一回合停 1 回合并出站前移），还是继续比赛？");

        yield return new WaitWhile(() => inputState.WaitingForPitChoice);

        pitChoicePanel.SetActive(false);
        if (hudUI != null)
            hudUI.SetStatus("");
    }

    /// <summary>玩家选择预定进站 / 本圈继续比赛。</summary>
    public void ChoosePit(bool enter)
    {
        if (!inputState.WaitingForPitChoice) return;
        PlayerState selectedPlayer = pitWaitingPlayer;
        inputState.EndPitChoice();
        ApplyPitChoice(selectedPlayer, enter);
    }

    private void ApplyPitChoice(PlayerState p, bool enter)
    {
        if (p == null) return;

        PitLaneRules.RecordApproachChoice(p, enter);
        if (hudUI != null)
            hudUI.AppendLog(enter
                ? $"{p.name} 预定进站：越过维修区入口后下一回合执行。"
                : $"{p.name} 选择本圈不进站。接近下一圈入口时可再次选择。");
        if (!p.isAI && enter)
        {
            TryAdvanceTutorialIfExpected(
                TutorialAction.SelectPit,
                $"position:{p.position},requested:true");
        }
    }

    private void DecideAIPit(PlayerState p, int distance)
    {
        // AI 启发：热量高 → 在入口前窗口预定进站；决策不会立即传送赛车。
        bool enter = p.HeatRatio >= 0.6f;
        PitLaneRules.RecordApproachChoice(p, enter);
        if (hudUI != null)
            hudUI.AppendLog(enter
                ? $"{p.name}（AI）在距入口 {distance} 格处预定进站。"
                : $"{p.name}（AI）在距入口 {distance} 格处选择不进站。");
    }

    private void ExecuteScheduledPitStop(PlayerState p)
    {
        if (p == null) return;

        var result = RacePitStopExecution.Execute(
            p, ResolveScheduledPitExit, RecoverAllHeatWithPresentation,
            player => TeamGearRules.IsChina(player.teamId) ? ChinaGearShiftRules.RecoverGear : config.minGear);
        if (!result.success)
        {
            if (hudUI != null) hudUI.AppendLog(result.message);
            return;
        }

        MoveCarTo(p, p.position);       // 移动到维修区出口
        if (hudUI != null)
            hudUI.AppendLog($"<color=green>{p.name} 进站执行：本回合停靠，冷却全部热量，出站后前进 {result.exitMoveBonus} 格（{result.pitExitPosition}→{result.exitPosition}）。</color>");
        if (!p.isAI)
        {
            TryAdvanceTutorialIfExpected(
                TutorialAction.ResolvePitOnNextTurn,
                $"pit_exit:{result.pitExitPosition},exit:{result.exitPosition},bonus:{result.exitMoveBonus}");
        }
    }

    private PitStopResult ResolveScheduledPitExit(PlayerState p)
    {
        int exitMoveBonus = config != null
            ? config.pitExitMoveBonus
            : PitLaneRules.DEFAULT_EXIT_MOVE_BONUS;
        if (config != null && config.enableTechTree && session != null && p.techState != null)
            exitMoveBonus += session.GetModifiers(p).pitExitMoveBonus;

        return PitLaneRules.EnterPit(p, GetPitRuleNodes(), exitMoveBonus);
    }

    private bool IsPitLaneEnabledForCurrentSession()
    {
        return tutorialPitRuleNodes != null || (config != null && config.enablePitLane);
    }

    private IReadOnlyList<TrackNode> GetPitRuleNodes()
    {
        return tutorialPitRuleNodes ?? trackManager?.Nodes;
    }

    /// <summary>
    /// US L3 母亲之路：经过地标时的自动结算。
    /// 繁荣(前 2 次过地标) → 免费冷却 2；衰退 → 引擎有热时修复付 1 热，否则跳过；复兴 → 手牌热量转移动。
    /// </summary>
    private void ResolveMotherRoadPass(PlayerState p, int landmarkIndex, int rewindPos)
    {
        RaceMotherRoadExecution.Execute(
            p, landmarkIndex, config.totalLaps,
            amount => CoolHeatWithPresentation(p, amount),
            amount => TryPayHeat(p, amount, rewindPos, "mother road repair"),
            amount => AdvanceInstantTechnologyMovement(p, amount),
            message => hudUI?.AppendLog(message));
    }

    // ====== 特技牌 ======

    /// <summary>
    /// Legacy UI entry point retained for scene bindings. Normal hand interaction
    /// confirms selected speed groups or one selected trick through the shared action button.
    /// </summary>
    public void OnTrickCardClicked(CardData card)
    {
        if (IsTutorialActionInputBlocked) return;
        if (!phaseState.CanAcceptCards(inputState)) return;
        if (!config.enableTrickCards) return;
        if (Player == null) return;

        ConfirmPlayerCard(Player, card);
    }

    /// <summary>玩家与 AI 共用的特技牌结算入口。返回 true 表示打出成功。</summary>
    private bool PlayTrickCard(PlayerState p, CardData card)
    {
        if (p == null || card == null || !p.deck.ContainsInHand(card))
        {
            if (p != null && !p.isAI && hudUI != null)
                hudUI.SetStatus("<color=orange>该特技牌已不在手牌中</color>");
            return false;
        }

        var def = session.TrickDb.Get(card.trickId);
        if (def != null && def.effectType == TrickEffectType.KantoOden &&
            p.playedSpeedCardsThisTurn.Count > 0)
        {
            if (!p.isAI && hudUI != null)
                hudUI.SetStatus("<color=orange>关东慢煮必须在打出速度牌前使用</color>");
            return false;
        }

        if (!p.isAI && tutorialScenario != null &&
            tutorialScenario.id == "tutorial_team_uk_v1" &&
            tutorialDirector?.CurrentStep != null &&
            !TutorialSpecialtyCardRules.ValidateUkTrickSelection(
                tutorialDirector.CurrentStep.id, card.trickId, out string ukGuideReason))
        {
            hudUI?.SetStatus($"<color=orange>教程提示：{ukGuideReason}</color>");
            raceLogWriter?.Append($"[TUTORIAL_CARD_GUIDE] accepted=false reason={ukGuideReason}");
            return false;
        }
        if (!p.isAI && tutorialScenario != null &&
            tutorialScenario.id == "tutorial_team_jp_v1" &&
            tutorialDirector?.CurrentStep != null &&
            !TutorialSpecialtyCardRules.ValidateJpTrickSelection(
                tutorialDirector.CurrentStep.id, card.trickId, out string jpGuideReason))
        {
            hudUI?.SetStatus($"<color=orange>教程提示：{jpGuideReason}</color>");
            raceLogWriter?.Append($"[TUTORIAL_CARD_GUIDE] accepted=false reason={jpGuideReason}");
            return false;
        }

        int engineBeforeTrick = p.deck.heatPool.remaining;
        int moveBonusBeforeTrick = p.trickMoveBonusThisTurn;

        var result = session.PlayTrick(p, card);
        if (!result.success)
        {
            if (!p.isAI && hudUI != null)
                hudUI.SetStatus($"<color=orange>{result.message}</color>");
            return false;
        }

        if (!p.deck.DiscardTrickCard(card))
        {
            Debug.LogError($"Failed to move confirmed trick card '{card.trickId}' from hand to discard.");
            return false;
        }
        if (!p.isAI && cardHandUI != null)
        {
            cardHandUI.PlayCardTransitions(
                new List<CardData> { card },
                CardVisualZone.Hand,
                CardVisualZone.DiscardPile,
                AudioEventNames.CardPlay);
            cardHandUI.UpdateDeckInfo(p);
        }
        raceLogWriter?.Append(
            $"[TRICK] {p.name} source={(p.isAI ? "AI" : "PLAYER")} id={card.trickId} " +
            $"effect={(def != null ? def.effectType.ToString() : "unknown")}");
        if (hudUI != null)
            hudUI.AppendLog($"{p.name} 打出特技牌: {result.message}");

        int heatBeforeTrick = p.deck.CountHeatInHand();
        int speedBeforeTrick = p.deck.CountSpeedInHand();
        ApplyTrickEffects(p, card, result);

        if (!p.isAI)
        {
            if (card.trickId == "uk-scone")
            {
                bool resolved = TutorialSpecialtyCardRules.UkSconeResolved(
                    engineBeforeTrick, p.deck.heatPool.remaining,
                    moveBonusBeforeTrick, p.trickMoveBonusThisTurn);
                if (resolved)
                {
                    TryAdvanceTutorialIfExpected(TutorialAction.PlayUkScone, $"trick:{card.trickId}");
                    TryAdvanceTutorialIfExpected(TutorialAction.ResolveUkSpecialtyScone,
                        "heat_paid:1,movement_bonus:2");
                }
            }
            else if (card.trickId == "uk-english-breakfast-tea")
            {
                bool resolved = TutorialSpecialtyCardRules.UkTeaResolved(
                    heatBeforeTrick, p.deck.CountHeatInHand(),
                    engineBeforeTrick, p.deck.heatPool.remaining);
                if (resolved)
                {
                    TryAdvanceTutorialIfExpected(TutorialAction.PlayUkEnglishBreakfastTea,
                        $"trick:{card.trickId}");
                    TryAdvanceTutorialIfExpected(TutorialAction.ResolveUkSpecialtyTea,
                        "heat_cooled:1,engine_restored:1");
                }
            }
            else if (card.trickId == "jp-kanto-oden" &&
                     TutorialSpecialtyCardRules.JpKantoResolved(
                         p.kantoOdenSkipThisTurn, p.gear,
                         p.trickState.kantoOdenAccumulatedCards,
                         heatBeforeTrick, p.deck.CountHeatInHand(),
                         engineBeforeTrick, p.deck.heatPool.remaining))
                TryAdvanceTutorialIfExpected(TutorialAction.ResolveJpKantoSkip,
                    $"skip:true,carry:{p.trickState.kantoOdenAccumulatedCards},cooled:1");
            else if (card.trickId == "cn-ice-jelly")
                TryAdvanceTutorialIfExpected(TutorialAction.PlayChinaIceJelly, card.trickId);
            else if (card.trickId == "us-fries")
                TryAdvanceTutorialIfExpected(TutorialAction.PlayUsFries, card.trickId);
            else if (card.trickId == "us-cola")
                TryAdvanceTutorialIfExpected(TutorialAction.PlayUsCola, card.trickId);
            else if (card.trickId == "it-chianti" &&
                     heatBeforeTrick - p.deck.CountHeatInHand() == 1 &&
                     speedBeforeTrick - p.deck.CountSpeedInHand() == 1)
                TryAdvanceTutorialIfExpected(TutorialAction.ResolveItChianti,
                    "cooled:1,discarded_speed:1");
        }

        // CN L2 连击追踪：特技
        if (p.techState != null)
            TechTreeRules.TrackDimSumCombo(p.techState, true, false, false);
        return true;
    }

    /// <summary>应用特技牌效果：热量支付/冷却、移动、抽牌、弃牌、限时热量、跳过回合。</summary>
    private void ApplyTrickEffects(PlayerState p, CardData card, TrickPlayResult result)
    {
        RaceTrickEffectExecutor.Execute(
            p, result,
            amount => TryPayHeat(p, amount, p.positionAtTurnStart, "scone"),
            amount => CoolHeatWithPresentation(p, amount),
            () =>
            {
                var def = card != null ? session.TrickDb.Get(card.trickId) : null;
                return def != null ? def.effectType : (TrickEffectType?)null;
            },
            message => hudUI?.AppendLog(message));
    }

    /// <summary>AI 特技牌启发：热量高 → 防守牌；热量低 → 攻击牌；末位 → 关东慢煮。</summary>
    private void DecideAITrick(PlayerState p)
    {
        if (!config.enableTrickCards) return;

        var tricks = p.deck.GetTricksInHand();
        if (tricks.Count == 0) return;

        foreach (CardData trick in tricks)
        {
            var def = session.TrickDb.Get(trick.trickId);
            if (def == null) continue;

            bool isLastPlace = def.effectType == TrickEffectType.KantoOden &&
                session.GetRank(p) >= session.Players.Count;
            bool play = AITrickSelectionRules.ShouldAttempt(
                def, p.HeatRatio, config.aiHeatWarningThreshold, isLastPlace);

            if (play && PlayTrickCard(p, trick))
                return;
        }
    }

    // ====== 印地安纳波利斯起点换道 ======

    private IEnumerator WaitForIndianapolisLaneChoice()
    {
        if (laneChangePanel == null)
            yield break;

        inputState.BeginLaneChangeSelection();
        int humanLane = laneIndices.Count > 0 ? laneIndices[0] : 0;
        laneInButton.interactable = trackManager.GetLaneTowardsInside(humanLane) != humanLane;
        laneOutButton.interactable = trackManager.GetLaneTowardsOutside(humanLane) != humanLane;
        laneKeepButton.interactable = true;
        laneChangePanel.SetActive(true);

        if (hudUI != null)
            hudUI.SetStatus("通过印地起点：选择向内、保持或向外一格");

        yield return new WaitWhile(() => inputState.WaitingForLaneChange);

        laneChangePanel.SetActive(false);
        if (hudUI != null)
            hudUI.SetStatus("车道已确定，继续比赛");
    }

    /// <summary>
    /// direction: +1 toward the inside, 0 keep the lane, -1 toward the outside.
    /// </summary>
    public void ChooseIndianapolisLaneChange(int direction)
    {
        if (!inputState.WaitingForLaneChange || trackManager == null)
            return;

        int oldLane = laneIndices.Count > 0 ? laneIndices[0] : 0;
        if (!RaceLaneRules.TryChooseLane(oldLane, trackManager.LaneCount, direction, out int playerLaneIndex))
            return;

        laneIndices[0] = playerLaneIndex;
        MoveCarToNode(Player, trackManager.StartFinishNodeIndex, playerLaneIndex);
        inputState.EndLaneChangeSelection();
        if (hudUI != null)
            hudUI.AppendLog(LaneChoicePresentationRules.GetResolvedLog(oldLane, playerLaneIndex));
    }

    // ====== 步骤 8：弃牌 ======

    /// <summary>
    /// 弃牌步骤 — 玩家可选择弃掉手中任意非热量牌，之后补牌至手牌上限。
    /// </summary>
    private IEnumerator DiscardStep()
    {
        if (cardHandUI == null) yield break;
        var player = Player;
        if (player == null) yield break;

        inputState.BeginDiscardSelection();
        if (cardHandUI != null)
        {
            cardHandUI.SetDiscardMode(true);
            cardHandUI.ShowHand(this, player);
        }
        if (hudUI != null)
        {
            hudUI.RefreshPlayerResources(player);
            hudUI.SetStatus("弃牌: 点击要弃掉的牌 (非热量牌), 然后点确认弃牌");
        }

        yield return new WaitWhile(() => inputState.WaitingForDiscard);

        // 收集选中牌并弃掉
        if (cardHandUI != null)
        {
            List<CardData> toDiscard = cardHandUI.GetSelectedCards();
            List<CardData> discardedCards = player.deck.DiscardPlayableCardInstancesFromHand(toDiscard);
            cardHandUI.PlayCardTransitions(
                discardedCards,
                CardVisualZone.Hand,
                CardVisualZone.DiscardPile,
                AudioEventNames.CardDiscard);
            cardHandUI.RemoveCardUIs(discardedCards);
            cardHandUI.UpdateDeckInfo(player);
            raceLogWriter?.Append(
                $"[DISCARD] {player.name} selected={toDiscard.Count} " +
                $"discarded={discardedCards.Count}");
            if (discardedCards.Count > 0 && hudUI != null)
                hudUI.AppendLog($"{player.name} discards {discardedCards.Count} card(s).");
            cardHandUI.SetDiscardMode(false);
        }
    }

    // ====== 收尾 ======

    private void CleanupTurn(PlayerState p)
    {
        // Played permanent BBQ heat also enters discard; temporary heat vanishes.
        if (!p.isAI && cardHandUI != null && p.playedSpeedCardsThisTurn.Count > 0)
        {
            var discardVisuals = p.playedSpeedCardsThisTurn.FindAll(card => !card.IsHeat || !card.isTemp);
            cardHandUI.PlayCardTransitions(
                discardVisuals,
                CardVisualZone.Hand,
                CardVisualZone.DiscardPile,
                AudioEventNames.CardPlay);
        }
        RaceTurnCleanup.Execute(p, ResolveCleanupSkills, ReportTemporaryCardCleanup, ResolveCleanupTechnology);
    }

    private void ResolveCleanupSkills(PlayerState p)
    {
        RaceTurnSkillCleanup.Execute(p,
            participant => session != null ? session.EffectiveSpinMax(participant) : 3,
            participant => overtakesThisTurn.TryGetValue(participant, out int count) ? count : 0,
            (participant, spinMax) => hudUI?.AppendLog(
                $"{participant.name} 最后冲刺代价：强制打转，失控 {participant.spinCounter}/{spinMax}。"));
    }

    private void ReportTemporaryCardCleanup(PlayerState p, int tempRemoved)
    {
        if (hudUI != null)
            hudUI.AppendLog($"{p.name} 限时热量牌销毁 {tempRemoved} 张。");
    }

    private void ResolveCleanupTechnology(PlayerState p)
    {
        // RaceTurnCleanup gates this adapter after skill and temporary-card cleanup.
        RaceTurnTechnologyCleanup.Execute(p, session, ApplyYinYang, ReportCleanupCombo, ApplyCleanupGrill);
    }

    private void ReportCleanupCombo(PlayerState p)
    {
        if (hudUI != null)
            hudUI.AppendLog($"<color=orange>{p.name} 点心连击！额外触发阴阳茶。</color>");
    }

    private void ApplyCleanupGrill(PlayerState p, int cooldown)
    {
        HeatCoolingResult result = p.deck.CoolRecordedHeatWithSources(p.heatPaidCardsThisTurn, cooldown);
        int cooled = result.Total;
        if (cooled > 0)
        {
            if (!p.isAI && cardHandUI != null)
            {
                cardHandUI.PlayHeatTransitions(result.FromHand, CardVisualZone.Hand, CardVisualZone.DiscardPile);
                cardHandUI.PlayHeatTransitions(result.FromDraw, CardVisualZone.DrawPile, CardVisualZone.DiscardPile);
                cardHandUI.PlayHeatTransitions(cooled, CardVisualZone.DiscardPile, CardVisualZone.Engine);
            }
            RefreshHumanHeatPresentation(p);
            if (!p.isAI) AudioService.PlaySfx(AudioEventNames.HeatCool);
        }
        if (cooled > 0 && hudUI != null)
            hudUI.AppendLog($"<color=green>{p.name} 烤肉拼盘：自动冷却 {cooled} 张热量牌。</color>");
    }

    /// <summary>应用阴阳茶结果：Go(阴) → 付 1 引擎热并前进；Recover(阳) → 仅从手牌冷却。</summary>
    private void ApplyYinYang(PlayerState p, YinYangResult result)
    {
        if (!result.triggered) return;

        if (result.isYin)
        {
            if (TryPayHeat(
                    p,
                    1,
                    p.positionAtTurnStart,
                    "yin yang (yin)",
                    HeatPaymentDestination.Discard))
            {
                AdvanceInstantTechnologyMovement(p, result.extraMovement);
                if (hudUI != null)
                    hudUI.AppendLog($"<color=orange>{p.name} 阴阳茶(阴)：付 1 热 → +{result.extraMovement} 格。</color>");
            }
        }
        else if (result.isYang)
        {
            // 阴阳茶的 Recover 分支是明确的“手牌冷却”，不使用全牌区优先级冷却。
            int cooled = RemoveHandHeatWithPresentation(p, result.heatToCool);
            if (cooled > 0 && hudUI != null)
                hudUI.AppendLog($"<color=cyan>{p.name} 阴阳茶(阳)：自动冷却 {cooled} 张热量牌。</color>");
        }
    }

    /// <summary>
    /// Apply non-animated technology movement with the same lap settlement as
    /// China's Go bonus. Scene movement still owns animated lane cues.
    /// </summary>
    private void AdvanceInstantTechnologyMovement(PlayerState p, int movement)
    {
        int oldPos = p.position;
        RaceMovementRules.InstantMovementPlan plan = RaceMovementRules.PlanInstantTechnologyMovement(
            trackManager.Nodes, oldPos, movement);
        p.position = plan.FinalPosition;
        SyncRegionalHeatCapacity(p);
        for (int i = 0; i < plan.FinishCrossings && !p.hasFinished; i++)
            OnPlayerCrossedStartFinish(p);
        RegisterPitEntryCrossing(p, plan.StartPosition, plan.RawEnd);
        MoveCarTo(p, p.position);
    }

    // ====== 圈数与完赛 ======

    /// <summary>Settle the crossing before deciding whether movement must await a lane choice.</summary>
    private bool RegisterStartFinishCrossing(PlayerState p)
    {
        OnPlayerCrossedStartFinish(p);
        return p == Player && !p.hasFinished && trackManager.AllowsStartFinishLaneChange;
    }

    public void OnPlayerCrossedStartFinish(PlayerState p)
    {
        if (p.hasFinished) return;

        TutorialRunPhase? tutorialPhase = tutorialDirector != null
            ? tutorialDirector.Phase
            : (TutorialRunPhase?)null;
        if (TutorialPracticeRules.IsGuidedLap(tutorialPhase))
        {
            p.lap++;
            session.OnNewLap(p);
            raceLogWriter?.Append(
                $"[TUTORIAL_GUIDED_LAP] player={p.name} lap={p.lap} counts_for_practice=false");
            if (hudUI != null)
                hudUI.AppendLog($"{p.name} 越过终点线；引导阶段不计入自由练习圈。");
            return;
        }

        int requiredLaps = TutorialPracticeRules.GetRequiredLapCount(
            tutorialPhase,
            config.totalLaps);
        bool allowWeatherRoll = tutorialScenario == null && config.enableWeather;

        RaceLapWeatherTransition transition = RaceLapCrossingExecution.Execute(
            p, session, weatherState, requiredLaps, allowWeatherRoll,
            finished => AudioService.PlaySfx(finished
                ? AudioEventNames.Finish
                : AudioEventNames.LapCross),
            message => hudUI?.AppendLog(message));

        if (transition.HasFinished)
        {
            if (!p.isAI && tutorialDirector != null &&
                tutorialDirector.Phase == TutorialRunPhase.Practice)
            {
                bool completed = tutorialDirector.CompletePracticeLap(out string failureReason);
                if (completed)
                    GameSettingsRuntime.MarkTutorialCompleted();
                FlushTutorialEvents();
                raceLogWriter?.Append(
                    $"[TUTORIAL_PRACTICE] event=lap_complete accepted={completed} " +
                    $"lap={p.lap} required={requiredLaps} reason={failureReason ?? "none"}");
                tutorialGuideUI?.Refresh();
            }
        }
    }

    // ====== 游戏结束 ======

    private bool CheckGameEnd()
    {
        if (session == null) return true;
        TutorialRunPhase? tutorialPhase = tutorialDirector != null
            ? tutorialDirector.Phase
            : (TutorialRunPhase?)null;
        if (TutorialPracticeRules.ShouldEndImmediately(tutorialPhase))
            return true;
        // A blown player is removed from future turns, but does not end the
        // race for the remaining active participants.  A finisher also only
        // locks its own result; the loop ends when no non-blown participant
        // still needs to finish.
        return session.IsRaceOver();
    }

    private void ShowGameOver()
    {
        CompleteRaceWithPersistence(
            saveRaceTechState, saveRaceDriverXp, createRaceCareerRepository);
    }

    // Keep the real result/HUD path testable without touching player storage.
    // Resolve career storage only in the unsaved career branch, as before.
    private void CompleteRaceWithPersistence(
        System.Action<TechTreeState> saveTechState,
        System.Action<string, int> saveDriverXp,
        System.Func<CareerRepository> createCareerRepository)
    {
        // 为未完赛玩家按当前排名补记名次
        session.AssignRemainingFinishers();
        RefreshCarBadges();

        string result = RaceRanking.FormatResults(session.Players);
        if (tutorialScenario != null)
        {
            result = TutorialRaceResultPresentation.BuildSummary(
                result, tutorialDirector != null && tutorialDirector.Phase == TutorialRunPhase.Completed);
        }
        else if (careerRaceLaunch != null)
        {
            if (careerResultRecorded)
            {
                result += CareerRaceResultPresentation.AlreadySaved;
                raceLogWriter?.Append(
                    $"[CAREER_RESULT] status=already_saved result_id={careerRaceLaunch.ResultId}");
            }
            else if (CareerRaceSettlement.TryRecord(
                         careerRaceLaunch,
                         trackManager != null && trackManager.LoadedTrackConfig != null
                             ? trackManager.LoadedTrackConfig.trackId
                             : string.Empty,
                         session.Players,
                         createCareerRepository(),
                         out CareerSeasonState updatedCareer,
                         out string failureReason))
            {
                careerResultRecorded = true;
                result += CareerRaceResultPresentation.BuildSavedSummary(updatedCareer);
                raceLogWriter?.Append(CareerRaceLogFormatter.BuildSaved(careerRaceLaunch, updatedCareer));
            }
            else
            {
                result += CareerRaceResultPresentation.BuildRejectedSummary(failureReason);
                raceLogWriter?.Append(CareerRaceLogFormatter.BuildRejected(careerRaceLaunch, failureReason));
            }
        }
        else
        {
            result += "\n\n" + SettleNormalRaceRewards(
                saveTechState, saveDriverXp);
        }

        if (hudUI != null) hudUI.ShowGameOver(result);
        if (cardHandUI != null) cardHandUI.HideAll();
        tutorialGuideUI?.Refresh();
        if (raceLogWriter != null && raceLogWriter.IsActive)
        {
            raceLogWriter.End(result, TutorialRaceResultPresentation.GetTermination(
                tutorialScenario != null,
                tutorialDirector != null && tutorialDirector.Phase == TutorialRunPhase.Completed));
            Debug.Log($"[RaceTestLog] Finished: {raceLogWriter.FilePath}");
        }
    }

    // Keep live mode/track resolution separate from presentation and persistence.
    private string SettleNormalRaceRewards(
        System.Action<TechTreeState> saveTechState,
        System.Action<string, int> saveDriverXp)
    {
        string trackCountry = trackManager != null && trackManager.LoadedTrackConfig != null
            ? trackManager.LoadedTrackConfig.country
            : string.Empty;
        return NormalRaceRewardSettlement.Settle(
            session, trackCountry, tutorialScenario != null, careerRaceLaunch != null,
            saveTechState, saveDriverXp);
    }

    // ====== 番狂わせ（JP L3） ======

    private void TickBankuruwaseForAll()
    {
        if (!config.enableTechTree) return;

        session.AdvanceBankuruwaseForAll((p, transition) =>
        {
            if (transition == BankuruwaseTurnTransition.Activated)
            {
                if (hudUI != null)
                    hudUI.AppendLog($"<color=cyan>{p.name} 番狂わせ激活：连续 3 回合获得四种汤底效果，转子引擎额外冷却 1。</color>");
            }
            else if (transition == BankuruwaseTurnTransition.Expired)
            {
                if (hudUI != null)
                    hudUI.AppendLog($"{p.name} 番狂わせ效果结束。");
            }
        });
    }

    // ====== 辅助 ======

    private Transform GetCarTransform(PlayerState p)
    {
        int index = GetCarIndex(p);
        return index >= 0 && index < carInstances.Count && carInstances[index] != null
            ? carInstances[index].transform
            : null;
    }

    private int GetCarIndex(PlayerState p)
    {
        if (session == null || p == null || session.Players == null)
            return -1;
        return session.Players.IndexOf(p);
    }

    private int GetLane(PlayerState p)
    {
        return GetVisualLaneIndex(p);
    }

    private int GetVisualLaneIndex(PlayerState p)
    {
        int idx = GetCarIndex(p);
        if (idx < 0 || idx >= laneIndices.Count || trackManager == null)
            return trackManager != null && p != null
                ? trackManager.GetDefaultLaneIndex(p.isAI)
                : 0;

        if (TrackPresentationRules.IsIndianapolis(trackManager.TrackId))
            return Mathf.Clamp(laneIndices[idx], 0, trackManager.LaneCount - 1);

        bool trailingInParallel = RaceLaneRules.IsTrailingInParallel(
            p, session != null ? session.Players : null, idx);
        return TrackPresentationRules.GetStandardTrafficLaneIndex(
            trackManager.TrackId,
            trailingInParallel);
    }

    private void RefreshVisualCarLanes()
    {
        if (session == null || trackManager == null)
            return;

        for (int i = 0; i < session.Players.Count && i < carInstances.Count; i++)
        {
            PlayerState p = session.Players[i];
            if (p == null || carInstances[i] == null)
                continue;

            int lane = GetVisualLaneIndex(p);
            laneIndices[i] = lane;
            MoveCarToNode(p, p.position, lane);
        }

        RefreshCarBadges();
    }

    private void RefreshCarBadges()
    {
        if (session == null)
            return;

        foreach (RaceRanking.RankEntry entry in session.GetRankings())
        {
            int carIndex = GetCarIndex(entry.player);
            if (carIndex < 0 || carIndex >= carInstances.Count || carInstances[carIndex] == null)
                continue;

            RaceCarBadgeUI badge = carInstances[carIndex].GetComponent<RaceCarBadgeUI>();
            if (badge != null)
                badge.Refresh(entry.player, entry.rank);
        }
    }

    private AIController GetAIController(PlayerState p)
    {
        aiControllers.TryGetValue(p, out var ctrl);
        return ctrl;
    }

    private void MoveCarTo(PlayerState p, int position)
    {
        int idx = GetCarIndex(p);
        if (idx < 0 || idx >= carInstances.Count || carInstances[idx] == null) return;
        int lane = GetVisualLaneIndex(p);
        laneIndices[idx] = lane;
        MoveCarToNode(p, position, lane);
        RefreshCarBadges();
    }

    private void MoveCarToNode(PlayerState p, int position, int lane)
    {
        int idx = GetCarIndex(p);
        if (idx < 0 || idx >= carInstances.Count || carInstances[idx] == null) return;
        var car = carInstances[idx];
        int displayPosition = position;
        int displayLane = lane;
        if (TryGetThunderstormGridSlot(p, out int gridPosition, out int gridLane))
        {
            displayPosition = gridPosition;
            displayLane = gridLane;
        }

        car.transform.position = trackManager.GetNodePosition(displayPosition, displayLane);
        // 传送后朝向下一节点（失控回退 / 进站出口 / 阴阳茶 +1）
        int nextIdx = (displayPosition + 1) % trackManager.TotalNodes;
        GetCarOrientationController().FaceImmediately(car, trackManager.GetNodePosition(nextIdx, displayLane));
    }

    /// <summary>
    /// Keeps the 12-car thunderstorm readable before turn one: gameplay state
    /// remains a shared starting cell, while the presentation uses a staggered
    /// track-grid formation. The real positions take over on turn one.
    /// </summary>
    private bool TryGetThunderstormGridSlot(
        PlayerState player,
        out int displayPosition,
        out int displayLane)
    {
        displayPosition = 0;
        displayLane = 0;
        if (freeRaceRoster == null || session == null || trackManager == null)
            return false;

        return TrackPresentationRules.TryGetThunderstormGridSlot(
            raceTurnNumber, freeRaceRoster.Length, session.Players.IndexOf(player),
            trackManager.StartFinishNodeIndex, trackManager.TotalNodes, trackManager.LaneCount,
            out displayPosition, out displayLane);
    }

    // ====== 赛车朝向（P2 #17 随赛道方向旋转） ======

    private CarOrientationController GetCarOrientationController()
    {
        if (carOrientationController == null)
            carOrientationController = new CarOrientationController(config);
        return carOrientationController;
    }

    private ICarMovementAnimator GetCarMovementAnimator()
    {
        if (carMovementAnimator == null)
            carMovementAnimator = new CarMovementAnimator(config, GetCarOrientationController());
        return carMovementAnimator;
    }

    // ====== UI 回调 ======

    private void ConfigureCornerLimitPresentation()
    {
        if (trackManager == null)
            return;
        trackManager.ConfigureCornerLimitPresentation(
            ResolveCornerLimitBreakdown,
            OnCornerLimitLabelClicked);
    }

    private CornerLimitBreakdown ResolveCornerLimitBreakdown(int cornerId, int laneIndex)
    {
        int baseLimit = trackManager != null
            ? trackManager.GetCornerSpeedLimit(cornerId, laneIndex)
            : 99;
        return session != null
            ? session.GetCornerLimitBreakdown(Player, baseLimit, false)
            : CornerLimitBreakdown.FromBase(baseLimit);
    }

    private void OnCornerLimitLabelClicked(int cornerId, int laneIndex)
    {
        if (trackManager == null || hudUI == null)
            return;
        hudUI.ShowCornerLimitDetails(
            trackManager.GetCornerName(cornerId),
            ResolveCornerLimitBreakdown(cornerId, laneIndex));
    }

    public void OnGearButtonClicked(int gear)
    {
        if (IsTutorialActionInputBlocked) return;
        if (!phaseState.CanAcceptGear(inputState)) return;
        if (Player != null && TeamGearRules.IsChina(Player.teamId) && gear > ChinaGearShiftRules.GoGear)
        {
            AudioService.PlayUi(AudioEventNames.GearFailure);
            return;
        }
        if (!inputState.SelectGear(gear))
        {
            AudioService.PlayUi(AudioEventNames.GearFailure);
            return;
        }
        // 高亮选中的档位按钮
        hudUI?.SelectGearPresentation(gear);
        tutorialGuideUI?.DismissOperationCallout(TutorialFocusOperation.Gear);
        if (hudUI != null)
            hudUI.SetStatus($"已选 {TeamGearRules.GetDisplayName(Player.teamId, gear)} 档 - 点击确认锁定");
    }

    public void OnConfirmGearClicked()
    {
        if (IsTutorialActionInputBlocked) return;
        if (!phaseState.CanAcceptGear(inputState)) return;
        if (!inputState.ConfirmGear()) return;
        SetGearControlsInteractable(false);
        hudUI?.RefreshDriverSkill(this, Player);
    }

    public void OnDriverSkillButtonClicked()
    {
        PlayerState player = Player;
        if (player == null || player.driverSkill == null) return;

        DriverSkillActivationContext context = BuildDriverSkillActivationContext(player);
        if (!player.driverSkill.TryActivate(player.DriverProfile, context, out string reason))
        {
            hudUI?.SetStatus($"<color=orange>{reason}</color>");
            hudUI?.RefreshDriverSkill(this, player);
            return;
        }

        AudioService.PlayUi(AudioEventNames.UiConfirm);
        string message = $"{player.DriverProfile.ActiveName} 已发动（剩余 {player.driverSkill.UsesRemaining} 次）";
        hudUI?.AppendLog($"<color=#D9A7FF>{message}</color>");
        hudUI?.SetStatus(message + "；请选择并确认档位");
        hudUI?.RefreshDriverSkill(this, player);
        trackManager?.RefreshCornerLimitLabels();
        raceLogWriter?.Append(
            $"[DRIVER_SKILL] driver={player.driverId} skill={player.driverSkill.Skill} " +
            $"tier={player.driverSkill.Tier} duration={player.driverSkill.ActiveTurnsRemaining} " +
            $"uses_remaining={player.driverSkill.UsesRemaining}");
    }

    public string GetDriverSkillButtonLabel(PlayerState player, out bool interactable)
    {
        interactable = false;
        if (player == null || player.driverSkill == null) return "车手技能";
        return DriverSkillPresentationRules.GetButtonLabel(
            player.DriverProfile, player.driverSkill,
            BuildDriverSkillActivationContext(player), out interactable);
    }

    private DriverSkillActivationContext BuildDriverSkillActivationContext(PlayerState player)
    {
        return DriverSkillRaceContextRules.Build(
            player,
            player == Player && phaseState.CanAcceptGear(inputState),
            config != null ? config.totalLaps : 0,
            session?.Players,
            trackManager != null ? trackManager.TotalNodes : 0);
    }

    public void OnPlayCardsButtonClicked()
    {
        if (IsTutorialActionInputBlocked) return;
        // 弃牌模式 — 点击按钮确认整组弃牌
        if (inputState.WaitingForDiscard)
        {
            inputState.EndDiscardSelection();
            return;
        }

        if (!phaseState.CanAcceptCards(inputState)) return;
        if (cardHandUI == null) return;
        var player = Player;
        if (player == null) return;

        List<CardData> selected = cardHandUI.GetSelectedPlayCards();
        if (selected.Count > 0)
        {
            if (selected[0].IsTrick)
            {
                // Trick cards are intentionally single-card, immediate actions.
                ConfirmPlayerCard(player, selected[0]);
            }
            else
            {
                ConfirmPlayerSpeedCards(player, selected);
            }
            return;
        }

        FinishPlayerCardPhase(player);
    }

    /// <summary>
    /// Commits one legacy card entry from the human hand. Speed cards use the
    /// same atomic path as multi-select groups; trick cards resolve immediately.
    /// </summary>
    private bool ConfirmPlayerCard(PlayerState player, CardData card)
    {
        if (player == null || card == null || !player.deck.ContainsInHand(card))
        {
            if (hudUI != null)
                hudUI.SetStatus("<color=orange>该牌已不在手牌中</color>");
            cardHandUI?.ShowHand(this, player);
            return false;
        }

        if (card.IsHeat && !IsPlayableSpeedCard(player, card))
        {
            if (hudUI != null)
                hudUI.SetStatus("<color=orange>热量牌不可打出</color>");
            return false;
        }

        if (card.IsTrick)
        {
            if (!config.enableTrickCards || !PlayTrickCard(player, card))
                return false;

            RefreshHumanHand(player);
            if (hudUI != null)
                hudUI.RefreshPlayerResources(player);

            if (hudUI != null)
            {
                var def = session.TrickDb.Get(card.trickId);
                string trickName = def != null ? def.name : "特技牌";
                if (TrickCardRules.HasHotpotAttack(player.trickState))
                    hudUI.SetStatus("ATTACK 已待命：下一张确认的速度牌 +1，且整张不计弯道限速");
                else
                    hudUI.SetStatus(player.kantoOdenSkipThisTurn
                        ? $"已发动 {trickName}，本回合出牌结束"
                        : $"已发动 {trickName}；选择下一张牌或结束出牌");
            }

            // 关东慢煮在确认后立即结束本回合出牌阶段。
            if (player.kantoOdenSkipThisTurn)
            {
                inputState.EndCardSelection();
                cardHandUI.HideAll();
            }
            else
            {
                cardHandUI.BlockActionButtonBriefly();
            }
            return true;
        }

        if (IsPlayableSpeedCard(player, card))
            return ConfirmPlayerSpeedCards(player, new List<CardData> { card });

        return false;
    }

    private bool ConfirmPlayerSpeedCards(PlayerState player, IReadOnlyList<CardData> cards)
    {
        if (player == null || cards == null || cards.Count == 0)
            return false;

        if (!ValidateTutorialSpecialtySpeedSelection(player, cards))
            return false;

        bool hotpotWasArmed = TrickCardRules.HasHotpotAttack(player.trickState);
        int maxCards = GetMaxSpeedCardsThisTurn(player);
        SpeedCardCommitResult commit = CardPlayRules.CommitSpeedCards(player, cards, maxCards,
            config != null && config.enableTechTree ? session : null,
            trackManager != null ? trackManager.TotalNodes : 0);
        if (commit != SpeedCardCommitResult.Success)
        {
            if (hudUI != null)
            {
                string message = GearRequirementFeedbackRules.FormatSpeedCommitFailure(commit, maxCards);
                hudUI.SetStatus($"<color=orange>{message}</color>");
            }
            if (commit == SpeedCardCommitResult.CardNotInHand)
                RefreshHumanHand(player);
            return false;
        }

        if (player.techState != null)
            TechTreeRules.TrackDimSumCombo(player.techState, false, true, false);
        if (hotpotWasArmed && player.hotpotAttackAppliedThisTurn)
            TryAdvanceTutorialIfExpected(TutorialAction.PlayChinaHotpot,
                $"attack_card:{player.hotpotAttackCardValueThisTurn},played:{cards.Count}");

        if (hudUI != null)
        {
            hudUI.AppendLog(GearRequirementFeedbackRules.FormatCommittedSpeedLog(player.name, cards));
            if (hotpotWasArmed && player.hotpotAttackAppliedThisTurn)
            {
                hudUI.AppendLog(
                    $"火锅 ATTACK：速度 {player.hotpotAttackCardValueThisTurn} 的牌获得 +1，整张不计弯道限速。");
            }
            hudUI.SetStatus(GearRequirementFeedbackRules.FormatCommittedSpeedStatus(
                player.playedSpeedCardsThisTurn.Count, maxCards));
        }
        LogPlayedCards(player, "PLAYER");
        RefreshHumanHand(player);
        cardHandUI.BlockActionButtonBriefly();
        return true;
    }

    /// <summary>Ends human card play and applies the existing missing-card engine-failure rule.</summary>
    private void FinishPlayerCardPhase(PlayerState player)
    {
        if (!ValidateTutorialSpecialtySpeedCompletion(player))
            return;

        int speedCount = player.playedSpeedCardsThisTurn.Count;

        // 引擎故障：速度牌不足时，每缺 1 张 → +1 热量入手牌。引擎不足 → 失控
        int required = GetRequiredSpeedCardsThisTurn(player);
        int missing = RaceRules.GetMissingSpeedCardCount(required, speedCount);
        if (missing > 0)
        {
            bool heatPaid = TryPayHeat(player, missing, player.position, "engine failure");
            TryAdvanceTutorialIfExpected(
                TutorialAction.TriggerMissingCardPenalty,
                $"required:{required},played:{speedCount},missing:{missing},paid:{heatPaid}");
            if (!heatPaid)
            {
                // 已确认速度牌仍属于本回合已打出区域，CleanupTurn 会将其放入弃牌堆。
                player.playedHeatCardsThisTurn.Clear();
                cardHandUI.ClearPendingPlaySelection();
                inputState.EndCardSelection();
                if (hudUI != null)
                    hudUI.RefreshPlayerResources(player);
                cardHandUI.HideAll();
                return;
            }
            if (hudUI != null)
                hudUI.AppendLog($"Engine failure! Missing {missing} speed card(s). +{missing} Heat to hand.");
        }

        else
        {
            TryAdvanceTutorialIfExpected(
                TutorialAction.SelectRequiredGearAndCards,
                $"gear:{player.gear},required:{required},played:{speedCount}");
            if (player.teamId == TeamId.CN &&
                player.gear == ChinaGearShiftRules.GoGear)
            {
                TutorialAction goAction = player.chinaConsecutiveGearCount == 1
                    ? TutorialAction.CompleteChinaFirstGo
                    : TutorialAction.CompleteChinaConsecutiveGo;
                TryAdvanceTutorialIfExpected(goAction,
                    $"consecutive:{player.chinaConsecutiveGearCount},required:{required},played:{speedCount}");
            }
        }

        if (hudUI != null)
            hudUI.RefreshPlayerResources(player);
        cardHandUI.ClearPendingPlaySelection();
        inputState.EndCardSelection();
        cardHandUI.HideAll();
        if (player.teamId == TeamId.JP && player.gear == 1 &&
            player.extraCardSlotsThisTurn == 2 && speedCount == 3)
            TryAdvanceTutorialIfExpected(TutorialAction.ResolveJpKantoRelease,
                "gear:1,carry_slots:2,played:3");
    }

    private bool ValidateTutorialSpecialtySpeedSelection(
        PlayerState player,
        IReadOnlyList<CardData> selected)
    {
        if (tutorialScenario == null || tutorialDirector == null ||
            tutorialDirector.CurrentStep == null || tutorialDirector.IsActiveStepComplete)
            return true;

        TutorialStepId step = tutorialDirector.CurrentStep.id;
        if (TutorialSpeedSelectionGate.Validate(
                tutorialScenario.id, step, player, selected, out string reason))
            return true;

        cardHandUI?.ClearPendingPlaySelection();
        hudUI?.SetStatus($"<color=orange>教程提示：{reason}</color>");
        raceLogWriter?.Append(
            $"[TUTORIAL_CARD_GUIDE] accepted=false step={step} reason={reason}");
        return false;
    }

    private bool ValidateTutorialSpecialtySpeedCompletion(PlayerState player)
    {
        if (tutorialScenario == null || tutorialDirector == null ||
            tutorialDirector.CurrentStep == null)
            return true;

        TutorialStepId step = tutorialDirector.CurrentStep.id;
        if (TutorialSpeedCompletionGate.Validate(
                tutorialScenario.id, step, tutorialDirector.IsActiveStepComplete,
                player, out string reason))
            return true;

        hudUI?.SetStatus($"<color=orange>教程提示：{reason}</color>");
        raceLogWriter?.Append(
            $"[TUTORIAL_CARD_GUIDE] accepted=false step={step} reason={reason}");
        return false;
    }

    private void RefreshHumanHand(PlayerState player)
    {
        if (player == null || player.isAI || cardHandUI == null) return;
        cardHandUI.ShowHand(this, player);
        cardHandUI.UpdateDeckInfo(player);
    }

    // ====== 重置 ======

    public void ResetGame()
    {
        DispatchRaceRestart(
            RestartTutorialGuidedSection,
            RestartTutorialPracticeLap,
            SceneLoader.LoadMainMenu,
            ResetRaceRuntime);
    }

    // Preserve mode precedence while keeping destructive scene/lifecycle I/O
    // outside the restart decision. No mutable global callback overrides.
    private void DispatchRaceRestart(
        System.Action restartGuided,
        System.Action restartPractice,
        System.Action returnToMenu,
        System.Action restartRuntime)
    {
        if (tutorialDirector != null)
        {
            if (tutorialDirector.Phase == TutorialRunPhase.Practice ||
                tutorialDirector.Phase == TutorialRunPhase.Completed)
                restartPractice();
            else
                restartGuided();
            return;
        }

        if (careerRaceLaunch != null && careerResultRecorded)
        {
            returnToMenu();
            return;
        }

        restartRuntime();
    }

    private void ResetTutorialRace(bool startInPractice, string reason)
    {
        RestartTutorialRuntime(startInPractice, reason, ResetRaceRuntime);
    }

    // Preserve the closure -> practice flag -> rebuild order, without global hooks.
    private void RestartTutorialRuntime(bool startInPractice, string reason, System.Action rebuildRuntime)
    {
        if (tutorialScenario == null)
            return;

        if (raceLogWriter != null && raceLogWriter.IsActive)
            raceLogWriter.End($"tutorial reset: {reason}", RaceLogTermination.Restarted);
        initializeTutorialInPractice = startInPractice;
        rebuildRuntime();
    }

    private void ResetRaceRuntime()
    {
        RebuildRaceRuntime(
            controller => Destroy(controller),
            InitializeGame,
            InitializeTutorialGuideUI,
            () => StartCoroutine(GameLoop()));
    }

    // Retire old bindings before initialization; Destroy remains deferred in play.
    // Exceptions intentionally stop the remaining work, as in the original path.
    private void RebuildRaceRuntime(
        System.Action<AIController> retireController,
        System.Action initializeRace,
        System.Action initializeGuide,
        System.Action startLoop)
    {
        StopAllCoroutines();
        foreach (var c in GetComponents<AIController>())
            retireController(c);
        aiControllers.Clear();
        if (hudUI != null && hudUI.gameOverPanel != null)
            hudUI.gameOverPanel.SetActive(false);
        initializeRace();
        initializeGuide();
        startLoop();
    }
}

/// <summary>Pure presentation copy for the pit decision UI.</summary>
public static class PitChoicePresentationRules
{
    public static string BuildPrompt(int distance)
    {
        int safeDistance = distance < 0 ? 0 : distance;
        return $"距入口 {safeDistance} 格 · 预定后将在越过入口的下一回合停站、冷却并从出口前移";
    }

    public static string GetConfirmationTitle(bool enter)
    {
        return enter ? "确认预定进站" : "确认本圈不进站";
    }

    public static string GetConfirmationMessage(bool enter)
    {
        return enter
            ? "车辆将在通过维修区入口后的下一回合停站、冷却，并获得维修区出口推进。"
            : "本圈将不进入维修区，赛车会按正常流程继续结算。确定吗？";
    }
}
