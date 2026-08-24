using System.Collections;
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

    [Header("图标设置")]
    public Vector2 iconSize = new Vector2(100, 100);

    [Header("选中态表现")]
    [Tooltip("选中卡牌的目标缩放；不改变 LayoutGroup 计算的槽位尺寸。")]
    public float selectedScale = 1.08f;
    [Tooltip("选中卡牌向上移动的像素距离。")]
    public float selectedLift = 24f;
    [Tooltip("选中态过渡时长（秒，使用未缩放时间）。")]
    public float selectionDuration = 0.14f;
    public Color selectedShadowColor = new Color(0.15f, 0.75f, 1f, 0.85f);
    public Vector2 selectedShadowDistance = new Vector2(3f, -3f);

    [Header("卡牌数据")]
    public CardData cardData;
    public bool isSelected;

    private System.Action<CardUI> onClickCallback;
    private Image overlayImage; // 运行时动态创建的选中态叠加层
    private Image iconImage;    // 运行时动态创建的速度数字/热量图标
    private Shadow selectionShadow;
    private RectTransform rectTransform;
    private Vector2 baseAnchoredPosition;
    private Vector3 baseScale = Vector3.one;
    private bool hasBaseTransform;
    private Coroutine selectionRoutine;

    // 颜色常量（精灵图缺失时的回退方案）
    private static readonly Color COLOR_DEFAULT = new Color(1f, 1f, 1f, 1f);
    private static readonly Color COLOR_SELECTED = new Color(0.3f, 0.9f, 0.3f, 1f);
    private static readonly Color COLOR_HEAT = new Color(0.55f, 0.35f, 0.28f, 1f);

    void Awake()
    {
        EnsureSelectionComponents();

        // 创建选中态叠加层（覆盖在背景之上，图标之下）
        CreateOverlayIfNeeded();

        // 创建中央图标（显示速度数字或热量图标）
        CreateIconIfNeeded();
    }

    private void EnsureSelectionComponents()
    {
        rectTransform = GetComponent<RectTransform>();

        // Shadow 挂在卡牌根 Image 上，不占用额外布局尺寸；只有选中时才显示。
        selectionShadow = GetComponent<Shadow>();
        if (selectionShadow == null)
            selectionShadow = gameObject.AddComponent<Shadow>();
        selectionShadow.useGraphicAlpha = true;
        selectionShadow.effectColor = Color.clear;
        selectionShadow.effectDistance = Vector2.zero;
    }

    private void CreateOverlayIfNeeded()
    {
        // 创建选中态叠加层（覆盖在背景之上，图标之下）
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

    private void CreateIconIfNeeded()
    {
        // 创建中央图标（显示速度数字或热量图标）
        if (iconImage == null)
        {
            GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(transform, false);
            RectTransform irt = iconGO.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.5f, 0.5f);
            irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = iconSize;
            irt.anchoredPosition = Vector2.zero;

            iconImage = iconGO.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
        }
    }

    public void SetupCard(CardData data, System.Action<CardUI> callback, string labelOverride = null)
    {
        EnsureSelectionComponents();
        CreateOverlayIfNeeded();
        CreateIconIfNeeded();
        StopSelectionAnimation();
        if (hasBaseTransform && rectTransform != null)
        {
            rectTransform.localScale = baseScale;
            rectTransform.anchoredPosition = baseAnchoredPosition;
        }
        cardData = data;
        onClickCallback = callback;
        isSelected = false;
        hasBaseTransform = false;

        // 设置背景精灵图
        if (backgroundImage != null)
        {
            if (data.IsTrick)
            {
                // 特技牌复用速度牌背景 + 金色调
                backgroundImage.sprite = speedBgSprite;
                if (backgroundImage.sprite == null)
                    backgroundImage.color = new Color(1f, 0.85f, 0.4f, 1f);
            }
            else if (data.IsHeat && heatBgSprite != null)
                backgroundImage.sprite = heatBgSprite;
            else if (!data.IsHeat && speedBgSprite != null)
                backgroundImage.sprite = speedBgSprite;

            // 没有精灵图时回退到纯色
            if (backgroundImage.sprite == null)
                backgroundImage.color = data.IsHeat ? COLOR_HEAT : COLOR_DEFAULT;
            else
                backgroundImage.color = Color.white;
        }

        // 特技牌：直接显示名称（不显示数字/图标）
        if (data.IsTrick)
        {
            if (iconImage != null) iconImage.enabled = false;
            if (valueText != null)
            {
                valueText.text = labelOverride ?? "特技";
                valueText.color = new Color(0.45f, 0.25f, 0f, 1f);
            }
        }
        else
        {
            // 设置中央图标（精灵图优先，文字回退）
            Sprite iconSprite = null;
            if (!data.IsHeat && numberSprites != null
                && data.value >= 1 && data.value <= numberSprites.Length)
            {
                iconSprite = numberSprites[data.value - 1];
            }
            else if (data.IsHeat && heatIconSprite != null)
            {
                iconSprite = heatIconSprite;
            }

            if (iconImage != null)
            {
                iconImage.sprite = iconSprite;
                iconImage.enabled = iconSprite != null;
            }

            // 文字回退：精灵图不可用时显示文字
            if (valueText != null)
            {
                if (iconSprite != null)
                {
                    valueText.text = "";
                }
                else
                {
                    valueText.text = data.IsHeat ? "" : data.value.ToString();
                    valueText.color = Color.white;
                }
            }
        }

        // 选中覆盖层初始隐藏
        if (overlayImage != null)
        {
            overlayImage.sprite = selectedOverlaySprite;
            overlayImage.color = new Color(1, 1, 1, 0);
        }

        // 热量牌不是可执行动作，避免它进入键盘/手柄的无效焦点序列。
        Button button = GetComponent<Button>();
        if (button != null)
            button.interactable = !data.IsHeat;

        UpdateVisual();
    }

    public void OnCardClicked()
    {
        // 热量牌不可打出 — 点击无响应
        if (cardData != null && cardData.IsHeat) return;

        // 选择规则由 CardHandUI 按当前模式统一处理：
        // 正常出牌只允许一个待确认项，弃牌阶段允许多选。
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

        if (selectionShadow != null)
        {
            selectionShadow.effectColor = isSelected ? selectedShadowColor : Color.clear;
            selectionShadow.effectDistance = isSelected ? selectedShadowDistance : Vector2.zero;
        }
    }

    /// <summary>程序化设置选中状态（不触发回调）。</summary>
    public void SetSelectedWithoutNotify(bool selected)
    {
        EnsureSelectionComponents();
        if (rectTransform != null && !hasBaseTransform)
        {
            baseAnchoredPosition = rectTransform.anchoredPosition;
            baseScale = rectTransform.localScale;
            hasBaseTransform = true;
        }

        isSelected = selected;
        UpdateVisual();
        AnimateSelectionTransform();
    }

    private void AnimateSelectionTransform()
    {
        if (rectTransform == null || !hasBaseTransform)
            return;

        if (selectionRoutine != null)
            StopCoroutine(selectionRoutine);

        Vector3 targetScale = baseScale * (isSelected ? Mathf.Max(1f, selectedScale) : 1f);
        Vector2 targetPosition = baseAnchoredPosition +
            (isSelected ? Vector2.up * Mathf.Max(0f, selectedLift) : Vector2.zero);

        if (selectionDuration <= 0f || !isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            rectTransform.localScale = targetScale;
            rectTransform.anchoredPosition = targetPosition;
            return;
        }

        selectionRoutine = StartCoroutine(AnimateSelectionRoutine(targetScale, targetPosition));
    }

    private IEnumerator AnimateSelectionRoutine(Vector3 targetScale, Vector2 targetPosition)
    {
        Vector3 startScale = rectTransform.localScale;
        Vector2 startPosition = rectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < selectionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / selectionDuration);
            t = t * t * (3f - 2f * t); // SmoothStep，避免卡牌突然跳动
            rectTransform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, t);
            yield return null;
        }

        rectTransform.localScale = targetScale;
        rectTransform.anchoredPosition = targetPosition;
        selectionRoutine = null;
    }

    private void StopSelectionAnimation()
    {
        if (selectionRoutine != null)
        {
            StopCoroutine(selectionRoutine);
            selectionRoutine = null;
        }
    }

    private void OnDisable()
    {
        StopSelectionAnimation();
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
