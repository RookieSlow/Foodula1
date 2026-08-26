using System;
using System.Collections.Generic;

/// <summary>尾流判定结果；规则层同时返回加成与被跟随的前车，供表现层使用。</summary>
public readonly struct SlipstreamResult
{
    public PlayerState Leader { get; }
    public int Bonus { get; }
    public bool Triggered => Leader != null && Bonus > 0;

    public SlipstreamResult(PlayerState leader, int bonus)
    {
        Leader = leader;
        Bonus = bonus;
    }
}

/// <summary>
/// 一回合内的完整尾流链。规则最多保留两次触发，表现层可逐段展示命中的前车。
/// </summary>
public readonly struct SlipstreamChainResult
{
    private static readonly IReadOnlyList<SlipstreamResult> EmptySteps = Array.Empty<SlipstreamResult>();
    private readonly IReadOnlyList<SlipstreamResult> steps;

    public IReadOnlyList<SlipstreamResult> Steps => steps ?? EmptySteps;
    public int TotalBonus { get; }
    public bool Triggered => Steps.Count > 0 && TotalBonus > 0;

    public SlipstreamChainResult(List<SlipstreamResult> resolvedSteps)
    {
        if (resolvedSteps == null || resolvedSteps.Count == 0)
        {
            steps = EmptySteps;
            TotalBonus = 0;
            return;
        }

        steps = resolvedSteps.ToArray();
        int total = 0;
        for (int i = 0; i < steps.Count; i++)
            total += steps[i].Bonus;
        TotalBonus = total;
    }
}

/// <summary>
/// 单场比赛的完整运行时状态 — 纯 C# 层（ADR-002 分层架构）。
/// 聚合 5 个核心系统：多车排名（RaceRanking）、天气（WeatherRules）、
/// 维修区（PitLaneRules）、特技牌（TrickCardRules）、科技树（TechTreeRules）。
///
/// MVPGameManager 在比赛循环中驱动本类；EditMode 测试可直接构造验证，
/// 不依赖任何 MonoBehaviour / Unity API（Random 源注入保证确定性）。
/// </summary>
public class RaceSession
{
    /// <summary>参赛车辆。Players[0] 恒为人类玩家。</summary>
    public List<PlayerState> Players = new List<PlayerState>();

    /// <summary>当前天气。</summary>
    public WeatherType Weather = WeatherType.Sunny;

    /// <summary>赛道天气池（来自 TrackConfig.weatherPool，换圈掷骰用）。</summary>
    public string[] WeatherPool;

    /// <summary>特技牌数据库（12 张，6 队 × 攻/守）。</summary>
    public TrickCardDatabase TrickDb;

    /// <summary>科技树数据库（36 节点）。</summary>
    public TechTreeDatabase TechDb;

    /// <summary>下一名完赛者的顺位（1 起）。</summary>
    public int NextFinishOrder = 1;

    /// <summary>确定性随机源（测试注入种子）。</summary>
    public IRandomSource Random;

    /// <summary>尾流基础加成（紧跟前方赛车获得的额外移动）。</summary>
    public const int SLIPSTREAM_BASE_BONUS = 2;

    public RaceSession(IRandomSource random = null)
    {
        Random = random ?? new UnityRandomSource();
        TrickDb = TrickCardDatabaseFactory.CreateDefault();
        TechDb = TechTreeDatabaseFactory.CreateDefault();
    }

