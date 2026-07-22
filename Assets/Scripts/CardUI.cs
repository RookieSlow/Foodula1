using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 单张卡牌的 UI 表现。挂载在 CardPrefab 上。
/// 支持速度牌（显示数值）和热量牌（显示 🔥）。
/// </summary>
public class CardUI : MonoBehaviour
{
    [Header("UI 组件")]
    public TMP_Text valueText;
    public Image backgroundImage;

    [Header("卡牌数据")]
    public CardData cardData;
    public bool isSelected;

    private System.Action<CardUI> onClickCallback;

    // 颜色常量
    private static readonly Color COLOR_DEFAULT = new Color(1f, 1f, 1f, 1f);
    private static readonly Color COLOR_SELECTED = new Color(0.3f, 0.9f, 0.3f, 1f);
    private static readonly Color COLOR_HEAT = new Color(0.55f, 0.35f, 0.28f, 1f);   // 暗棕橙色 — 不可打出，视觉上"沉重"

    public void SetupCard(CardData data, System.Action<CardUI> callback)
    {
        cardData = data;
        onClickCallback = callback;
        isSelected = false;

        if (valueText != null)
        {
            valueText.text = data.IsHeat ? "H" : data.value.ToString();
            valueText.color = data.IsHeat ? Color.white : new Color(0.1f, 0.1f, 0.1f);
        }

        UpdateVisual();
    }

    public void OnCardClicked()
    {
        // 热量牌不可打出 — 点击无响应
        if (cardData != null && cardData.IsHeat) return;

        isSelected = !isSelected;
        UpdateVisual();
        onClickCallback?.Invoke(this);
    }

    private void UpdateVisual()
    {
        if (backgroundImage != null)
        {
            if (cardData.IsHeat)
                backgroundImage.color = COLOR_HEAT; // 热量牌永远不可选中，统一暗色
            else
                backgroundImage.color = isSelected ? COLOR_SELECTED : COLOR_DEFAULT;
        }
    }

    /// <summary>程序化设置选中状态（不触发回调）。</summary>
    public void SetSelectedWithoutNotify(bool selected)
    {
        isSelected = selected;
        UpdateVisual();
    }
}
