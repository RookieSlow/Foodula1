using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 引擎牌库 — 每玩家独立。弯道超速/急刹/引擎故障时从此抽取热量牌放入弃牌堆。
/// 冷却时热量牌归还至此。包装类以支持引用传递。
/// </summary>
public class HeatPool
{
    public int remaining;

    public HeatPool(int initial)
    {
        remaining = initial;
    }
}

/// <summary>
/// 牌组系统 — 纯 C# 逻辑类（非 MonoBehaviour）。
/// 管理牌组（drawPile）、手牌（hand）、弃牌堆（discardPile）和该玩家持有的引擎牌库引用。
///
/// 热量牌生命周期: 热量池 →(弯道超速/急刹/引擎故障)→ 弃牌堆 →(洗牌)→ 牌组 →(抽牌)→ 手牌(不可打出!) →(降档冷却/G1散热)→ 热量池
/// </summary>
public class CardDeck
{
    private List<CardData> drawPile = new List<CardData>();
    private List<CardData> hand = new List<CardData>();
    private List<CardData> discardPile = new List<CardData>();
    private IRandomSource randomSource = new UnityRandomSource();

    /// <summary>该玩家的引擎牌库 — 每玩家独立的 HeatPool 实例。弯道超速/急刹/引擎故障从此抽取。</summary>
    public HeatPool heatPool;

    // --- 只读属性 ---
    public IReadOnlyList<CardData> Hand => hand;
    public int HandCount => hand.Count;
    public int DrawPileCount => drawPile.Count;
    public int DiscardPileCount => discardPile.Count;

    /// <summary>牌组 + 弃牌堆 总数（判断是否会抽干）。</summary>
    public int TotalAvailableForDraw => drawPile.Count + discardPile.Count;

    /// <summary>
    /// 用配置初始化牌组。
    /// 速度牌 + 热量牌 → 全部放入牌组，然后洗牌。
    /// </summary>
    public void InitializeDeck(GameConfigSO config, HeatPool enginePool, IRandomSource source = null)
    {
        heatPool = enginePool;
        randomSource = source ?? new UnityRandomSource();
        drawPile.Clear();
        hand.Clear();
        discardPile.Clear();

        // 加入速度牌
        foreach (int val in config.speedCardDistribution)
        {
            drawPile.Add(new CardData(CardType.Speed, val));
        }

        // 加入初始热量牌
        for (int i = 0; i < config.initialHeatCards; i++)
        {
            drawPile.Add(new CardData(CardType.Heat, 0));
        }

        ShuffleDrawPile();
    }

