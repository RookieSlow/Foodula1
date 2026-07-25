using UnityEngine;

/// <summary>
/// MVP 游戏配置 — 所有可调参数集中在 ScriptableObject 中，
/// 可在 Unity Inspector 中直接修改，无需重新编译。
/// </summary>
[CreateAssetMenu(menuName = "Foodular1/MVP Game Config", fileName = "MVPGameConfig")]
public class GameConfigSO : ScriptableObject
{
    [Header("赛道")]
    [Tooltip("从 Resources/Configs/Tracks/ 加载的赛道ID（留空则使用硬编码42节点赛道）")]
    public string trackId = "";

    [Tooltip("JSON 赛道世界空间缩放")]
    public float trackWorldSize = 30f;

    [Tooltip("赛道总节点数（硬编码赛道模式）")]
    public int trackNodeCount = 42;

    [Tooltip("比赛总圈数（硬编码赛道模式；JSON 赛道自动读取）")]
    public int totalLaps = 3;

    [Tooltip("起点/终点线所在节点索引")]
    public int startFinishNodeIndex = 0;

    [Header("牌组")]
    [Tooltip("手牌上限")]
    public int handSize = 7;

    [Tooltip("速度牌分布 — 数组中的每个值代表一张速度牌的数值")]
    public int[] speedCardDistribution = { 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4 };

    [Tooltip("初始牌组中的热量牌数量")]
    public int initialHeatCards = 3;

    [Tooltip("每玩家的独立引擎牌库大小（热量牌数量）")]
    public int heatPoolPerPlayer = 6;

    [Header("档位")]
    [Tooltip("最低档位")]
    public int minGear = 1;

    [Tooltip("最高档位")]
    public int maxGear = 4;

    [Header("动画")]
    [Tooltip("赛车每步移动速度 (单位/秒)")]
    public float moveAnimSpeed = 12f;

    [Tooltip("每节点移动后的暂停时间 (秒)")]
    public float nodeDelay = 0.02f;

    [Header("AI")]
    [Tooltip("AI 热量警告阈值 (热量牌数/手牌上限)")]
    [Range(0f, 1f)]
    public float aiHeatWarningThreshold = 0.7f;

    [Tooltip("AI 弯道预判距离 (节点数)")]
    public int aiLookAheadNodes = 6;

    [Tooltip("AI 激进推进的热量阈值 (热量牌数/手牌上限)")]
    [Range(0f, 1f)]
    public float aiAggressiveHeatThreshold = 0.3f;

    /// <summary>
    /// 速度牌总数（从分布数组计算）。
    /// </summary>
    public int SpeedCardCount => speedCardDistribution.Length;

    /// <summary>
    /// 初始牌组总数（速度牌 + 热量牌）。
    /// </summary>
    public int InitialDeckSize => speedCardDistribution.Length + initialHeatCards;
}
