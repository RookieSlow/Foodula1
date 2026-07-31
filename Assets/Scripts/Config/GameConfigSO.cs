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

    [Tooltip("JSON track world-space height; defaults to the width for square layouts.")]
    public float trackWorldHeight = 30f;

    [Tooltip("赛道总节点数（硬编码赛道模式）")]
    public int trackNodeCount = 42;

    [Tooltip("比赛总圈数（硬编码赛道模式；JSON 赛道自动读取）")]
    public int totalLaps = 3;

    [Tooltip("起点/终点线所在节点索引")]
    public int startFinishNodeIndex = 0;

    [Header("Race Camera")]
    [Tooltip("Track cells visible behind the player's nearest cell.")]
    [Min(0)]
    public int cameraCellsBehind = 15;

    [Tooltip("Track cells visible ahead of the player's nearest cell.")]
    [Min(0)]
    public int cameraCellsAhead = 15;

    [Tooltip("Extra space around the local track window.")]
    [Min(1f)]
    public float cameraPaddingMultiplier = 1.15f;

    [Tooltip("Minimum orthographic half-height for the main camera.")]
    [Min(0.01f)]
    public float cameraMinimumOrthographicSize = 0.75f;

    [Tooltip("Seconds used to smooth main camera position changes.")]
    [Min(0.01f)]
    public float cameraPositionSmoothTime = 0.18f;

    [Tooltip("Seconds used to smooth main camera zoom changes.")]
    [Min(0.01f)]
    public float cameraZoomSmoothTime = 0.2f;

    [Tooltip("Minimap size in reference-resolution pixels.")]
    public Vector2 minimapSize = new Vector2(320f, 180f);

    [Tooltip("Minimap distance from the top-right corner.")]
    public Vector2 minimapMargin = new Vector2(20f, 20f);

    [Tooltip("Extra world-space framing around the complete track.")]
    [Min(1f)]
    public float minimapWorldPaddingMultiplier = 1.08f;

    [Tooltip("Minimap border thickness in reference-resolution pixels.")]
    [Min(0f)]
    public float minimapFrameThickness = 5f;

    [Tooltip("Player and AI marker size in reference-resolution pixels.")]
    [Min(1f)]
    public float minimapMarkerSize = 16f;

    [Tooltip("Minimap render texture width.")]
    [Min(64)]
    public int minimapRenderWidth = 640;

    [Tooltip("Minimap render texture height.")]
    [Min(64)]
    public int minimapRenderHeight = 360;

    [Tooltip("Maximum fraction of median cell spacing occupied by a node visual.")]
    [Range(0.1f, 1f)]
    public float trackNodeSpacingFillRatio = 0.65f;

    [Tooltip("Smallest scale multiplier allowed for dense-track nodes and labels.")]
    [Range(0.01f, 1f)]
    public float trackNodeMinimumScaleMultiplier = 0.03f;

    [Tooltip("Maximum fraction of median cell spacing occupied by the track line.")]
    [Range(0.05f, 1f)]
    public float trackLineSpacingFillRatio = 0.3f;

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

    [Tooltip("一次跨两档时消耗的引擎热量")]
    [Min(0)]
    public int twoGearShiftHeatCost = 1;

    [Tooltip("1 档反应步骤最多冷却的热量牌数量")]
    [Min(0)]
    public int gearOneCooldown = 3;

    [Tooltip("2 档反应步骤最多冷却的热量牌数量")]
    [Min(0)]
    public int gearTwoCooldown = 1;

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

    [Tooltip("AI 在弯道风险下改用保守选牌的热量阈值")]
    [Range(0f, 1f)]
    public float aiCautiousHeatThreshold = 0.5f;

    [Tooltip("AI 对已选速度牌顺序进行随机变化的概率")]
    [Range(0f, 1f)]
    public float aiCardVariationChance = 0.1f;

    /// <summary>
    /// 速度牌总数（从分布数组计算）。
    /// </summary>
    public int SpeedCardCount => speedCardDistribution.Length;

    /// <summary>
    /// 初始牌组总数（速度牌 + 热量牌）。
    /// </summary>
    public int InitialDeckSize => speedCardDistribution.Length + initialHeatCards;
}
