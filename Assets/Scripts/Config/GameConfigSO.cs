using UnityEngine;

/// <summary>
/// MVP 游戏配置 — 所有可调参数集中在 ScriptableObject 中，
/// 可在 Unity Inspector 中直接修改，无需重新编译。
/// </summary>
[CreateAssetMenu(menuName = "Foodula1/MVP Game Config", fileName = "MVPGameConfig")]
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
    public float cameraPaddingMultiplier = 1.08f;

    [Tooltip("Minimum orthographic half-height for the main camera.")]
    [Min(0.01f)]
    public float cameraMinimumOrthographicSize = 0.75f;

    [Tooltip("Seconds used to smooth main camera position changes.")]
    [Min(0.01f)]
    public float cameraPositionSmoothTime = 0.18f;

    [Tooltip("Seconds used to smooth main camera zoom changes.")]
    [Min(0.01f)]
    public float cameraZoomSmoothTime = 0.18f;

    [Tooltip("World movement multiplier while dragging the main track view.")]
    [Min(0.01f)]
    public float cameraDragSensitivity = 1f;

    [Tooltip("Mouse-wheel zoom strength for the main track view.")]
    [Range(0.01f, 1f)]
    public float cameraZoomSensitivity = 0.16f;

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

    [Tooltip("横向车道中心间距（世界单位）。普通赛道使用 2 条车道，印第安纳波利斯使用 4 条车道。")]
    [Min(0.01f)]
    public float trackLaneSpacing = 0.28f;

    [Header("牌组")]
    [Tooltip("手牌上限")]
    public int handSize = 7;

    [Tooltip("速度牌分布 — 数组中的每个值代表一张速度牌的数值")]
    public int[] speedCardDistribution = { 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4 };

    [Tooltip("已废弃：热量牌不属于普通牌组；保留此字段仅为兼容旧配置，运行时不再读取")]
    public int initialHeatCards = 0;

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

    [Tooltip("赛车在两个相邻赛道格之间完成一次跳跃的时长 (秒)")]
    [Min(0.01f)]
    public float nodeMoveDuration = 0.15f;

    [Tooltip("赛车逐格跳跃时相对赛道中心线的视觉高度 (世界单位)")]
    [Min(0f)]
    public float nodeBounceHeight = 0.08f;

    [Tooltip("每节点移动后的暂停时间 (秒)")]
    [Min(0f)]
    public float nodeDelay = 0.06f;

    [Tooltip("车辆开始移动前留给摄像机切换焦点的时间 (秒)")]
    [Min(0f)]
    public float movementFocusLeadDelay = 0.14f;

    [Tooltip("车辆完成移动后保持焦点的时间 (秒)")]
    [Min(0f)]
    public float movementFocusTrailDelay = 0.1f;

    [Tooltip("赛车精灵朝向 (度)：0=向右, 90=向上。赛车素材车头朝右，因此默认使用 0")]
    [Range(0, 359)]
    public float carSpriteFacingAngle = 0f;

    [Tooltip("赛车随赛道方向旋转的速度 (度/秒)")]
    [Min(1f)]
    public float carRotateSpeed = 540f;

    [Tooltip("赛车图标相对于赛道世界单位的显示缩放。")]
    [Min(0.01f)]
    public float carSpriteScale = 0.28f;

    [Header("比赛规模 (多车)")]
    [Tooltip("AI 对手数量 (0-3)。多车系统：玩家 + N 名 AI 同场竞技")]
    [Range(0, 3)]
    public int aiOpponentCount = 1;

    [Tooltip("玩家所属车队 (决定科技树与特技牌)")]
    public TeamId playerTeam = TeamId.CN;
    public string playerDriverId = "";

    [Tooltip("AI 车队分配 — 数组前 N 个按顺序分配给 AI 对手")]
    public TeamId[] aiTeams = { TeamId.UK, TeamId.DE, TeamId.IT };

    [Header("系统开关 (5 核心系统)")]
    [Tooltip("天气系统：弯道限速修正 + 每圈换天")]
    public bool enableWeather = true;

    [Tooltip("维修区系统：进站/出站/冷却 (赛道需有 pit_entry/pit_exit)")]
    public bool enablePitLane = true;

    [Tooltip("进站停 1 回合后，在 pit_exit 之外额外前进的格数；快充技术可继续增加")]
    [Min(0)]
    public int pitExitMoveBonus = 1;

    [Tooltip("特技牌系统：开局 4 张车队特技牌，每回合限 1")]
    public bool enableTrickCards = true;

    [Tooltip("测试辅助：保证中国队玩家开局手牌中有 1 张 ATTACK 特技牌")]
    public bool ensurePlayerAttackTrickInOpeningHand = false;

    [Tooltip("科技树系统：demo 预算解锁 L1 节点，修正手牌/热量池/弯速等")]
    public bool enableTechTree = true;

    [Tooltip("JP L2 汤底选择（demo：科技树解锁 L2 后自动选用；None=不选）")]
    public BrothType jpDemoBroth = BrothType.None;

    [Tooltip("UK L3 日不落 demo：自动复制该目标国的 L2/L3 专属科技")]
    public bool enableUkSunNeverSetsDemo = false;
    public TeamId ukSunNeverSetsTargetTeam = TeamId.DE;

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

    [Tooltip("AI 计划尾流时考虑的前车当前前向距离（格）")]
    [Min(1)]
    public int aiSlipstreamPlanningRange = 2;

    [Tooltip("中国 AI 在引擎可支付时允许承担的单回合弯道热量")]
    [Min(0)]
    public int aiChinaAffordableCornerHeat = 1;

    [Tooltip("AI 对已选速度牌顺序进行随机变化的概率")]
    [Range(0f, 1f)]
    public float aiCardVariationChance = 0.1f;

    /// <summary>
    /// 速度牌总数（从分布数组计算）。
    /// </summary>
    public int SpeedCardCount => speedCardDistribution.Length;

    /// <summary>
    /// 普通初始牌组总数（速度牌 + 启用时每队 4 张特技牌）。
    /// 热量牌存放在独立引擎池，不计入普通牌组。
    /// </summary>
    public int InitialDeckSize => speedCardDistribution.Length + (enableTrickCards ? 4 : 0);
}
