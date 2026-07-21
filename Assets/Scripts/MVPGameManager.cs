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

    [Header("赛车 Prefab")]
    public GameObject carPrefab;

    // --- 运行时状态 ---
    private PlayerState player;
    private PlayerState ai;
    private HeatPool sharedHeatPool;
    private GamePhase phase;

    private GameObject playerCarInstance;
    private GameObject aiCarInstance;

    private WaitForSeconds nodeWait;
    private bool waitingForPlayerGear;
    private bool waitingForPlayerCards;
    private int playerGearChoice;
    private int pendingGear;
    private Dictionary<int, Image> gearButtonImages = new Dictionary<int, Image>();

    // 弯道判定用的位置暂存
    private int playerOldPosition;
    private int aiOldPosition;

    // --- 属性 ---
    public PlayerState Player => player;
    public PlayerState AI => ai;
    public HeatPool SharedHeatPool => sharedHeatPool;
    public GamePhase CurrentPhase => phase;
    public GameConfigSO Config => config;
    public TrackManager Track => trackManager;

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
        if (hudUI == null || cardHandUI == null)
            AutoCreateUI();

        nodeWait = new WaitForSeconds(config.nodeDelay);
        InitializeGame();
        StartCoroutine(GameLoop());
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
        int poolSize = config.heatPoolPerPlayer * 2;
        sharedHeatPool = new HeatPool(poolSize);

        player = new PlayerState("You", false, config.startFinishNodeIndex, config.minGear);
        player.deck.InitializeDeck(config, sharedHeatPool);

        ai = new PlayerState("AI", true, config.startFinishNodeIndex, config.minGear);
        ai.deck.InitializeDeck(config, sharedHeatPool);

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
        if (psr != null) { psr.color = Color.red; }
        playerCarInstance.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        Vector3 aiStartPos = startPos + new Vector3(0.3f, 0.3f, 0);
        aiCarInstance = Instantiate(carPrefab, aiStartPos, Quaternion.identity);
        aiCarInstance.name = "AICar";
        SpriteRenderer asr = aiCarInstance.GetComponent<SpriteRenderer>();
        if (asr != null) { asr.color = Color.blue; }
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

            // ====== PHASE A: 决策阶段 ======

            // Step 1a: 等待玩家选档位（点击 G1-G4 预览，点 CONFIRM 确认）
            phase = GamePhase.WaitingForGear;
            waitingForPlayerGear = true;
            pendingGear = player.gear;
            // 重置档位按钮高亮
            foreach (var kv in gearButtonImages)
                kv.Value.color = (kv.Key == player.gear) ? new Color(0.3f, 0.8f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.8f);
            if (hudUI != null) hudUI.SetStatus($"Select gear (current: G{player.gear})");
            if (cardHandUI != null)
            {
                cardHandUI.SetGearSelectionMode(true);
                // 显示牌堆信息
                cardHandUI.UpdateDeckInfo(player);
            }

            yield return new WaitWhile(() => waitingForPlayerGear);

            ApplyGearShift(player, playerGearChoice);

            // Step 1b: AI 选档位
            if (!ai.isBlown && !ai.hasFinished)
            {
                int aiGearChoice = aiController.DecideGear();
                ApplyGearShift(ai, aiGearChoice);
            }

            // Step 2: 双方抽牌
            bool playerCanDraw = player.deck.DrawToHand(config.handSize);
            if (!playerCanDraw)
            {
                player.isBlown = true;
                if (hudUI != null) hudUI.AppendLog("<color=red>BLOWN ENGINE! No cards to draw - you are eliminated!</color>");
                break;
            }

            if (!ai.isBlown && !ai.hasFinished)
            {
                bool aiCanDraw = ai.deck.DrawToHand(config.handSize);
                if (!aiCanDraw)
                {
                    ai.isBlown = true;
                    if (hudUI != null) hudUI.AppendLog($"<color=orange>{ai.name} blown engine!</color>");
                }
            }

            // G1 冷却奖励：1 档时抽牌后自动消除 1 张热量（手牌优先，否则从牌组删）
            ApplyGear1Cooling(player);
            ApplyGear1Cooling(ai);

            // 更新手牌 UI
            if (cardHandUI != null) cardHandUI.ShowHand(this, player);

            // 全热手牌 → 强制 1 档（避免卡死），免费降档（无激进惩罚）
            if (player.deck.CountSpeedInHand() == 0 && player.gear > config.minGear)
            {
                player.gear = config.minGear;
                if (hudUI != null)
                    hudUI.AppendLog("<color=orange>No speed cards! Forced to Gear 1.</color>");
            }

            // Step 3a: 等待玩家选牌
            phase = GamePhase.WaitingForCards;
            waitingForPlayerCards = true;
            if (cardHandUI != null)
            {
                cardHandUI.SetGearSelectionMode(false);
                cardHandUI.ShowHand(this, player);
                cardHandUI.UpdateDeckInfo(player);
            }
            if (hudUI != null)
                hudUI.SetStatus($"Gear {player.gear} - select up to {player.gear} speed cards (missing = +Heat)");

            yield return new WaitWhile(() => waitingForPlayerCards);

            // Step 3b: AI 选牌
            if (!ai.isBlown && !ai.hasFinished)
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

            // 玩家移动 + 弯道判定
            if (!player.hasFinished && !player.isBlown)
            {
                yield return StartCoroutine(AnimateMovement(player, playerCarInstance));
                ResolveCorners(player, playerOldPosition, playerRawEnd);
            }

            // AI 移动 + 弯道判定
            if (!ai.hasFinished && !ai.isBlown)
            {
                yield return StartCoroutine(AnimateMovement(ai, aiCarInstance));
                ResolveCorners(ai, aiOldPosition, aiRawEnd);
            }

            // Step 7: 收尾
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

    // ====== 档位处理 ======

    private void ApplyGearShift(PlayerState p, int targetGear)
    {
        targetGear = Mathf.Clamp(targetGear, config.minGear, config.maxGear);
        int oldGear = p.gear;

        if (targetGear > oldGear)
        {
            p.gear = Mathf.Min(oldGear + 1, targetGear);
        }
        else if (targetGear < oldGear)
        {
            int gearsDropped = oldGear - targetGear;

            if (gearsDropped <= 2)
            {
                // 降 1-2 档：正常冷却，无惩罚
                p.deck.RemoveHeatFromHand(gearsDropped);
            }
            else
            {
                // 降 3+ 档：急刹冲击引擎 → 反而产生热量，无冷却
                p.deck.DrawHeatFromPool(gearsDropped);
            }

            p.gear = targetGear;
        }

        p.selectedGearThisTurn = p.gear;
    }

    // ====== G1 冷却奖励 ======

    /// <summary>
    /// 1 档专属：抽牌后自动消除 1 张热量牌。
    /// 优先从手牌移除 → 回热量池；如果手牌无 H，从牌组/弃牌堆删除 → 回热量池。
    /// </summary>
    private void ApplyGear1Cooling(PlayerState p)
    {
        if (p.isBlown || p.hasFinished) return;
        if (p.gear != config.minGear) return;

        // 优先从手牌消除
        int removed = p.deck.RemoveHeatFromHand(1);
        if (removed > 0)
        {
            if (hudUI != null)
                hudUI.AppendLog($"{p.name} (G1): cooled 1 Heat from hand.");
            return;
        }

        // 手牌没有 → 从牌组/弃牌堆删除
        if (p.deck.RemoveOneHeatFromDeck())
        {
            if (hudUI != null)
                hudUI.AppendLog($"{p.name} (G1): removed 1 Heat from deck.");
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

        // 使用取模前的位置确保路径遍历方向正确
        HashSet<int> corners = trackManager.GetUniqueCornersCrossed(oldPos, rawEndPos);

        int totalSpeed = SumCardValues(p.playedSpeedCardsThisTurn);
        string log = "";

        foreach (int cornerId in corners)
        {
            int limit = trackManager.GetCornerSpeedLimit(cornerId);
            if (totalSpeed > limit)
            {
                int overspeed = totalSpeed - limit;
                int drawn = p.deck.DrawHeatFromPool(overspeed);
                string cname = trackManager.GetCornerName(cornerId);
                log += $"{p.name} overspeeds at {cname} by {overspeed}! +{drawn} Heat.\n";

                if (drawn < overspeed)
                    log += $"<color=orange>Heat pool low! Missing {overspeed - drawn}.</color>\n";
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

    // ====== 收尾 ======

    private void CleanupTurn(PlayerState p)
    {
        // 所有打出的牌都进弃牌堆（含热量牌），只有降档冷却才能把热量归还池
        p.deck.DiscardSpeedCards(p.playedSpeedCardsThisTurn);
        p.deck.DiscardSpeedCards(p.playedHeatCardsThisTurn);
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
        if (phase != GamePhase.WaitingForCards) return;
        if (cardHandUI == null) return;

        List<CardData> selected = cardHandUI.GetSelectedCards();
        int speedCount = 0, heatCount = 0;
        foreach (var c in selected) { if (c.IsSpeed) speedCount++; else heatCount++; }

        // 不能超出档位要求
        if (speedCount > player.gear)
        {
            if (hudUI != null)
                hudUI.SetStatus($"<color=orange>Too many speed cards! Gear {player.gear} max.</color>");
            return;
        }
        if (heatCount > player.gear)
        {
            if (hudUI != null)
                hudUI.SetStatus($"<color=orange>Too many heat cards! Max {player.gear} at gear {player.gear}.</color>");
            return;
        }

        // 引擎故障：速度牌不足时，每缺 1 张 → +1 热量到弃牌堆
        int missing = player.gear - speedCount;
        if (missing > 0)
        {
            player.deck.DrawHeatFromPool(missing);
            if (hudUI != null)
                hudUI.AppendLog($"Engine failure! Missing {missing} speed card(s). +{missing} Heat to discard.");
        }

        player.playedSpeedCardsThisTurn.Clear();
        player.playedHeatCardsThisTurn.Clear();

        List<CardData> toRemove = new List<CardData>();
        foreach (var card in selected)
        {
            toRemove.Add(card);
            if (card.IsSpeed)
                player.playedSpeedCardsThisTurn.Add(card);
            else
                player.playedHeatCardsThisTurn.Add(card);
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
