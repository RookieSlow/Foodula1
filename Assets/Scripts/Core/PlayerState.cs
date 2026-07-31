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

    // --- 持久状态 ---
    public int gear;           // 当前档位 1-4
    public int position;       // 赛道节点索引
    public int lap;            // 已完成的圈数 (0, 1, 2)
    public bool hasFinished;   // 已完成第 3 圈
    public bool isBlown;       // 退赛淘汰（失控计数器 = 3）
    public int spinCounter;    // 失控计数器 0-3，到 3 淘汰
    public bool skipNextTurn;  // 失控恢复，跳过下一回合
    public int finishOrder;    // 完赛顺序（0=未完赛, 1=第一, 2=第二...）
    public CardDeck deck;      // 牌组/手牌/弃牌堆管理

    // --- 每回合临时状态 ---
    public int selectedGearThisTurn;
    public List<CardData> playedSpeedCardsThisTurn = new List<CardData>();
    public List<CardData> playedHeatCardsThisTurn = new List<CardData>();
    public int totalMovementThisTurn;

    public PlayerState(string name, bool isAI, int startPosition, int startGear)
    {
        this.name = name;
        this.isAI = isAI;
        this.position = startPosition;
        this.gear = startGear;
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
    }
}
