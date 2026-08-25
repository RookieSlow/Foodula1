using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compact visual preview for a draw or discard pile. The component is created
/// at runtime inside the existing pile panel, so authored scene layouts remain
/// untouched.
/// </summary>
public sealed class CardPilePreviewUI : MonoBehaviour
{
    [Header("缩略图")]
    public int visibleCardCount = 3;
    public Vector2 cardSize = new Vector2(42f, 58f);
    public float cardSpacing = 18f;

    [Header("牌堆厚度")]
    [Tooltip("用于表达牌堆数量的最大可见牌背层数；精确数量仍由徽标显示。")]
    public int maxStackLayers = 7;
    [Tooltip("每增加多少张牌，多显示一层牌背。")]
    public int cardsPerStackLayer = 3;
    public Vector2 stackLayerOffset = new Vector2(2.4f, 1.7f);

    private readonly List<Image> cardImages = new List<Image>();
    private readonly List<Image> cardIconImages = new List<Image>();
    private readonly List<TMP_Text> cardLabels = new List<TMP_Text>();
    private readonly List<Image> stackBackImages = new List<Image>();

    private RectTransform cardLayer;
    private TMP_Text countBadge;
    private TMP_Text emptyLabel;
    private Sprite speedBgSprite;
    private Sprite heatBgSprite;
    private Sprite[] numberSprites;
    private Sprite heatIconSprite;
    private TMP_FontAsset fontAsset;
    private bool newestCardIsAtEnd;

    public int DisplayedCardCount { get; private set; }
    public int DisplayedPileCount { get; private set; }
    public int DisplayedStackLayerCount { get; private set; }

    /// <summary>
    /// Configures art references and whether the pile's newest card is at the
    /// end of its list (discard pile) or at the beginning (draw pile).
    /// </summary>
    public void Configure(
        Sprite speedBackground,
        Sprite heatBackground,
        Sprite[] speedNumbers,
        Sprite heatIcon,
        TMP_FontAsset font,
        bool newestAtEnd)
    {
        speedBgSprite = speedBackground;
        heatBgSprite = heatBackground;
        numberSprites = speedNumbers;
        heatIconSprite = heatIcon;
        fontAsset = font;
        newestCardIsAtEnd = newestAtEnd;
        EnsureVisuals();
    }

    /// <summary>Refreshes thumbnails and the quantity badge from live deck data.</summary>
    public void Refresh(IReadOnlyList<CardData> pile, int pileCount)
    {
        EnsureVisuals();
        DisplayedPileCount = Mathf.Max(0, pileCount);

        List<CardData> cards = CardPilePreviewRules.SelectVisibleCards(
            pile, visibleCardCount, newestCardIsAtEnd);
        DisplayedCardCount = cards.Count;

        for (int i = 0; i < cardImages.Count; i++)
        {
            bool visible = i < cards.Count;
            cardImages[i].gameObject.SetActive(visible);
            if (visible)
            {
                ApplyCardVisual(i, cards[i]);
                cardIconImages[i].gameObject.SetActive(cardIconImages[i].sprite != null);
                cardLabels[i].gameObject.SetActive(cardLabels[i].text.Length > 0);
            }
            else
            {
                cardIconImages[i].gameObject.SetActive(false);
                cardLabels[i].gameObject.SetActive(false);
            }
        }

        bool hasCards = cards.Count > 0;
        if (emptyLabel != null)
        {
            emptyLabel.gameObject.SetActive(!hasCards);
            emptyLabel.text = "空";
        }

        if (countBadge != null)
            countBadge.text = DisplayedPileCount.ToString();

        DisplayedStackLayerCount = CardPilePreviewRules.GetStackLayerCount(
            DisplayedPileCount, maxStackLayers, cardsPerStackLayer);
        for (int i = 0; i < stackBackImages.Count; i++)
            stackBackImages[i].gameObject.SetActive(i < DisplayedStackLayerCount);
    }

    private void EnsureVisuals()
    {
        if (cardLayer == null)
        {
            GameObject layerObject = new GameObject("CardPreviewLayer", typeof(RectTransform));
            layerObject.transform.SetParent(transform, false);
            cardLayer = layerObject.GetComponent<RectTransform>();
            cardLayer.anchorMin = new Vector2(0.08f, 0.18f);
            cardLayer.anchorMax = new Vector2(0.92f, 0.68f);
            cardLayer.offsetMin = Vector2.zero;
            cardLayer.offsetMax = Vector2.zero;
            cardLayer.pivot = new Vector2(0.5f, 0.5f);

            int stackLayers = Mathf.Max(1, maxStackLayers);
            for (int i = stackLayers - 1; i >= 0; i--)
            {
                Vector2 offset = new Vector2(
                    -stackLayerOffset.x * i,
                    -stackLayerOffset.y * i);
                float alpha = Mathf.Lerp(0.34f, 0.82f, 1f - i / (float)stackLayers);
                CreateStackBack("StackBack" + i, offset, alpha);
            }
            for (int i = 0; i < Mathf.Max(1, visibleCardCount); i++)
                CreateCardThumb(i);
        }

        if (countBadge == null)
            countBadge = CreateCountBadge();

        if (emptyLabel == null)
            emptyLabel = CreateLabel("EmptyLabel", "空", 16f);
    }

