using System.Collections.Generic;

/// <summary>
/// 单个玩家/AI 的完整运行时状态。纯数据类，不继承 MonoBehaviour。
/// 由 MVPGameManager 创建和管理。
/// </summary>
[System.Serializable]
public class PlayerState
{
    public string name;
    public bool isAI;
    public TeamId teamId;
    /// <summary>Selected driver catalog ID; empty means use the team default.</summary>
    public string driverId;
    /// <summary>Persistent driver XP carried by a campaign/save layer.</summary>
    public int driverXp;

    public DriverProfile DriverProfile
    {
        get
        {
            return DriverCatalog.TryGet(driverId, out DriverProfile profile)
                ? profile
                : DriverCatalog.GetDefaultForTeam(teamId);
        }
    }

    public int DriverLevel => DriverProgression.GetLevel(driverXp);

    /// <summary>Per-race active skill state. Initialized by race setup.</summary>
    public DriverSkillRuntimeState driverSkill = new DriverSkillRuntimeState();

    // --- 持久状态 ---
    public int gear;           // 标准车队 1-4；中国队 1=Recover、2=Go
    /// <summary>Set by the race setup when the electric dual-gear module is active.</summary>
    public bool usesChinaGearSystem;
    /// <summary>
    /// 中国电动双档连续使用次数。切换 Go/Recover 时由 TeamGearRules 重置。
    /// 标准车队保持 0，避免把车队特有状态散落到通用换档逻辑中。
    /// </summary>
    public int chinaConsecutiveGearCount;
    public int position;       // 赛道节点索引
    public int lap;            // 已完成的圈数 (0, 1, 2)
    public bool hasFinished;   // 已完成第 3 圈
    public bool isBlown;       // 退赛淘汰（失控计数器 = 3）
    public int spinCounter;    // 失控计数器 0-3，到 3 淘汰
    public bool skipNextTurn;  // 失控恢复，跳过下一回合
    /// <summary>在维修区入口前窗口内已选择进站，等待车辆越过入口。</summary>
    public bool pitStopRequested;
    /// <summary>车辆已越过入口，下一回合开始时执行进站停靠。</summary>
    public bool pitStopScheduled;
    /// <summary>本次入口接近窗口已经作出选择，避免连续弹窗。</summary>
    public bool pitChoiceResolvedThisLap;
    public int finishOrder;    // 完赛顺序（0=未完赛, 1=第一, 2=第二...）
    public CardDeck deck;      // 牌组/手牌/弃牌堆管理

    // --- 扩展系统状态（5 系统接入） ---
    /// <summary>科技树运行时状态（enableTechTree=false 时为 null）。</summary>
    public TechTreeState techState;

    /// <summary>特技牌运行时状态（每回合/每场跟踪）。</summary>
    public TrickCardState trickState = new TrickCardState();

    // --- 每回合临时状态 ---
    public int selectedGearThisTurn;
    public List<CardData> playedSpeedCardsThisTurn = new List<CardData>();
    public List<CardData> playedHeatCardsThisTurn = new List<CardData>();
    public int totalMovementThisTurn;

    /// <summary>本回合特技牌附加移动（司康 / 关东慢煮前等）。</summary>
    public int trickMoveBonusThisTurn;

    /// <summary>弯道判定用总速度（火锅底料强化的整张 ATTACK 速度牌不计入弯道判定）。</summary>
    public int cornerTotalThisTurn;

    /// <summary>火锅底料本回合是否已经强化了一张正常速度牌。</summary>
    public bool hotpotAttackAppliedThisTurn;

    /// <summary>被火锅底料标记为 ATTACK 的速度牌原始数值；只用于本回合结算。</summary>
    public int hotpotAttackCardValueThisTurn;

    /// <summary>额外出牌槽（关东慢煮累加或其他科技效果；火锅底料不再增加槽位）。</summary>
    public int extraCardSlotsThisTurn;

    /// <summary>关东慢煮：本回合剩余部分跳过（仅本回合，回合开始清除）。</summary>
    public bool kantoOdenSkipThisTurn;

    /// <summary>本回合尾流距离加成（FullEnglish / 其他临时来源）。</summary>
    public int slipstreamRangeBonusThisTurn;

    /// <summary>
    /// Italy's Passione in Curva reward armed by a successfully completed
    /// corner. It persists until the next playable turn consumes it on the
    /// first speed card; ordinary per-turn cleanup must not clear it.
    /// </summary>
    public bool italyCornerExitBoostReady;

    /// <summary>回合开始时的位置（失控回退 / 阴阳茶结算用）。</summary>
    public int positionAtTurnStart;

    public PlayerState(string name, bool isAI, int startPosition, int startGear)
    {
        this.name = name;
        this.isAI = isAI;
        this.position = startPosition;
        this.gear = startGear;
        this.chinaConsecutiveGearCount = 0;
        this.usesChinaGearSystem = false;
        this.driverId = string.Empty;
        this.driverXp = 0;
        this.lap = 0;
        this.hasFinished = false;
        this.isBlown = false;
        this.deck = new CardDeck();
    }

    /// <summary>
    /// 手牌中热量牌所占比率 (0-1)。
    /// </summary>
    public float HeatRatio
    {
        get
        {
            int total = deck.HandCount;
            if (total == 0) return 0f;
            return (float)deck.CountHeatInHand() / total;
        }
    }

    /// <summary>
    /// 手牌中速度牌数量。
    /// </summary>
    public int SpeedCardCount => deck.CountSpeedInHand();

    /// <summary>
    /// 重置临时回合状态。
    /// </summary>
    public void ClearTurnState()
    {
        playedSpeedCardsThisTurn.Clear();
        playedHeatCardsThisTurn.Clear();
        totalMovementThisTurn = 0;
        selectedGearThisTurn = gear;
        trickMoveBonusThisTurn = 0;
        cornerTotalThisTurn = 0;
        hotpotAttackAppliedThisTurn = false;
        hotpotAttackCardValueThisTurn = 0;
        extraCardSlotsThisTurn = 0;
        kantoOdenSkipThisTurn = false;
        slipstreamRangeBonusThisTurn = 0;
        positionAtTurnStart = position;
        driverSkill?.BeginTurn();
    }
}
