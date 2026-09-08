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
    private bool careerResultRecorded;
    private string careerInitializationFailure;
    private TutorialRuntimeDirector tutorialDirector;
    private TutorialGuideUI tutorialGuideUI;
    private TutorialOpponentCue pendingTutorialOpponentCue;
    private TutorialPlayerCheckpoint pendingTutorialPlayerCheckpoint;
    private bool pendingTutorialGuideRefreshAtTurnStart;
    private int pendingTutorialGuideEarliestTurn;
    private IReadOnlyList<TrackNode> tutorialPitRuleNodes;
    private bool initializeTutorialInPractice;
    private readonly RacePhaseState phaseState = new RacePhaseState();
    private RaceTestLogWriter raceLogWriter;
    private int raceTurnNumber;

    private List<GameObject> carInstances = new List<GameObject>();
    private List<int> laneIndices = new List<int>();
    private Dictionary<PlayerState, AIController> aiControllers = new Dictionary<PlayerState, AIController>();
    private Dictionary<PlayerState, int> overtakesThisTurn = new Dictionary<PlayerState, int>();
    private readonly Dictionary<PlayerState, SlipstreamChainResult> slipstreamsThisTurn = new Dictionary<PlayerState, SlipstreamChainResult>();
    private readonly RaceWeatherState weatherState = new RaceWeatherState();
    private RaceCameraController raceCameraController;
    private CarOrientationController carOrientationController;
    private ICarMovementAnimator carMovementAnimator;

    private WaitForSeconds nodeWait;
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
    public GamePhase CurrentPhase => phaseState.Current;
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

    void Awake()
    {
    }

    void OnDestroy()
    {
        if (raceLogWriter != null && raceLogWriter.IsActive)
            raceLogWriter.End("scene destroyed");
    }

    void Start()
    {
        GameSettingsRuntime.EnsureLoadedAndApplyDisplay();
        // 自动创建默认配置
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GameConfigSO>();
            Debug.LogWarning("MVPGameManager: GameConfigSO not set. Using defaults. Create one via Create > Foodula1 > MVP Game Config for better control.");
        }
        carOrientationController = new CarOrientationController(config);
        carMovementAnimator = new CarMovementAnimator(config, carOrientationController);

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

        nodeWait = new WaitForSeconds(
            GameSettingsRuntime.ScaleAnimationDuration(config.nodeDelay));
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

        laneInButton = CreateActionButton(panelRect, "LaneInButton", "向内一格",
            new Vector2(-150f, -15f), new Color(0.55f, 0.85f, 1f),
            () => ChooseIndianapolisLaneChange(1));
        laneKeepButton = CreateActionButton(panelRect, "LaneKeepButton", "保持车道",
            new Vector2(0f, -15f), new Color(0.8f, 0.8f, 0.8f),
            () => ChooseIndianapolisLaneChange(0));
        laneOutButton = CreateActionButton(panelRect, "LaneOutButton", "向外一格",
            new Vector2(150f, -15f), new Color(1f, 0.75f, 0.45f),
            () => ChooseIndianapolisLaneChange(-1));

        laneChangePanel.SetActive(false);
    }

    private void CreatePitChoiceUI()
    {
        Canvas canvas = hudUI != null
            ? hudUI.GetComponentInParent<Canvas>()
            : FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        pitChoicePanel = new GameObject("PitChoicePanel", typeof(RectTransform));
        pitChoicePanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = pitChoicePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 175f);
        panelRect.sizeDelta = new Vector2(620f, 105f);

        CreateTMPText(panelRect, "PitPrompt", "距维修区入口 10 格内：是否预定进站？", 18,
            new Vector2(0f, 32f), new Vector2(580f, 28f),
            FindObjectOfType<TMP_Text>()?.font);

        pitEnterButton = CreateActionButton(panelRect, "PitEnterButton", "预定进站（过入口后停1回合）",
            new Vector2(-160f, -15f), new Color(0.45f, 0.85f, 0.55f),
            () => ChoosePit(true));
        pitSkipButton = CreateActionButton(panelRect, "PitSkipButton", "继续比赛",
            new Vector2(160f, -15f), new Color(0.8f, 0.8f, 0.8f),
            () => ChoosePit(false));

        pitChoicePanel.SetActive(false);
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
                btn.onClick.AddListener(() => OnGearButtonClicked(capturedGear));
            }
        }
    }

    // ====== 初始化 ======

    private void InitializeGame()
    {
        int startFinishNodeIndex = trackManager.StartFinishNodeIndex;

        tutorialScenario = TutorialLaunchState.ActivateRequested();
        careerRaceLaunch = tutorialScenario == null
            ? CareerRaceLaunchState.ActivateRequested()
            : null;
        if (tutorialScenario != null)
            CareerRaceLaunchState.Clear();
        careerResultRecorded = false;
        careerInitializationFailure = string.Empty;
        if (careerRaceLaunch != null && !ValidateCareerRaceLaunch(out careerInitializationFailure))
            return;
        tutorialDirector = tutorialScenario != null
            ? new TutorialRuntimeDirector(tutorialScenario, initializeTutorialInPractice)
            : null;
        initializeTutorialInPractice = false;
        pendingTutorialOpponentCue = null;
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
        session.TeamVehicleBonusesEnabled = tutorialScenario == null;
        if (tutorialScenario != null && tutorialScenario.opponentScript.Count > 0)
            session.SlipstreamRangeOverride = tutorialScenario.opponentScript[0].expectedSlipstreamDistance;
        aiControllers.Clear();
        weatherState.Reset();
        raceTurnNumber = 0;

        // 人类玩家（Players[0]）
        DriverProfile humanDriver = tutorialScenario != null
            ? DriverCatalog.GetDefaultForTeam(tutorialScenario.playerTeam)
            : careerRaceLaunch != null
                ? DriverCatalog.GetDefaultForTeam(careerRaceLaunch.PlayerTeam)
                : DriverSelectionState.ResolveDriver(config.playerDriverId, config.playerTeam);
        var human = new PlayerState("你", false, startFinishNodeIndex, config.minGear);
        human.driverId = humanDriver.Id;
        human.driverXp = tutorialScenario != null ? 0 : DriverProgressStore.Load(humanDriver.Id);
        SetupPlayerForRace(
            human,
            humanDriver.Team,
            careerRaceLaunch != null ? careerRaceLaunch.TechSnapshot : null);
        session.Players.Add(human);
        human.driverSkill.Initialize(
            humanDriver,
            human.DriverLevel,
            tutorialScenario == null);

        // AI 对手
        int aiCount = tutorialScenario != null
            ? tutorialScenario.opponentCount
            : careerRaceLaunch != null
                ? careerRaceLaunch.Competitors.Count - 1
                : Mathf.Clamp(config.aiOpponentCount, 0, 3);
        for (int i = 0; i < aiCount; i++)
        {
            TeamId team = tutorialScenario != null
                ? tutorialScenario.opponentTeam
                : careerRaceLaunch != null
                    ? GetCareerOpponentTeam(i)
                    : (i < config.aiTeams.Length ? config.aiTeams[i] : TeamId.JP);
            DriverProfile aiDriver = DriverCatalog.GetDefaultForTeam(team);
            var aiState = new PlayerState($"AI{i + 1}", true, startFinishNodeIndex, config.minGear);
            aiState.driverId = aiDriver.Id;
            aiState.driverXp = 0;
            SetupPlayerForRace(aiState, team);
            aiState.driverSkill.Initialize(aiDriver, aiState.DriverLevel, false);
            session.Players.Add(aiState);
            var ctrl = gameObject.AddComponent<AIController>();
            ctrl.Initialize(
                this,
                aiState,
                tutorialScenario != null
                    ? new SystemRandomSource(TutorialScenarioDefinition.RuntimeSeed + i + 1)
                    : null);
            aiControllers[aiState] = ctrl;
        }

        SpawnCars();
        BeginRaceTestLog(humanDriver, human);

        if (hudUI != null)
        {
            hudUI.AppendLog(tutorialScenario != null
                ? $"教程车辆: UK / {humanDriver.DisplayName}（车手增益关闭）"
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

    private void PresentNextTutorialLesson()
    {
        ApplyTutorialPendingCue();
        TutorialStepDefinition nextStep = tutorialDirector.CurrentStep;
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

        if (cue.Weather != null && session != null)
        {
            WeatherType? scriptedWeather = WeatherRules.ParseWeather(cue.Weather.weatherId);
            if (scriptedWeather.HasValue)
            {
                session.Weather = scriptedWeather.Value;
                raceLogWriter?.Append(
                    $"[TUTORIAL_CUE] type=weather step={cue.Weather.step} " +
                    $"weather={cue.Weather.weatherId} applied=true");
                hudUI?.AppendLog($"<color=cyan>教程脚本天气：{session.WeatherLabel}</color>");
                hudUI?.Refresh(this, Player, AI, session.Players);
            }
            else
            {
                raceLogWriter?.Append(
                    $"[TUTORIAL_CUE] type=weather step={cue.Weather.step} " +
                    $"weather={cue.Weather.weatherId} applied=false reason=unknown_weather");
            }
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
            PresentNextTutorialLesson();
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
        if (tutorialDirector != null)
        {
            tutorialDirector.ExitTutorial();
            FlushTutorialEvents();
            raceLogWriter?.Append("[TUTORIAL_PRACTICE] event=exit_requested");
        }

        StopAllCoroutines();
        if (raceLogWriter != null && raceLogWriter.IsActive)
            raceLogWriter.End("tutorial exited without normal rewards");
        SceneLoader.LoadMainMenu();
    }

    private void BeginRaceTestLog(DriverProfile humanDriver, PlayerState human)
    {
        if (raceLogWriter == null)
            raceLogWriter = new RaceTestLogWriter();
        else if (raceLogWriter.IsActive)
            raceLogWriter.End("race reset");

        // The menu selection is resolved by TrackManager before the race
        // starts.  The config asset can still contain its default track ID, so
        // log the track that was actually loaded rather than the asset value.
        string trackId = trackManager != null
            ? trackManager.TrackId
            : (config != null ? TrackSelectionState.ResolveTrackId(config.trackId) : "");
        string trackName = trackManager != null && trackManager.LoadedTrackConfig != null
            ? trackManager.LoadedTrackConfig.trackName
            : trackId;
        raceLogWriter.BeginRace(trackId, trackName, human.name, human.teamId, session.Players.Count - 1);
        if (tutorialScenario != null)
        {
            raceLogWriter.Append(
                $"[TUTORIAL_SETUP] event=runtime_ready scenario={tutorialScenario.id} " +
                $"phase={tutorialDirector.Phase}");
            raceLogWriter.Append(
                $"[TUTORIAL_SETUP] event=exact_deck_loaded " +
                $"count={tutorialScenario.exactDrawOrder.Count} opening={tutorialScenario.openingHandSize}");
            raceLogWriter.Append(
                $"[TUTORIAL_SETUP] event=weather_script_ready " +
                $"start={tutorialScenario.guidedStartWeatherId} practice={tutorialScenario.practiceWeatherId}");
            raceLogWriter.Append(
                $"[TUTORIAL_SETUP] event=player_checkpoints_ready " +
                $"count={tutorialScenario.playerCheckpoints.Count} exact_zones=true");
            if (tutorialScenario.tutorialPitLane != null)
            {
                raceLogWriter.Append(
                    $"[TUTORIAL_SETUP] event=virtual_pit_ready " +
                    $"entry={tutorialScenario.tutorialPitLane.entryCell} " +
                    $"exit={tutorialScenario.tutorialPitLane.exitCell} official_track_mutated=false");
            }
            TutorialOpponentCue cue = tutorialScenario.opponentScript.Count > 0
                ? tutorialScenario.opponentScript[0]
                : null;
            if (cue != null)
            {
                raceLogWriter.Append(
                    $"[TUTORIAL_SETUP] event=opponent_script_ready step={cue.step} " +
                    $"leader={cue.leaderCell} player={cue.playerCell} distance={cue.expectedSlipstreamDistance}");
            }
        }
        else if (careerRaceLaunch != null)
        {
            raceLogWriter.Append(
                $"[CAREER_SETUP] result_id={careerRaceLaunch.ResultId} " +
                $"race={careerRaceLaunch.RaceIndex + 1}/{CareerModeRules.RaceCount} " +
                $"track={careerRaceLaunch.TrackId} team={careerRaceLaunch.PlayerTeam} " +
                $"competitors={careerRaceLaunch.Competitors.Count} tech_snapshot=true");
        }
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

            raceLogWriter.Append(
                $"[STATE] {p.name} role={(p.isAI ? "AI" : "PLAYER")} lap={p.lap} position={p.position} " +
                $"gear={p.gear} engine_heat={p.deck.heatPool.remaining} hand_speed={p.deck.CountSpeedInHand()} " +
                $"blown={p.isBlown} finished={p.hasFinished}");
        }
    }

    private void LogPlayedCards(PlayerState player, string source)
    {
        if (raceLogWriter == null || player == null)
            return;

        string values = "";
        for (int i = 0; i < player.playedSpeedCardsThisTurn.Count; i++)
        {
            if (i > 0)
                values += ",";
            values += player.playedSpeedCardsThisTurn[i].value;
        }

        TeamGearRules.SpeedCardRequirement requirement = GetSpeedCardRequirement(player);
        raceLogWriter.Append(
            $"[CARDS] {player.name} source={source} count={player.playedSpeedCardsThisTurn.Count} values=[{values}] " +
            $"gear_limit={requirement.TotalCardCount} base_limit={requirement.BaseCardCount} " +
            $"extra_slots={requirement.ExtraCardCount}");
    }

    /// <summary>
    /// 单个玩家的比赛初始化：车队分配、科技树、独立引擎热量池，以及速度/特技普通牌组。
    /// </summary>
    private void SetupPlayerForRace(
        PlayerState p,
        TeamId teamId,
        CareerTechSnapshot careerTechSnapshot = null)
    {
        p.teamId = teamId;
        p.usesChinaGearSystem = teamId == TeamId.CN;
        int poolSize = tutorialScenario != null
            ? tutorialScenario.engineHeatCapacity
            : TeamVehicleRules.GetBaseHeatPoolSize(teamId, config.heatPoolPerPlayer);

        if (tutorialScenario != null)
        {
            p.techState = null;
        }
        else if (careerTechSnapshot != null)
        {
            if (!CareerTechSnapshotMapper.TryCreateRuntimeState(
                    careerTechSnapshot, session.TechDb, out TechTreeState careerTechState))
            {
                Debug.LogError("[CAREER] Invalid technology snapshot; career race has no active technology.");
                p.techState = null;
                careerInitializationFailure = "科技快照无法映射到当前科技数据库";
            }
            else
            {
                p.techState = careerTechState;
                TechTreeRules.ResetPerRaceState(p.techState);
                poolSize = session.EffectiveHeatPoolSize(p, poolSize);
            }
        }
        else if (config.enableTechTree)
        {
            p.techState = p.isAI
                ? session.CreateDemoTechState(teamId)
                : TechTreeProfileStore.GetOrCreate(teamId, session.TechDb);
            TechTreeRules.ResetPerRaceState(p.techState);
            poolSize = session.EffectiveHeatPoolSize(p, poolSize);

            // JP L2 汤底：demo 自动选择（None=不选）
            if (config.jpDemoBroth != BrothType.None && session.GetModifiers(p).hasBrothSelection)
                TechTreeRules.SelectBroth(p.techState, config.jpDemoBroth);

            // UK L3 日不落：demo 自动复制目标国 L2/L3 专属 flag
            if (config.enableUkSunNeverSetsDemo && session.GetModifiers(p).hasSunNeverSets)
                p.techState.sunNeverSetsTarget = config.ukSunNeverSetsTargetTeam;
        }
        else
        {
            p.techState = null;
        }

        p.trickState = new TrickCardState();
        p.trickState.ResetPerRace();
        if (tutorialScenario != null)
        {
            List<CardData> exactCards = p.isAI
                ? tutorialScenario.CreateOpponentDeck()
                : tutorialScenario.CreateExactDeck();
            p.deck.InitializeExactOrder(exactCards, new HeatPool(poolSize));
            p.deck.DrawToHand(tutorialScenario.openingHandSize);
        }
        else
        {
            p.deck.InitializeDeck(config, new HeatPool(poolSize));
            if (config.enableTrickCards)
                p.deck.AddTrickCardsToDrawPile(session.CreateInitialTrickCards(teamId));
            p.deck.DrawToHand(session.EffectiveHandSize(p, config.handSize));
        }

        if (tutorialScenario == null && !p.isAI && teamId == TeamId.CN && config.ensurePlayerAttackTrickInOpeningHand)
        {
            string attackId = session.TrickDb.GetAttackId(teamId);
            if (!p.deck.EnsureTrickCardInHand(attackId))
                Debug.LogWarning($"[MVPGameManager] 无法保证中国队 ATTACK 牌 {attackId} 进入开局手牌。");
        }
    }

    private TeamId GetCareerOpponentTeam(int opponentIndex)
    {
        int current = 0;
        for (int i = 0; i < careerRaceLaunch.Competitors.Count; i++)
        {
            TeamId team = careerRaceLaunch.Competitors[i];
            if (team == careerRaceLaunch.PlayerTeam)
                continue;
            if (current++ == opponentIndex)
                return team;
        }
        return TeamId.JP;
    }

    private bool ValidateCareerRaceLaunch(out string failureReason)
    {
        failureReason = string.Empty;
        CareerLoadResult loaded = CareerRuntimeRepository.CreateDefault().Load();
        if (loaded.Status != CareerLoadStatus.Loaded)
        {
            failureReason = loaded.Status == CareerLoadStatus.Invalid
                ? "生涯存档校验失败"
                : "未找到当前生涯存档";
            return false;
        }

        if (!careerRaceLaunch.Matches(loaded.State))
        {
            failureReason = "启动请求与当前生涯存档不一致";
            return false;
        }

        string loadedTrackId = trackManager != null && trackManager.LoadedTrackConfig != null
            ? trackManager.LoadedTrackConfig.trackId
            : string.Empty;
        if (!string.Equals(loadedTrackId, careerRaceLaunch.TrackId, System.StringComparison.Ordinal))
        {
            failureReason = "指定正式赛道加载失败，已阻止 fallback 赛道计入生涯";
            return false;
        }
        return true;
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
        trackManager.BindPlayerReadability(PlayerCarTransform);
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
            raceTurnNumber++;
            raceLogWriter?.Append($"[TURN_START] turn={raceTurnNumber} weather={WeatherLabel}");
            LogPlayerSnapshots();
            // ──── 回合开始 ────
            raceCameraController?.BeginTurn();
            foreach (var p in session.Players)
                session.BeginTurn(p);
            // JP L3 万骨涌：末位/次末位自动激活，逐回合递减
            TickBankuruwaseForAll();

            var turnOrder = session.GetTurnOrder(); // 末位先行（追赶优势）
            var turnSkipped = new HashSet<PlayerState>(); // 本回合被跳过的玩家（失控/维修区）

            // ====== PHASE A1: 档位决策 ======
            foreach (var p in turnOrder)
            {
                // 已完赛或已爆缸的赛车不再参与后续回合；否则在玩家 DNF
                // 后比赛继续时，A1 仍会向玩家请求档位输入。
                if (p.isBlown || p.hasFinished)
                {
                    turnSkipped.Add(p);
                    continue;
                }

                // 维修区预选在上一回合越过入口时登记；本回合开始才真正执行，
                // 因此“停一回合 + 出口后前移”不会发生在入口提示的同一回合。
                if (p.pitStopScheduled)
                {
                    ExecuteScheduledPitStop(p);
                    yield return WaitForTutorialNavigation();
                    turnSkipped.Add(p);
                    continue;
                }

                if (RaceTurnRules.ShouldSkip(p))
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
                        hudUI.SetStatus($"选择档位 (当前: {TeamGearRules.GetDisplayName(p.teamId, p.gear)})");
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

                // UK L2 英式全餐：手牌同时有热/速/特技 → 尾流距离+1 + 1 张限时热量牌
                if (p.techState != null && config.enableTechTree &&
                    session.GetModifiers(p).hasFullEnglish &&
                    TechTreeRules.ShouldTriggerFullEnglish(
                        p.deck.CountHeatInHand() > 0,
                        p.deck.CountSpeedInHand() > 0,
                        TrickCardRules.CountTricksInHand(p.deck) > 0))
                {
                    p.slipstreamRangeBonusThisTurn = 1;
                    p.deck.AddCardsToHand(new List<CardData> { CardData.CreateTempHeat() });
                    if (hudUI != null)
                        hudUI.AppendLog($"{p.name} 英式全餐：尾流距离+1，获得 1 张限时热量牌。");
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
            phaseState.BeginAnimation();
            raceLogWriter?.Append("[MOVE_PHASE] begin");
            foreach (var p in turnOrder)
            {
                if (RaceTurnRules.IsInactive(p, turnSkipped)) continue;

                int oldPos = p.position;
                int rawEnd = oldPos + p.cornerTotalThisTurn;

                // 移动 → 反应(冷却) → 弯道判定
                yield return StartCoroutine(AnimateMovement(p, GetCarIndex(p)));
                ReactStep(p);
                bool completedCorner = ResolveCorners(p, oldPos, rawEnd);
                session.ArmItalyCornerExitBonus(p, completedCorner);

                if (!p.isAI)
                {
                    TryAdvanceTutorialIfExpected(
                        TutorialAction.ResolveSpeedMovement,
                        $"from:{oldPos},to:{p.position},movement:{p.totalMovementThisTurn}");
                }

                ResolveLandmarkPasses(p, oldPos, oldPos + p.totalMovementThisTurn);
                RegisterPitEntryCrossing(p, oldPos, oldPos + p.totalMovementThisTurn);
                yield return WaitForTutorialNavigation();
            }
            raceLogWriter?.Append("[MOVE_PHASE] end");

            // ====== PHASE C：回合结束时按实际落位结算尾流 ======
            // 尾流不能在回合开始或基础移动前触发；此处所有车辆都已完成
            // 移动、反应和弯道判定，规则层读取的是本回合结束时的实际位置。
            ApplyPendingTutorialOpponentCue();
            ResolveSlipstreamsAtTurnEnd(turnOrder, turnSkipped);
            if (Player != null &&
                slipstreamsThisTurn.TryGetValue(Player, out SlipstreamChainResult tutorialSlipstream) &&
                tutorialSlipstream.Triggered)
            {
                TryAdvanceTutorialIfExpected(
                    TutorialAction.ResolveSlipstream,
                    $"leader:{tutorialSlipstream.Steps[0].Leader.position},bonus:{tutorialSlipstream.TotalBonus}");
            }
            yield return StartCoroutine(PlaySlipstreamPhase(turnOrder, turnSkipped));
            yield return StartCoroutine(ApplySlipstreamMovement(turnOrder, turnSkipped));
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
        if (p.skipNextTurn)
        {
            p.skipNextTurn = false;
            p.gear = TeamGearRules.IsChina(p.teamId)
                ? ChinaGearShiftRules.RecoverGear
                : config.minGear;
            p.chinaConsecutiveGearCount = 0;
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

        int spinIncrement = 1;
        if (session != null)
            spinIncrement += WeatherRules.GetExtraSpinCounter(session.Weather);
        p.spinCounter += spinIncrement;
        int spinMax = session != null ? session.EffectiveSpinMax(p) : 3;
        bool eliminated = p.spinCounter >= spinMax;

        // 回收全部热量回引擎
        RecoverAllHeatWithPresentation(p);

        // 回退位置
        p.position = rewindPos;

        // 强制 1 档
        p.gear = TeamGearRules.IsChina(p.teamId)
            ? ChinaGearShiftRules.RecoverGear
            : config.minGear;
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

        amount = DriverSkillRules.ApplyHeatMultiplier(p?.driverSkill, amount);
        if (p?.driverSkill != null)
            amount = Mathf.Max(0, amount - p.driverSkill.ConsumePassiveHeatDiscount());

        // 特技牌：黑面包垫底 — 本次热量支付 -1（最少 1）
        amount = TrickCardRules.ApplySchwarzbrot(p.trickState, amount);
        if (amount <= 0) return true;

        int drawn = p.deck.DrawHeatFromPool(amount, destination);
        if (drawn < amount)
        {
            // UK L1 炸鱼薯条：每场限 1 次 — 忽略本次热量判定，回收 1 张热量牌至引擎
            if (p.techState != null && session != null &&
                TechTreeRules.CanUseFishAndChips(p.techState, session.TechDb))
            {
                TechTreeRules.UseFishAndChips(p.techState);
                p.deck.RemoveOneHeatFromDeck();
                if (hudUI != null)
                    hudUI.AppendLog($"<color=green>{p.name} 炸鱼薯条！忽略本次热量判定，回收 1 张热量牌至引擎。</color>");
                return true;
            }

            HandleSpin(p, rewindPos, reason);
            return false;
        }

        // DE L3 烤肉拼盘跟踪 + CN L2 连击追踪（付热）
        if (session != null)
            session.TrackHeatPaid(p, drawn);
        if (p.techState != null)
            TechTreeRules.TrackDimSumCombo(p.techState, false, false, true);

        if (!p.isAI && cardHandUI != null)
        {
            CardVisualZone target = destination == HeatPaymentDestination.Discard
                ? CardVisualZone.DiscardPile
                : CardVisualZone.Hand;
            cardHandUI.PlayHeatTransitions(drawn, CardVisualZone.Engine, target);
            cardHandUI.UpdateDeckInfo(p);
        }
        if (!p.isAI)
        {
            AudioService.PlaySfx(AudioEventNames.HeatPay);
            TryAdvanceTutorialIfExpected(
                TutorialAction.PayHeat,
                $"amount:{drawn},destination:{destination},reason:{reason}");
        }
        return true;
    }

    // ====== 档位处理 ======

    private void ApplyGearShift(PlayerState p, int targetGear)
    {
        int previousGear = p.gear;
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

        int shiftHeatCost = DriverSkillRules.ApplyGearHeatCost(
            p.driverSkill, shift.HeatCost, false);
        if (TryPayHeat(p, shiftHeatCost, p.position, "shift 2 gears"))
        {
            p.gear = shift.TargetGear;
            p.chinaConsecutiveGearCount = shift.IsChina ? shift.ConsecutiveCount : 0;
            if (previousGear != p.gear)
                AudioService.PlaySfx(AudioEventNames.GearShift);

            // China Go overclock heat is a distinct cost from the standard
            // two-gear shift payment and follows the normal spin-out path.
            int additionalHeat = DriverSkillRules.ApplyGearHeatCost(
                p.driverSkill, shift.AdditionalHeat, true);
            if (additionalHeat > 0)
                TryPayHeat(p, additionalHeat, p.position,
                    $"{TeamGearRules.GetDisplayName(p.teamId, p.gear)} overclock");
        }
        else if (!p.isAI)
        {
            AudioService.PlaySfx(AudioEventNames.GearFailure);
        }
        // 若 TryPayHeat 失败（失控），HandleSpin 已将档位设为最低档

        p.selectedGearThisTurn = p.gear;
        raceLogWriter?.Append(
            $"[GEAR] {p.name} role={(p.isAI ? "AI" : "PLAYER")} requested={targetGear} " +
            $"selected={p.gear} engine_heat={p.deck.heatPool.remaining}");
    }

    // ====== 步骤 5：反应（冷却） ======

    /// <summary>
    /// HEAT 规则书步骤 5：根据档位执行冷却。
    /// G1 = 冷却 3，G2 = 冷却 1，G3/G4 = 无冷却。
    /// 科技加成：JP 盐味汤底 / 万骨涌额外冷却。
    /// </summary>
    private void ReactStep(PlayerState p)
    {
        if (p.isBlown || p.hasFinished) return;

        int cooldown = TeamGearRules.GetCooldown(
            p.teamId, p.gear, p.chinaConsecutiveGearCount,
            config.gearOneCooldown, config.gearTwoCooldown);

        // Standard teams layer their cooling-efficiency stat on the normal
        // G1/G2 reaction step. China's Recover cooldown is self-contained and
        // remains governed solely by ChinaGearShiftRules.
        if (!TeamGearRules.IsChina(p.teamId) && session.TeamVehicleBonusesEnabled)
            cooldown += TeamVehicleRules.GetCooling(p.teamId);

        if (p.techState != null)
        {
            cooldown += TechTreeRules.GetBrothCooldownPerTurn(p.techState);
            cooldown += TechTreeRules.GetBankuruwaseCooldownPerTurn(p.techState);
        }

        if (p.driverSkill != null)
            cooldown += p.driverSkill.PassiveCoolingBonusThisTurn;

        if (session != null && !DriverSkillRules.IsWeatherImmune(p.driverSkill))
            cooldown = WeatherRules.ApplyWeatherToCooling(cooldown, session.Weather);

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

        int handHeat = p.deck.CountHeatInHand();
        int drawHeat = p.deck.CountHeatInDrawPile();
        int discardHeat = p.deck.CountHeatInDiscardPile();
        int cooled = p.deck.CoolHeat(amount);
        if (!p.isAI && cooled > 0 && cardHandUI != null)
        {
            int remaining = cooled;
            int fromHand = Mathf.Min(remaining, handHeat);
            remaining -= fromHand;
            int fromDraw = Mathf.Min(remaining, drawHeat);
            remaining -= fromDraw;
            int fromDiscard = Mathf.Min(remaining, discardHeat);

            cardHandUI.PlayHeatTransitions(fromHand, CardVisualZone.Hand, CardVisualZone.Engine);
            cardHandUI.PlayHeatTransitions(fromDraw, CardVisualZone.DrawPile, CardVisualZone.Engine);
            cardHandUI.PlayHeatTransitions(fromDiscard, CardVisualZone.DiscardPile, CardVisualZone.Engine);
            cardHandUI.UpdateDeckInfo(p);
        }
        if (!p.isAI && cooled > 0)
        {
            AudioService.PlaySfx(AudioEventNames.HeatCool);
            TryAdvanceTutorialIfExpected(
                TutorialAction.CoolHeatCard,
                $"cooled:{cooled},requested:{amount}");
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
            cardHandUI.UpdateDeckInfo(p);
        }
        if (!p.isAI && cooled > 0)
            AudioService.PlaySfx(AudioEventNames.HeatCool);
        return cooled;
    }

    private void RecoverAllHeatWithPresentation(PlayerState p)
    {
        if (p == null || p.deck == null)
            return;

        int handHeat = p.deck.CountHeatInHand();
        int drawHeat = p.deck.CountHeatInDrawPile();
        int discardHeat = p.deck.CountHeatInDiscardPile();
        p.deck.RecoverAllHeatToPool();

        if (!p.isAI && cardHandUI != null)
        {
            cardHandUI.PlayHeatTransitions(handHeat, CardVisualZone.Hand, CardVisualZone.Engine);
            cardHandUI.PlayHeatTransitions(drawHeat, CardVisualZone.DrawPile, CardVisualZone.Engine);
            cardHandUI.PlayHeatTransitions(discardHeat, CardVisualZone.DiscardPile, CardVisualZone.Engine);
            cardHandUI.UpdateDeckInfo(p);
        }
        if (!p.isAI && handHeat + drawHeat + discardHeat > 0)
            AudioService.PlaySfx(AudioEventNames.HeatCool);
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
            p.position = (p.position + Mathf.Max(0, totalMove)) % trackManager.TotalNodes;
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
                yield return new WaitForSeconds(leadDelay);
        }

        for (int i = p.position + 1; i <= targetPos; i++)
        {
            int nodeIdx = i % totalNodes;
            Vector3 target = trackManager.GetNodePosition(nodeIdx, laneIndex);

            yield return StartCoroutine(GetCarMovementAnimator().MoveToNode(car, target));

            // 检测跨过起点/终点线
            if (trackManager.GetNode(nodeIdx).isStartFinish)
            {
                OnPlayerCrossedStartFinish(p);
                if (p == Player && !p.hasFinished && trackManager.AllowsStartFinishLaneChange)
                    yield return StartCoroutine(WaitForIndianapolisLaneChoice());
            }

            yield return nodeWait;
        }

        p.position = targetPos % totalNodes;
        RefreshVisualCarLanes();

        int overtakeCount = 0;
        overtakesThisTurn.TryGetValue(p, out overtakeCount);
        if (playOvertake && overtakeCount > 0 && raceEventFX != null)
        {
            // Keep the movement camera on the winner for a dedicated
            // slow-motion close-up before returning to normal race pacing.
            raceCameraController?.BeginVehicleMovement(car.transform);
            yield return StartCoroutine(raceEventFX.PlayOvertake(car.transform, overtakeCount));
        }

        if (totalMove > 0)
        {
            float trailDelay = GameSettingsRuntime.ScaleAnimationDuration(
                config.movementFocusTrailDelay);
            if (trailDelay > 0f)
                yield return new WaitForSeconds(trailDelay);
            raceCameraController?.EndVehicleMovement();
        }
    }

    // ====== 移动力计算（科技 + 特技加成） ======

    /// <summary>Returns the base and extra speed-card slots for the current turn.</summary>
    public TeamGearRules.SpeedCardRequirement GetSpeedCardRequirement(PlayerState p)
    {
        int extraSlots = p.extraCardSlotsThisTurn;
        if (TrickCardRules.HasHotpotAttack(p.trickState))
            extraSlots += 1;

        return TeamGearRules.GetSpeedCardRequirement(
            p.teamId, p.gear, p.chinaConsecutiveGearCount, extraSlots);
    }

    /// <summary>Formats the base-versus-extra slot breakdown for player feedback.</summary>
    public string GetSpeedCardRequirementLabel(PlayerState p)
    {
        TeamGearRules.SpeedCardRequirement requirement = GetSpeedCardRequirement(p);
        string gearName = TeamGearRules.GetDisplayName(p.teamId, p.gear);
        return requirement.ExtraCardCount > 0
            ? $"{gearName} 档（基础 {requirement.BaseCardCount} + 额外 {requirement.ExtraCardCount}）"
            : $"{gearName} 档";
    }

    /// <summary>本回合最大可出速度牌数 = 档位基础要求 + 额外槽。</summary>
    public int GetMaxSpeedCardsThisTurn(PlayerState p)
    {
        return GetSpeedCardRequirement(p).TotalCardCount;
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
            p.cornerTotalThisTurn = RaceRules.SumCardValues(p.playedSpeedCardsThisTurn) +
                DriverSkillRules.GetSpeedPerCardBonus(p.driverSkill) * p.playedSpeedCardsThisTurn.Count;
        }

        // 第二轮：加成（需要弯道信息与对手移动）
        foreach (var p in turnOrder)
        {
            if (RaceTurnRules.IsInactive(p, turnSkipped)) continue;

            int rawEnd = p.position + p.cornerTotalThisTurn;
            int lane = GetLane(p);
            bool crossedCorner = trackManager.GetUniqueCornersCrossed(p.position, rawEnd).Count > 0;

            int bonus = session.ComputeMovementBonus(p, crossedCorner);
            bonus += session.ConsumeItalyCornerExitBonus(p);
            // DE L2 猪肘悬挂：过弯 → 出弯后 +1 移动（弯道判定在 ResolveCorners 跳过）
            if (p.techState != null && TechTreeRules.ShouldTriggerWurstplatte(p.techState, session.TechDb, crossedCorner))
                bonus += 1;
            // JP L1 寿司：速度精确等于弯道限速 → 每弯 +2
            bonus += GetNigiriBonus(p, crossedCorner, rawEnd, lane);
            // JP 特技牌 鱼雷天妇罗：超车 +1
            bonus += GetTorpedoBonus(p, turnOrder);
            // CN 特技牌 火锅底料：ATTACK 牌 +1（不计入弯道判定）
            bonus += CardPlayRules.GetHotpotMovementBonus(p);
            // 特技牌即时移动（司康 +2 等）
            bonus += p.trickMoveBonusThisTurn;
            bonus += DriverSkillRules.GetMovementBonus(p.driverSkill, crossedCorner);
            bonus -= DriverSkillRules.GetGutterMovementPenalty(p.driverSkill);
            if (p.driverSkill != null)
                bonus += p.driverSkill.PassiveMovementBonusThisTurn;

            // DE L1 黑啤酒燃料：每圈一次，付 1 热 → +2 移动
            // （自动激活；引擎预留 1 热防失控）。
            if (p.techState != null && config.enableTechTree &&
                session.GetModifiers(p).hasSchwarzbierFuel &&
                p.deck.heatPool != null && p.deck.heatPool.remaining > 1 &&
                TechTreeRules.CanTriggerSchwarzbierFuelThisLap(p.techState, p.lap))
            {
                if (TryPayHeat(p, 1, p.positionAtTurnStart, "schwarzbier fuel"))
                {
                    TechTreeRules.UseSchwarzbierFuel(p.techState, p.lap);
                    bonus += 2;
                    if (hudUI != null)
                        hudUI.AppendLog($"{p.name} 黑啤酒燃料（本圈一次）：付 1 热 → +2 移动。");
                }
            }

            // US L1 得来速：经过地标（起点线/中点）→ +1 移动
            if (p.techState != null && config.enableTechTree &&
                session.GetModifiers(p).hasDriveThru)
            {
                var (lm1, lm2) = RaceSession.GetLandmarks(trackManager.TotalNodes);
                if (RaceSession.CrossedLandmark(p.position, rawEnd, lm1, trackManager.TotalNodes) ||
                    RaceSession.CrossedLandmark(p.position, rawEnd, lm2, trackManager.TotalNodes))
                {
                    bonus += 1;
                    if (hudUI != null)
                        hudUI.AppendLog($"{p.name} 得来速：经过地标 +1 移动。");
                }
            }

            // US L2 美式烧烤：处于 BBQ 区（地标 5 格内）→ +2 移动（近似：热量当 2 速）
            if (p.techState != null && config.enableTechTree &&
                session.GetModifiers(p).hasSmokedBBQ &&
                RaceSession.IsInBBQZone(rawEnd % trackManager.TotalNodes, trackManager.TotalNodes))
            {
                bonus += 2;
                if (hudUI != null)
                    hudUI.AppendLog($"{p.name} 美式烧烤区：+2 移动。");
            }

            // 先保存不含尾流的基础移动。尾流必须等所有车辆完成这段移动、
            // 反应和弯道判定后，依据实际落位在回合末结算。
            p.totalMovementThisTurn = p.cornerTotalThisTurn + bonus;
            p.totalMovementThisTurn = Mathf.Max(0, p.totalMovementThisTurn);
            raceLogWriter?.Append(
                $"[MOVE_PLAN] {p.name} role={(p.isAI ? "AI" : "PLAYER")} position={p.position} " +
                $"corner_speed={p.cornerTotalThisTurn} non_slipstream_bonus={bonus} " +
                $"base_total={p.totalMovementThisTurn}");
        }


        // Mansell's upgraded charge rewards a real overtake after every base
        // movement has been planned, so the result is independent of loop order.
        foreach (var p in turnOrder)
        {
            if (RaceTurnRules.IsInactive(p, turnSkipped)) continue;
            int projectedOvertakes = RaceMovementRules.CountOvertakes(
                p, turnOrder, trackManager.TotalNodes, true, RaceTurnRules.ShouldSkip);
            int driverOvertakeBonus = DriverSkillRules.GetOvertakeBonus(p.driverSkill, projectedOvertakes);
            if (driverOvertakeBonus > 0)
                p.totalMovementThisTurn += driverOvertakeBonus;
        }

        // Overtake presentation belongs to the base movement pass. Tailwind is
        // resolved later from the settled positions and gets its own phase.
        foreach (var p in turnOrder)
        {
            if (RaceTurnRules.IsInactive(p, turnSkipped))
            {
                overtakesThisTurn[p] = 0;
                continue;
            }
            overtakesThisTurn[p] = RaceMovementRules.CountOvertakes(
                p, turnOrder, trackManager.TotalNodes, true, RaceTurnRules.ShouldSkip);
        }
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

        var settledMovements = new Dictionary<PlayerState, int>(session.Players.Count);
        foreach (PlayerState racer in session.Players)
        {
            if (racer != null)
                settledMovements[racer] = 0;
        }

        var resolvedChains = new Dictionary<PlayerState, SlipstreamChainResult>();
        foreach (PlayerState follower in turnOrder)
        {
            if (RaceTurnRules.IsInactive(follower, turnSkipped))
            {
                slipstreamsThisTurn[follower] = default;
                continue;
            }

            SlipstreamChainResult chain = session.ComputeSlipstreamChain(
                follower,
                session.Players,
                trackManager.TotalNodes,
                settledMovements,
                2,
                turnOrder);
            slipstreamsThisTurn[follower] = chain;
            resolvedChains[follower] = chain;
        }

        // Resolve every chain from the same settled base state before adding
        // any bonus. Otherwise an earlier follower's tailwind could alter its
        // tie-break movement and accidentally make a second car look like a
        // front car in the same-cell case.
        foreach (PlayerState follower in turnOrder)
        {
            if (!resolvedChains.TryGetValue(follower, out SlipstreamChainResult chain))
                continue;
            int baseMovement = follower.totalMovementThisTurn;
            follower.totalMovementThisTurn += chain.TotalBonus;
            raceLogWriter?.Append(
                $"[MOVE_PLAN_FINAL] {follower.name} position={follower.position} " +
                $"base_total={baseMovement} tailwind_bonus={chain.TotalBonus} " +
                $"total={follower.totalMovementThisTurn}");
        }
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
            hudUI.SetStatus("尾流阶段结束 · 执行额外移动");

        if (raceEventFX != null)
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

        var (lm1, lm2) = RaceSession.GetLandmarks(trackManager.TotalNodes);
        if (RaceSession.CrossedLandmark(oldPos, rawMovementEnd, lm1, trackManager.TotalNodes))
            ResolveMotherRoadPass(p, 0, oldPos);
        if (RaceSession.CrossedLandmark(oldPos, rawMovementEnd, lm2, trackManager.TotalNodes))
            ResolveMotherRoadPass(p, 1, oldPos);
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
        if (p.pitStopRequested && !p.isBlown && !p.hasFinished)
        {
            p.pitStopRequested = false;
            p.pitStopScheduled = true;
            if (hudUI != null)
                hudUI.AppendLog($"{p.name} 已越过维修区入口，下一回合执行进站。");
        }
        else
        {
            // Passing without a reservation leaves the next approach window
            // available on the following lap.
            p.pitStopRequested = false;
            p.pitChoiceResolvedThisLap = false;
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
            hudUI.SetStatus("移动阶段结束 · 正在结算尾流");
        if (cardHandUI != null)
            yield return StartCoroutine(cardHandUI.WaitForCardTransitions());
        yield return new WaitForSecondsRealtime(0.12f);

        raceLogWriter?.Append(
            $"[SLIPSTREAM_PHASE] begin events={events.Count} " +
            $"visuals={(raceEventFX != null ? "enabled" : "disabled")} " +
            $"time_scale_before={Time.timeScale:F2}");
        if (hudUI != null)
            hudUI.SetStatus($"尾流阶段：{events.Count} 段气流，额外移动即将执行");

        if (raceEventFX != null)
        {
            Transform focus = events[0].Follower != null
                ? events[0].Follower
                : events[0].Leader;
            raceCameraController?.BeginVehicleMovement(focus);
            try
            {
                yield return StartCoroutine(raceEventFX.PlaySlipstreams(events));
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
            yield return new WaitForSecondsRealtime(postGap);
        if (hudUI != null)
            hudUI.SetStatus("尾流加成阶段：按奖励推进");
    }

    private int GetNigiriBonus(PlayerState p, bool crossedCorner, int rawEnd, int lane)
    {
        if (!crossedCorner || p.techState == null) return 0;
        if (!TechTreeRules.HasUniqueTech(p.techState, session.TechDb, TechEffectType.Nigiri)) return 0;

        int bonus = 0;
        foreach (var cornerId in trackManager.GetUniqueCornersCrossed(p.position, rawEnd))
        {
            int limit = session.EffectiveCornerLimit(p, trackManager.GetCornerSpeedLimit(cornerId, lane), false);
            if (p.cornerTotalThisTurn == limit) bonus += 2;
        }
        return bonus;
    }

    private int GetTorpedoBonus(PlayerState p, List<PlayerState> turnOrder)
    {
        if (!TrickCardRules.IsTorpedoTempuraActive(p.trickState)) return 0;

        int overtakes = RaceMovementRules.CountOvertakes(
            p, turnOrder, trackManager.TotalNodes, false, RaceTurnRules.ShouldSkip);
        return overtakes > 0 ? overtakes * TrickCardRules.GetTorpedoOvertakeBonus() : 0;
    }

    // ====== 弯道判定（per-corner-segment，含天气/科技修正） ======

    private bool ResolveCorners(PlayerState p, int oldPos, int rawEndPos)
    {
        if (p.cornerTotalThisTurn <= 0) return false;
        if (p.isBlown) return false;

        HashSet<int> corners = trackManager.GetUniqueCornersCrossed(oldPos, rawEndPos);
        if (corners.Count == 0) return false;
        int totalSpeed = p.cornerTotalThisTurn;
        int laneIndex = GetLane(p);
        string log = "";

        // DE L2 猪肘悬挂：出弯后跳过弯道判定（移动加成已在 ComputeMovements 结算）
        if (p.techState != null &&
            TechTreeRules.ShouldTriggerWurstplatte(p.techState, session.TechDb, corners.Count > 0))
        {
            if (hudUI != null)
                hudUI.AppendLog($"{p.name} 猪肘悬挂：跳过本回合弯道判定。");
            return true;
        }

        foreach (int cornerId in corners)
        {
            if (p.driverSkill != null && p.driverSkill.TryConsumeCornerIgnore())
            {
                log += $"{p.name} 使用 {p.DriverProfile.ActiveName} 无视 {trackManager.GetCornerName(cornerId)} 限速。\n";
                continue;
            }
            // 限速 = 基础 + 科技弯速加成 − 天气惩罚
            int limit = session.EffectiveCornerLimit(p, trackManager.GetCornerSpeedLimit(cornerId, laneIndex));
            if (totalSpeed > limit)
            {
                int overspeed = totalSpeed - limit;
                // 科技：每圈 1 次热量减免（最少为 1）
                int heat = Mathf.Max(1,
                    overspeed - session.ConsumeHeatReduction(p));
                if (session.TeamVehicleBonusesEnabled)
                    heat += TeamVehicleRules.GetCornerHeatPenalty(p.teamId);
                heat = DriverSkillRules.ReduceCornerHeat(p.driverSkill, heat);
                string cname = trackManager.GetCornerName(cornerId);

                // 尝试支付热量；引擎不足 → 失控
                if (heat > 0 && !TryPayHeat(p, heat, oldPos, $"overspeed at {cname} ({totalSpeed}>{limit})"))
                {
                    if (hudUI != null) hudUI.AppendLog(log);
                    return false; // 失控中断后续弯道判定
                }

                AudioService.PlaySfx(AudioEventNames.CornerOver);
                log += heat > 0
                    ? $"{p.name} 在 {cname} 超速 (lane {laneIndex + 1}, 限速 {limit}) 超 {overspeed}！+{heat} 热量。\n"
                    : $"{p.name} 使用 {p.DriverProfile.ActiveName} 零热量通过 {cname}。\n";
            }
            else
            {
                AudioService.PlaySfx(AudioEventNames.CornerSafe);
                log += $"{p.name} 安全通过 {trackManager.GetCornerName(cornerId)} (lane {laneIndex + 1}, {totalSpeed}<={limit})。\n";
            }
        }

        if (string.IsNullOrEmpty(log))
            log = $"{p.name} 直道 - 无弯道。\n";

        if (hudUI != null) hudUI.AppendLog(log);
        return true;
    }

    // ====== 维修区 ======

    private IEnumerator ResolvePitApproachChoice(PlayerState p)
    {
        IReadOnlyList<TrackNode> pitNodes = GetPitRuleNodes();
        if (p == null || !IsPitLaneEnabledForCurrentSession() || !PitLaneRules.HasPitLane(pitNodes))
            yield break;
        if (p.isBlown || p.hasFinished || p.pitChoiceResolvedThisLap ||
            p.pitStopRequested || p.pitStopScheduled)
            yield break;

        int distance = PitLaneRules.GetDistanceToPitEntry(p.position, pitNodes);
        if (distance <= 0 || distance > PitLaneRules.DEFAULT_APPROACH_WINDOW)
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

        p.pitChoiceResolvedThisLap = true;
        p.pitStopRequested = enter;
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
        p.pitChoiceResolvedThisLap = true;
        p.pitStopRequested = enter;
        if (hudUI != null)
            hudUI.AppendLog(enter
                ? $"{p.name}（AI）在距入口 {distance} 格处预定进站。"
                : $"{p.name}（AI）在距入口 {distance} 格处选择不进站。");
    }

    private void ExecuteScheduledPitStop(PlayerState p)
    {
        if (p == null) return;

        // 这个标记只消费一次；本回合由 GameLoop A1 负责把玩家加入 turnSkipped。
        p.pitStopScheduled = false;
        p.skipNextTurn = false;

        int exitMoveBonus = config != null
            ? config.pitExitMoveBonus
            : PitLaneRules.DEFAULT_EXIT_MOVE_BONUS;
        if (config != null && config.enableTechTree && session != null && p.techState != null)
            exitMoveBonus += session.GetModifiers(p).pitExitMoveBonus;

        var result = PitLaneRules.EnterPit(p, GetPitRuleNodes(), exitMoveBonus);
        if (!result.success)
        {
            if (hudUI != null) hudUI.AppendLog(result.message);
            return;
        }

        RecoverAllHeatWithPresentation(p); // 进站冷却全部热量回引擎
        p.gear = TeamGearRules.IsChina(p.teamId) ? ChinaGearShiftRules.RecoverGear : config.minGear;
        p.chinaConsecutiveGearCount = 0;
        p.pitChoiceResolvedThisLap = false;
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
    /// 繁荣(前 2 次过地标) → 免费冷却 2；衰退 → 自动修复付 1 热；复兴 → 手牌热量转移动。
    /// </summary>
    private void ResolveMotherRoadPass(PlayerState p, int landmarkIndex, int rewindPos)
    {
        var result = TechTreeRules.ResolveMotherRoadPass(p.techState, landmarkIndex, config.totalLaps);
        switch (result.phase)
        {
            case MotherRoadResult.MotherRoadPhase.Prosperity:
            {
                int cooled = CoolHeatWithPresentation(p, result.freeCooldown);
                if (hudUI != null)
                    hudUI.AppendLog($"<color=green>{p.name} 母亲之路(繁荣)：自动冷却 {cooled} 张热量牌。</color>");
                break;
            }
            case MotherRoadResult.MotherRoadPhase.Decline:
            {
                // 自动修复：付 1 热（引擎不足 → 失控回退）
                if (TryPayHeat(p, 1, rewindPos, "mother road repair"))
                {
                    TechTreeRules.RepairLandmark(p.techState);
                    if (hudUI != null)
                        hudUI.AppendLog($"{p.name} 母亲之路(衰退)：修复地标，付 1 热。");
                }
                break;
            }
            case MotherRoadResult.MotherRoadPhase.Revival:
            {
                // 复兴终极：手牌热量 → 移动（每张 +1，热量牌回收至引擎）
                int heatInHand = p.deck.CountHeatInHand();
                int converted = TechTreeRules.UseMotherRoadUltimate(p.techState, landmarkIndex, heatInHand);
                if (converted > 0)
                {
                    var heatCards = new List<CardData>();
                    foreach (var c in p.deck.Hand)
                        if (c.IsHeat) heatCards.Add(c);
                    int returned = p.deck.ReturnHeatCardsToPool(heatCards);
                    p.position = (p.position + returned) % trackManager.TotalNodes;
                    MoveCarTo(p, p.position);
                    if (hudUI != null)
                        hudUI.AppendLog($"<color=orange>{p.name} 母亲之路(复兴)！{returned} 张热量牌转为移动。</color>");
                }
                break;
            }
        }
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

        ApplyTrickEffects(p, card, result);

        if (!p.isAI)
        {
            if (card.trickId == "uk-scone")
            {
                TryAdvanceTutorialIfExpected(
                    TutorialAction.PlayUkScone,
                    $"trick:{card.trickId}");
            }
            else if (card.trickId == "uk-english-breakfast-tea")
            {
                TryAdvanceTutorialIfExpected(
                    TutorialAction.PlayUkEnglishBreakfastTea,
                    $"trick:{card.trickId}");
            }
        }

        // CN L2 连击追踪：特技
        if (p.techState != null)
            TechTreeRules.TrackDimSumCombo(p.techState, true, false, false);
        return true;
    }

    /// <summary>应用特技牌效果：热量支付/冷却、移动、抽牌、弃牌、限时热量、跳过回合。</summary>
    private void ApplyTrickEffects(PlayerState p, CardData card, TrickPlayResult result)
    {
        // 热量支付（司康：付 1 热 → +2 移动）
        if (result.heatToPay > 0)
        {
            if (!TryPayHeat(p, result.heatToPay, p.positionAtTurnStart, "scone"))
                return; // 失控中断（后续效果不应用）
        }

        // 冷却（红茶 / 关东慢煮）
        if (result.heatToCool > 0)
        {
            int cooled = CoolHeatWithPresentation(p, result.heatToCool);
            if (cooled > 0 && hudUI != null)
                hudUI.AppendLog($"{p.name} 特技冷却 {cooled} 张热量牌。");
        }

        // 移动加成（司康 +2）
        p.trickMoveBonusThisTurn += result.extraMovement;

        // 抽牌（可乐 +1）
        if (result.cardsToDraw > 0)
            p.deck.DrawToHand(p.deck.HandCount + result.cardsToDraw);

        // 弃 1 速度牌换冷却（基安蒂）
        if (result.requiresSpeedDiscard)
        {
            var speeds = p.deck.GetBottomNSpeedCards(1);
            if (speeds.Count > 0)
            {
                p.deck.RemoveFromHand(speeds);
                p.deck.DiscardSpeedCards(speeds);
                if (hudUI != null)
                    hudUI.AppendLog($"{p.name} 弃掉 {speeds[0].value} 速度牌。");
            }
        }

        // 限时热量牌（薯条：上回合过地标才可触发）
        if (TrickCardRules.HasTempHeat(p.trickState))
        {
            p.deck.AddCardsToHand(new List<CardData> { CardData.CreateTempHeat() });
            TrickCardRules.ConsumeTempHeat(p.trickState);
            if (hudUI != null)
                hudUI.AppendLog($"{p.name} 获得 1 张限时热量牌（回合结束销毁）。");
        }

        // 关东慢煮：跳过本回合，累加出牌数到下一回合
        var def = card != null ? session.TrickDb.Get(card.trickId) : null;
        if (def != null && def.effectType == TrickEffectType.KantoOden)
        {
            p.kantoOdenSkipThisTurn = true;
            TrickCardRules.AccumulateKantoOden(p.trickState, p.gear);
            if (hudUI != null)
                hudUI.AppendLog($"{p.name} 关东慢煮生效：本回合跳过，下回合可多出 {p.gear} 张牌。");
        }
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

            bool play = false;
            if (def.IsDefense && p.HeatRatio >= config.aiHeatWarningThreshold)
                play = true;
            else if (def.IsAttack && p.HeatRatio <= 0.35f)
                play = true;
            else if (def.effectType == TrickEffectType.KantoOden && session.GetRank(p) >= session.Players.Count)
                play = true;

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
        int playerLaneIndex = oldLane;
        if (direction > 0)
            playerLaneIndex = trackManager.GetLaneTowardsInside(playerLaneIndex);
        else if (direction < 0)
            playerLaneIndex = trackManager.GetLaneTowardsOutside(playerLaneIndex);

        if (direction != 0 && oldLane == playerLaneIndex)
            return;

        laneIndices[0] = playerLaneIndex;
        MoveCarToNode(Player, trackManager.StartFinishNodeIndex, playerLaneIndex);
        inputState.EndLaneChangeSelection();
        string choice = playerLaneIndex == oldLane
            ? "保持当前车道"
            : playerLaneIndex > oldLane ? "向内一格" : "向外一格";
        if (hudUI != null)
            hudUI.AppendLog($"印地起点换道：{choice}（第 {playerLaneIndex + 1} 道）");
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
        // 速度牌 → 弃牌堆。热量牌不参与普通抽牌/弃牌，只能通过冷却回引擎。
        if (!p.isAI && cardHandUI != null && p.playedSpeedCardsThisTurn.Count > 0)
        {
            cardHandUI.PlayCardTransitions(
                p.playedSpeedCardsThisTurn,
                CardVisualZone.Hand,
                CardVisualZone.DiscardPile,
                AudioEventNames.CardPlay);
        }
        p.deck.DiscardSpeedCards(p.playedSpeedCardsThisTurn);

        ResolveDriverSkillTurnEnd(p);
        int passiveOvertakes = 0;
        overtakesThisTurn.TryGetValue(p, out passiveOvertakes);
        p.driverSkill?.ResolvePassiveTurnEnd(passiveOvertakes);

        // 限时热量牌销毁（薯条）
        int tempRemoved = p.deck.RemoveTempCardsFromHand();
        if (tempRemoved > 0 && hudUI != null)
            hudUI.AppendLog($"{p.name} 限时热量牌销毁 {tempRemoved} 张。");

        // A participant can cross the finish line during phase B. Its played
        // cards still need to leave the played area, but no end-of-turn effect
        // may mutate a locked finish result afterwards.
        if (!RaceTurnRules.IsTerminal(p) && p.techState != null)
        {
            // CN L1 阴阳茶：阴（无手牌热）→ 付 1 热 +1 格；阳（有手牌热）→ 自动冷却 1
            ApplyYinYang(p, session.ResolveEndOfTurn(p));

            // CN L2 连击：特技 → 速度 → 付热 完整序列 → 额外触发阴阳
            if (TechTreeRules.CheckDimSumCombo(p.techState, session.TechDb))
            {
                if (hudUI != null)
                    hudUI.AppendLog($"<color=orange>{p.name} 点心连击！额外触发阴阳茶。</color>");
                ApplyYinYang(p, session.ResolveEndOfTurn(p));
            }

            // DE L3 烤肉拼盘：每场 1 次，自动冷却本回合支付的全部热量
            int grillCooldown = session.GetGrillSpezialCooldown(p);
            if (grillCooldown > 0)
            {
                session.ActivateGrillSpezial(p);
                int cooled = CoolHeatWithPresentation(p, grillCooldown);
                if (cooled > 0 && hudUI != null)
                    hudUI.AppendLog($"<color=green>{p.name} 烤肉拼盘：自动冷却 {cooled} 张热量牌。</color>");
            }
        }

        // 打出区已全部转移到弃牌堆，立即清空引用以维持单一区域所有权。
        p.playedSpeedCardsThisTurn.Clear();
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
                p.position = (p.position + 1) % trackManager.TotalNodes;
                MoveCarTo(p, p.position);
                if (hudUI != null)
                    hudUI.AppendLog($"<color=orange>{p.name} 阴阳茶(阴)：付 1 热 → +1 格。</color>");
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

    // ====== 圈数与完赛 ======

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

        RaceLapWeatherTransition transition = RaceLapWeatherRules.Advance(
            p.lap,
            requiredLaps,
            weatherState.LastRolledLap,
            allowWeatherRoll);
        p.lap = transition.Lap;
        session.OnNewLap(p);
        AudioService.PlaySfx(transition.HasFinished
            ? AudioEventNames.Finish
            : AudioEventNames.LapCross);
        if (hudUI != null)
            hudUI.AppendLog($"{p.name} 完成第 {p.lap} 圈！");

        // 每圈掷骰换天（同一圈内多辆车过线只掷一次）
        if (transition.ShouldRollWeather)
        {
            weatherState.MarkLapRolled(transition.Lap);
            WeatherType before = session.Weather;
            WeatherType after = session.RollWeatherForLap();
            if (after != before && hudUI != null)
            {
                hudUI.AppendLog($"<color=cyan>天气变化: {WeatherRules.GetDisplayName(before)} → {session.WeatherLabel}</color>");
            }
        }

        if (transition.HasFinished)
        {
            p.hasFinished = true;
            session.AssignFinish(p);
            if (hudUI != null)
                hudUI.AppendLog($"<color=green><b>{p.name} 完赛！</b></color>");

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
        // 为未完赛玩家按当前排名补记名次
        AssignRemainingFinishers();
        RefreshCarBadges();

        string result = RaceRanking.FormatResults(session.Players);
        if (tutorialScenario != null)
        {
            if (tutorialDirector != null && tutorialDirector.Phase == TutorialRunPhase.Completed)
                result = "勒芒教程练习完成！\n\n" + result;
            else
                result = "本次练习未完成；可以使用教程面板重新开始。\n\n" + result;
            result += "\n\n教程模式：不发放 RP、车手 XP、解锁或正常赛事进度。";
        }
        else if (careerRaceLaunch != null)
        {
            if (careerResultRecorded)
            {
                result += "\n\n生涯赛果已保存。返回主菜单可查看更新后的积分榜。";
                raceLogWriter?.Append(
                    $"[CAREER_RESULT] status=already_saved result_id={careerRaceLaunch.ResultId}");
            }
            else if (CareerRaceSettlement.TryRecord(
                         careerRaceLaunch,
                         trackManager != null && trackManager.LoadedTrackConfig != null
                             ? trackManager.LoadedTrackConfig.trackId
                             : string.Empty,
                         session.Players,
                         CareerRuntimeRepository.CreateDefault(),
                         out CareerSeasonState updatedCareer,
                         out string failureReason))
            {
                careerResultRecorded = true;
                List<CareerStanding> careerStandings = CareerModeRules.GetStandings(updatedCareer);
                CareerStanding playerStanding = careerStandings
                    .Find(entry => entry.TeamId == updatedCareer.LockedTeam);
                result += $"\n\n生涯赛果已保存：总分 {playerStanding?.Points ?? 0}，" +
                          $"总排名第 {playerStanding?.Rank ?? 0} 名。";
                if (updatedCareer.Phase == CareerPhase.SummerBreak)
                    result += "\n已进入夏休，返回主菜单调整一次生涯科技树。";
                else if (updatedCareer.Phase == CareerPhase.Completed)
                {
                    CareerStanding champion = careerStandings.Count > 0 ? careerStandings[0] : null;
                    result += $"\n八站生涯已完成。总冠军：" +
                              $"{(champion == null ? "—" : champion.TeamId.ToString())}" +
                              $"（{champion?.Points ?? 0} 分）。";
                }
                raceLogWriter?.Append(CareerRaceLogFormatter.BuildSaved(careerRaceLaunch, updatedCareer));
            }
            else
            {
                result += $"\n\n<color=red>{failureReason}</color>。返回主菜单后可重新开始当前站。";
                raceLogWriter?.Append(CareerRaceLogFormatter.BuildRejected(careerRaceLaunch, failureReason));
            }
        }
        else
        {
            result += "\n\n" + BuildRPReport();
            result += "\n\n" + BuildDriverXpReport();
        }

        if (hudUI != null) hudUI.ShowGameOver(result);
        if (cardHandUI != null) cardHandUI.HideAll();
        tutorialGuideUI?.Refresh();
        if (raceLogWriter != null && raceLogWriter.IsActive)
        {
            raceLogWriter.End(result);
            Debug.Log($"[RaceTestLog] Finished: {raceLogWriter.FilePath}");
        }
    }

    private void AssignRemainingFinishers()
    {
        var unfinished = new List<PlayerState>();
        foreach (var p in session.Players)
            if (!p.isBlown && !p.hasFinished)
                unfinished.Add(p);
        if (unfinished.Count == 0) return;

        foreach (var p in RaceRanking.SortByPosition(unfinished))
            session.AssignFinish(p);
    }

    /// <summary>按最终名次发放 RP（含 IT L3 骏马图腾加成），记入各队科技树。</summary>
    private string BuildRPReport()
    {
        var lines = new List<string> { "RP 奖励:" };
        var rankings = session.GetRankings();
        foreach (var e in rankings)
        {
            var p = e.player;
            int rp = TechTreeRules.CalculateRaceRP(e.rank);
            if (p.techState != null)
            {
                if (TechTreeRules.ShouldApplyCavallino(p.techState, session.TechDb, e.rank))
                {
                    string country = trackManager.LoadedTrackConfig != null
                        ? trackManager.LoadedTrackConfig.country
                        : "";
                    rp = TechTreeRules.ApplyCavallinoRampante(rp, e.rank, TechTreeRules.IsCavallinoHomeRace(country));
                }
                p.techState.rpBalance += rp;
                if (!p.isAI)
                    TechTreeProfileStore.Save(p.techState);
            }
            lines.Add($"{e.rank}. {p.name}: +{rp} RP{(p.techState != null ? $" (余额 {p.techState.rpBalance})" : "")}");
        }
        return string.Join("\n", lines);
    }

    private string BuildDriverXpReport()
    {
        var lines = new List<string> { "车手 XP:" };
        foreach (RaceRanking.RankEntry entry in session.GetRankings())
        {
            PlayerState player = entry.player;
            DriverProfile driver = player.DriverProfile;
            int earned = player.isBlown
                ? 0
                : DriverProgression.CalculateRaceXp(entry.rank, driver.TalentMultiplier, driver.Team);
            int previousLevel = player.DriverLevel;
            player.driverXp += earned;
            if (!player.isAI && tutorialScenario == null)
                DriverProgressStore.Save(driver.Id, player.driverXp);
            lines.Add($"{player.name}（{driver.ShortName}）: +{earned} XP → Lv{player.DriverLevel}");
            if (player.DriverLevel > previousLevel)
                lines.Add($"  {driver.ShortName} 解锁了新的车手技能层级。");
        }

        return string.Join("\n", lines);
    }

    // ====== 万骨涌（JP L3） ======

    private void TickBankuruwaseForAll()
    {
        if (!config.enableTechTree) return;

        int total = session.Players.Count;
        foreach (var p in session.Players)
        {
            if (p.techState == null) continue;

            if (!p.techState.bankuruwaseActive &&
                TechTreeRules.ShouldTriggerBankuruwase(p.techState, session.TechDb, session.GetRank(p), total))
            {
                TechTreeRules.ActivateBankuruwase(p.techState);
                if (hudUI != null)
                    hudUI.AppendLog($"<color=cyan>{p.name} 万骨涌激活：3 回合全属性加成 + 每回合冷却 1。</color>");
            }
            else if (p.techState.bankuruwaseActive)
            {
                bool stillActive = TechTreeRules.TickBankuruwase(p.techState);
                if (!stillActive && hudUI != null)
                    hudUI.AppendLog($"{p.name} 万骨涌效果结束。");
            }
        }
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
        car.transform.position = trackManager.GetNodePosition(position, lane);
        // 传送后朝向下一节点（失控回退 / 进站出口 / 阴阳茶 +1）
        int nextIdx = (position + 1) % trackManager.TotalNodes;
        GetCarOrientationController().FaceImmediately(car, trackManager.GetNodePosition(nextIdx, lane));
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
        raceLogWriter?.Append(
            $"[DRIVER_SKILL] driver={player.driverId} skill={player.driverSkill.Skill} " +
            $"tier={player.driverSkill.Tier} duration={player.driverSkill.ActiveTurnsRemaining} " +
            $"uses_remaining={player.driverSkill.UsesRemaining}");
    }

    public string GetDriverSkillButtonLabel(PlayerState player, out bool interactable)
    {
        interactable = false;
        if (player == null || player.driverSkill == null) return "车手技能";
        DriverProfile profile = player.DriverProfile;
        if (player.driverSkill.IsActive)
            return $"{profile.ActiveName}  {FormatSkillDuration(player.driverSkill.ActiveTurnsRemaining)}";

        DriverSkillActivationContext context = BuildDriverSkillActivationContext(player);
        interactable = DriverSkillRules.CanActivate(profile, player.driverSkill, context, out string reason);
        return interactable
            ? $"{profile.ActiveName}  ×{player.driverSkill.UsesRemaining}"
            : $"{profile.ActiveName}  {reason}";
    }

    private DriverSkillActivationContext BuildDriverSkillActivationContext(PlayerState player)
    {
        HeatGaugeState gauge = player?.deck != null ? HeatGaugeRules.Evaluate(player.deck) : default;
        int remaining = gauge.EngineRemaining;
        int capacity = gauge.Capacity;
        return new DriverSkillActivationContext(
            player == Player && phaseState.CanAcceptGear(inputState),
            player != null ? player.lap : 0,
            config != null ? config.totalLaps : 0,
            remaining,
            capacity,
            CountNearbyOpponentsBehind(player, 3));
    }

    private int CountNearbyOpponentsBehind(PlayerState player, int range)
    {
        if (player == null || session == null || trackManager == null || trackManager.TotalNodes <= 0)
            return 0;
        int count = 0;
        foreach (PlayerState opponent in session.Players)
        {
            if (opponent == null || opponent == player || opponent.isBlown || opponent.hasFinished ||
                opponent.lap != player.lap)
                continue;
            int distance = RaceSession.ForwardDistance(opponent.position, player.position, trackManager.TotalNodes);
            if (distance > 0 && distance <= range) count++;
        }
        return count;
    }

    private static string FormatSkillDuration(int turns)
    {
        return turns == int.MaxValue ? "本场有效" : $"{turns} 回合";
    }

    private void ResolveDriverSkillTurnEnd(PlayerState player)
    {
        if (player?.driverSkill == null || !player.driverSkill.ActivatedThisTurn ||
            player.driverSkill.Skill != DriverActiveSkillId.FinalSprint)
            return;

        int spinMax = session != null ? session.EffectiveSpinMax(player) : 3;
        player.spinCounter = Mathf.Min(spinMax, player.spinCounter + 1);
        if (player.spinCounter >= spinMax)
            player.isBlown = true;
        else if (player.driverSkill.Tier < 3)
            player.skipNextTurn = true;
        hudUI?.AppendLog($"{player.name} 最后冲刺代价：强制打转，失控 {player.spinCounter}/{spinMax}。" );
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

        if (card.IsHeat)
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

        if (card.IsSpeed)
            return ConfirmPlayerSpeedCards(player, new List<CardData> { card });

        return false;
    }

    private bool ConfirmPlayerSpeedCards(PlayerState player, IReadOnlyList<CardData> cards)
    {
        if (player == null || cards == null || cards.Count == 0)
            return false;

        int maxCards = GetMaxSpeedCardsThisTurn(player);
        SpeedCardCommitResult commit = CardPlayRules.CommitSpeedCards(player, cards, maxCards);
        if (commit != SpeedCardCommitResult.Success)
        {
            if (hudUI != null)
            {
                string message = commit == SpeedCardCommitResult.SpeedLimitReached
                    ? $"速度牌已达上限：{maxCards} 张（本次选择未提交）"
                    : "出牌失败：手牌状态已变化";
                hudUI.SetStatus($"<color=orange>{message}</color>");
            }
            if (commit == SpeedCardCommitResult.CardNotInHand)
                RefreshHumanHand(player);
            return false;
        }

        if (player.techState != null)
            TechTreeRules.TrackDimSumCombo(player.techState, false, true, false);

        if (hudUI != null)
        {
            int speedTotal = 0;
            for (int i = 0; i < cards.Count; i++)
                speedTotal += cards[i].value;
            hudUI.AppendLog($"{player.name} 确认 {cards.Count} 张速度牌（速度总和 {speedTotal}）。");
            hudUI.SetStatus(
                $"已打出 {player.playedSpeedCardsThisTurn.Count}/{maxCards} 张速度牌；可继续多选或结束出牌");
        }
        LogPlayedCards(player, "PLAYER");
        RefreshHumanHand(player);
        cardHandUI.BlockActionButtonBriefly();
        return true;
    }

    /// <summary>Ends human card play and applies the existing missing-card engine-failure rule.</summary>
    private void FinishPlayerCardPhase(PlayerState player)
    {
        int speedCount = player.playedSpeedCardsThisTurn.Count;

        // 引擎故障：速度牌不足时，每缺 1 张 → +1 热量入手牌。引擎不足 → 失控
        int required = GetMaxSpeedCardsThisTurn(player);
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
        }

        if (hudUI != null)
            hudUI.RefreshPlayerResources(player);
        cardHandUI.ClearPendingPlaySelection();
        inputState.EndCardSelection();
        cardHandUI.HideAll();
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
        if (tutorialDirector != null)
        {
            if (tutorialDirector.Phase == TutorialRunPhase.Practice ||
                tutorialDirector.Phase == TutorialRunPhase.Completed)
                RestartTutorialPracticeLap();
            else
                RestartTutorialGuidedSection();
            return;
        }

        if (careerRaceLaunch != null && careerResultRecorded)
        {
            SceneLoader.LoadMainMenu();
            return;
        }

        ResetRaceRuntime();
    }

    private void ResetTutorialRace(bool startInPractice, string reason)
    {
        if (tutorialScenario == null)
            return;

        if (raceLogWriter != null && raceLogWriter.IsActive)
            raceLogWriter.End($"tutorial reset: {reason}");
        initializeTutorialInPractice = startInPractice;
        ResetRaceRuntime();
    }

    private void ResetRaceRuntime()
    {
        StopAllCoroutines();
        foreach (var c in GetComponents<AIController>())
            Destroy(c);
        aiControllers.Clear();
        if (hudUI != null && hudUI.gameOverPanel != null)
            hudUI.gameOverPanel.SetActive(false);
        InitializeGame();
        InitializeTutorialGuideUI();
        StartCoroutine(GameLoop());
    }
}
