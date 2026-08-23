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
    private readonly RacePhaseState phaseState = new RacePhaseState();
    private RaceTestLogWriter raceLogWriter;
    private int raceTurnNumber;

    private List<GameObject> carInstances = new List<GameObject>();
    private List<int> laneIndices = new List<int>();
    private Dictionary<PlayerState, AIController> aiControllers = new Dictionary<PlayerState, AIController>();
    private Dictionary<PlayerState, int> overtakesThisTurn = new Dictionary<PlayerState, int>();
    private readonly RaceWeatherState weatherState = new RaceWeatherState();
    private RaceCameraController raceCameraController;
    private CarOrientationController carOrientationController;
    private ICarMovementAnimator carMovementAnimator;

    private WaitForSeconds nodeWait;
    private readonly RaceInputState inputState = new RaceInputState();
    private Dictionary<int, Image> gearButtonImages = new Dictionary<int, Image>();
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

        nodeWait = new WaitForSeconds(config.nodeDelay);
        InitializeGame();
        InitializeRaceCamera();
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
    /// 从 HUDUI 的 public 按钮字段收集档位按钮 Image 引用，
    /// 用于选中高亮和重置颜色。
    /// </summary>
    private void CollectGearButtonImages()
    {
        gearButtonImages.Clear();
        gearButtons.Clear();
        confirmGearControl = null;

        RegisterGearButton(1, hudUI != null ? hudUI.gear1Button : null, "Gear1Btn");
        RegisterGearButton(2, hudUI != null ? hudUI.gear2Button : null, "Gear2Btn");
        RegisterGearButton(3, hudUI != null ? hudUI.gear3Button : null, "Gear3Btn");
        RegisterGearButton(4, hudUI != null ? hudUI.gear4Button : null, "Gear4Btn");

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
        Image image = button.GetComponent<Image>();
        if (image != null)
            gearButtonImages[gear] = image;
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

        session = new RaceSession();
        aiControllers.Clear();
        weatherState.Reset();
        raceTurnNumber = 0;

        // 人类玩家（Players[0]）
        DriverProfile humanDriver = DriverSelectionState.ResolveDriver(config.playerDriverId, config.playerTeam);
        var human = new PlayerState("你", false, startFinishNodeIndex, config.minGear);
        human.driverId = humanDriver.Id;
        SetupPlayerForRace(human, humanDriver.Team);
        session.Players.Add(human);

        // AI 对手
        int aiCount = Mathf.Clamp(config.aiOpponentCount, 0, 3);
        for (int i = 0; i < aiCount; i++)
        {
            TeamId team = i < config.aiTeams.Length ? config.aiTeams[i] : TeamId.JP;
            DriverProfile aiDriver = DriverCatalog.GetDefaultForTeam(team);
            var aiState = new PlayerState($"AI{i + 1}", true, startFinishNodeIndex, config.minGear);
            aiState.driverId = aiDriver.Id;
            SetupPlayerForRace(aiState, team);
            session.Players.Add(aiState);
            var ctrl = gameObject.AddComponent<AIController>();
            ctrl.Initialize(this, aiState);
            aiControllers[aiState] = ctrl;
        }

        SpawnCars();
        BeginRaceTestLog(humanDriver, human);

        if (hudUI != null)
            hudUI.AppendLog($"车手: {humanDriver.DisplayName}（{humanDriver.Style}，XP {humanDriver.TalentMultiplier:0.0}x）");

        // 天气：比赛开始时从赛道天气池抽取
        if (config.enableWeather)
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
    }

    private void BeginRaceTestLog(DriverProfile humanDriver, PlayerState human)
    {
        if (raceLogWriter == null)
            raceLogWriter = new RaceTestLogWriter();
        else if (raceLogWriter.IsActive)
            raceLogWriter.End("race reset");

        string trackId = config != null ? config.trackId : "";
        string trackName = trackManager != null && trackManager.LoadedTrackConfig != null
            ? trackManager.LoadedTrackConfig.trackName
            : trackId;
        raceLogWriter.BeginRace(trackId, trackName, human.name, human.teamId, session.Players.Count - 1);
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

        raceLogWriter.Append(
            $"[CARDS] {player.name} source={source} count={player.playedSpeedCardsThisTurn.Count} values=[{values}] " +
            $"gear_limit={GetMaxSpeedCardsThisTurn(player)}");
    }

    /// <summary>
    /// 单个玩家的比赛初始化：车队分配、科技树、独立引擎热量池，以及速度/特技普通牌组。
    /// </summary>
    private void SetupPlayerForRace(PlayerState p, TeamId teamId)
    {
        p.teamId = teamId;
        p.usesChinaGearSystem = teamId == TeamId.CN;
        int poolSize = TeamVehicleRules.GetBaseHeatPoolSize(teamId, config.heatPoolPerPlayer);

        if (config.enableTechTree)
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

        p.deck.InitializeDeck(config, new HeatPool(poolSize));

        p.trickState = new TrickCardState();
        p.trickState.ResetPerRace();
        if (config.enableTrickCards)
            p.deck.AddTrickCardsToDrawPile(session.CreateInitialTrickCards(teamId));

        p.deck.DrawToHand(session.EffectiveHandSize(p, config.handSize));

        if (!p.isAI && teamId == TeamId.CN && config.ensurePlayerAttackTrickInOpeningHand)
        {
            string attackId = session.TrickDb.GetAttackId(teamId);
            if (!p.deck.EnsureTrickCardInHand(attackId))
                Debug.LogWarning($"[MVPGameManager] 无法保证中国队 ATTACK 牌 {attackId} 进入开局手牌。");
        }
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

            carInstances.Add(instance);
        }

        RefreshVisualCarLanes();
    }

    // ====== 主游戏循环 ======

    private IEnumerator GameLoop()
    {
        while (phaseState.IsRunning)
        {
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
                // 维修区预选在上一回合越过入口时登记；本回合开始才真正执行，
                // 因此“停一回合 + 出口后前移”不会发生在入口提示的同一回合。
                if (p.pitStopScheduled)
                {
                    ExecuteScheduledPitStop(p);
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
                    foreach (var kv in gearButtonImages)
                        kv.Value.color = (kv.Key == p.gear) ? new Color(0.3f, 0.8f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.8f);
                    if (hudUI != null)
                        hudUI.SetStatus($"选择档位 (当前: {TeamGearRules.GetDisplayName(p.teamId, p.gear)})");
                    if (cardHandUI != null) { cardHandUI.SetGearSelectionMode(true); cardHandUI.UpdateDeckInfo(p); }

                    yield return new WaitWhile(() => inputState.WaitingForGear);
                    SetGearControlsInteractable(false);
                    ApplyGearShift(p, inputState.PlayerGearChoice);
                    if (hudUI != null)
                        hudUI.RefreshPlayerResources(p);
                }
            }

            // ====== PHASE A2: 抽牌 ======
            foreach (var p in turnOrder)
            {
                if (turnSkipped.Contains(p)) continue;

                int handSize = session.EffectiveHandSize(p, config.handSize) + p.extraCardSlotsThisTurn;
                bool canDraw = p.deck.DrawToHand(handSize);
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
                        hudUI.SetStatus($"{TeamGearRules.GetDisplayName(p.teamId, p.gear)} 档 - 可多选速度牌后确认（最多 {GetMaxSpeedCardsThisTurn(p)} 张；特技牌单张确认）");
                    }

                    yield return new WaitWhile(() => inputState.WaitingForCards);
                    raceCameraController?.FocusPlayerAfterCardPlay();
                }

                // AI 的特技阶段固定早于速度选牌；人类在每张速度牌确认时记录真实顺序。
                if (p.isAI && p.techState != null && p.playedSpeedCardsThisTurn.Count > 0)
                    TechTreeRules.TrackDimSumCombo(p.techState, false, true, false);
            }

            // ====== 维修区预选 ======
            // 在车辆本回合移动前检查入口前十格窗口；选择只登记意图，
            // 真正的停靠要等车辆越过入口后的下一回合开始。
            foreach (var p in turnOrder)
            {
                if (RaceTurnRules.IsInactive(p, turnSkipped)) continue;
                yield return StartCoroutine(ResolvePitApproachChoice(p));
            }

            // ====== 计算移动力（科技 + 特技加成） ======
            ComputeMovements(turnOrder, turnSkipped);

            // ====== PHASE B: 执行阶段 ======
            phaseState.BeginAnimation();
            foreach (var p in turnOrder)
            {
                if (RaceTurnRules.IsInactive(p, turnSkipped)) continue;

                int oldPos = p.position;
                int rawEnd = oldPos + p.cornerTotalThisTurn;

                // 移动 → 反应(冷却) → 弯道判定
                yield return StartCoroutine(AnimateMovement(p, GetCarIndex(p)));
                ReactStep(p);
                ResolveCorners(p, oldPos, rawEnd);

                // US L3 母亲之路：经过地标自动结算（繁荣→冷却2 / 衰退→自动修复 / 复兴→终极转化）
                if (p.techState != null && config.enableTechTree &&
                    TechTreeRules.HasUniqueTech(p.techState, session.TechDb, TechEffectType.MotherRoad))
                {
                    var (lm1, lm2) = RaceSession.GetLandmarks(trackManager.TotalNodes);
                    if (RaceSession.CrossedLandmark(oldPos, p.position, lm1, trackManager.TotalNodes))
                        ResolveMotherRoadPass(p, 0, oldPos);
                    if (RaceSession.CrossedLandmark(oldPos, p.position, lm2, trackManager.TotalNodes))
                        ResolveMotherRoadPass(p, 1, oldPos);
                }

                // 维修区入口检测：入口前已经选择进站的车辆在此登记，
                // 下一回合 A1 再执行停靠；本回合不会被传送或额外跳过。
                int rawMovementEnd = oldPos + p.totalMovementThisTurn;
                if (config.enablePitLane && PitLaneRules.HasPitLane(trackManager.Nodes) &&
                    PitLaneRules.CrossedPitEntry(oldPos, rawMovementEnd, trackManager.Nodes))
                {
                    if (p.pitStopRequested && !p.isBlown && !p.hasFinished)
                    {
                        p.pitStopRequested = false;
                        p.pitStopScheduled = true;
                        if (hudUI != null)
                            hudUI.AppendLog($"{p.name} 已越过维修区入口，下一回合执行进站。");
                    }
                    else
                    {
                        // 本次通过未选择进站；下一圈再次接近入口时可重新选择。
                        p.pitStopRequested = false;
                        p.pitChoiceResolvedThisLap = false;
                    }
                }
            }

            // ====== 弃牌（可选，仅玩家） ======
            var human = Player;
            if (human != null && !human.hasFinished && !human.isBlown &&
                !turnSkipped.Contains(human) && !RaceTurnRules.ShouldSkip(human))
            {
                yield return StartCoroutine(DiscardStep());
            }

            // ====== 收尾 + 补牌 ======
            foreach (var p in session.Players)
                CleanupTurn(p);

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
        p.deck.RecoverAllHeatToPool();

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

        if (TryPayHeat(p, shift.HeatCost, p.position, "shift 2 gears"))
        {
            p.gear = shift.TargetGear;
            p.chinaConsecutiveGearCount = shift.IsChina ? shift.ConsecutiveCount : 0;

            // China Go overclock heat is a distinct cost from the standard
            // two-gear shift payment and follows the normal spin-out path.
            if (shift.AdditionalHeat > 0)
                TryPayHeat(p, shift.AdditionalHeat, p.position,
                    $"{TeamGearRules.GetDisplayName(p.teamId, p.gear)} overclock");
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
        if (!TeamGearRules.IsChina(p.teamId))
            cooldown += TeamVehicleRules.GetCooling(p.teamId);

        if (p.techState != null)
        {
            cooldown += TechTreeRules.GetBrothCooldownPerTurn(p.techState);
            cooldown += TechTreeRules.GetBankuruwaseCooldownPerTurn(p.techState);
        }

        if (session != null)
            cooldown = WeatherRules.ApplyWeatherToCooling(cooldown, session.Weather);

        if (cooldown > 0)
        {
            int removed = p.deck.CoolHeat(cooldown);
            if (removed > 0 && hudUI != null)
                hudUI.AppendLog($"{p.name} ({TeamGearRules.GetDisplayName(p.teamId, p.gear)}): cools {removed} Heat → engine.");
        }
    }

    // ====== 移动动画（含圈数检测） ======

    private IEnumerator AnimateMovement(PlayerState p, int carIndex)
    {
        if (carIndex < 0 || carIndex >= carInstances.Count || carInstances[carIndex] == null) yield break;

        GameObject car = carInstances[carIndex];
        int laneIndex = GetVisualLaneIndex(p);
        laneIndices[carIndex] = laneIndex;
        int totalMove = p.totalMovementThisTurn;
        int totalNodes = trackManager.TotalNodes;
        int targetPos = p.position + totalMove;

        if (totalMove > 0)
        {
            raceCameraController?.BeginVehicleMovement(car.transform);
            if (config.movementFocusLeadDelay > 0f)
                yield return new WaitForSeconds(config.movementFocusLeadDelay);
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
        if (overtakeCount > 0 && raceEventFX != null)
        {
            // Keep the movement camera on the winner for a dedicated
            // slow-motion close-up before returning to normal race pacing.
            raceCameraController?.BeginVehicleMovement(car.transform);
            yield return StartCoroutine(raceEventFX.PlayOvertake(car.transform, overtakeCount));
        }

        if (totalMove > 0)
        {
            if (config.movementFocusTrailDelay > 0f)
                yield return new WaitForSeconds(config.movementFocusTrailDelay);
            raceCameraController?.EndVehicleMovement();
        }
    }

    // ====== 移动力计算（科技 + 特技加成） ======

    /// <summary>本回合最大可出速度牌数 = 档位 + 额外槽（关东慢煮/火锅底料）。</summary>
    public int GetMaxSpeedCardsThisTurn(PlayerState p)
    {
        int max = TeamGearRules.GetSpeedCardCount(
            p.teamId, p.gear, p.chinaConsecutiveGearCount,
            p.extraCardSlotsThisTurn);
        if (TrickCardRules.HasHotpotAttack(p.trickState)) max += 1;
        return max;
    }

    /// <summary>Returns the lane currently used to render and judge a racer.</summary>
    public int GetLaneIndexForPlayer(PlayerState p)
    {
        return GetVisualLaneIndex(p);
    }

    private void ComputeMovements(List<PlayerState> turnOrder, HashSet<PlayerState> turnSkipped)
    {
        overtakesThisTurn.Clear();

        // 第一轮：基础速度总和（弯道判定用，不含特技/科技加成）
        foreach (var p in turnOrder)
        {
            if (RaceTurnRules.IsInactive(p, turnSkipped))
            {
                p.totalMovementThisTurn = 0;
                p.cornerTotalThisTurn = 0;
                continue;
            }
            p.cornerTotalThisTurn = RaceRules.SumCardValues(p.playedSpeedCardsThisTurn);
        }

        // 第二轮：加成（需要弯道信息与对手移动）
        foreach (var p in turnOrder)
        {
            if (RaceTurnRules.IsInactive(p, turnSkipped)) continue;

            int rawEnd = p.position + p.cornerTotalThisTurn;
            int lane = GetLane(p);
            bool crossedCorner = trackManager.GetUniqueCornersCrossed(p.position, rawEnd).Count > 0;

            int bonus = session.ComputeMovementBonus(p, crossedCorner);
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

            // 尾流：模拟移动后紧跟前方车 → 基础 +2（帕尔玛/筋斗云叠加；前车冰糕阻断）
            bonus += session.ComputeSlipstreamBonus(p, session.Players, trackManager.TotalNodes);

            // DE L1 黑啤酒燃料：付 1 热 → +2 移动（自动激活；引擎预留 1 热防失控）
            if (p.techState != null && config.enableTechTree &&
                session.GetModifiers(p).hasSchwarzbierFuel &&
                p.deck.heatPool != null && p.deck.heatPool.remaining > 1)
            {
                if (TryPayHeat(p, 1, p.positionAtTurnStart, "schwarzbier fuel"))
                {
                    bonus += 2;
                    if (hudUI != null)
                        hudUI.AppendLog($"{p.name} 黑啤酒燃料：付 1 热 → +2 移动。");
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

            p.totalMovementThisTurn = p.cornerTotalThisTurn + bonus;
            raceLogWriter?.Append(
                $"[MOVE_PLAN] {p.name} role={(p.isAI ? "AI" : "PLAYER")} position={p.position} " +
                $"corner_speed={p.cornerTotalThisTurn} bonus={bonus} total={p.totalMovementThisTurn}");
        }

        // Visual overtake events use the final movement values, including
        // technology and trick bonuses, so the close-up matches what players
        // actually see on the track.
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

    private int GetNigiriBonus(PlayerState p, bool crossedCorner, int rawEnd, int lane)
    {
        if (!crossedCorner || p.techState == null) return 0;
        if (!TechTreeRules.HasUniqueTech(p.techState, session.TechDb, TechEffectType.Nigiri)) return 0;

        int bonus = 0;
        foreach (var cornerId in trackManager.GetUniqueCornersCrossed(p.position, rawEnd))
        {
            int limit = session.EffectiveCornerLimit(p, trackManager.GetCornerSpeedLimit(cornerId, lane));
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

    private void ResolveCorners(PlayerState p, int oldPos, int rawEndPos)
    {
        if (p.cornerTotalThisTurn <= 0) return;
        if (p.isBlown) return;

        HashSet<int> corners = trackManager.GetUniqueCornersCrossed(oldPos, rawEndPos);
        int totalSpeed = p.cornerTotalThisTurn;
        int laneIndex = GetLane(p);
        string log = "";

        // DE L2 猪肘悬挂：出弯后跳过弯道判定（移动加成已在 ComputeMovements 结算）
        if (p.techState != null &&
            TechTreeRules.ShouldTriggerWurstplatte(p.techState, session.TechDb, corners.Count > 0))
        {
            if (hudUI != null)
                hudUI.AppendLog($"{p.name} 猪肘悬挂：跳过本回合弯道判定。");
            return;
        }

        foreach (int cornerId in corners)
        {
            // 限速 = 基础 + 科技弯速加成 − 天气惩罚
            int limit = session.EffectiveCornerLimit(p, trackManager.GetCornerSpeedLimit(cornerId, laneIndex));
            if (totalSpeed > limit)
            {
                int overspeed = totalSpeed - limit;
                // 科技：每圈 1 次热量减免（最少为 1）
                int heat = Mathf.Max(1,
                    overspeed - session.ConsumeHeatReduction(p));
                heat += TeamVehicleRules.GetCornerHeatPenalty(p.teamId);
                string cname = trackManager.GetCornerName(cornerId);

                // 尝试支付热量；引擎不足 → 失控
                if (!TryPayHeat(p, heat, oldPos, $"overspeed at {cname} ({totalSpeed}>{limit})"))
                {
                    if (hudUI != null) hudUI.AppendLog(log);
                    return; // 失控中断后续弯道判定
                }

                log += $"{p.name} 在 {cname} 超速 (lane {laneIndex + 1}, 限速 {limit}) 超 {overspeed}！+{heat} 热量。\n";
            }
            else
            {
                log += $"{p.name} 安全通过 {trackManager.GetCornerName(cornerId)} (lane {laneIndex + 1}, {totalSpeed}<={limit})。\n";
            }
        }

        if (string.IsNullOrEmpty(log))
            log = $"{p.name} 直道 - 无弯道。\n";

        if (hudUI != null) hudUI.AppendLog(log);
    }

    // ====== 维修区 ======

    private IEnumerator ResolvePitApproachChoice(PlayerState p)
    {
        if (p == null || !config.enablePitLane || !PitLaneRules.HasPitLane(trackManager.Nodes))
            yield break;
        if (p.isBlown || p.hasFinished || p.pitChoiceResolvedThisLap ||
            p.pitStopRequested || p.pitStopScheduled)
            yield break;

        int distance = PitLaneRules.GetDistanceToPitEntry(p.position, trackManager.Nodes);
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

        var result = PitLaneRules.EnterPit(p, trackManager.Nodes, exitMoveBonus);
        if (!result.success)
        {
            if (hudUI != null) hudUI.AppendLog(result.message);
            return;
        }

        p.deck.RecoverAllHeatToPool(); // 进站冷却全部热量回引擎
        p.gear = TeamGearRules.IsChina(p.teamId) ? ChinaGearShiftRules.RecoverGear : config.minGear;
        p.chinaConsecutiveGearCount = 0;
        p.pitChoiceResolvedThisLap = false;
        MoveCarTo(p, p.position);       // 移动到维修区出口
        if (hudUI != null)
            hudUI.AppendLog($"<color=green>{p.name} 进站执行：本回合停靠，冷却全部热量，出站后前进 {result.exitMoveBonus} 格（{result.pitExitPosition}→{result.exitPosition}）。</color>");
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
                int cooled = p.deck.CoolHeat(result.freeCooldown);
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
        raceLogWriter?.Append(
            $"[TRICK] {p.name} source={(p.isAI ? "AI" : "PLAYER")} id={card.trickId} " +
            $"effect={(def != null ? def.effectType.ToString() : "unknown")}");
        if (hudUI != null)
            hudUI.AppendLog($"{p.name} 打出特技牌: {result.message}");

        ApplyTrickEffects(p, card, result);

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
            int cooled = p.deck.CoolHeat(result.heatToCool);
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
            int discarded = player.deck.DiscardPlayableCardsFromHand(toDiscard);
            if (discarded > 0 && hudUI != null)
                hudUI.AppendLog($"{player.name} discards {discarded} card(s).");
            cardHandUI.SetDiscardMode(false);
            RefreshHumanHand(player);
        }
    }

    // ====== 收尾 ======

    private void CleanupTurn(PlayerState p)
    {
        // 速度牌 → 弃牌堆。热量牌不参与普通抽牌/弃牌，只能通过冷却回引擎。
        p.deck.DiscardSpeedCards(p.playedSpeedCardsThisTurn);

        // 限时热量牌销毁（薯条）
        int tempRemoved = p.deck.RemoveTempCardsFromHand();
        if (tempRemoved > 0 && hudUI != null)
            hudUI.AppendLog($"{p.name} 限时热量牌销毁 {tempRemoved} 张。");

        // 回合结束科技结算
        if (p.techState != null)
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
                int cooled = p.deck.CoolHeat(grillCooldown);
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
            int cooled = p.deck.RemoveHeatFromHand(result.heatToCool);
            if (cooled > 0 && hudUI != null)
                hudUI.AppendLog($"<color=cyan>{p.name} 阴阳茶(阳)：自动冷却 {cooled} 张热量牌。</color>");
        }
    }

    // ====== 圈数与完赛 ======

    public void OnPlayerCrossedStartFinish(PlayerState p)
    {
        if (p.hasFinished) return;

        RaceLapWeatherTransition transition = RaceLapWeatherRules.Advance(
            p.lap,
            config.totalLaps,
            weatherState.LastRolledLap,
            config.enableWeather);
        p.lap = transition.Lap;
        session.OnNewLap(p);
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
        }
    }

    // ====== 游戏结束 ======

    private bool CheckGameEnd()
    {
        var human = session.Human;
        if (human == null) return true;
        // 人类完赛或爆缸 → 结束（如 AI 先完赛则继续跑到人类完赛）
        if (human.isBlown || human.hasFinished) return true;
        // 所有人完赛/爆缸 → 结束
        return session.IsRaceOver();
    }

    private void ShowGameOver()
    {
        // 为未完赛玩家按当前排名补记名次
        AssignRemainingFinishers();

        string result = RaceRanking.FormatResults(session.Players);
        result += "\n\n" + BuildRPReport();
        result += "\n\n" + BuildDriverXpReport();

        if (hudUI != null) hudUI.ShowGameOver(result);
        if (cardHandUI != null) cardHandUI.HideAll();
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
        if (!phaseState.CanAcceptGear(inputState)) return;
        if (Player != null && TeamGearRules.IsChina(Player.teamId) && gear > ChinaGearShiftRules.GoGear)
            return;
        if (!inputState.SelectGear(gear)) return;
        // 高亮选中的档位按钮
        foreach (var kv in gearButtonImages)
        {
            kv.Value.color = (kv.Key == gear) ? new Color(0.3f, 0.8f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.8f);
        }
        if (hudUI != null)
            hudUI.SetStatus($"已选 {TeamGearRules.GetDisplayName(Player.teamId, gear)} 档 - 点击确认锁定");
    }

    public void OnConfirmGearClicked()
    {
        if (!phaseState.CanAcceptGear(inputState)) return;
        if (!inputState.ConfirmGear()) return;
        SetGearControlsInteractable(false);
    }

    public void OnPlayCardsButtonClicked()
    {
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
            if (!TryPayHeat(player, missing, player.position, "engine failure"))
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
        StopAllCoroutines();
        foreach (var c in GetComponents<AIController>())
            Destroy(c);
        aiControllers.Clear();
        InitializeGame();
        StartCoroutine(GameLoop());
    }
}