    /// <summary>
    /// Fisher-Yates 洗牌 — 仅洗牌组（drawPile）。
    /// </summary>
    public void ShuffleDrawPile()
    {
        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int j = randomSource.NextInt(0, i + 1);
            CardData temp = drawPile[i];
            drawPile[i] = drawPile[j];
            drawPile[j] = temp;
        }
    }

    /// <summary>
    /// 从牌组抽牌到手牌，直至手牌数达到 handSize。
    /// 牌组不够时自动洗入弃牌堆。
    /// 如果牌组+弃牌堆+热量池全部耗尽 → 返回 false（爆缸）。
    /// </summary>
    public bool DrawToHand(int handSize)
    {
        while (hand.Count < handSize)
        {
            // 牌组空 → 洗入弃牌堆
            if (drawPile.Count == 0)
            {
                if (discardPile.Count > 0)
                {
                    drawPile.AddRange(discardPile);
                    discardPile.Clear();
                    ShuffleDrawPile();
                }
                else
                {
                    // 牌组和弃牌堆都空 → 无牌可抽（热量牌只能通过弯道惩罚进入弃牌堆后再循环）
                    return false;
                }
            }

            hand.Add(drawPile[0]);
            drawPile.RemoveAt(0);
        }
        return true;
    }

    /// <summary>
    /// 从手牌中移除并返回选中的卡牌。
    /// </summary>
    public List<CardData> RemoveFromHand(List<CardData> selected)
    {
        List<CardData> removed = new List<CardData>();
        foreach (CardData card in selected)
        {
            if (hand.Remove(card))
            {
                removed.Add(card);
            }
        }
        return removed;
    }

    /// <summary>
    /// 将打出的速度牌放入弃牌堆。
    /// </summary>
    public void DiscardSpeedCards(List<CardData> cards)
    {
        foreach (CardData card in cards)
        {
            if (card.IsSpeed)
            {
                discardPile.Add(card);
            }
        }
    }

    /// <summary>
    /// 将热量牌归还到该玩家的引擎牌库。
    /// </summary>
    public void ReturnHeatCardsToPool(List<CardData> cards)
    {
        foreach (CardData card in cards)
        {
            if (card.IsHeat)
            {
                heatPool.remaining++;
            }
        }
    }

    /// <summary>
    /// 从该玩家的引擎牌库抽取 count 张热量牌，放入弃牌堆。
    /// 返回实际抽到的数量（热量池不足时取走全部剩余）。
    /// </summary>
    public int DrawHeatFromPool(int count)
    {
        int drawn = 0;
        for (int i = 0; i < count; i++)
        {
            if (heatPool.remaining <= 0) break;
            discardPile.Add(new CardData(CardType.Heat, 0));
            heatPool.remaining--;
            drawn++;
        }
        return drawn;
    }

    /// <summary>
    /// 降档冷却 — 从手牌移除最多 count 张热量牌，归还热量池。
    /// 返回实际移除的数量。
    /// </summary>
    public int RemoveHeatFromHand(int count)
    {
        int removed = 0;
        for (int i = hand.Count - 1; i >= 0 && removed < count; i--)
        {
            if (hand[i].IsHeat)
            {
                heatPool.remaining++;
                hand.RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }

    /// <summary>
    /// 手牌中速度牌的数量。
    /// </summary>
    public int CountSpeedInHand()
    {
        int count = 0;
        foreach (CardData card in hand)
        {
            if (card.IsSpeed) count++;
        }
        return count;
    }

    /// <summary>
    /// 手牌中热量牌的数量。
    /// </summary>
    public int CountHeatInHand()
    {
        int count = 0;
        foreach (CardData card in hand)
        {
            if (card.IsHeat) count++;
        }
        return count;
    }

    /// <summary>
    /// 从手牌中获取所有速度牌（按数值降序排列）。
    /// </summary>
    public List<CardData> GetSpeedCardsSortedDesc()
    {
        List<CardData> speeds = new List<CardData>();
        foreach (CardData card in hand)
        {
            if (card.IsSpeed) speeds.Add(card);
        }
        speeds.Sort((a, b) => b.value.CompareTo(a.value));
        return speeds;
    }

    /// <summary>
    /// 从手牌中获取前 N 张速度牌（按数值降序）。
    /// </summary>
    public List<CardData> GetTopNSpeedCards(int n)
    {
        List<CardData> sorted = GetSpeedCardsSortedDesc();
        if (sorted.Count <= n) return sorted;
        return sorted.GetRange(0, n);
    }

    /// <summary>
    /// 从手牌中获取最小的 N 张速度牌（按数值升序）。
    /// </summary>
    public List<CardData> GetBottomNSpeedCards(int n)
    {
        List<CardData> speeds = new List<CardData>();
        foreach (CardData card in hand)
        {
            if (card.IsSpeed) speeds.Add(card);
        }
        speeds.Sort((a, b) => a.value.CompareTo(b.value));
        if (speeds.Count <= n) return speeds;
        return speeds.GetRange(0, n);
    }

    /// <summary>
    /// 失控恢复 — 回收所有热量牌（手牌 + 牌组 + 弃牌堆）到引擎牌库。
    /// 引擎重新点火，散落在外的热量全部收回。
    /// </summary>
    public void RecoverAllHeatToPool()
    {
        // 手牌
        for (int i = hand.Count - 1; i >= 0; i--)
        {
            if (hand[i].IsHeat)
            {
                heatPool.remaining++;
                hand.RemoveAt(i);
            }
        }
        // 牌组
        for (int i = drawPile.Count - 1; i >= 0; i--)
        {
            if (drawPile[i].IsHeat)
            {
                heatPool.remaining++;
                drawPile.RemoveAt(i);
            }
        }
        // 弃牌堆
        for (int i = discardPile.Count - 1; i >= 0; i--)
        {
            if (discardPile[i].IsHeat)
            {
                heatPool.remaining++;
                discardPile.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 从牌组或弃牌堆中移除 1 张热量牌，归还热量池。优先牌组。
    /// 返回是否成功移除。
    /// </summary>
    public bool RemoveOneHeatFromDeck()
    {
        // 优先从牌组移除
        for (int i = drawPile.Count - 1; i >= 0; i--)
        {
            if (drawPile[i].IsHeat)
            {
                drawPile.RemoveAt(i);
                heatPool.remaining++;
                return true;
            }
        }
        // 再从弃牌堆移除
        for (int i = discardPile.Count - 1; i >= 0; i--)
        {
            if (discardPile[i].IsHeat)
            {
                discardPile.RemoveAt(i);
                heatPool.remaining++;
                return true;
            }
        }
        return false;
    }

    /// <summary>牌组+弃牌堆中速度牌数量（未抽到手牌的牌）。</summary>
    public int CountSpeedInDeck()
    {
        int count = 0;
        foreach (var c in drawPile) if (c.IsSpeed) count++;
        foreach (var c in discardPile) if (c.IsSpeed) count++;
        return count;
    }

    /// <summary>牌组+弃牌堆中热量牌数量。</summary>
    public int CountHeatInDeck()
    {
        int count = 0;
        foreach (var c in drawPile) if (c.IsHeat) count++;
        foreach (var c in discardPile) if (c.IsHeat) count++;
        return count;
    }
}
