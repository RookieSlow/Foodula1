using System.Collections.Generic;
using System.Linq;
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
    private bool hidePileOrder;
    private string pileTitle = "牌堆";
    private TrickCardDatabase trickDatabase;
    private Button inspectButton;
    private TMP_Text inspectHint;
    private readonly List<CardData> pileSnapshot = new List<CardData>();

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

    /// <summary>Enables the click-to-inspect view without exposing draw order.</summary>
    public void ConfigureInspection(
        TrickCardDatabase database,
        string title,
        bool concealOrder)
    {
        trickDatabase = database;
        pileTitle = string.IsNullOrWhiteSpace(title) ? "牌堆" : title;
        hidePileOrder = concealOrder;
        EnsureInteraction();
    }

    /// <summary>Refreshes thumbnails and the quantity badge from live deck data.</summary>
    public void Refresh(IReadOnlyList<CardData> pile, int pileCount)
    {
        EnsureVisuals();
        DisplayedPileCount = Mathf.Max(0, pileCount);
        pileSnapshot.Clear();
        if (pile != null)
        {
            for (int i = 0; i < pile.Count; i++)
                if (pile[i] != null) pileSnapshot.Add(pile[i]);
        }

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

        if (inspectButton != null)
            inspectButton.interactable = DisplayedPileCount > 0;
        if (inspectHint != null)
            inspectHint.text = DisplayedPileCount > 0 ? "点击查看" : "暂无卡牌";
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

    private void EnsureInteraction()
    {
        if (inspectButton == null)
        {
            Image target = GetComponent<Image>();
            if (target == null)
            {
                target = gameObject.AddComponent<Image>();
                target.color = new Color(0f, 0f, 0f, 0.001f);
            }
            target.raycastTarget = true;

            inspectButton = GetComponent<Button>();
            if (inspectButton == null)
                inspectButton = gameObject.AddComponent<Button>();
            inspectButton.targetGraphic = target;
            inspectButton.onClick.RemoveListener(ShowInspector);
            inspectButton.onClick.AddListener(ShowInspector);
        }

        if (inspectHint == null)
        {
            inspectHint = CreateLabel("InspectHint", "点击查看", 12f);
            RectTransform rect = inspectHint.rectTransform;
            rect.anchorMin = new Vector2(0.06f, 0f);
            rect.anchorMax = new Vector2(0.6f, 0.2f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            inspectHint.alignment = TextAlignmentOptions.MidlineLeft;
            inspectHint.color = new Color(0.58f, 0.76f, 0.94f);
        }
    }

    private void ShowInspector()
    {
        if (pileSnapshot.Count == 0)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        GameObject overlay = new GameObject(
            "CardPileInspectorOverlay",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        Image dimmer = overlay.GetComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.72f);
        Button dismiss = overlay.GetComponent<Button>();
        dismiss.targetGraphic = dimmer;
        dismiss.onClick.AddListener(() => Destroy(overlay));

        GameObject panel = new GameObject(
            "CardPileInspectorPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(Outline));
        panel.transform.SetParent(overlay.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(940f, 650f);
        panel.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.09f, 0.99f);
        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color(0.22f, 0.68f, 0.95f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text title = CreateInspectorText(panel.transform, "Title",
            $"{pileTitle} · {DisplayedPileCount} 张", 28f, FontStyles.Bold);
        SetInspectorRect(title.rectTransform, new Vector2(-45f, 288f), new Vector2(760f, 44f));
        title.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_Text note = CreateInspectorText(panel.transform, "OrderNote",
            hidePileOrder
                ? "为避免泄露抽牌顺序，这里按种类汇总显示牌堆组成。"
                : "按弃牌时间从新到旧显示。",
            15f, FontStyles.Normal);
        SetInspectorRect(note.rectTransform, new Vector2(-45f, 252f), new Vector2(760f, 30f));
        note.alignment = TextAlignmentOptions.MidlineLeft;
        note.color = new Color(0.66f, 0.76f, 0.86f);

        Button close = CreateInspectorButton(panel.transform, "关闭", new Vector2(400f, 288f));
        close.onClick.AddListener(() => Destroy(overlay));

        GameObject viewport = new GameObject(
            "Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(panel.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        SetInspectorRect(viewportRect, new Vector2(0f, -25f), new Vector2(870f, 500f));
        viewport.GetComponent<Image>().color = new Color(0.02f, 0.035f, 0.06f, 0.75f);

        GameObject content = new GameObject(
            "Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = 10f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 28f;

        List<CardPileEntry> entries = CardPileInspectorRules.BuildEntries(
            pileSnapshot, newestCardIsAtEnd, hidePileOrder, trickDatabase);
        for (int i = 0; i < entries.Count; i++)
            CreateInspectorEntry(content.transform, entries[i]);

        overlay.transform.SetAsLastSibling();
    }

    private void CreateInspectorEntry(Transform parent, CardPileEntry entry)
    {
        GameObject row = new GameObject(
            "CardEntry", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = new Color(0.075f, 0.105f, 0.155f, 0.96f);
        LayoutElement element = row.GetComponent<LayoutElement>();
        element.preferredHeight = 104f;

        GameObject miniCard = new GameObject("MiniCard", typeof(RectTransform), typeof(Image), typeof(Outline));
        miniCard.transform.SetParent(row.transform, false);
        RectTransform cardRect = miniCard.GetComponent<RectTransform>();
        SetInspectorRect(cardRect, new Vector2(-382f, 0f), new Vector2(64f, 84f));
        Image cardImage = miniCard.GetComponent<Image>();
        cardImage.color = entry.Card.IsHeat
            ? new Color(0.58f, 0.29f, 0.2f)
            : entry.Card.IsTrick
                ? new Color(0.88f, 0.64f, 0.16f)
                : new Color(0.18f, 0.48f, 0.76f);
        miniCard.GetComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.32f);

        TMP_Text glyph = CreateInspectorText(miniCard.transform, "Glyph",
            CardPileInspectorRules.GetCardGlyph(entry.Card, trickDatabase), 24f, FontStyles.Bold);
        SetInspectorRect(glyph.rectTransform, Vector2.zero, new Vector2(58f, 76f));
        glyph.alignment = TextAlignmentOptions.Center;

        TMP_Text heading = CreateInspectorText(row.transform, "Heading",
            entry.Count > 1 ? $"{entry.Title}  ×{entry.Count}" : entry.Title,
            20f, FontStyles.Bold);
        SetInspectorRect(heading.rectTransform, new Vector2(35f, 23f), new Vector2(720f, 32f));
        heading.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_Text effect = CreateInspectorText(row.transform, "Effect", entry.Effect, 15f, FontStyles.Normal);
        SetInspectorRect(effect.rectTransform, new Vector2(35f, -20f), new Vector2(720f, 48f));
        effect.alignment = TextAlignmentOptions.TopLeft;
        effect.color = new Color(0.8f, 0.87f, 0.94f);
    }

    private TMP_Text CreateInspectorText(
        Transform parent, string name, string value, float size, FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateInspectorButton(Transform parent, string label, Vector2 position)
    {
        GameObject buttonObject = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetInspectorRect(buttonObject.GetComponent<RectTransform>(), position, new Vector2(110f, 42f));
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.48f, 0.2f, 0.22f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateInspectorText(buttonObject.transform, "Label", label, 17f, FontStyles.Bold);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static void SetInspectorRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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

public sealed class CardPileEntry
{
    public CardData Card { get; }
    public int Count { get; }
    public string Title { get; }
    public string Effect { get; }

    public CardPileEntry(CardData card, int count, string title, string effect)
    {
        Card = card;
        Count = count;
        Title = title;
        Effect = effect;
    }
}

/// <summary>Pure pile ordering and card-description rules shared by the inspector and tests.</summary>
public static class CardPileInspectorRules
{
    public static List<CardPileEntry> BuildEntries(
        IReadOnlyList<CardData> pile,
        bool newestCardIsAtEnd,
        bool concealOrder,
        TrickCardDatabase database)
    {
        var result = new List<CardPileEntry>();
        if (pile == null)
            return result;

        if (concealOrder)
        {
            IEnumerable<IGrouping<string, CardData>> groups = pile
                .Where(card => card != null)
                .GroupBy(GetGroupingKey)
                .OrderBy(group => GetSortKey(group.First()));
            foreach (IGrouping<string, CardData> group in groups)
            {
                CardData card = group.First();
                result.Add(new CardPileEntry(
                    card, group.Count(), GetCardTitle(card, database), GetCardEffect(card, database)));
            }
            return result;
        }

        if (newestCardIsAtEnd)
        {
            for (int i = pile.Count - 1; i >= 0; i--)
                AddExactEntry(result, pile[i], database);
        }
        else
        {
            for (int i = 0; i < pile.Count; i++)
                AddExactEntry(result, pile[i], database);
        }
        return result;
    }

    public static string GetCardTitle(CardData card, TrickCardDatabase database)
    {
        if (card == null) return "未知卡牌";
        if (card.IsHeat) return card.isTemp ? "限时热量牌" : "热量牌";
        if (card.IsSpeed) return $"速度牌 {card.value}";
        TrickCardDef definition = database?.Get(card.trickId);
        return definition != null ? $"{definition.name} · {(definition.IsAttack ? "进攻" : "防御")}" : $"特技牌 {card.trickId}";
    }

    public static string GetCardEffect(CardData card, TrickCardDatabase database)
    {
        if (card == null) return "没有可用说明。";
        if (card.IsHeat)
            return card.isTemp
                ? "本回合可作为临时资源使用；回合结束后销毁。"
                : "不可打出、不参与普通抽牌；只能通过冷却返回引擎。";
        if (card.IsSpeed)
            return $"打出后提供 {card.value} 点基础移动；计入挡位出牌要求与弯道限速。";
        TrickCardDef definition = database?.Get(card.trickId);
        return definition != null ? definition.description : "未找到该特技牌的效果说明。";
    }

    public static string GetCardGlyph(CardData card, TrickCardDatabase database)
    {
        if (card == null) return "?";
        if (card.IsHeat) return "热";
        if (card.IsSpeed) return card.value.ToString();
        TrickCardDef definition = database?.Get(card.trickId);
        return definition != null && !string.IsNullOrWhiteSpace(definition.icon)
            ? definition.icon : "特";
    }

    private static void AddExactEntry(
        ICollection<CardPileEntry> result,
        CardData card,
        TrickCardDatabase database)
    {
        if (card == null) return;
        result.Add(new CardPileEntry(card, 1, GetCardTitle(card, database), GetCardEffect(card, database)));
    }

    private static string GetGroupingKey(CardData card)
    {
        if (card.IsSpeed) return $"0:{card.value}";
        if (card.IsHeat) return card.isTemp ? "1:temp" : "1:heat";
        return $"2:{card.trickId}";
    }

    private static string GetSortKey(CardData card)
    {
        return GetGroupingKey(card);
    }
}