    /// <summary>
    /// 重置玩家的每回合状态，并把关东慢煮累积的出牌槽转入新回合。
    /// 特技牌结算不依赖科技树是否启用。
    /// </summary>
    public void BeginTurn(PlayerState player)
    {
        if (player == null) return;

        player.ClearTurnState();
        player.trickState?.ResetPerTurn();
        if (player.techState != null)
            TechTreeRules.ResetPerTurnState(player.techState);
        if (player.trickState != null)
            player.extraCardSlotsThisTurn += TrickCardRules.ConsumeKantoOden(player.trickState);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 多车 / 排名（RaceRanking）
    // ═══════════════════════════════════════════════════════════════════

    public PlayerState Human => Players.Count > 0 ? Players[0] : null;

    /// <summary>当前排名（完赛者优先 → 圈数 → 位置）。</summary>
    public List<RaceRanking.RankEntry> GetRankings() => RaceRanking.GetRankings(Players);

    /// <summary>本回合行动顺序 — 末位先行（追赶优势）。</summary>
    public List<PlayerState> GetTurnOrder() => RaceRanking.GetTurnOrder(Players);

    /// <summary>所有人完赛或爆缸。</summary>
    public bool IsRaceOver() => RaceRanking.IsRaceOver(Players);

    /// <summary>指定玩家的当前名次（1 起）。</summary>
    public int GetRank(PlayerState p) => RaceRanking.GetCurrentRank(p, Players);

    /// <summary>分配完赛顺位（调用方负责设置 hasFinished）。</summary>
    public int AssignFinish(PlayerState p) => RaceRanking.AssignFinishOrder(p, ref NextFinishOrder);

    // ═══════════════════════════════════════════════════════════════════
    // 天气（WeatherRules）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>比赛开始时从赛道天气池抽取初始天气。</summary>
    public void InitializeWeather(string[] weatherPool, string defaultWeather)
    {
        WeatherPool = weatherPool;
        Weather = WeatherRules.SelectInitialWeather(weatherPool, defaultWeather, Random);
    }

    /// <summary>每圈结束时掷骰是否换天（30% 概率）。返回新天气。</summary>
    public WeatherType RollWeatherForLap()
    {
        Weather = WeatherRules.RollWeatherChange(Weather, WeatherPool, Random);
        return Weather;
    }

    /// <summary>Current Chinese HUD label for the active weather profile.</summary>
    public string WeatherLabel => WeatherRules.GetDisplayName(Weather);

    // ═══════════════════════════════════════════════════════════════════
    // 科技树（TechTreeRules）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 创建带 demo 预算的科技树状态：解锁 4 个 L1 通用 + 本队 L1 专属，并全部激活。
    /// 正式版由科技树 UI（用户）负责选择激活节点。
    /// </summary>
    public TechTreeState CreateDemoTechState(TeamId teamId)
    {
        var state = TechTreeRules.CreateDemoState(teamId);
        UnlockDemoTech(state);
        TechTreeRules.ActivateAllUnlocked(state);
        return state;
    }

    private void UnlockDemoTech(TechTreeState state)
    {
        // China uses the EV-named common catalogue.  The effects are shared
        // with the standard catalogue, but keeping the IDs aligned here is
        // important because EV L2 prerequisites point to EV L1 nodes.
        string[] commons = state.teamId == TeamId.CN
            ? new[]
            {
                "cn-ev-l1-heat-pump",
                "cn-ev-l1-pmsm",
                "cn-ev-l1-torque-vector",
                "cn-ev-l1-solid-state"
            }
            : new[]
            {
                "common-l1-heat-coating",
                "common-l1-lightweight-chassis",
                "common-l1-track-memory",
                "common-l1-expanded-tank"
            };
        foreach (var id in commons)
            TechTreeRules.UnlockNode(state, id, TechDb);

        var uniques = TechDb.GetUniqueInTier(state.teamId, TechTreeTier.L1);
        if (uniques.Count > 0)
            TechTreeRules.UnlockNode(state, uniques[0].id, TechDb);
    }

    /// <summary>
    /// 该玩家当前生效的全部科技修正。
    /// UK L3 日不落：若已选择目标国，将目标国 L2/L3 专属 flag 合并进来。
    /// </summary>
    public TechModifiers GetModifiers(PlayerState p)
    {
        var m = TechTreeRules.ComputeModifiers(p.techState, TechDb);

        // 日不落：复制目标国 L2+L3 专属科技 flag（数值类效果由调用方手动合并）
        if (m.hasSunNeverSets && p.techState != null && p.techState.sunNeverSetsTarget.HasValue)
        {
            var targetTechs = TechTreeRules.GetSunNeverSetsTargetTechs(p.techState, TechDb);
            foreach (var node in targetTechs)
            {
                foreach (var effect in node.effects)
                    ApplyEffectFlag(ref m, effect);
            }
        }
        return m;
    }

    /// <summary>将单个效果写入 TechModifiers（日不落合并用）。</summary>
    private static void ApplyEffectFlag(ref TechModifiers m, TechEffect effect)
    {
        switch (effect.type)
        {
            case TechEffectType.FishAndChips: m.hasFishAndChips = true; break;
            case TechEffectType.FullEnglish: m.hasFullEnglish = true; break;
            case TechEffectType.SunNeverSets: m.hasSunNeverSets = true; break;
            case TechEffectType.SchwarzbierFuel: m.hasSchwarzbierFuel = true; break;
            case TechEffectType.WurstplatteSuspension: m.hasWurstplatteSuspension = true; break;
            case TechEffectType.GrillSpezial: m.hasGrillSpezial = true; break;
            case TechEffectType.CavallinoRampante: m.hasCavallinoRampante = true; break;
            case TechEffectType.DriveThru: m.hasDriveThru = true; break;
            case TechEffectType.SmokedBBQ: m.hasSmokedBBQ = true; break;
            case TechEffectType.MotherRoad: m.hasMotherRoad = true; break;
            case TechEffectType.YinYangTea: m.hasYinYangTea = true; break;
            case TechEffectType.DimSumCombo: m.hasDimSumCombo = true; break;
            case TechEffectType.SomersaultCloud: m.hasSomersaultCloud = true; break;
            case TechEffectType.Nigiri: m.hasNigiri = true; break;
            case TechEffectType.BrothSelection: m.hasBrothSelection = true; break;
            case TechEffectType.Bankuruwase: m.hasBankuruwase = true; break;
            case TechEffectType.HeatReductionPerLap:
                m.heatReductionPerLap = System.Math.Max(m.heatReductionPerLap, (int)effect.value);
                break;
            case TechEffectType.CornerLimitBonus:
                m.cornerLimitBonus = System.Math.Max(m.cornerLimitBonus, (int)effect.value);
                break;
            case TechEffectType.SpeedBonusStraight:
                m.speedBonusStraight = System.Math.Max(m.speedBonusStraight, (int)effect.value);
                break;
            case TechEffectType.DurabilityBonus:
                m.durabilityBonus = System.Math.Max(m.durabilityBonus, (int)effect.value);
                break;
            case TechEffectType.SlipstreamRangeBonus:
                m.slipstreamRangeBonus = System.Math.Max(m.slipstreamRangeBonus, (int)effect.value);
                break;
            case TechEffectType.PitExitMoveBonus:
                m.pitExitMoveBonus = System.Math.Max(m.pitExitMoveBonus, (int)effect.value);
                break;
            case TechEffectType.EngineCapacityBonus:
                m.engineCapacityBonus += (int)effect.value;
                break;
            case TechEffectType.HandSizeBonus:
                m.handSizeBonus += (int)effect.value;
                break;
            case TechEffectType.SpinCounterMaxBonus:
                m.spinCounterMaxBonus += (int)effect.value;
                break;
            case TechEffectType.LightweightDoubler:
                m.hasPizzaSottile = true;
                break;
        }
    }

    /// <summary>有效手牌上限 = 基础 + 科技加成。</summary>
    public int EffectiveHandSize(PlayerState p, int baseHandSize)
    {
        if (p.techState == null) return baseHandSize;
        return baseHandSize + GetModifiers(p).handSizeBonus;
    }

    /// <summary>有效引擎热量池 = 基础 + 耐久/容量加成（含 SmokedBBQ +2）。</summary>
    public int EffectiveHeatPoolSize(PlayerState p, int basePoolSize)
    {
        if (p.techState == null) return basePoolSize;
        var m = GetModifiers(p);
        return basePoolSize + m.durabilityBonus + m.EffectiveEngineCapacityBonus;
    }

    /// <summary>有效失控淘汰阈值（基础 3 + 科技加成，IT L2 上限 4）。</summary>
    public int EffectiveSpinMax(PlayerState p)
    {
        return p.techState == null ? 3 : GetModifiers(p).EffectiveSpinCounterMax;
    }

    /// <summary>
    /// 弯道判定限速 = 基础限速 + 科技弯速加成（GDD 堆叠公式） − 天气惩罚。
    /// baseLimit &gt;= 99 视为无弯道（不修正）。
    /// </summary>
    public int EffectiveCornerLimit(PlayerState p, int baseLimit)
    {
        if (baseLimit >= 99) return baseLimit;
        // Team handling is a base-car attribute; tech-tree bonuses layer on
        // top of it. This keeps the corner formula in one pure entry point.
        int bonus = p != null ? TeamVehicleRules.GetHandling(p.teamId) : 0;
        if (p.techState != null)
        {
            var m = GetModifiers(p);
            bonus = TechTreeRules.ComputeEffectiveCornerLimitBonus(
                m.cornerLimitBonus,
                0,
                0,
                m.hasSomersaultCloud ? 1 : 0,
                m.hasBankuruwase && p.techState.bankuruwaseActive ? 1 : 0);
        }
        int limit = baseLimit + bonus;
        limit = WeatherRules.ApplyWeatherToCornerLimit(limit, Weather);
        return System.Math.Max(1, limit);
    }

    /// <summary>本圈弯道超速热量减免（科技，每圈 1 次）。返回本次减免量并消耗。</summary>
    public int ConsumeHeatReduction(PlayerState p)
    {
        if (p.techState == null) return 0;
        int reduction = TechTreeRules.GetHeatReductionThisLap(p.techState, TechDb);
        if (reduction > 0)
            TechTreeRules.ConsumeHeatReduction(p.techState);
        return reduction;
    }

    /// <summary>新的一圈开始 — 重置每圈科技跟踪。</summary>
    public void OnNewLap(PlayerState p)
    {
        if (p.techState != null)
            TechTreeRules.ResetHeatReductionForLap(p.techState);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 特技牌（TrickCardRules）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Go 模式：高挡位激进驾驶（火锅底料前置）。</summary>
    public bool IsGoMode(PlayerState p)
    {
        if (p == null) return false;
        return TeamGearRules.IsChina(p.teamId) && p.usesChinaGearSystem
            ? ChinaGearShiftRules.IsGo(p.gear)
            : p.gear >= 3;
    }

    /// <summary>Recover 模式：低挡位冷却驾驶（冰糕前置）。</summary>
    public bool IsRecoverMode(PlayerState p)
    {
        if (p == null) return false;
        return TeamGearRules.IsChina(p.teamId) && p.usesChinaGearSystem
            ? ChinaGearShiftRules.IsRecover(p.gear)
            : p.gear <= 2;
    }

    /// <summary>
    /// 尝试打出特技牌。校验顺序：类型 → 定义 → 每回合限 1 → 模式前置。
    /// 成功时在 trickState 上设置回合标志并返回效果结果。
    /// </summary>
    public TrickPlayResult PlayTrick(PlayerState p, CardData card)
    {
        if (p == null || p.deck == null)
            return TrickPlayResult.Fail("玩家牌库不可用");
        if (card == null || !card.IsTrick)
            return TrickPlayResult.Fail("不是特技牌");
        if (!p.deck.ContainsInHand(card))
            return TrickPlayResult.Fail("该特技牌不在手牌中");
        var def = TrickDb.Get(card.trickId);
        if (def == null)
            return TrickPlayResult.Fail($"未知特技牌: {card.trickId}");
        if (!TrickCardRules.CanPlayTrick(p.trickState))
            return TrickPlayResult.Fail("本回合已打出过特技牌（每回合限 1）");
        if (!TrickCardRules.CanPlaySpecificTrick(def, IsGoMode(p), IsRecoverMode(p)))
            return TrickPlayResult.Fail("当前驾驶模式无法使用该特技牌");
        return TrickCardRules.ResolvePlay(
            def, p.trickState,
            p.deck.heatPool != null && p.deck.heatPool.remaining > 0,
            p.deck.CountHeatInHand() > 0,
            p.deck.CountSpeedInHand() > 0,
            p.trickState.crossedLandmarkLastTurn);
    }

    /// <summary>开局特技牌（每队 4 张：2 攻 2 守）。</summary>
    public List<CardData> CreateInitialTrickCards(TeamId teamId)
    {
        return TrickCardRules.CreateInitialTrickCards(teamId, TrickDb);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 移动力计算（科技 + 特技牌粘合）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 本回合移动加成 = 科技直道加成（未过弯时） + 特技牌加成（酸菜）。
    /// crossedCorner: 本回合移动是否经过弯道（决定直道加成与酸菜结算）。
    /// 帕尔玛干酪的尾流加成都计入 ComputeSlipstreamBonus（只有吃到尾流才生效）。
    /// </summary>
    public int ComputeMovementBonus(PlayerState p, bool crossedCorner)
    {
        int bonus = 0;
        if (!crossedCorner)
        {
            // Base vehicle pace applies on straights. Standard teams use the
            // chassis profile plus any team-specific card conversion. China
            // uses the same documented profile only while Go is active; the
            // Recover mode remains a deliberately conservative one-card turn.
            if (!TeamGearRules.IsChina(p.teamId) || !p.usesChinaGearSystem)
            {
                bonus += TeamVehicleRules.GetStraightMovementBonus(p.teamId);
                if (p.playedSpeedCardsThisTurn != null)
                {
                    if (p.playedSpeedCardsThisTurn.Count > 0)
                        bonus += TeamVehicleRules.GetStraightTurnBonus(p.teamId);
                    foreach (CardData card in p.playedSpeedCardsThisTurn)
                        if (card != null)
                            bonus += TeamVehicleRules.GetStraightCardBonus(p.teamId, card.value);
                }
            }
            else if (IsGoMode(p))
            {
                // Go expresses the electric drivetrain's documented +1 top
                // speed / +2 acceleration package on a straight.  Recover
                // remains deliberately conservative at one card.
                bonus += TeamVehicleRules.GetStraightMovementBonus(p.teamId);
            }
        }

        if (p.techState != null && !crossedCorner &&
            (!TeamGearRules.IsChina(p.teamId) || !p.usesChinaGearSystem || IsGoMode(p)))
            bonus += GetModifiers(p).EffectiveSpeedBonusStraight;
        bonus += TrickCardRules.GetSauerkrautBonus(p.trickState, crossedCorner);
        return bonus;
    }

    /// <summary>
    /// Consumes Italy's stored corner-exit acceleration on the next playable
    /// turn. The GDD grants +1 to the first speed card after a corner, not to
    /// the movement that is currently crossing that corner.
    /// </summary>
    public int ConsumeItalyCornerExitBonus(PlayerState p)
    {
        if (p == null || p.teamId != TeamId.IT || !p.italyCornerExitBoostReady ||
            p.playedSpeedCardsThisTurn == null || p.playedSpeedCardsThisTurn.Count == 0)
            return 0;

        p.italyCornerExitBoostReady = false;
        return TeamVehicleRules.GetCornerExitBonus(p.teamId);
    }

    /// <summary>Arms Italy's next-turn acceleration after a corner was completed without spinning.</summary>
    public void ArmItalyCornerExitBonus(PlayerState p, bool completedCorner)
    {
        if (p != null && p.teamId == TeamId.IT && completedCorner)
            p.italyCornerExitBoostReady = true;
    }

    /// <summary>本回合是否跨过起点/终点线（US 特技牌/科技以起点线为地标 1）。</summary>
    public static bool CrossedStartLine(int oldPos, int newPos, int totalCells)
    {
        return TechTreeRules.CrossedPositionForward(oldPos, newPos, 0, totalCells);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 尾流系统（Slipstream）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 计算完整尾流链。若传入 plannedMovements，则使用其中的移动量模拟终点；
    /// 运行时在回合末传入全员为 0 的映射，以直接比较基础移动结算后的实际落位。
    /// 同格时按设计案比较本回合基础移动力；基础移动力相同时，按 arrivalOrder
    /// 的先后决定谁先到达、谁是后车。这样不会把同一格误判成双向前车。
    /// 获得尾流后从新位置再判定一次，且不会重复跟随同一辆前车。GDD 规定每回合最多触发两次。
    /// plannedMovements 为空时使用速度牌总值，供纯模拟和兼容调用使用。
    /// </summary>
    public SlipstreamChainResult ComputeSlipstreamChain(
        PlayerState p,
        IReadOnlyList<PlayerState> players,
        int totalNodes,
        IReadOnlyDictionary<PlayerState, int> plannedMovements = null,
        int maxTriggers = 2,
        IReadOnlyList<PlayerState> arrivalOrder = null)
    {
        if (p == null || players == null || totalNodes <= 0 || maxTriggers <= 0)
            return default;
        if (p.isBlown || p.hasFinished || !WeatherRules.CanSlipstream(Weather))
            return default;

        int range = 1 + GetModifiers(p).slipstreamRangeBonus + p.slipstreamRangeBonusThisTurn;
        range = WeatherRules.ApplyWeatherToSlipstreamRange(range, Weather);
        if (range <= 0)
            return default;

        int triggerLimit = Math.Min(2, maxTriggers);
        int mySim = p.position + GetPlannedMovement(p, plannedMovements);
        var usedLeaders = new HashSet<PlayerState>();
        var steps = new List<SlipstreamResult>(triggerLimit);

        for (int trigger = 0; trigger < triggerLimit; trigger++)
        {
            PlayerState leader = null;
            int bestGap = int.MaxValue;

            foreach (PlayerState candidate in players)
            {
                if (candidate == null || candidate == p || candidate.isBlown || candidate.hasFinished ||
                    usedLeaders.Contains(candidate))
                    continue;

                int candidateSim = candidate.position + GetPlannedMovement(candidate, plannedMovements);
                int gap = ForwardDistance(mySim, candidateSim, totalNodes);
                if (gap == 0 && !IsSameCellLeader(
                        p, candidate, players, plannedMovements, arrivalOrder))
                    continue;
                if (gap < bestGap)
                {
                    bestGap = gap;
                    leader = candidate;
                }
            }

            // 最近车辆不在前方半圈或超出尾流范围时，链条结束。
            if (leader == null || bestGap > totalNodes / 2 || bestGap > range)
                break;

            // 冰糕阻断身后气流；不能越过最近车辆去吸更远的车。
            if (TrickCardRules.IsIceJellyActive(leader.trickState))
                break;

            int bonus = GetSlipstreamMovementBonus(p);
            if (bonus <= 0)
                break;

            steps.Add(new SlipstreamResult(leader, bonus));
            usedLeaders.Add(leader);
            mySim += bonus;
        }

        return new SlipstreamChainResult(steps);
    }

    private static bool IsSameCellLeader(
        PlayerState follower,
        PlayerState candidate,
        IReadOnlyList<PlayerState> players,
        IReadOnlyDictionary<PlayerState, int> plannedMovements,
        IReadOnlyList<PlayerState> arrivalOrder)
    {
        // Cars on different laps may share a node index, but they are not
        // physically alongside one another for slipstream purposes.
        if (follower.lap != candidate.lap)
            return false;

        int followerMovement = GetBaseMovementForTie(follower, plannedMovements);
        int candidateMovement = GetBaseMovementForTie(candidate, plannedMovements);
        if (candidateMovement != followerMovement)
            return candidateMovement > followerMovement;

        // Runtime passes the actual base-movement order. Pure callers that do
        // not have a separate order use the player list as a deterministic
        // fallback, which still prevents a same-cell cycle.
        IReadOnlyList<PlayerState> order = arrivalOrder ?? players;
        int followerIndex = IndexOfReference(order, follower);
        int candidateIndex = IndexOfReference(order, candidate);
        return candidateIndex >= 0 && followerIndex >= 0 && candidateIndex < followerIndex;
    }

    private static int GetBaseMovementForTie(
        PlayerState player,
        IReadOnlyDictionary<PlayerState, int> plannedMovements)
    {
        // During the end-of-turn resolver totalMovementThisTurn is still the
        // non-slipstream base total. Older pure tests only populate the raw
        // card total, so retain that compatibility fallback.
        if (player.totalMovementThisTurn != 0)
            return player.totalMovementThisTurn;
        if (player.cornerTotalThisTurn != 0)
            return player.cornerTotalThisTurn;
        return GetPlannedMovement(player, plannedMovements);
    }

    private static int IndexOfReference(IReadOnlyList<PlayerState> players, PlayerState target)
    {
        if (players == null)
            return -1;
        for (int i = 0; i < players.Count; i++)
            if (ReferenceEquals(players[i], target))
                return i;
        return -1;
    }

    /// <summary>兼容只需要第一次命中前车的表现与既有调用。</summary>
    public SlipstreamResult ComputeSlipstream(PlayerState p, IReadOnlyList<PlayerState> players, int totalNodes)
    {
        SlipstreamChainResult chain = ComputeSlipstreamChain(p, players, totalNodes, null, 1);
        return chain.Triggered ? chain.Steps[0] : default;
    }

    /// <summary>兼容只需要数值的模拟与 AI；返回最多两段尾流的总移动。</summary>
    public int ComputeSlipstreamBonus(PlayerState p, IReadOnlyList<PlayerState> players, int totalNodes)
    {
        return ComputeSlipstreamChain(p, players, totalNodes).TotalBonus;
    }

    private static int GetPlannedMovement(
        PlayerState player,
        IReadOnlyDictionary<PlayerState, int> plannedMovements)
    {
        if (plannedMovements != null && plannedMovements.TryGetValue(player, out int movement))
            return movement;
        return player.cornerTotalThisTurn;
    }

    private int GetSlipstreamMovementBonus(PlayerState p)
    {
        int bonus = SLIPSTREAM_BASE_BONUS + TeamVehicleRules.GetSlipstreamBonus(p.teamId);
        bonus += TrickCardRules.GetParmigianoBonus(p.trickState);
        // 筋斗云：本回合打过 ATTACK 特技牌 → 每段尾流 +2。
        if (p.techState != null &&
            TechTreeRules.HasSomersaultCloud(p.techState, TechDb) &&
            PlayedAttackTrickThisTurn(p))
        {
            bonus += TechTreeRules.GetSomersaultCloudSlipstreamBonus();
        }
        return WeatherRules.ApplyWeatherToSlipstreamBonus(bonus, Weather);
    }

    /// <summary>环形赛道前向距离（a 到 b 沿赛道方向）。</summary>
    public static int ForwardDistance(int fromPos, int toPos, int totalNodes)
    {
        return (toPos - fromPos + totalNodes) % totalNodes;
    }

    /// <summary>本回合是否打出过 ATTACK 类特技牌。</summary>
    private bool PlayedAttackTrickThisTurn(PlayerState p)
    {
        if (string.IsNullOrEmpty(p.trickState.trickPlayedThisTurnId)) return false;
        var def = TrickDb.Get(p.trickState.trickPlayedThisTurnId);
        return def != null && def.IsAttack;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 地标（US 科技/特技）：地标 1 = 起点线，地标 2 = 赛道中点
    // ═══════════════════════════════════════════════════════════════════

    public static (int lm1, int lm2) GetLandmarks(int totalCells)
    {
        return TechTreeRules.GetLandmarkPositions(totalCells);
    }

    /// <summary>本回合移动是否跨过某地标（前进方向）。</summary>
    public static bool CrossedLandmark(int oldPos, int newPos, int landmark, int totalCells)
    {
        return TechTreeRules.CrossedPositionForward(oldPos, newPos, landmark, totalCells);
    }

    /// <summary>是否处于 BBQ 区（美式烧烤：地标周围 5 格）。</summary>
    public static bool IsInBBQZone(int position, int totalCells)
    {
        var (lm1, lm2) = GetLandmarks(totalCells);
        return TechTreeRules.IsInBBQZone(position, lm1, lm2, totalCells);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 回合结算（CN 阴阳茶 / DE 烤肉拼盘）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>回合结束时按中国队当前 Go/Recover 模式结算阴阳茶。</summary>
    public YinYangResult ResolveEndOfTurn(PlayerState p)
    {
        if (RaceTurnRules.IsTerminal(p) || p.techState == null)
            return YinYangResult.NoTrigger;
        return TechTreeRules.ResolveYinYang(p.techState, TechDb, IsGoMode(p));
    }

    /// <summary>回合结束时可用的 DE 烤肉拼盘冷却量（本回合已支付的热量）。</summary>
    public int GetGrillSpezialCooldown(PlayerState p)
    {
        if (RaceTurnRules.IsTerminal(p) || p.techState == null)
            return 0;
        return TechTreeRules.CanUseGrillSpezial(p.techState, TechDb)
            ? TechTreeRules.GetGrillSpezialCooldown(p.techState)
            : 0;
    }

    /// <summary>标记烤肉拼盘已使用（调用方随后应用冷却）。</summary>
    public void ActivateGrillSpezial(PlayerState p)
    {
        if (p.techState != null)
            TechTreeRules.ActivateGrillSpezial(p.techState);
    }

    /// <summary>记录本回合支付的热量（烤肉拼盘跟踪）。</summary>
    public void TrackHeatPaid(PlayerState p, int heatPaid)
    {
        if (p.techState != null && heatPaid > 0)
            TechTreeRules.TrackGrillSpezialHeat(p.techState, heatPaid);
    }
}
