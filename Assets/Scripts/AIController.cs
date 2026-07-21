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

    private System.Random rng = new System.Random();

    public void Initialize(MVPGameManager gameManager, PlayerState aiState)
    {
        game = gameManager;
        ai = aiState;
        config = gameManager.Config;
        track = gameManager.Track;
    }

    // ====== 档位决策 ======

    /// <summary>
    /// 行为树 — 返回 AI 选择的目标档位。
    /// </summary>
    public int DecideGear()
    {
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
        int lookAhead = config.aiLookAheadNodes;
        // 用降 1 档后的估算（如果可降）来评估降档是否有帮助
        int estCurrentGear = EstimateMovement(currentGear);
        int estLowerGear = currentGear > config.minGear ? EstimateMovement(currentGear - 1) : estCurrentGear;

        // 检查前方所有弯道（不只第一个）
        for (int i = 1; i <= lookAhead; i++)
        {
            int checkPos = (ai.position + i) % track.TotalNodes;
            TrackNode node = track.GetNode(checkPos);

            if (node.cornerId > 0)
            {
                int limit = node.speedLimit;

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

        // ── P3: 尾流 — MVP 跳过 ──

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

        int gear = ai.gear;
        List<CardData> speedCards = ai.deck.GetSpeedCardsSortedDesc();
        float heatRatio = ai.HeatRatio;

        List<CardData> chosen;

        // 热量高 → 选最小牌
        if (heatRatio >= config.aiHeatWarningThreshold)
        {
            chosen = ai.deck.GetBottomNSpeedCards(gear);
        }
        // 弯道风险 → 偏保守
        else if (HasCornerRisk(gear) && heatRatio >= 0.5f)
        {
            chosen = ai.deck.GetBottomNSpeedCards(gear);
        }
        // 常规：选最大的 N 张速度牌
        else
        {
            chosen = new List<CardData>();
            for (int i = 0; i < gear && i < speedCards.Count; i++)
            {
                chosen.Add(speedCards[i]);
            }

            // 10% 概率随机洗牌增加变化
            if (rng.NextDouble() < 0.1f && chosen.Count > 1)
            {
                ShuffleList(chosen);
            }
        }

        // 从手牌移除选中卡牌
        ai.deck.RemoveFromHand(chosen);
        foreach (var card in chosen)
        {
            ai.playedSpeedCardsThisTurn.Add(card);
        }

        // 引擎故障：速度牌不足时，每缺 1 张 +1 热量到弃牌堆
        int missing = gear - chosen.Count;
        if (missing > 0)
        {
            ai.deck.DrawHeatFromPool(missing);
        }

        // 热量高时主动打出热量牌清手牌（最多 gear 张）
        if (heatRatio >= 0.5f)
        {
            int heatToPlay = Mathf.Min(ai.deck.CountHeatInHand(), gear);
            List<CardData> heatCards = new List<CardData>();
            foreach (var card in ai.deck.Hand)
            {
                if (card.IsHeat && heatCards.Count < heatToPlay)
                    heatCards.Add(card);
            }
            ai.deck.RemoveFromHand(heatCards);
            foreach (var card in heatCards)
                ai.playedHeatCardsThisTurn.Add(card);
        }
    }

    // ====== 辅助方法 ======

    /// <summary>
    /// 预估本回合移动力 = 手牌中最大 N 张速度牌之和。
    /// </summary>
    private int EstimateMovement(int gear)
    {
        List<CardData> topN = ai.deck.GetTopNSpeedCards(gear);
        int sum = 0;
        foreach (var c in topN) sum += c.value;
        return sum;
    }

    /// <summary>
    /// 检查前方第一个弯道是否有超速风险。
    /// </summary>
    private bool HasCornerRisk(int gear)
    {
        int estimatedMove = EstimateMovement(gear);
        int lookAhead = config.aiLookAheadNodes;

        for (int i = 1; i <= lookAhead; i++)
        {
            int checkPos = (ai.position + i) % track.TotalNodes;
            TrackNode node = track.GetNode(checkPos);

            if (node.cornerId > 0)
            {
                return estimatedMove > node.speedLimit;
            }
        }
        return false;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
