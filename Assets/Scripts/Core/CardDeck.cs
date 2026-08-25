using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 引擎牌库 — 每玩家独立。弯道超速/急刹/引擎故障时从此支付热量牌，
/// 放入手牌或弃牌堆；明确的冷却/回收效果再将热量归还至此。
/// 包装类以支持引用传递。
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
/// Destination for a heat card paid from the independent engine pool.
/// Heat cards never use the ordinary draw path; the destination is chosen by
/// the rule that caused the payment.
/// </summary>
public enum HeatPaymentDestination
{
    Hand,
    Discard
}

/// <summary>
/// 牌组系统 — 纯 C# 逻辑类（非 MonoBehaviour）。
/// 管理牌组（drawPile）、手牌（hand）、弃牌堆（discardPile）和该玩家持有的引擎牌库引用。
///
/// 热量牌生命周期: 热量池 →(支付热量)→ 手牌或弃牌堆 →(冷却)→ 热量池。
/// 普通抽牌只循环速度牌和特技牌；热量牌不属于普通抽牌堆。
/// 速度牌/特技牌生命周期: 牌组 → 手牌 → 打出/弃置 → 弃牌堆 → 洗回牌组。
/// </summary>
public class CardDeck
{
    private List<CardData> drawPile = new List<CardData>();
    private List<CardData> hand = new List<CardData>();
    private List<CardData> discardPile = new List<CardData>();
    private IRandomSource randomSource = new UnityRandomSource();

    /// <summary>该玩家的引擎牌库 — 每玩家独立的 HeatPool 实例，支付热量时从此扣除。</summary>
    public HeatPool heatPool;

    // --- 只读属性 ---
    public IReadOnlyList<CardData> Hand => hand;
    public IReadOnlyList<CardData> DrawPile => drawPile;
    public IReadOnlyList<CardData> DiscardPile => discardPile;
    public int HandCount => hand.Count;
    public int DrawPileCount => drawPile.Count;
    public int DiscardPileCount => discardPile.Count;

    /// <summary>牌组 + 弃牌堆中仍可普通抽取的卡牌数量。</summary>
    public int TotalAvailableForDraw => CountPlayableCards(drawPile) + CountPlayableCards(discardPile);

    /// <summary>Returns whether this exact runtime card is currently in hand.</summary>
    public bool ContainsInHand(CardData card) => card != null && hand.Contains(card);

    /// <summary>
    /// 用配置初始化牌组。
    /// 速度牌 → 普通抽牌堆，然后洗牌。
    /// 热量牌属于独立引擎热量池，不在此处初始化，也不参与普通抽牌。
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
    /// 从普通抽牌堆抽牌到手牌，直至手牌数达到 handSize。
    /// 牌组不够时只将非热量牌从弃牌堆洗回；热量牌留在原区域等待冷却。
    /// 如果没有可普通抽取的速度/特技牌 → 返回 false。
    /// </summary>
    public bool DrawToHand(int handSize)
    {
        while (hand.Count < handSize)
        {
            int drawIndex = FindPlayableCardIndex(drawPile);
            if (drawIndex < 0)
            {
                RecyclePlayableDiscardCards();
                drawIndex = FindPlayableCardIndex(drawPile);
                if (drawIndex < 0) return false;
            }

            hand.Add(drawPile[drawIndex]);
            drawPile.RemoveAt(drawIndex);
        }
        return true;
    }

    private static int FindPlayableCardIndex(List<CardData> pile)
    {
        if (pile == null) return -1;
        for (int i = 0; i < pile.Count; i++)
        {
            CardData card = pile[i];
            if (card != null && !card.IsHeat) return i;
        }
        return -1;
    }

    private static int CountPlayableCards(List<CardData> pile)
    {
        if (pile == null) return 0;
        int count = 0;
        foreach (CardData card in pile)
            if (card != null && !card.IsHeat) count++;
        return count;
    }

