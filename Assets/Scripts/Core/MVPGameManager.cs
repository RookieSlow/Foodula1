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
/// MVP 游戏主管理器 — 协程驱动的回合制 HEAT 核心循环。
/// 挂载到场景中的 GameManager GameObject 上。
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
    private PlayerState player;
    private PlayerState ai;
    private GamePhase phase;

    private GameObject playerCarInstance;
    private GameObject aiCarInstance;

    private WaitForSeconds nodeWait;
    private bool waitingForPlayerGear;
    private bool waitingForPlayerCards;
    private bool waitingForPlayerDiscard;
    private int playerGearChoice;
    private int pendingGear;
    private Dictionary<int, Image> gearButtonImages = new Dictionary<int, Image>();

    // 弯道判定用的位置暂存
    private int playerOldPosition;
    private int aiOldPosition;

    // --- 属性 ---
    public PlayerState Player => player;
    public PlayerState AI => ai;
    public GamePhase CurrentPhase => phase;
    public GameConfigSO Config => config;
    public TrackManager Track => trackManager;

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

        nodeWait = new WaitForSeconds(config.nodeDelay);
        InitializeGame();
        StartCoroutine(GameLoop());
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
            hudUI.statusText = CreateTMPText(hudGO.transform, "StatusText", "Select Gear (1-4)", 22,
                new Vector2(-300, 180), new Vector2(420, 30), fontAsset);
            hudUI.gearText = CreateTMPText(hudGO.transform, "GearText", "Gear: 1", 18,
                new Vector2(-400, 150), new Vector2(150, 25), fontAsset);
            hudUI.heatText = CreateTMPText(hudGO.transform, "HeatText", "Heat Pool: 12", 18,
                new Vector2(-400, 125), new Vector2(280, 25), fontAsset);
            hudUI.lapText = CreateTMPText(hudGO.transform, "LapText", "Lap: 0/3", 18,
                new Vector2(-400, 100), new Vector2(200, 25), fontAsset);
            hudUI.positionText = CreateTMPText(hudGO.transform, "PositionText", "Pos: 0/42", 18,
                new Vector2(-400, 75), new Vector2(200, 25), fontAsset);
            hudUI.aiStatusText = CreateTMPText(hudGO.transform, "AIStatusText", "AI: ready", 16,
                new Vector2(250, 50), new Vector2(200, 25), fontAsset);
            hudUI.logText = CreateTMPText(hudGO.transform, "LogText", "", 13,
                new Vector2(0, -160), new Vector2(750, 180), fontAsset);

            // 创建 4 个档位按钮 + CONFIRM 按钮（屏幕顶部）
            CreateGearButton(canvas.transform, "Gear1Btn", "G1", new Vector2(-380, 220), 1);
            CreateGearButton(canvas.transform, "Gear2Btn", "G2", new Vector2(-290, 220), 2);
            CreateGearButton(canvas.transform, "Gear3Btn", "G3", new Vector2(-200, 220), 3);
            CreateGearButton(canvas.transform, "Gear4Btn", "G4", new Vector2(-110, 220), 4);
            CreateActionButton(canvas.transform, "ConfirmGearBtn", "CONFIRM", new Vector2(10, 220),
                new Color(0.4f, 0.7f, 1f), () => OnConfirmGearClicked());

            // 创建 PLAY 和 RESET 按钮（替代旧的 NextRound/Reset）
            CreateActionButton(canvas.transform, "PlayBtn", "PLAY", new Vector2(-300, -185), Color.green,
                () => OnPlayCardsButtonClicked());
            CreateActionButton(canvas.transform, "ResetBtn", "RESET", new Vector2(-150, -185), Color.yellow,
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
                "Deck: 12S + 3H", 14, new Vector2(-300, -100), new Vector2(200, 25), fontAsset);

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

    private void CreateActionButton(Transform parent, string name, string label, Vector2 pos, Color color,
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

    private void BindExistingButton(string name, string methodName)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
        {
            UnityEngine.UI.Button btn = go.GetComponent<UnityEngine.UI.Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                if (methodName == nameof(OnPlayCardsButtonClicked))
                    btn.onClick.AddListener(OnPlayCardsButtonClicked);
                else if (methodName == nameof(ResetGame))
                    btn.onClick.AddListener(ResetGame);
            }
        }
    }

    // ====== 初始化 ======

    private void InitializeGame()
    {
        int poolSize = config.heatPoolPerPlayer;

        player = new PlayerState("You", false, config.startFinishNodeIndex, config.minGear);
        player.deck.InitializeDeck(config, new HeatPool(poolSize));

        ai = new PlayerState("AI", true, config.startFinishNodeIndex, config.minGear);
        ai.deck.InitializeDeck(config, new HeatPool(poolSize));

        player.deck.DrawToHand(config.handSize);
        ai.deck.DrawToHand(config.handSize);

        SpawnCars();

        if (aiController != null)
            aiController.Initialize(this, ai);

        phase = GamePhase.WaitingForGear;
        waitingForPlayerGear = true;
        waitingForPlayerCards = false;

        if (hudUI != null)
        {
            hudUI.SetGameManager(this);
            hudUI.Refresh(this, player, ai);
        }
        if (cardHandUI != null)
            cardHandUI.ShowHand(this, player);
    }

    private void SpawnCars()
    {
        if (carPrefab == null) return;

        // 清理旧实例
        if (playerCarInstance != null) Destroy(playerCarInstance);
        if (aiCarInstance != null) Destroy(aiCarInstance);

        Vector3 startPos = trackManager.GetNodePosition(config.startFinishNodeIndex);

        playerCarInstance = Instantiate(carPrefab, startPos, Quaternion.identity);
        playerCarInstance.name = "PlayerCar";
        SpriteRenderer psr = playerCarInstance.GetComponent<SpriteRenderer>();
        if (psr != null)
        {
            // 精灵图优先；没有则回退到颜色
            if (carSprites.Length > 0 && carSprites[0] != null)
                psr.sprite = carSprites[0];
            else
                psr.color = Color.red;
        }
        playerCarInstance.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        Vector3 aiStartPos = startPos + new Vector3(0.3f, 0.3f, 0);
        aiCarInstance = Instantiate(carPrefab, aiStartPos, Quaternion.identity);
        aiCarInstance.name = "AICar";
        SpriteRenderer asr = aiCarInstance.GetComponent<SpriteRenderer>();
        if (asr != null)
        {
            if (carSprites.Length > 1 && carSprites[1] != null)
                asr.sprite = carSprites[1];
            else
                asr.color = Color.blue;
        }
        aiCarInstance.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
    }

    // ====== 主游戏循环 ======

    private IEnumerator GameLoop()
    {
        while (phase != GamePhase.GameOver)
        {
            // ──── 回合开始 ────
            player.ClearTurnState();
            ai.ClearTurnState();

            bool playerSkip = player.skipNextTurn && !player.isBlown && !player.hasFinished;
            bool aiSkip = ai.skipNextTurn && !ai.isBlown && !ai.hasFinished;

            // ====== PHASE A: 决策阶段 ======

            if (playerSkip)
            {
                // 失控恢复 — 跳过本回合，G1 冷却仍生效
                player.skipNextTurn = false;
                player.gear = config.minGear;
                if (hudUI != null)
                    hudUI.AppendLog($"{player.name} sits out this turn (spin recovery).");
            }
            else
            {
                // Step 1a: 等待玩家选档位
                phase = GamePhase.WaitingForGear;
                waitingForPlayerGear = true;
                pendingGear = player.gear;
                foreach (var kv in gearButtonImages)
                    kv.Value.color = (kv.Key == player.gear) ? new Color(0.3f, 0.8f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.8f);
                if (hudUI != null) hudUI.SetStatus($"Select gear (current: G{player.gear})");
                if (cardHandUI != null) { cardHandUI.SetGearSelectionMode(true); cardHandUI.UpdateDeckInfo(player); }

                yield return new WaitWhile(() => waitingForPlayerGear);
                ApplyGearShift(player, playerGearChoice);
            }

            // Step 1b: AI 选档位
            if (aiSkip)
            {
                ai.skipNextTurn = false;
                ai.gear = config.minGear;
                if (hudUI != null)
                    hudUI.AppendLog($"{ai.name} sits out this turn (spin recovery).");
            }
            else if (!ai.isBlown && !ai.hasFinished)
            {
                int aiGearChoice = aiController.DecideGear();
                ApplyGearShift(ai, aiGearChoice);
            }

            // Step 2: 双方抽牌
            if (!playerSkip)
            {
                bool playerCanDraw = player.deck.DrawToHand(config.handSize);
                if (!playerCanDraw)
                {
                    if (hudUI != null)
                        hudUI.AppendLog($"<color=orange>{player.name}: deck exhausted! Playing with {player.deck.HandCount} cards.</color>");
                }
            }

            if (!aiSkip && !ai.isBlown && !ai.hasFinished)
            {
                bool aiCanDraw = ai.deck.DrawToHand(config.handSize);
                if (!aiCanDraw)
                {
                    if (hudUI != null)
                        hudUI.AppendLog($"<color=orange>{ai.name}: deck exhausted!</color>");
                }
            }

            // G1 自动散热已移除 — 改用步骤 5 反应阶段的档位冷却

            // 更新手牌 UI
            if (cardHandUI != null) cardHandUI.ShowHand(this, player);

            // 全热手牌 → 强制 1 档
            if (!playerSkip && player.deck.CountSpeedInHand() == 0 && player.gear > config.minGear)
            {
                player.gear = config.minGear;
                if (hudUI != null)
                    hudUI.AppendLog("<color=orange>No speed cards! Forced to Gear 1.</color>");
            }

            // Step 3a: 等待玩家选牌
            if (!playerSkip)
            {
                phase = GamePhase.WaitingForCards;
                waitingForPlayerCards = true;
                if (cardHandUI != null)
                {
                    cardHandUI.SetGearSelectionMode(false);
                    cardHandUI.ShowHand(this, player);
                    cardHandUI.UpdateDeckInfo(player);
                }
                if (hudUI != null)
                    hudUI.SetStatus($"Gear {player.gear} - select {player.gear} speed card(s) (heat cards stay in hand)");

                yield return new WaitWhile(() => waitingForPlayerCards);
            }

            // Step 3b: AI 选牌
            if (!aiSkip && !ai.isBlown && !ai.hasFinished)
            {
                aiController.SelectCards();
            }

            // 计算双方移动力
            player.totalMovementThisTurn = SumCardValues(player.playedSpeedCardsThisTurn);
            ai.totalMovementThisTurn = SumCardValues(ai.playedSpeedCardsThisTurn);

            // ====== PHASE B: 执行阶段 ======
            phase = GamePhase.Animating;

            // 保存移动前位置 + 计算原始目标位置（用于弯道判定）
            playerOldPosition = player.position;
            aiOldPosition = ai.position;
            int playerRawEnd = player.position + player.totalMovementThisTurn;
            int aiRawEnd = ai.position + ai.totalMovementThisTurn;

            // 玩家：移动 → 反应(冷却) → 弯道判定
            if (!player.hasFinished && !player.isBlown)
            {
                yield return StartCoroutine(AnimateMovement(player, playerCarInstance));
                ReactStep(player);  // G1=冷却3, G2=冷却1
                ResolveCorners(player, playerOldPosition, playerRawEnd);
            }

            // AI：移动 → 反应(冷却) → 弯道判定
            if (!ai.hasFinished && !ai.isBlown)
            {
                yield return StartCoroutine(AnimateMovement(ai, aiCarInstance));
                ReactStep(ai);
                ResolveCorners(ai, aiOldPosition, aiRawEnd);
            }

            // Step 8: 弃牌（可选，仅玩家）
            if (!player.hasFinished && !player.isBlown)
            {
                yield return StartCoroutine(DiscardStep());
            }

            // Step 9: 收尾 + 补牌
            CleanupTurn(player);
            CleanupTurn(ai);

            // 检查游戏是否结束
            if (CheckGameEnd()) break;

            // 刷新 UI
            if (hudUI != null) hudUI.Refresh(this, player, ai);
        }

        // ──── 游戏结束 ────
        phase = GamePhase.GameOver;
        ShowGameOver();
    }

    // ====== 失控处理 ======

    /// <summary>
    /// 统一失控处理 — 引擎不足支付时触发。
    /// 弯心超速/引擎故障/急刹→引擎干了→失控。
    /// 计数器 3 次 → 退赛淘汰。
    /// </summary>
    public void HandleSpin(PlayerState p, int rewindPos, string reason)
    {
        if (p.isBlown) return;

        p.spinCounter++;
        bool eliminated = p.spinCounter >= 3;

        // 回收全部热量回引擎
        p.deck.RecoverAllHeatToPool();

        // 回退位置
        p.position = rewindPos;

        // 强制 1 档
        p.gear = config.minGear;

        // 跳过下回合
        p.skipNextTurn = true;

        // 移动赛车回退位置
        if (p == player && playerCarInstance != null)
            playerCarInstance.transform.position = trackManager.GetNodePosition(rewindPos);
        else if (p == ai && aiCarInstance != null)
            aiCarInstance.transform.position = trackManager.GetNodePosition(rewindPos);

        string tag = eliminated ? "<color=red>ELIMINATED!</color>" : $"<color=orange>[{p.spinCounter}/3]</color>";
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
    /// 返回 true 表示支付成功，false 表示已触发失控。
    /// </summary>
    private bool TryPayHeat(PlayerState p, int amount, int rewindPos, string reason)
    {
        if (amount <= 0) return true;
        int drawn = p.deck.DrawHeatFromPool(amount);
        if (drawn < amount)
        {
            HandleSpin(p, rewindPos, reason);
            return false;
        }
        return true;
    }

    // ====== 档位处理 ======

    private void ApplyGearShift(PlayerState p, int targetGear)
    {
        targetGear = Mathf.Clamp(targetGear, config.minGear, config.maxGear);
        int oldGear = p.gear;
        int delta = targetGear - oldGear;
        int absDelta = Mathf.Abs(delta);

        if (absDelta <= 1)
        {
            // ±1 档：免费
            p.gear = targetGear;
        }
        else
        {
            // ±2 档：支付 1 热
            int actualTarget = oldGear + System.Math.Sign(delta) * 2;
            actualTarget = Mathf.Clamp(actualTarget, config.minGear, config.maxGear);
            if (TryPayHeat(p, 1, p.position, "shift 2 gears"))
            {
                p.gear = actualTarget;
            }
            // 若 TryPayHeat 失败（失控），HandleSpin 已将档位设为 1
        }

        p.selectedGearThisTurn = p.gear;
    }

    // ====== 步骤 5：反应（冷却） ======

    /// <summary>
    /// HEAT 规则书步骤 5：根据档位执行冷却。
    /// G1 = 冷却 3，G2 = 冷却 1，G3/G4 = 无冷却。
    /// </summary>
    private void ReactStep(PlayerState p)
    {
        if (p.isBlown || p.hasFinished) return;

        int cooldown = 0;
        if (p.gear == 1) cooldown = 3;
        else if (p.gear == 2) cooldown = 1;

        if (cooldown > 0)
        {
            int removed = p.deck.RemoveHeatFromHand(cooldown);
            if (removed > 0 && hudUI != null)
                hudUI.AppendLog($"{p.name} (G{p.gear}): cools {removed} Heat → engine.");
        }
    }

    // ====== 卡牌工具 ======

    private int SumCardValues(List<CardData> cards)
    {
        int sum = 0;
        foreach (var c in cards) sum += c.value;
        return sum;
    }

    // ====== 移动动画（含圈数检测） ======

    private IEnumerator AnimateMovement(PlayerState p, GameObject carInstance)
    {
        if (carInstance == null) yield break;

        int totalMove = p.totalMovementThisTurn;
        int totalNodes = trackManager.TotalNodes;
        int targetPos = p.position + totalMove;

        for (int i = p.position + 1; i <= targetPos; i++)
        {
            int nodeIdx = i % totalNodes;
            Vector3 target = trackManager.GetNodePosition(nodeIdx);

            while (Vector3.Distance(carInstance.transform.position, target) > 0.02f)
            {
                carInstance.transform.position = Vector3.MoveTowards(
                    carInstance.transform.position, target, config.moveAnimSpeed * Time.deltaTime);
                yield return null;
            }
            carInstance.transform.position = target;

            // 检测跨过起点/终点线
            if (nodeIdx == config.startFinishNodeIndex)
            {
                OnPlayerCrossedStartFinish(p);
            }

            yield return nodeWait;
        }

        p.position = targetPos % totalNodes;
    }

    // ====== 弯道判定（per-corner-segment） ======

    private void ResolveCorners(PlayerState p, int oldPos, int rawEndPos)
    {
        if (p.totalMovementThisTurn <= 0) return;
        if (p.isBlown) return;

        HashSet<int> corners = trackManager.GetUniqueCornersCrossed(oldPos, rawEndPos);

        int totalSpeed = SumCardValues(p.playedSpeedCardsThisTurn);
        string log = "";

        foreach (int cornerId in corners)
        {
            int limit = trackManager.GetCornerSpeedLimit(cornerId);
            if (totalSpeed > limit)
            {
                int overspeed = totalSpeed - limit;
                string cname = trackManager.GetCornerName(cornerId);

                // 尝试支付热量；引擎不足 → 失控
                if (!TryPayHeat(p, overspeed, oldPos, $"overspeed at {cname} ({totalSpeed}>{limit})"))
                {
                    if (hudUI != null) hudUI.AppendLog(log);
                    return; // 失控中断后续弯道判定
                }

                log += $"{p.name} overspeeds at {cname} by {overspeed}! +{overspeed} Heat.\n";
            }
            else
            {
                log += $"{p.name} safely passes {trackManager.GetCornerName(cornerId)} ({totalSpeed}<={limit}).\n";
            }
        }

        if (string.IsNullOrEmpty(log))
            log = $"{p.name} straight - no corners.\n";

        if (hudUI != null) hudUI.AppendLog(log);
    }

    // ====== 步骤 8：弃牌 ======

    /// <summary>
    /// 弃牌步骤 — 玩家可选择弃掉手中任意非热量牌，之后补牌至 7 张。
    /// </summary>
    private System.Collections.IEnumerator DiscardStep()
    {
        if (cardHandUI == null) yield break;

        waitingForPlayerDiscard = true;
        if (cardHandUI != null)
        {
            cardHandUI.SetDiscardMode(true);
            cardHandUI.ShowHand(this, player);
        }
        if (hudUI != null)
            hudUI.SetStatus("Discard: click cards to discard (non-heat only), then PLAY.");

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
    }

    // ====== 圈数与完赛 ======

    public void OnPlayerCrossedStartFinish(PlayerState p)
    {
        if (p.hasFinished) return;

        p.lap++;
        if (hudUI != null)
            hudUI.AppendLog($"{p.name} completes lap {p.lap}!");

        if (p.lap >= config.totalLaps)
        {
            p.hasFinished = true;
            if (hudUI != null)
                hudUI.AppendLog($"<color=green><b>{p.name} FINISHES!</b></color>");
        }
    }

    // ====== 游戏结束 ======

    private bool CheckGameEnd()
    {
        // 任一方爆缸 → 结束
        if (player.isBlown || ai.isBlown) return true;
        // 任一方完赛 → 结束（另一方继续完成当前回合后结束）
        if (player.hasFinished || ai.hasFinished) return true;
        return false;
    }

    private void ShowGameOver()
    {
        string result = "=== RACE OVER ===\n\n";

        if (player.isBlown)
            result += "<color=red>BLOWN ENGINE!</color>\n";
        else if (player.hasFinished)
            result += $"<color=green>You finished {config.totalLaps} laps!</color>\n";
        else
            result += $"You: {player.lap} laps, pos {player.position}\n";

        if (ai.isBlown)
            result += $"<color=orange>{ai.name} BLOWN</color>\n";
        else if (ai.hasFinished)
            result += $"{ai.name} finished {config.totalLaps} laps\n";
        else
            result += $"{ai.name}: {ai.lap} laps, pos {ai.position}\n";

        result += "\nRanking:\n";
        result += GetRanking();

        if (hudUI != null) hudUI.ShowGameOver(result);
        if (cardHandUI != null) cardHandUI.HideAll();
    }

    private string GetRanking()
    {
        var players = new List<PlayerState> { player, ai };
        players.Sort((a, b) =>
        {
            int lapCmp = b.lap.CompareTo(a.lap);
            if (lapCmp != 0) return lapCmp;
            return b.position.CompareTo(a.position);
        });

        string ranking = "";
        int rank = 1;
        foreach (var p in players)
        {
            string status = p.isBlown ? " (BLOWN)" : p.hasFinished ? " (FIN)" : "";
            ranking += $"{rank}. {p.name} - Lap {p.lap} Pos {p.position}{status}\n";
            rank++;
        }
        return ranking;
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
            hudUI.SetStatus($"Gear {gear} selected - click CONFIRM to lock in");
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

        List<CardData> selected = cardHandUI.GetSelectedCards();
        // selected 已经只包含速度牌（热量牌不可选中）
        int speedCount = selected.Count;

        // 不能超出档位要求
        if (speedCount > player.gear)
        {
            if (hudUI != null)
                hudUI.SetStatus($"<color=orange>Too many speed cards! Gear {player.gear} max.</color>");
            return;
        }

        // 引擎故障：速度牌不足时，每缺 1 张 → +1 热量到弃牌堆。引擎不足 → 失控
        int missing = player.gear - speedCount;
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
        InitializeGame();
        StartCoroutine(GameLoop());
    }
}