    private void CreateStackBack(string name, Vector2 position, float alpha)
    {
        GameObject backObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        backObject.transform.SetParent(cardLayer, false);
        RectTransform rect = backObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = cardSize;
        rect.anchoredPosition = position;

        Image image = backObject.GetComponent<Image>();
        image.sprite = speedBgSprite;
        image.color = new Color(0.16f, 0.22f, 0.34f, alpha);
        image.preserveAspect = true;
        image.raycastTarget = false;
        // Layers are created from deepest to nearest for correct draw order;
        // keep the list nearest-first so increasing pile counts reveal outward.
        stackBackImages.Insert(0, image);
    }

    private void CreateCardThumb(int index)
    {
        GameObject cardObject = new GameObject("CardThumb" + index, typeof(RectTransform), typeof(Image));
        cardObject.transform.SetParent(cardLayer, false);
        RectTransform rect = cardObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = cardSize;
        float centeredIndex = index - (Mathf.Max(1, visibleCardCount) - 1) * 0.5f;
        rect.anchoredPosition = new Vector2(centeredIndex * cardSpacing, 0f);

        Image background = cardObject.GetComponent<Image>();
        background.preserveAspect = true;
        background.raycastTarget = false;
        cardImages.Add(background);

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(cardObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.56f);
        iconRect.sizeDelta = new Vector2(cardSize.x * 0.68f, cardSize.y * 0.48f);
        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        cardIconImages.Add(icon);

        TMP_Text label = CreateLabel("Label", "", 12f, cardObject.transform);
        label.alignment = TextAlignmentOptions.Center;
        cardLabels.Add(label);
    }

    private TMP_Text CreateCountBadge()
    {
        GameObject badgeObject = new GameObject("CountBadge", typeof(RectTransform), typeof(Image));
        badgeObject.transform.SetParent(transform, false);
        RectTransform rect = badgeObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.63f, 0.04f);
        rect.anchorMax = new Vector2(0.96f, 0.27f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image background = badgeObject.GetComponent<Image>();
        background.color = new Color(0.02f, 0.04f, 0.08f, 0.94f);
        background.raycastTarget = false;

        TMP_Text text = CreateLabel("Count", "0", 14f, badgeObject.transform);
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    private TMP_Text CreateLabel(string name, string value, float size, Transform parent = null)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform));
        labelObject.transform.SetParent(parent != null ? parent : transform, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = size;
        label.color = Color.white;
        label.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private void ApplyCardVisual(int index, CardData card)
    {
        Image background = cardImages[index];
        Image icon = cardIconImages[index];
        TMP_Text label = cardLabels[index];

        background.sprite = card.IsHeat ? heatBgSprite : speedBgSprite;
        if (background.sprite == null)
            background.color = card.IsHeat
                ? new Color(0.55f, 0.35f, 0.28f, 1f)
                : new Color(0.32f, 0.52f, 0.78f, 1f);
        else
            background.color = card.IsTrick
                ? new Color(1f, 0.84f, 0.38f, 1f)
                : Color.white;

        Sprite iconSprite = null;
        if (card.IsHeat)
            iconSprite = heatIconSprite;
        else if (card.IsSpeed && numberSprites != null
            && card.value >= 1 && card.value <= numberSprites.Length)
            iconSprite = numberSprites[card.value - 1];

        icon.sprite = iconSprite;
        label.text = card.IsTrick ? "特" : (iconSprite == null && !card.IsHeat ? card.value.ToString() : "");
        label.color = card.IsTrick ? new Color(0.35f, 0.20f, 0f, 1f) : Color.white;
    }
}

/// <summary>Pure ordering rules for the small draw/discard pile preview.</summary>
public static class CardPilePreviewRules
{
    /// <summary>
    /// Converts an exact card count into a compact physical stack. One visual
    /// layer represents a small packet of cards; the badge remains exact.
    /// </summary>
    public static int GetStackLayerCount(int pileCount, int maxLayers, int cardsPerLayer)
    {
        if (pileCount <= 0 || maxLayers <= 0)
            return 0;

        int layerSize = Mathf.Max(1, cardsPerLayer);
        return Mathf.Clamp(Mathf.CeilToInt(pileCount / (float)layerSize), 1, maxLayers);
    }

    public static List<CardData> SelectVisibleCards(
        IReadOnlyList<CardData> pile,
        int maxCards,
        bool newestCardIsAtEnd)
    {
        List<CardData> visible = new List<CardData>();
        if (pile == null || maxCards <= 0)
            return visible;

        if (newestCardIsAtEnd)
        {
            for (int i = pile.Count - 1; i >= 0 && visible.Count < maxCards; i--)
                if (pile[i] != null) visible.Add(pile[i]);
        }
        else
        {
            for (int i = 0; i < pile.Count && visible.Count < maxCards; i++)
                if (pile[i] != null) visible.Add(pile[i]);
        }

        return visible;
    }
}
