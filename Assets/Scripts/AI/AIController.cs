using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 控制器 — 简单 4 优先级行为树。
/// 不继承 MonoBehaviour 也可以，但为了在 Inspector 中可见，保留为 MonoBehaviour。
/// </summary>
public class AIController : MonoBehaviour
{
    private MVPGameManager game;
    private PlayerState ai;
    private GameConfigSO config;
    private TrackManager track;

    private IRandomSource randomSource = new UnityRandomSource();

    public void Initialize(MVPGameManager gameManager, PlayerState aiState, IRandomSource source = null)
    {
        game = gameManager;
        ai = aiState;
        config = gameManager.Config;
        track = gameManager.Track;
        randomSource = source ?? new UnityRandomSource();
    }

    // ====== 档位决策 ======

    /// <summary>
    /// 行为树 — 返回 AI 选择的目标档位。
    /// </summary>
    public int DecideGear()
    {
        if (ai != null && TeamGearRules.IsChina(ai.teamId))
        {
            int target = ChinaGearShiftRules.ChooseAiGear(
                ai.gear,
                ai.chinaConsecutiveGearCount,
                ai.deck.CountSpeedInHand(),
                ai.HeatRatio,
                config.aiHeatWarningThreshold);

            if (target == ChinaGearShiftRules.GoGear)
            {
                ChinaGearShiftRules.Result go = ChinaGearShiftRules.Resolve(
                    ai.gear, ai.chinaConsecutiveGearCount, target);
                int projectedCornerHeat = GetChinaProjectedCornerHeat(go.SpeedCardCount);
                int missingCardHeat = RaceRules.GetMissingSpeedCardCount(
                    go.SpeedCardCount, ai.deck.CountSpeedInHand());
                int engineHeat = ai.deck.heatPool != null ? ai.deck.heatPool.remaining : 0;
                if (ChinaGearShiftRules.ShouldForceRecoverForCorner(
                    projectedCornerHeat,
                    go.AdditionalHeat + missingCardHeat,
                    engineHeat,
                    config.aiChinaAffordableCornerHeat))
                    return ChinaGearShiftRules.RecoverGear;
            }

            return target;
        }

        int currentGear = ai.gear;
        int speedInHand = ai.deck.CountSpeedInHand();
        float heatRatio = ai.HeatRatio;

        // ── P1: 生存检查 ──
        // 热量过高 → 降档冷却
        if (heatRatio >= config.aiHeatWarningThreshold)
        {
            int target = Mathf.Max(config.minGear, currentGear - 2);
            // 降到 1 档如果热量极高
            if (heatRatio >= 0.85f)
                target = config.minGear;
            return target;
        }

        // 手牌速度牌不足当前档位 → 降档
        if (speedInHand < currentGear)
        {
            int viableGear = Mathf.Max(config.minGear, speedInHand);
            return viableGear;
        }

        // ── P2: 弯道策略 ──
        // 用降 1 档后的估算（如果可降）来评估降档是否有帮助
        int estCurrentGear = EstimateMovement(currentGear);
        int estLowerGear = currentGear > config.minGear ? EstimateMovement(currentGear - 1) : estCurrentGear;
        int lookAhead = Mathf.Min(
            config.aiLookAheadNodes,
            Mathf.Max(1, estCurrentGear));

        // 检查前方所有弯道（不只第一个）
        for (int i = 1; i <= lookAhead; i++)
        {
            int checkPos = (ai.position + i) % track.TotalNodes;
            TrackNode node = track.GetNode(checkPos);

            if (node.cornerId > 0)
            {
                int limit = GetEffectiveCornerLimit(node.cornerId);

                // 当前档位超速？
                if (estCurrentGear > limit)
                {
                    // 降 1 档能解决？
                    if (estLowerGear <= limit)
                        return Mathf.Max(config.minGear, currentGear - 1);

                    // 降 1 档仍超速 → 降 2 档
                    if (currentGear > config.minGear + 1)
                        return Mathf.Max(config.minGear, currentGear - 2);

                    return config.minGear;
                }
                // 不 break，继续检查后续弯道
            }
        }

        // ── P3: 尾流 ──
        // 具体牌组选取在 A4 执行；此处保持当前合法档位，避免为了追尾流
        // 绕过 P1 生存检查或 P2 弯道预判。

        // ── P4: 常规推进 ──
        if (heatRatio <= config.aiAggressiveHeatThreshold && speedInHand >= 3)
        {
            // 低热量 → 升档冲刺
            return Mathf.Min(config.maxGear, currentGear + 1);
        }

        if (heatRatio <= 0.5f)
        {
            // 中低热量 → 保持档位
            return currentGear;
        }

        // 中高热量 → 降 1 档
        return Mathf.Max(config.minGear, currentGear - 1);
    }

