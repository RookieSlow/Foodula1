using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 单张卡牌的 UI 表现。挂载在 CardPrefab 上。
/// 支持精灵图背景（速度牌/热量牌）、选中态叠加、数字/热量图标。
/// </summary>
public class CardUI : MonoBehaviour
{
    [Header("UI 组件")]
    public TMP_Text valueText;
    public Image backgroundImage;

    [Header("精灵图 — 卡面")]
    public Sprite speedBgSprite;        // card_speed_bg.png
    public Sprite heatBgSprite;         // card_heat_bg.png
    public Sprite selectedOverlaySprite; // card_selected_overlay.png

    [Header("精灵图 — 图标")]
    public Sprite[] numberSprites = new Sprite[4]; // card_num_1~4.png
    public Sprite heatIconSprite;        // card_heat_icon.png

    [Header("卡牌数据")]
    public CardData cardData;
    public bool isSelected;

    private System.Action<CardUI> onClickCallback;
    private Image overlayImage; // 运行时动态创建的选中态叠加层

    // 颜色常量（精灵图缺失时的回退方案）
    private static readonly Color COLOR_DEFAULT = new Color(1f, 1f, 1f, 1f);
    private static readonly Color COLOR_SELECTED = new Color(0.3f, 0.9f, 0.3f, 1f);
    private static readonly Color COLOR_HEAT = new Color(0.55f, 0.35f, 0.28f, 1f);

    void Awake()
    {
        // 创建选中态叠加层（覆盖在背景之上，文字之下）
        if (overlayImage == null)
        {
            GameObject overlayGO = new GameObject("Overlay", typeof(RectTransform));
            overlayGO.transform.SetParent(transform, false);
            RectTransform ort = overlayGO.GetComponent<RectTransform>();
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.sizeDelta = Vector2.zero;
            ort.anchoredPosition = Vector2.zero;

            overlayImage = overlayGO.AddComponent<Image>();
            overlayImage.raycastTarget = false; // 不拦截点击
            overlayImage.color = new Color(1, 1, 1, 0); // 默认透明
            overlayImage.preserveAspect = true;
        }
    }

    public void SetupCard(CardData data, System.Action<CardUI> callback)
    {
        cardData = data;
        onClickCallback = callback;
        isSelected = false;

        // 设置背景精灵图
        if (backgroundImage != null)
        {
            if (data.IsHeat && heatBgSprite != null)
                backgroundImage.sprite = heatBgSprite;
            else if (!data.IsHeat && speedBgSprite != null)
                backgroundImage.sprite = speedBgSprite;

            // 没有精灵图时回退到纯色
            if (backgroundImage.sprite == null)
                backgroundImage.color = data.IsHeat ? COLOR_HEAT : COLOR_DEFAULT;
            else
                backgroundImage.color = Color.white;
        }

        // 设置中央图标/数字
        if (valueText != null)
        {
            valueText.text = data.IsHeat ? "" : data.value.ToString();
            valueText.color = Color.white;
        }

        // 选中覆盖层初始隐藏
        if (overlayImage != null)
        {
            overlayImage.sprite = selectedOverlaySprite;
            overlayImage.color = new Color(1, 1, 1, 0);
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
        // 选中态叠加层
        if (overlayImage != null)
        {
            overlayImage.color = isSelected
                ? new Color(1, 1, 1, 0.35f)
                : new Color(1, 1, 1, 0);
        }

        // 精灵图模式下不需要改色；纯色回退模式下改变背景色
        if (backgroundImage != null && backgroundImage.sprite == null)
        {
            if (cardData != null && cardData.IsHeat)
                backgroundImage.color = COLOR_HEAT;
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

    /// <summary>批量设置所有精灵图引用（由 CardHandUI 在实例化后调用）。</summary>
    public void SetSprites(Sprite speedBg, Sprite heatBg, Sprite selectedOverlay,
        Sprite[] numbers, Sprite heatIcon)
    {
        speedBgSprite = speedBg;
        heatBgSprite = heatBg;
        selectedOverlaySprite = selectedOverlay;
        numberSprites = numbers;
        heatIconSprite = heatIcon;
    }
}
