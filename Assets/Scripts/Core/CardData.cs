/// <summary>
/// 卡牌类型枚举。
/// </summary>
public enum CardType
{
    Speed,  // 速度牌 — 打出产生移动
    Heat,   // 热量牌 — 不可打出，仅占手牌位。只能通过降档冷却或 G1 散热移除
    Trick   // 特技牌 — 车队专属，不占档位出牌位，每回合最多打1张
}

/// <summary>
/// 卡牌数据 — 纯数据类，非 MonoBehaviour。
/// </summary>
[System.Serializable]
public class CardData
{
    public CardType type;
    public int value; // 速度牌: 1-4; 热量牌: 0; 特技牌: 0（无速度值）

    /// <summary>特技牌 ID（仅 Trick 类型时非空）。</summary>
    public string trickId;

    public CardData(CardType type, int value)
    {
        this.type = type;
        this.value = value;
        this.trickId = null;
    }

    /// <summary>Create a trick card with the given ID.</summary>
    public static CardData CreateTrick(string trickId)
    {
        return new CardData(CardType.Trick, 0) { trickId = trickId };
    }

    public bool IsSpeed => type == CardType.Speed;
    public bool IsHeat => type == CardType.Heat;
    public bool IsTrick => type == CardType.Trick;

    public override string ToString()
    {
        if (IsHeat) return "H";
        if (IsTrick) return $"T[{trickId}]";
        return value.ToString();
    }
}