    // ====== 选牌决策 ======

    /// <summary>
    /// 从 AI 手牌中选择速度牌。结果存入 ai.playedSpeedCardsThisTurn。
    /// </summary>
    public void SelectCards()
    {
        ai.playedSpeedCardsThisTurn.Clear();
        ai.playedHeatCardsThisTurn.Clear();

        int maxCards = game.GetMaxSpeedCardsThisTurn(ai);
        bool cornerRisk = HasCornerRisk(maxCards);
        List<CardData> chosen = null;
        if (!cornerRisk && ai.HeatRatio < config.aiCautiousHeatThreshold)
            chosen = TryChooseSlipstreamCards(maxCards);

        if (chosen == null)
        {
            chosen = AIPlanner.ChooseSpeedCards(
                ai.deck,
                maxCards,
                ai.HeatRatio,
                cornerRisk,
                config.aiHeatWarningThreshold,
                config.aiCautiousHeatThreshold,
                config.aiCardVariationChance,
                randomSource);
        }

        // 引擎故障：速度牌不足时，每缺 1 张 +1 热量到弃牌堆。引擎不足 → 失控
        int requiredCards = game.GetMaxSpeedCardsThisTurn(ai);
        int missing = RaceRules.GetMissingSpeedCardCount(requiredCards, chosen.Count);
        if (missing > 0)
        {
            if (!game.TryPayHeat(ai, missing, ai.position, "engine failure"))
            {
                // 失控：自动选择尚未确认，速度牌保留在手牌中。
                ai.playedSpeedCardsThisTurn.Clear();
                ai.playedHeatCardsThisTurn.Clear();
                return;
            }
        }

        // 支付成功后再提交选牌，避免失控时速度牌从手牌永久丢失。
        ai.deck.RemoveFromHand(chosen);
        foreach (var card in chosen)
        {
            ai.playedSpeedCardsThisTurn.Add(card);
        }
        // 热量牌不可打出 — 始终留在手牌中，等待降档冷却或 G1 散热移除
    }

    // ====== 辅助方法 ======

    /// <summary>
    /// Tries to select an exact-card-count combination that places the AI
    /// within one cell of a nearby opponent after both planned movements.
    /// Corner-risk and heat gates are applied by <see cref="SelectCards"/>.
    /// </summary>
    private List<CardData> TryChooseSlipstreamCards(int cardLimit)
    {
        if (game == null || game.Session == null || track == null || ai == null ||
            track.TotalNodes <= 0 || cardLimit <= 0 || config == null)
            return null;

        int planningRange = Mathf.Max(1, config.aiSlipstreamPlanningRange);
        int bestTargetMovement = int.MaxValue;
        List<CardData> bestCards = null;

        foreach (PlayerState candidate in game.Session.Players)
        {
            if (candidate == null || candidate == ai || candidate.isBlown || candidate.hasFinished ||
                candidate.lap != ai.lap)
                continue;

            int leaderMovement = EstimateOpponentMovement(candidate);
            if (!AIPlanner.TryGetSlipstreamTargetMovement(
                ai.position,
                candidate.position,
                leaderMovement,
                track.TotalNodes,
                planningRange,
                out int targetMovement))
            {
                continue;
            }

            if (targetMovement >= bestTargetMovement)
                continue;

            if (AIPlanner.TryFindExactSpeedCards(
                ai.deck,
                cardLimit,
                targetMovement,
                out List<CardData> cards))
            {
                bestTargetMovement = targetMovement;
                bestCards = cards;
            }
        }

        return bestCards;
    }

