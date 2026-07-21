/// <summary>
/// 卡牌类型枚举。
/// </summary>
public enum CardType
{
    Speed,  // 速度牌 — 打出产生移动
    Heat    // 热量牌 — 打出 = 0 移动，占手牌位
}

/// <summary>
/// 卡牌数据 — 纯数据类，非 MonoBehaviour。
/// </summary>
[System.Serializable]
public class CardData
{
    public CardType type;
    public int value; // 速度牌: 1-4; 热量牌: 0

    public CardData(CardType type, int value)
    {
        this.type = type;
        this.value = value;
    }

    public bool IsSpeed => type == CardType.Speed;
    public bool IsHeat => type == CardType.Heat;

    public override string ToString()
    {
        return IsHeat ? "H" : value.ToString();
    }
}