    private void RecyclePlayableDiscardCards()
    {
        bool moved = false;
        for (int i = discardPile.Count - 1; i >= 0; i--)
        {
            CardData card = discardPile[i];
            if (card == null || card.IsHeat) continue;

            drawPile.Add(card);
            discardPile.RemoveAt(i);
            moved = true;
        }

        if (moved)
            ShuffleDrawPile();
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
    /// Removes playable cards from hand and puts them directly into the discard pile.
    /// Heat cards are deliberately ignored because they can only leave hand through cooling.
    /// Returns the number of cards discarded.
    /// </summary>
    public int DiscardPlayableCardsFromHand(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return 0;

        int discarded = 0;
        foreach (CardData card in cards)
        {
            if (card == null || card.IsHeat) continue;
            if (hand.Remove(card))
            {
                discardPile.Add(card);
                discarded++;
            }
        }
        return discarded;
    }

    /// <summary>
    /// 消耗指定的手牌热量牌；永久热量归还引擎，限时热量直接销毁。
    /// 只有当前确实位于手牌中的同一张运行时卡牌才会被消耗，避免重复请求凭空增加热量。
    /// 返回实际消耗数量。
    /// </summary>
    public int ReturnHeatCardsToPool(IReadOnlyList<CardData> cards)
    {
        if (cards == null || heatPool == null) return 0;

        int returned = 0;
        foreach (CardData card in cards)
        {
            if (card == null || !card.IsHeat || !hand.Remove(card)) continue;
            if (!card.isTemp)
                heatPool.remaining++;
            returned++;
        }
        return returned;
    }

    /// <summary>
    /// 从该玩家的引擎牌库支付 count 张热量牌，放入弃牌堆。
    /// 这是显式的弃牌堆支付路径（例如中国队阴阳茶 Go）；它不参与普通抽牌。
    /// 返回实际抽到的数量（热量池不足时取走全部剩余）。
    /// </summary>
    public int DrawHeatFromPool(int count)
    {
        return DrawHeatFromPool(count, HeatPaymentDestination.Discard);
    }

    /// <summary>
    /// 从引擎支付 count 张永久热量牌，并按指定规则放入手牌或弃牌堆。
    /// </summary>
    public int DrawHeatFromPool(int count, HeatPaymentDestination destination)
    {
        if (heatPool == null || count <= 0) return 0;

        int drawn = 0;
        for (int i = 0; i < count; i++)
        {
            if (heatPool.remaining <= 0) break;
            List<CardData> target = destination == HeatPaymentDestination.Hand
                ? hand
                : discardPile;
            target.Add(new CardData(CardType.Heat, 0));
            heatPool.remaining--;
            drawn++;
        }
        return drawn;
    }

    /// <summary>
    /// 从引擎支付 count 张永久热量牌，直接放入手牌。
    /// 这是明确指定“入手牌”的支付路径，不属于普通抽牌。
    /// </summary>
    public int DrawHeatFromPoolToHand(int count)
    {
        return DrawHeatFromPool(count, HeatPaymentDestination.Hand);
    }

    /// <summary>
    /// 降档冷却 — 从手牌移除最多 count 张热量牌；永久热量归还热量池，限时热量销毁。
    /// 返回实际移除的数量。
    /// </summary>
    public int RemoveHeatFromHand(int count)
    {
        return RemoveHeatFromPile(hand, count);
    }

    /// <summary>
    /// 通用冷却：严格按“手牌 → 抽牌堆 → 弃牌堆”顺序移除热量牌。
    /// 永久热量牌归还引擎，限时热量牌直接销毁；不会因为冷却而洗牌。
    /// </summary>
    public int CoolHeat(int count)
    {
        if (count <= 0) return 0;

        int removed = RemoveHeatFromPile(hand, count);
        if (removed < count)
            removed += RemoveHeatFromPile(drawPile, count - removed);
        if (removed < count)
            removed += RemoveHeatFromPile(discardPile, count - removed);
        return removed;
    }

    private int RemoveHeatFromPile(List<CardData> pile, int count)
    {
        if (pile == null || count <= 0) return 0;

        int removed = 0;
        for (int i = pile.Count - 1; i >= 0 && removed < count; i--)
        {
            CardData card = pile[i];
            if (card == null || !card.IsHeat) continue;

            pile.RemoveAt(i);
            if (!card.isTemp && heatPool != null)
                heatPool.remaining++;
            removed++;
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
        return CountHeatCards(hand);
    }

    /// <summary>Heat currently stranded in the ordinary draw zone (legacy/test compatibility).</summary>
    public int CountHeatInDrawPile() => CountHeatCards(drawPile);

    /// <summary>Heat currently waiting for cooling in the discard zone.</summary>
    public int CountHeatInDiscardPile() => CountHeatCards(discardPile);

    private static int CountHeatCards(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return 0;
        int count = 0;
        foreach (CardData card in cards)
            if (card != null && card.IsHeat) count++;
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
    /// 失控恢复 — 回收所有热量牌到引擎牌库。
    /// 正常流程只有手牌和弃牌堆会持有永久热量；扫描抽牌堆是旧存档/测试状态的防线。
    /// </summary>
    public void RecoverAllHeatToPool()
    {
        // 手牌
        for (int i = hand.Count - 1; i >= 0; i--)
        {
            if (hand[i].IsHeat)
            {
                if (!hand[i].isTemp)
                    heatPool.remaining++;
                hand.RemoveAt(i);
            }
        }
        // 牌组
        for (int i = drawPile.Count - 1; i >= 0; i--)
        {
            if (drawPile[i].IsHeat)
            {
                if (!drawPile[i].isTemp)
                    heatPool.remaining++;
                drawPile.RemoveAt(i);
            }
        }
        // 弃牌堆
        for (int i = discardPile.Count - 1; i >= 0; i--)
        {
            if (discardPile[i].IsHeat)
            {
                if (!discardPile[i].isTemp)
                    heatPool.remaining++;
                discardPile.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 从牌组或弃牌堆中移除 1 张遗留热量牌，归还热量池。
    /// 正常运行时普通牌组没有热量，因此实际回退到弃牌堆；返回是否成功移除。
    /// </summary>
    public bool RemoveOneHeatFromDeck()
    {
        // 优先从牌组移除
        for (int i = drawPile.Count - 1; i >= 0; i--)
        {
            if (drawPile[i].IsHeat)
            {
                bool isTemp = drawPile[i].isTemp;
                drawPile.RemoveAt(i);
                if (!isTemp)
                    heatPool.remaining++;
                return true;
            }
        }
        // 再从弃牌堆移除
        for (int i = discardPile.Count - 1; i >= 0; i--)
        {
            if (discardPile[i].IsHeat)
            {
                bool isTemp = discardPile[i].isTemp;
                discardPile.RemoveAt(i);
                if (!isTemp)
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
        return CountHeatInDrawPile() + CountHeatInDiscardPile();
    }

    /// <summary>牌组+弃牌堆中特技牌数量。</summary>
    public int CountTricksInDeck()
    {
        int count = 0;
        foreach (var c in drawPile) if (c.IsTrick) count++;
        foreach (var c in discardPile) if (c.IsTrick) count++;
        return count;
    }

    // ====== 特技牌（Trick Cards） ======

    /// <summary>
    /// 将车队特技牌加入普通抽牌堆并重新洗牌。
    /// 特技牌不会直接进入开局手牌，而是与速度牌一起随机抽取；热量牌不属于该牌组。
    /// </summary>
    public int AddTrickCardsToDrawPile(IReadOnlyList<CardData> tricks)
    {
        if (tricks == null) return 0;

        int added = 0;
        foreach (var t in tricks)
        {
            if (t == null || !t.IsTrick) continue;
            drawPile.Add(t);
            added++;
        }

        if (added > 0)
            ShuffleDrawPile();
        return added;
    }

    /// <summary>
    /// Ensures one exact trick-card instance is in the opening hand. This is a
    /// test-assist hook: it preserves card conservation and the current hand
    /// size by swapping a non-heat opening card back into the draw pile.
    /// </summary>
    public bool EnsureTrickCardInHand(string trickId)
    {
        if (string.IsNullOrEmpty(trickId)) return false;

        for (int i = 0; i < hand.Count; i++)
        {
            CardData held = hand[i];
            if (held != null && held.IsTrick && held.trickId == trickId)
                return true;
        }

        CardData requested = null;
        for (int i = 0; i < drawPile.Count; i++)
        {
            CardData candidate = drawPile[i];
            if (candidate != null && candidate.IsTrick && candidate.trickId == trickId)
            {
                requested = candidate;
                drawPile.RemoveAt(i);
                break;
            }
        }

        if (requested == null)
        {
            for (int i = 0; i < discardPile.Count; i++)
            {
                CardData candidate = discardPile[i];
                if (candidate != null && candidate.IsTrick && candidate.trickId == trickId)
                {
                    requested = candidate;
                    discardPile.RemoveAt(i);
                    break;
                }
            }
        }

        if (requested == null) return false;

        if (hand.Count > 0)
        {
            int replacementIndex = -1;
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] != null && !hand[i].IsHeat)
                {
                    replacementIndex = i;
                    break;
                }
            }

            if (replacementIndex < 0)
                replacementIndex = hand.Count - 1;

            CardData displaced = hand[replacementIndex];
            hand.RemoveAt(replacementIndex);
            if (displaced != null)
                drawPile.Add(displaced);
        }

        hand.Add(requested);
        ShuffleDrawPile();
        return true;
    }

    /// <summary>
    /// Adds explicitly granted non-heat runtime cards or temporary heat cards
    /// directly to the hand. Permanent heat must use an engine-payment method
    /// such as <see cref="DrawHeatFromPoolToHand"/>; normal DrawToHand never
    /// inserts heat cards.
    /// </summary>
    public void AddCardsToHand(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return;
        foreach (var card in cards)
            if (card != null && (!card.IsHeat || card.isTemp)) hand.Add(card);
    }

    /// <summary>手牌中所有特技牌。</summary>
    public List<CardData> GetTricksInHand()
    {
        var result = new List<CardData>();
        foreach (var c in hand)
            if (c.IsTrick) result.Add(c);
        return result;
    }

    /// <summary>打出特技牌：从手牌移除并放入弃牌堆。返回是否成功。</summary>
    public bool DiscardTrickCard(CardData trick)
    {
        if (trick == null || !trick.IsTrick) return false;
        return DiscardPlayableCardsFromHand(new[] { trick }) == 1;
    }

    /// <summary>将限时卡牌（Fries 临时热量牌）从手牌移除并销毁。返回移除数量。</summary>
    public int RemoveTempCardsFromHand()
    {
        int removed = 0;
        for (int i = hand.Count - 1; i >= 0; i--)
        {
            if (hand[i].isTemp)
            {
                hand.RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }
}
