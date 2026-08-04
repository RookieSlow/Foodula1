using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 游戏阶段枚举。
/// </summary>
public enum GamePhase
{
    WaitingForGear,
    WaitingForCards,
    Animating,
    GameOver
}

/// <summary>
/// 比赛主管理器 — 协程驱动的回合制 HEAT 核心循环。
/// 挂载到场景中的 GameManager GameObject 上。
///
/// 5 大核心系统接入（2026-08-03）：
/// - 多车：RaceSession.Players + RaceRanking 排名/回合顺序（末位先行）
/// - 天气：比赛开始抽取 + 每圈掷骰换天，雨天弯道限速 -1
/// - 维修区：经过 pit_entry 时选择进站，冷却全部热量、停 1 回合
/// - 特技牌：开局 4 张，每回合限 1，点击直接打出
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
    public Sprite[] carSprites = new Sprite[6];

    [Header("UI Prefab (Demo模式)")]
    [Tooltip("拖入 RaceCanvas Prefab 以使用预制 UI；留空则回退到硬编码 MVP UI。")]
    public GameObject raceCanvasPrefab;
    [Tooltip("卡牌预制体引用，Prefab 模式下会自动传给 CardHandUI。")]
    public GameObject cardUIPrefab;

    // --- 运行时状态 ---
    private RaceSession session;
    private GamePhase phase;

    private List<GameObject> carInstances = new List<GameObject>();
    private List<int> laneIndices = new List<int>();
    private Dictionary<PlayerState, AIController> aiControllers = new Dictionary<PlayerState, AIController>();
    private int weatherRolledLap;

    private WaitForSeconds nodeWait;
    private bool waitingForPlayerGear;
    private bool waitingForPlayerCards;
    private bool waitingForPlayerDiscard;
    private int playerGearChoice;
    private int pendingGear;
    private Dictionary<int, Image> gearButtonImages = new Dictionary<int, Image>();

    // 印地安纳波利斯起点换道
    private bool waitingForPlayerLaneChange;
    private GameObject laneChangePanel;
    private Button laneInButton;
    private Button laneKeepButton;
    private Button laneOutButton;

    // 维修区
    private GameObject pitChoicePanel;
    private Button pitEnterButton;
    private Button pitSkipButton;
    private bool waitingForPitChoice;
    private PlayerState pitWaitingPlayer;

    /// <summary>6 车队回退颜色（精灵图缺失时）。</summary>
    private static readonly Color[] TEAM_COLORS =
    {
        new Color(0.85f, 0.2f, 0.2f),   // UK 红
        new Color(0.2f, 0.35f, 0.85f),  // DE 蓝
        new Color(0.2f, 0.7f, 0.35f),   // IT 绿
        new Color(0.9f, 0.7f, 0.15f),   // US 黄
        new Color(0.95f, 0.4f, 0.1f),   // CN 橙
        new Color(0.3f, 0.8f, 0.85f)    // JP 青
    };

    // --- 属性 ---
    public PlayerState Player => session != null ? session.Human : null;
    public PlayerState AI => session != null && session.Players.Count > 1 ? session.Players[1] : null;
    /// <summary>当前比赛的完整会话（多车/天气/特技/科技状态）。</summary>
    public RaceSession Session => session;
    public GamePhase CurrentPhase => phase;
    public GameConfigSO Config => config;
    public TrackManager Track => trackManager;
    /// <summary>当前天气显示名。</summary>
    public string WeatherLabel => session != null ? session.WeatherLabel : "☀️ 晴天";
    /// <summary>The currently rendered human car, if it has been spawned.</summary>
    public Transform PlayerCarTransform => carInstances.Count > 0 ? carInstances[0].transform : null;
    /// <summary>The currently rendered first AI car, if it has been spawned.</summary>
    public Transform AICarTransform => carInstances.Count > 1 ? carInstances[1].transform : null;

    void Awake()
    {
    }

    void Start()
    {
        // 自动创建默认配置
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GameConfigSO>();
            Debug.LogWarning("MVPGameManager: GameConfigSO not set. Using defaults. Create one via Create > Foodular1 > MVP Game Config for better control.");
        }

        // 自动创建缺失的引用
        if (trackManager == null)
            trackManager = GetComponent<TrackManager>();
        if (aiController == null)
            aiController = GetComponent<AIController>();

        // UI 初始化：Prefab 优先，硬编码回退
        if (raceCanvasPrefab != null)
        {
            InstantiateUIFromPrefab();
        }
        else if (hudUI == null || cardHandUI == null)
        {
            AutoCreateUI();
        }

        // 收集档位按钮图片引用（Prefab 模式从 HUDUI 获取，硬编码模式在 AutoCreateUI 中已填充）
        if (raceCanvasPrefab != null)
        {
            CollectGearButtonImages();
        }

        CreateLaneChangeUI();
        CreatePitChoiceUI();

        nodeWait = new WaitForSeconds(config.nodeDelay);
        InitializeGame();
        InitializeRaceCamera();
        StartCoroutine(GameLoop());
    }

    private void InitializeRaceCamera()
    {
        Camera raceCamera = Camera.main;
        if (raceCamera == null)
        {
            Debug.LogWarning("[MVPGameManager] Main camera not found; race camera setup skipped.");
            return;
        }

        RaceCameraController controller = raceCamera.GetComponent<RaceCameraController>();
        if (controller == null)
        {
            controller = raceCamera.gameObject.AddComponent<RaceCameraController>();
        }

        Canvas raceCanvas = hudUI != null
            ? hudUI.GetComponentInParent<Canvas>()
            : FindObjectOfType<Canvas>();
        controller.Initialize(this, raceCanvas);
    }

    /// <summary>
    /// 从 Prefab 实例化 UI — Demo 模式的主路径。
    /// 实例化后自动查找 HUDUI 和 CardHandUI 组件。
    /// </summary>
    private void InstantiateUIFromPrefab()
    {
        // 禁用场景中已有的所有 Canvas（避免双份 UI）
        foreach (var oldCanvas in FindObjectsOfType<Canvas>())
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
    /// 从 HUDUI 的 public 按钮字段收集档位按钮 Image 引用，
    /// 用于选中高亮和重置颜色。
    /// </summary>
    private void CollectGearButtonImages()
    {
        gearButtonImages.Clear();
        if (hudUI == null) return;

        if (hudUI.gear1Button != null) gearButtonImages[1] = hudUI.gear1Button.GetComponent<Image>();
        if (hudUI.gear2Button != null) gearButtonImages[2] = hudUI.gear2Button.GetComponent<Image>();
        if (hudUI.gear3Button != null) gearButtonImages[3] = hudUI.gear3Button.GetComponent<Image>();
        if (hudUI.gear4Button != null) gearButtonImages[4] = hudUI.gear4Button.GetComponent<Image>();
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

        // 从场景中获取 TMP 字体引用
        TMP_FontAsset fontAsset = null;
        TMP_Text existingTmp = FindObjectOfType<TMP_Text>();
        if (existingTmp != null) fontAsset = existingTmp.font;

        // 隐藏旧 UI 元素
        HideOldUIElement("StatusTextTMP");
        HideOldUIElement("ReadyStepsText");
        HideOldUIElement("NextRound");
        HideOldUIElement("Reset");

        // --- 创建 HUD ---
        if (hudUI == null)
        {
            GameObject hudGO = new GameObject("HUD", typeof(RectTransform));
            hudGO.transform.SetParent(canvas.transform, false);
            hudUI = hudGO.AddComponent<HUDUI>();

            // 创建子 TMP 文本（G按钮下方，拉开间距）
            hudUI.statusText = CreateTMPText(hudGO.transform, "StatusText", "选择档位 (1-4)", 22,
                new Vector2(-300, 180), new Vector2(420, 30), fontAsset);
            hudUI.gearText = CreateTMPText(hudGO.transform, "GearText", "档位: 1", 18,
                new Vector2(-400, 150), new Vector2(150, 25), fontAsset);
            hudUI.heatText = CreateTMPText(hudGO.transform, "HeatText", "引擎热量: 12", 18,
                new Vector2(-400, 125), new Vector2(280, 25), fontAsset);
            hudUI.lapText = CreateTMPText(hudGO.transform, "LapText", "圈数: 0/3", 18,
                new Vector2(-400, 100), new Vector2(200, 25), fontAsset);
            hudUI.positionText = CreateTMPText(hudGO.transform, "PositionText", $"位置: 0/{trackManager.TotalNodes}", 18,
                new Vector2(-400, 75), new Vector2(250, 25), fontAsset);
            hudUI.aiStatusText = CreateTMPText(hudGO.transform, "AIStatusText", "AI: 就绪", 16,
                new Vector2(250, 50), new Vector2(200, 25), fontAsset);
            hudUI.weatherText = CreateTMPText(hudGO.transform, "WeatherText", "☀️ 晴天", 16,
                new Vector2(250, 180), new Vector2(200, 25), fontAsset);
            hudUI.standingsText = CreateTMPText(hudGO.transform, "StandingsText", "", 14,
                new Vector2(250, 75), new Vector2(320, 100), fontAsset);
            hudUI.logText = CreateTMPText(hudGO.transform, "LogText", "", 13,
                new Vector2(0, -160), new Vector2(750, 180), fontAsset);

            // 创建 4 个档位按钮 + 确认按钮（屏幕顶部）
            CreateGearButton(canvas.transform, "Gear1Btn", "G1", new Vector2(-380, 220), 1);
            CreateGearButton(canvas.transform, "Gear2Btn", "G2", new Vector2(-290, 220), 2);
            CreateGearButton(canvas.transform, "Gear3Btn", "G3", new Vector2(-200, 220), 3);
            CreateGearButton(canvas.transform, "Gear4Btn", "G4", new Vector2(-110, 220), 4);
            CreateActionButton(canvas.transform, "ConfirmGearBtn", "确认", new Vector2(10, 220),
                new Color(0.4f, 0.7f, 1f), () => OnConfirmGearClicked());

            // 创建 出牌 和 重新开始 按钮
            CreateActionButton(canvas.transform, "PlayBtn", "出牌", new Vector2(-300, -185), Color.green,
                () => OnPlayCardsButtonClicked());
            CreateActionButton(canvas.transform, "ResetBtn", "重新开始", new Vector2(-150, -185), Color.yellow,
                () => ResetGame());

            // 创建游戏结束面板
            GameObject goPanel = new GameObject("GameOverPanel", typeof(RectTransform));
            goPanel.transform.SetParent(canvas.transform, false);
            hudUI.gameOverPanel = goPanel;
            hudUI.gameOverText = CreateTMPText(goPanel.transform, "GameOverText", "", 28,
                Vector2.zero, new Vector2(500, 300), fontAsset);
            goPanel.SetActive(false);
        }

        // --- 创建 CardHandUI ---
        if (cardHandUI == null)
        {
            Transform handContainer = canvas.transform.Find("HandContainer");
            if (handContainer == null)
            {
                GameObject hc = new GameObject("HandContainer", typeof(RectTransform));
                hc.transform.SetParent(canvas.transform, false);
                handContainer = hc.transform;
            }

            GameObject cardHandGO = new GameObject("CardHand", typeof(RectTransform));
            cardHandGO.transform.SetParent(canvas.transform, false);
            cardHandUI = cardHandGO.AddComponent<CardHandUI>();
            cardHandUI.handContainer = handContainer;
            cardHandUI.deckInfoText = CreateTMPText(cardHandGO.transform, "DeckInfo",
                "牌堆: 12速 + 3热", 14, new Vector2(-300, -100), new Vector2(200, 25), fontAsset);

            // 尝试从 Assets/Prefab/CardPrefab.prefab 加载
#if UNITY_EDITOR
            cardHandUI.cardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/CardPrefab.prefab");
#endif
            if (cardHandUI.cardPrefab == null)
                Debug.LogWarning("Could not auto-load CardPrefab. Set it manually on CardHand.");
        }

        // 绑定档位按钮回调
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

        CreateTMPText(panelRect, "PitPrompt", "通过维修区入口：是否进站？", 18,
            new Vector2(0f, 32f), new Vector2(580f, 28f),
            FindObjectOfType<TMP_Text>()?.font);

        pitEnterButton = CreateActionButton(panelRect, "PitEnterButton", "进站 (冷却全部热量)",
            new Vector2(-160f, -15f), new Color(0.45f, 0.85f, 0.55f),
            () => ChoosePit(true));
        pitSkipButton = CreateActionButton(panelRect, "PitSkipButton", "继续比赛",
            new Vector2(160f, -15f), new Color(0.8f, 0.8f, 0.8f),
            () => ChoosePit(false));

        pitChoicePanel.SetActive(false);
    }

    private TMP_Text CreateTMPText(Transform parent, string name, string text, int fontSize,
        Vector2 anchoredPos, Vector2 size, TMP_FontAsset font = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Left;
        if (font != null) tmp.font = font;

        return tmp;
    }

    private void CreateGearButton(Transform parent, string name, string label, Vector2 pos, int gear)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(80, 40);

        // Image
        UnityEngine.UI.Image img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(1, 1, 1, 0.8f);

        // Button
        UnityEngine.UI.Button btn = go.AddComponent<UnityEngine.UI.Button>();
        int capturedGear = gear;
        btn.onClick.AddListener(() => OnGearButtonClicked(capturedGear));
        gearButtonImages[capturedGear] = img;

        // Label
        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        RectTransform lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.sizeDelta = Vector2.zero;
        TMP_Text ltmp = labelGO.AddComponent<TMPro.TextMeshProUGUI>();
        ltmp.text = label;
        ltmp.fontSize = 18;
        ltmp.alignment = TMPro.TextAlignmentOptions.Center;
        ltmp.color = Color.black;
    }

    private Button CreateActionButton(Transform parent, string name, string label, Vector2 pos, Color color,
        UnityEngine.Events.UnityAction callback)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(120, 40);

        Image img = go.AddComponent<Image>();
        img.color = color;

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(callback);

        // Label - use standard UI.Text for reliability (avoids TMP font issues)
        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        RectTransform lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.sizeDelta = Vector2.zero;

        UnityEngine.UI.Text labelText = labelGO.AddComponent<UnityEngine.UI.Text>();
        labelText.text = label;
        labelText.fontSize = 20;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.black;
        labelText.fontStyle = FontStyle.Bold;
        // Use built-in Arial font
        Font arial = Font.CreateDynamicFontFromOSFont("Arial", 16);
        if (arial != null) labelText.font = arial;

        return btn;
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

    private void HideOldUIElement(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) go.SetActive(false);
    }

    // ====== 初始化 ======

    private void InitializeGame()
    {
        int startFinishNodeIndex = trackManager.StartFinishNodeIndex;

        session = new RaceSession();
        aiControllers.Clear();
        weatherRolledLap = 0;

        // 人类玩家（Players[0]）
        var human = new PlayerState("你", false, startFinishNodeIndex, config.minGear);
        SetupPlayerForRace(human, config.playerTeam);
        session.Players.Add(human);

        // AI 对手
        int aiCount = Mathf.Clamp(config.aiOpponentCount, 0, 3);
        for (int i = 0; i < aiCount; i++)
        {
            TeamId team = i < config.aiTeams.Length ? config.aiTeams[i] : TeamId.JP;
            var aiState = new PlayerState($"AI{i + 1}", true, startFinishNodeIndex, config.minGear);
            SetupPlayerForRace(aiState, team);
            session.Players.Add(aiState);
            var ctrl = gameObject.AddComponent<AIController>();
            ctrl.Initialize(this, aiState);
            aiControllers[aiState] = ctrl;
        }

        SpawnCars();

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

        phase = GamePhase.WaitingForGear;
        waitingForPlayerGear = true;
        waitingForPlayerCards = false;

        if (hudUI != null)
        {
            hudUI.SetGameManager(this);
            hudUI.Refresh(this, Player, AI, session.Players);
        }
        if (cardHandUI != null)
            cardHandUI.ShowHand(this, Player);
    }

    /// <summary>
    /// 单个玩家的比赛初始化：车队分配、科技树、热量池、手牌 + 特技牌。
    /// </summary>
    private void SetupPlayerForRace(PlayerState p, TeamId teamId)
    {
        p.teamId = teamId;
        int poolSize = config.heatPoolPerPlayer;

        if (config.enableTechTree)
        {
            p.techState = session.CreateDemoTechState(teamId);
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
            p.deck.AddTrickCardsToHand(session.CreateInitialTrickCards(teamId));

        p.deck.DrawToHand(session.EffectiveHandSize(p, config.handSize));
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
            Quaternion startRot = GetFacingRotation(
                trackManager.GetNodePosition(nextIdx, visualLane) - startPos);
            GameObject instance = Instantiate(carPrefab, startPos, startRot);
            instance.name = $"Car_{p.name}";
            instance.transform.localScale = new Vector3(0.2f, 0.2f, 1f);

            SpriteRenderer sr = instance.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                int teamIdx = (int)p.teamId;
                if (carSprites != null && carSprites.Length > teamIdx && carSprites[teamIdx] != null)
                    sr.sprite = carSprites[teamIdx];
                else if (teamIdx >= 0 && teamIdx < TEAM_COLORS.Length)
                    sr.color = TEAM_COLORS[teamIdx];
            }

            carInstances.Add(instance);
        }

        RefreshVisualCarLanes();
    }

    // ====== 主游戏循环 ======

    private IEnumerator GameLoop()
    {
        while (phase != GamePhase.GameOver)
        {
            // ──── 回合开始 ────
            foreach (var p in session.Players)
            {
                p.ClearTurnState();
                p.trickState.ResetPerTurn();
                if (p.techState != null)
                {
                    TechTreeRules.ResetPerTurnState(p.techState);
                    // 关东慢煮累积的出牌数 → 本回合额外出牌槽
                    p.extraCardSlotsThisTurn += TrickCardRules.ConsumeKantoOden(p.trickState);
                }
            }
            // JP L3 万骨涌：末位/次末位自动激活，逐回合递减
            TickBankuruwaseForAll();

            var turnOrder = session.GetTurnOrder(); // 末位先行（追赶优势）
            var turnSkipped = new HashSet<PlayerState>(); // 本回合被跳过的玩家（失控/维修区）

            // ====== PHASE A1: 档位决策 ======
            foreach (var p in turnOrder)
            {
                if (ShouldSkipTurn(p))
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
                    phase = GamePhase.WaitingForGear;
                    waitingForPlayerGear = true;
                    pendingGear = p.gear;
                    foreach (var kv in gearButtonImages)
                        kv.Value.color = (kv.Key == p.gear) ? new Color(0.3f, 0.8f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.8f);
                    if (hudUI != null) hudUI.SetStatus($"选择档位 (当前: G{p.gear})");
                    if (cardHandUI != null) { cardHandUI.SetGearSelectionMode(true); cardHandUI.UpdateDeckInfo(p); }

                    yield return new WaitWhile(() => waitingForPlayerGear);
                    ApplyGearShift(p, playerGearChoice);
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

            // ====== PHASE A4: 选牌 ======
            foreach (var p in turnOrder)
            {
                // 含 ShouldSkipTurn：AI 在 A3 打出关东慢煮后本回合不再选牌
                if (turnSkipped.Contains(p) || ShouldSkipTurn(p)) continue;

                if (p.isAI)
                {
                    GetAIController(p).SelectCards();
                }
                else
                {
                    phase = GamePhase.WaitingForCards;
                    waitingForPlayerCards = true;
                    if (cardHandUI != null)
                    {
                        cardHandUI.SetGearSelectionMode(false);
                        cardHandUI.ShowHand(this, p);
                        cardHandUI.UpdateDeckInfo(p);
                    }
                    if (hudUI != null)
                        hudUI.SetStatus($"G{p.gear} 档 - 选择速度牌 (最多 {GetMaxSpeedCardsThisTurn(p)} 张; 点特技牌直接打出)");

                    yield return new WaitWhile(() => waitingForPlayerCards);
                }

                // CN L2 连击追踪：特技 → 速度
                if (p.techState != null)
                    TechTreeRules.TrackDimSumCombo(p.techState, false, true, false);
            }

            // ====== 计算移动力（科技 + 特技加成） ======
            ComputeMovements(turnOrder, turnSkipped);

            // ====== PHASE B: 执行阶段 ======
            phase = GamePhase.Animating;
            foreach (var p in turnOrder)
            {
                if (turnSkipped.Contains(p) || ShouldSkipTurn(p) || p.isBlown || p.hasFinished) continue;

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

                // 维修区入口检测（进站 → 冷却全部热量 + 停 1 回合）
                if (config.enablePitLane && PitLaneRules.HasPitLane(trackManager.Nodes) &&
                    PitLaneRules.CrossedPitEntry(oldPos, p.position, trackManager.Nodes))
                {
                    if (p.isAI)
                    {
                        DecideAIPit(p);
                    }
                    else
                    {
                        pitWaitingPlayer = p;
                        yield return StartCoroutine(WaitForPitChoice());
                    }
                }
            }

            // ====== 弃牌（可选，仅玩家） ======
            var human = Player;
            if (human != null && !human.hasFinished && !human.isBlown &&
                !turnSkipped.Contains(human) && !ShouldSkipTurn(human))
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
        phase = GamePhase.GameOver;
        ShowGameOver();
    }

    // ====== 回合跳过（失控 / 维修区 / 关东慢煮） ======

    private bool ShouldSkipTurn(PlayerState p)
    {
        return p.skipNextTurn || p.kantoOdenSkipThisTurn;
    }

    private void ResolveSkip(PlayerState p)
    {
        // 失控恢复 / 进站停靠 — 跳过本回合，G1 冷却仍生效
        if (p.skipNextTurn)
        {
            p.skipNextTurn = false;
            p.gear = config.minGear;
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

        p.spinCounter++;
        int spinMax = session != null ? session.EffectiveSpinMax(p) : 3;
        bool eliminated = p.spinCounter >= spinMax;

        // 回收全部热量回引擎
        p.deck.RecoverAllHeatToPool();

        // 回退位置
        p.position = rewindPos;

        // 强制 1 档
        p.gear = config.minGear;

        // 跳过下回合
        p.skipNextTurn = true;

        // 移动赛车回退位置
        MoveCarTo(p, rewindPos);

        string tag = eliminated ? "<color=red>ELIMINATED!</color>" : $"<color=orange>[{p.spinCounter}/{spinMax}]</color>";
        if (hudUI != null)
            hudUI.AppendLog($"{p.name} <color=red>SPINS OUT!</color> Reason: {reason}. {tag}");

        if (eliminated)
        {
            p.isBlown = true;
            if (hudUI != null)
                hudUI.AppendLog($"<color=red><b>{p.name} has retired from the race!</b></color>");
        }
    }

    /// <summary>
    /// 尝试从引擎支付热量。若引擎不足 → 触发失控。
    /// 支持特技牌黑面包垫底（-1，最少 1）与英国 L1 炸鱼薯条（每场 1 次免单）。
    /// 返回 true 表示支付成功，false 表示已触发失控。
    /// </summary>
    private bool TryPayHeat(PlayerState p, int amount, int rewindPos, string reason)
    {
        if (amount <= 0) return true;

        // 特技牌：黑面包垫底 — 本次热量支付 -1（最少 1）
        amount = TrickCardRules.ApplySchwarzbrot(p.trickState, amount);
        if (amount <= 0) return true;

        int drawn = p.deck.DrawHeatFromPool(amount);
        if (drawn < amount)
        {
            // UK L1 炸鱼薯条：每场限 1 次 — 忽略本次热量判定，回收 1 张热量牌至引擎
            if (p.techState != null && TechTreeRules.CanUseFishAndChips(p.techState, session.TechDb))
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
        session.TrackHeatPaid(p, drawn);
        if (p.techState != null)
            TechTreeRules.TrackDimSumCombo(p.techState, false, false, true);
        return true;
    }

    // ====== 档位处理 ======

    private void ApplyGearShift(PlayerState p, int targetGear)
    {
        GearShiftResult shift = RaceRules.ResolveGearShift(
            p.gear,
            targetGear,
            config.minGear,
            config.maxGear,
            config.twoGearShiftHeatCost);

        if (TryPayHeat(p, shift.HeatCost, p.position, "shift 2 gears"))
        {
            p.gear = shift.TargetGear;
        }
        // 若 TryPayHeat 失败（失控），HandleSpin 已将档位设为最低档

        p.selectedGearThisTurn = p.gear;
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

        int cooldown = RaceRules.GetCooldown(
            p.gear,
            config.gearOneCooldown,
            config.gearTwoCooldown);

        if (p.techState != null)
        {
            cooldown += TechTreeRules.GetBrothCooldownPerTurn(p.techState);
            cooldown += TechTreeRules.GetBankuruwaseCooldownPerTurn(p.techState);
        }

        if (cooldown > 0)
        {
            int removed = p.deck.RemoveHeatFromHand(cooldown);
            if (removed > 0 && hudUI != null)
                hudUI.AppendLog($"{p.name} (G{p.gear}): cools {removed} Heat → engine.");
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

        for (int i = p.position + 1; i <= targetPos; i++)
        {
            int nodeIdx = i % totalNodes;
            Vector3 target = trackManager.GetNodePosition(nodeIdx, laneIndex);

            while (Vector3.Distance(car.transform.position, target) > 0.02f)
            {
                car.transform.position = Vector3.MoveTowards(
                    car.transform.position, target, config.moveAnimSpeed * Time.deltaTime);
                // 赛车随移动方向平滑旋转（P2 #17）
                RotateCarTowards(car, target);
                yield return null;
            }
            car.transform.position = target;
            RotateCarTowards(car, target);

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
    }

    // ====== 移动力计算（科技 + 特技加成） ======

    /// <summary>本回合最大可出速度牌数 = 档位 + 额外槽（关东慢煮/火锅底料）。</summary>
    public int GetMaxSpeedCardsThisTurn(PlayerState p)
    {
        int max = p.gear + p.extraCardSlotsThisTurn;
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
        // 第一轮：基础速度总和（弯道判定用，不含特技/科技加成）
        foreach (var p in turnOrder)
        {
            if (turnSkipped.Contains(p) || ShouldSkipTurn(p) || p.isBlown || p.hasFinished)
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
            if (turnSkipped.Contains(p) || ShouldSkipTurn(p) || p.isBlown || p.hasFinished) continue;

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
            if (TrickCardRules.HasHotpotAttack(p.trickState) && p.cornerTotalThisTurn > 0)
                bonus += TrickCardRules.GetHotpotSpeedBonus();
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

        int total = trackManager.TotalNodes;
        int myOld = p.position;
        int myNew = myOld + p.cornerTotalThisTurn;
        int overtakes = 0;

        foreach (var q in turnOrder)
        {
            if (q == p || q.isBlown || q.hasFinished || ShouldSkipTurn(q)) continue;
            int qOld = q.position;
            int qNew = qOld + q.cornerTotalThisTurn;
            // 对方之前领先我，模拟移动后我领先对方 → 超车
            if (IsAhead(qOld, myOld, total) && !IsAhead(qNew, myNew, total))
                overtakes++;
        }

        return overtakes > 0 ? overtakes * TrickCardRules.GetTorpedoOvertakeBonus() : 0;
    }

    /// <summary>aheadPos 是否在 behindPos 前方（环形赛道半圈内判定）。</summary>
    private static bool IsAhead(int aheadPos, int behindPos, int totalNodes)
    {
        int forward = (aheadPos - behindPos + totalNodes) % totalNodes;
        return forward <= totalNodes / 2;
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
                int heat = Mathf.Max(1, overspeed - session.ConsumeHeatReduction(p));
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

    private IEnumerator WaitForPitChoice()
    {
        if (pitChoicePanel == null) yield break;

        waitingForPitChoice = true;
        pitChoicePanel.SetActive(true);
        if (hudUI != null)
            hudUI.SetStatus("维修区入口：进站冷却全部热量，还是继续比赛？");

        yield return new WaitWhile(() => waitingForPitChoice);

        pitChoicePanel.SetActive(false);
        if (hudUI != null)
            hudUI.SetStatus("");
    }

    /// <summary>玩家选择进站 / 继续比赛。</summary>
    public void ChoosePit(bool enter)
    {
        if (!waitingForPitChoice) return;
        waitingForPitChoice = false;
        if (enter)
            EnterPit(pitWaitingPlayer);
    }

    private void DecideAIPit(PlayerState p)
    {
        // AI 启发：热量高 → 进站
        if (p.HeatRatio >= 0.6f)
        {
            EnterPit(p);
        }
        else if (hudUI != null)
        {
            hudUI.AppendLog($"{p.name} 选择不进站。");
        }
    }

    private void EnterPit(PlayerState p)
    {
        var result = PitLaneRules.EnterPit(p, trackManager.Nodes);
        if (!result.success)
        {
            if (hudUI != null) hudUI.AppendLog(result.message);
            return;
        }

        p.deck.RecoverAllHeatToPool(); // 进站冷却全部热量回引擎
        MoveCarTo(p, p.position);       // 移动到维修区出口
        if (hudUI != null)
            hudUI.AppendLog($"<color=green>{p.name} 进站：冷却全部热量，停靠 {result.turnsSkipped} 回合。</color>");
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
                int cooled = p.deck.RemoveHeatFromHand(result.freeCooldown);
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
                    p.deck.ReturnHeatCardsToPool(heatCards);
                    p.position = (p.position + converted) % trackManager.TotalNodes;
                    MoveCarTo(p, p.position);
                    if (hudUI != null)
                        hudUI.AppendLog($"<color=orange>{p.name} 母亲之路(复兴)！{converted} 张热量牌转为移动。</color>");
                }
                break;
            }
        }
    }

    // ====== 特技牌 ======

    /// <summary>玩家点击手牌中的特技牌 — 直接打出（每回合限 1）。</summary>
    public void OnTrickCardClicked(CardData card)
    {
        if (phase != GamePhase.WaitingForCards) return;
        if (!config.enableTrickCards) return;
        if (Player == null) return;

        PlayTrickCard(Player, card);

        // 关东慢煮：跳过本回合选牌与移动
        if (Player.kantoOdenSkipThisTurn)
        {
            Player.playedSpeedCardsThisTurn.Clear();
            waitingForPlayerCards = false;
            if (cardHandUI != null) cardHandUI.ShowHand(this, Player);
        }
    }

    /// <summary>玩家与 AI 共用的特技牌结算入口。</summary>
    private void PlayTrickCard(PlayerState p, CardData card)
    {
        var result = session.PlayTrick(p, card);
        if (!result.success)
        {
            if (!p.isAI && hudUI != null)
                hudUI.SetStatus($"<color=orange>{result.message}</color>");
            return;
        }

        p.deck.DiscardTrickCard(card);
        if (hudUI != null)
            hudUI.AppendLog($"{p.name} 打出特技牌: {result.message}");

        ApplyTrickEffects(p, card, result);

        // CN L2 连击追踪：特技
        if (p.techState != null)
            TechTreeRules.TrackDimSumCombo(p.techState, true, false, false);
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
            int cooled = p.deck.RemoveHeatFromHand(result.heatToCool);
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

        var def = session.TrickDb.Get(tricks[0].trickId);
        if (def == null) return;

        bool play = false;
        if (def.IsDefense && p.HeatRatio >= config.aiHeatWarningThreshold)
            play = true;
        else if (def.IsAttack && p.HeatRatio <= 0.35f)
            play = true;
        else if (def.effectType == TrickEffectType.KantoOden && session.GetRank(p) >= session.Players.Count)
            play = true;

        if (play)
            PlayTrickCard(p, tricks[0]);
    }

    // ====== 印地安纳波利斯起点换道 ======

    private IEnumerator WaitForIndianapolisLaneChoice()
    {
        if (laneChangePanel == null)
            yield break;

        waitingForPlayerLaneChange = true;
        int humanLane = laneIndices.Count > 0 ? laneIndices[0] : 0;
        laneInButton.interactable = trackManager.GetLaneTowardsInside(humanLane) != humanLane;
        laneOutButton.interactable = trackManager.GetLaneTowardsOutside(humanLane) != humanLane;
        laneKeepButton.interactable = true;
        laneChangePanel.SetActive(true);

        if (hudUI != null)
            hudUI.SetStatus("通过印地起点：选择向内、保持或向外一格");

        yield return new WaitWhile(() => waitingForPlayerLaneChange);

        laneChangePanel.SetActive(false);
        if (hudUI != null)
            hudUI.SetStatus("车道已确定，继续比赛");
    }

    /// <summary>
    /// direction: +1 toward the inside, 0 keep the lane, -1 toward the outside.
    /// </summary>
    public void ChooseIndianapolisLaneChange(int direction)
    {
        if (!waitingForPlayerLaneChange || trackManager == null)
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
        waitingForPlayerLaneChange = false;
        string choice = playerLaneIndex == oldLane
            ? "保持当前车道"
            : playerLaneIndex > oldLane ? "向内一格" : "向外一格";
        if (hudUI != null)
            hudUI.AppendLog($"印地起点换道：{choice}（第 {playerLaneIndex + 1} 道）");
    }

    // ====== 步骤 8：弃牌 ======

    /// <summary>
    /// 弃牌步骤 — 玩家可选择弃掉手中任意非热量牌，之后补牌至 7 张。
    /// </summary>
    private IEnumerator DiscardStep()
    {
        if (cardHandUI == null) yield break;
        var player = Player;
        if (player == null) yield break;

        waitingForPlayerDiscard = true;
        if (cardHandUI != null)
        {
            cardHandUI.SetDiscardMode(true);
            cardHandUI.ShowHand(this, player);
        }
        if (hudUI != null)
            hudUI.SetStatus("弃牌: 点击要弃掉的牌 (非热量牌), 然后点出牌");

        yield return new WaitWhile(() => waitingForPlayerDiscard);

        // 收集选中牌并弃掉
        if (cardHandUI != null)
        {
            List<CardData> toDiscard = cardHandUI.GetSelectedCards();
            foreach (var card in toDiscard)
            {
                if (!card.IsHeat)
                {
                    player.deck.RemoveFromHand(new List<CardData> { card });
                    player.deck.DiscardSpeedCards(new List<CardData> { card });
                }
            }
            if (toDiscard.Count > 0 && hudUI != null)
                hudUI.AppendLog($"{player.name} discards {toDiscard.Count} card(s).");
        }
    }

    // ====== 收尾 ======

    private void CleanupTurn(PlayerState p)
    {
        // 速度牌 → 弃牌堆。热量牌始终留在手牌中，只能通过降档冷却或 G1 散热移除。
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
                int cooled = p.deck.RemoveHeatFromHand(grillCooldown);
                if (cooled > 0 && hudUI != null)
                    hudUI.AppendLog($"<color=green>{p.name} 烤肉拼盘：自动冷却 {cooled} 张热量牌。</color>");
            }
        }
    }

    /// <summary>应用阴阳茶结果：阴 → 付 1 热 +1 格；阳 → 自动冷却。</summary>
    private void ApplyYinYang(PlayerState p, YinYangResult result)
    {
        if (!result.triggered) return;

        if (result.isYin)
        {
            if (TryPayHeat(p, 1, p.positionAtTurnStart, "yin yang (yin)"))
            {
                p.position = (p.position + 1) % trackManager.TotalNodes;
                MoveCarTo(p, p.position);
                if (hudUI != null)
                    hudUI.AppendLog($"<color=orange>{p.name} 阴阳茶(阴)：付 1 热 → +1 格。</color>");
            }
        }
        else if (result.isYang)
        {
            int cooled = p.deck.RemoveHeatFromHand(result.heatToCool);
            if (cooled > 0 && hudUI != null)
                hudUI.AppendLog($"<color=cyan>{p.name} 阴阳茶(阳)：自动冷却 {cooled} 张热量牌。</color>");
        }
    }

    // ====== 圈数与完赛 ======

    public void OnPlayerCrossedStartFinish(PlayerState p)
    {
        if (p.hasFinished) return;

        p.lap++;
        session.OnNewLap(p);
        if (hudUI != null)
            hudUI.AppendLog($"{p.name} 完成第 {p.lap} 圈！");

        // 每圈掷骰换天（同一圈内多辆车过线只掷一次）
        if (config.enableWeather && p.lap != weatherRolledLap)
        {
            weatherRolledLap = p.lap;
            WeatherType before = session.Weather;
            WeatherType after = session.RollWeatherForLap();
            if (after != before && hudUI != null)
            {
                string bLabel = before == WeatherType.Rainy ? "🌧️ 雨天" : "☀️ 晴天";
                hudUI.AppendLog($"<color=cyan>天气变化: {bLabel} → {session.WeatherLabel} (雨天弯道限速 -1)</color>");
            }
        }

        if (p.lap >= config.totalLaps)
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

        if (hudUI != null) hudUI.ShowGameOver(result);
        if (cardHandUI != null) cardHandUI.HideAll();
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
        var lines = new List<string> { "🏆 RP 奖励:" };
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
            }
            lines.Add($"{e.rank}. {p.name}: +{rp} RP{(p.techState != null ? $" (余额 {p.techState.rpBalance})" : "")}");
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

        bool trailingInParallel = IsTrailingInParallel(p);
        return TrackPresentationRules.GetStandardTrafficLaneIndex(
            trackManager.TrackId,
            trailingInParallel);
    }

    private bool IsTrailingInParallel(PlayerState candidate)
    {
        if (session == null || candidate == null || candidate.hasFinished || candidate.isBlown)
            return false;

        int candidateIndex = GetCarIndex(candidate);
        if (candidateIndex < 0)
            return false;

        for (int i = 0; i < session.Players.Count; i++)
        {
            PlayerState other = session.Players[i];
            if (other == candidate || other == null || other.hasFinished || other.isBlown)
                continue;

            // Discrete cells have no longitudinal tie-breaker; session order
            // keeps the side-by-side assignment deterministic.
            if (other.lap == candidate.lap && other.position == candidate.position &&
                i < candidateIndex)
                return true;
        }

        return false;
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
        RotateCarTowards(car, trackManager.GetNodePosition(nextIdx, lane));
    }

    // ====== 赛车朝向（P2 #17 随赛道方向旋转） ======

    /// <summary>朝向目标方向的四元数（扣除精灵固有朝向）。</summary>
    private Quaternion GetFacingRotation(Vector2 direction)
    {
        return Quaternion.Euler(0f, 0f, GetFacingAngle(direction));
    }

    /// <summary>朝向目标方向的角度（度，扣除精灵固有朝向）。</summary>
    private float GetFacingAngle(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return angle - config.carSpriteFacingAngle;
    }

    /// <summary>朝目标点平滑旋转（每帧调用的廉价实现）。</summary>
    private void RotateCarTowards(GameObject car, Vector3 target)
    {
        Vector3 dir = target - car.transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        float targetAngle = GetFacingAngle(dir);
        float currentAngle = car.transform.rotation.eulerAngles.z;
        float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, config.carRotateSpeed * Time.deltaTime);
        car.transform.rotation = Quaternion.Euler(0f, 0f, nextAngle);
    }

    // ====== UI 回调 ======

    public void OnGearButtonClicked(int gear)
    {
        if (phase != GamePhase.WaitingForGear) return;
        pendingGear = gear;
        // 高亮选中的档位按钮
        foreach (var kv in gearButtonImages)
        {
            kv.Value.color = (kv.Key == gear) ? new Color(0.3f, 0.8f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.8f);
        }
        if (hudUI != null)
            hudUI.SetStatus($"已选 G{gear} 档 - 点击确认锁定");
    }

    public void OnConfirmGearClicked()
    {
        if (phase != GamePhase.WaitingForGear) return;
        playerGearChoice = pendingGear;
        waitingForPlayerGear = false;
    }

    public void OnPlayCardsButtonClicked()
    {
        // 弃牌模式 — 点击 PLAY 确认弃牌
        if (waitingForPlayerDiscard)
        {
            waitingForPlayerDiscard = false;
            return;
        }

        if (phase != GamePhase.WaitingForCards) return;
        if (cardHandUI == null) return;
        var player = Player;
        if (player == null) return;

        List<CardData> selected = cardHandUI.GetSelectedCards();
        // selected 已经只包含速度牌（热量牌不可选中，特技牌点击即打出）
        int speedCount = selected.Count;
        int maxCards = GetMaxSpeedCardsThisTurn(player);

        // 不能超出档位 + 额外槽要求
        if (speedCount > maxCards)
        {
            if (hudUI != null)
                hudUI.SetStatus($"<color=orange>速度牌太多! 最多选 {maxCards} 张</color>");
            return;
        }

        // 引擎故障：速度牌不足时，每缺 1 张 → +1 热量到弃牌堆。引擎不足 → 失控
        int required = player.gear + player.extraCardSlotsThisTurn;
        int missing = RaceRules.GetMissingSpeedCardCount(required, speedCount);
        if (missing > 0)
        {
            if (!TryPayHeat(player, missing, player.position, "engine failure"))
            {
                // 失控发生，跳过出牌收尾
                player.playedSpeedCardsThisTurn.Clear();
                player.playedHeatCardsThisTurn.Clear();
                waitingForPlayerCards = false;
                return;
            }
            if (hudUI != null)
                hudUI.AppendLog($"Engine failure! Missing {missing} speed card(s). +{missing} Heat to discard.");
        }

        player.playedSpeedCardsThisTurn.Clear();
        player.playedHeatCardsThisTurn.Clear();

        List<CardData> toRemove = new List<CardData>();
        foreach (var card in selected)
        {
            toRemove.Add(card);
            player.playedSpeedCardsThisTurn.Add(card);
        }

        player.deck.RemoveFromHand(toRemove);
        waitingForPlayerCards = false;
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