    /// <summary>
    /// Estimates a leader's non-slipstream movement for the current planning
    /// pass. Already selected cards take precedence; otherwise the leader's
    /// highest legal speed cards provide a deterministic approximation.
    /// </summary>
    private int EstimateOpponentMovement(PlayerState opponent)
    {
        if (opponent == null || opponent.deck == null)
            return 0;
        if (opponent.playedSpeedCardsThisTurn != null &&
            opponent.playedSpeedCardsThisTurn.Count > 0)
        {
            return RaceRules.SumCardValues(opponent.playedSpeedCardsThisTurn);
        }

        int cardLimit = game != null
            ? game.GetMaxSpeedCardsThisTurn(opponent)
            : opponent.gear;
        return RaceRules.SumCardValues(opponent.deck.GetTopNSpeedCards(Mathf.Max(0, cardLimit)));
    }

    /// <summary>
    /// 预估本回合移动力 = 手牌中最大 N 张速度牌之和。
    /// </summary>
    private int EstimateMovement(int cardLimit)
    {
        List<CardData> topN = ai.deck.GetTopNSpeedCards(cardLimit);
        int sum = 0;
        foreach (var c in topN) sum += c.value;
        return sum;
    }

    /// <summary>
    /// Minimum movement the current hand can produce while still satisfying
    /// a mandatory card count. Used to distinguish avoidable corner risk
    /// (select low cards) from unavoidable risk (switch China to Recover).
    /// </summary>
    private int EstimateMinimumMovement(int cardLimit)
    {
        List<CardData> bottomN = ai.deck.GetBottomNSpeedCards(cardLimit);
        int sum = 0;
        foreach (var c in bottomN) sum += c.value;
        return sum;
    }

    /// <summary>
    /// 检查前方第一个弯道是否有超速风险。
    /// </summary>
    private bool HasCornerRisk(int cardLimit)
    {
        if (track == null || track.TotalNodes == 0)
        {
            return false;
        }

        int estimatedMove = EstimateMovement(cardLimit);
        int lookAhead = Mathf.Min(
            config.aiLookAheadNodes,
            Mathf.Max(1, estimatedMove));

        for (int i = 1; i <= lookAhead; i++)
        {
            int checkPos = (ai.position + i) % track.TotalNodes;
            TrackNode node = track.GetNode(checkPos);

            if (node.cornerId > 0)
            {
                return estimatedMove > GetEffectiveCornerLimit(node.cornerId);
            }
        }
        return false;
    }

    private int GetEffectiveCornerLimit(int cornerId)
    {
        if (track == null || cornerId <= 0)
            return 99;

        int lane = game != null ? game.GetLaneIndexForPlayer(ai) : track.GetDefaultLaneIndex(true);
        int baseLimit = track.GetCornerSpeedLimit(cornerId, lane);
        return game != null && game.Session != null
            ? game.Session.EffectiveCornerLimit(ai, baseLimit)
            : baseLimit;
    }

    private int GetChinaProjectedCornerHeat(int cardCount)
    {
        if (track == null || track.TotalNodes == 0)
            return 0;

        // Gear choice must use the lowest legal Go hand. If that hand is
        // safe, Go can remain active and SelectCards will deliberately choose
        // those low cards. Small, affordable overspeed is controlled by the
        // configured heat tolerance; repeated apex IDs are charged once.
        int estimatedMove = EstimateMinimumMovement(cardCount);
        int lookAhead = Mathf.Min(
            Mathf.Min(config.aiLookAheadNodes, track.TotalNodes - 1),
            Mathf.Max(1, estimatedMove));
        int projectedHeat = 0;
        var visitedCorners = new HashSet<int>();
        for (int i = 1; i <= lookAhead; i++)
        {
            TrackNode node = track.GetNode((ai.position + i) % track.TotalNodes);
            if (node.cornerId <= 0 || !visitedCorners.Add(node.cornerId))
                continue;

            projectedHeat += Mathf.Max(
                0,
                estimatedMove - GetEffectiveCornerLimit(node.cornerId));
        }

        return projectedHeat;
    }

}
