using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the in-race HUD arrangement used by the reference layout in
/// Assets/GameUI.png. The controller only moves and decorates existing UI
/// objects; gameplay and button listeners remain owned by HUDUI/CardHandUI.
/// </summary>
public class RaceUILayoutController : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.12f, 0.94f);
    private static readonly Color PanelColorAlt = new Color(0.08f, 0.105f, 0.16f, 0.96f);
    // The map display is a camera viewport, so it must not be covered by a
    // tinted UI image. The outline still marks its boundary.
    private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0f);
    private static readonly Color BottomColor = new Color(0.085f, 0.06f, 0.105f, 0.97f);
    private static readonly Color AccentColor = new Color(1f, 0.75f, 0.38f, 1f);

    private bool applied;
    private RectTransform operationPanel;
    private RectTransform scoreboardPanel;
    private RectTransform trackFrame;
    private RectTransform deckPanel;

    public bool IsApplied => applied;
    public RectTransform OperationPanel => operationPanel;
    public RectTransform TrackFrame => trackFrame;
    public bool UsesAuthoredLayout { get; private set; }
    public static Vector2 ReturnToMenuAnchorMin => new Vector2(0.08f, 0.82f);
    public static Vector2 ReturnToMenuAnchorMax => new Vector2(0.92f, 0.89f);
    public static Vector2 ResetAnchorMin => new Vector2(0.08f, 0.73f);
    public static Vector2 ResetAnchorMax => new Vector2(0.92f, 0.80f);
    public static Vector2 GearGridAnchorMin => new Vector2(0.08f, 0.49f);
    public static Vector2 GearGridAnchorMax => new Vector2(0.92f, 0.69f);
    public static Vector2 DriverSkillAnchorMin => new Vector2(0.08f, 0.40f);
    public static Vector2 DriverSkillAnchorMax => new Vector2(0.92f, 0.46f);
    public static Vector2 PromptPanelAnchorMin => new Vector2(0.04f, 0.16f);
    public static Vector2 PromptPanelAnchorMax => new Vector2(0.96f, 0.37f);
    public static Vector2 LogScrollAnchorMin => new Vector2(0.06f, 0.165f);
    public static Vector2 LogScrollAnchorMax => new Vector2(0.94f, 0.265f);
    public static Vector2 PrimaryActionAnchorMin => new Vector2(0.08f, 0.04f);
    public static Vector2 PrimaryActionAnchorMax => new Vector2(0.92f, 0.13f);

    /// <summary>Normalized screen rectangle reserved for the main race camera.</summary>
    public Rect TrackViewport
    {
        get
        {
            if (trackFrame == null)
                return new Rect(0f, 0f, 1f, 1f);
            Vector2 min = trackFrame.anchorMin;
            Vector2 max = trackFrame.anchorMax;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }

    public void ApplyLayout(HUDUI hud, CardHandUI cardHand)
    {
        if (applied)
            return;

        RectTransform root = GetComponent<RectTransform>();
        if (root == null)
            return;

        // A baked RaceCanvas is the authoring source of truth. Bind its named
        // panels and references without touching any authored RectTransforms.
        // Older canvases still receive the same safe runtime fallback below.
        if (TryBindAuthoredLayout(root))
        {
            BindAuthoredReferences(hud, cardHand);
            ArrangeOperationControls(hud, cardHand);
            ApplyAuthoredButtonStyles(hud, cardHand);
            UsesAuthoredLayout = true;
            applied = true;
            return;
        }

        BuildDefaultLayout(root, hud, cardHand);
    }

    /// <summary>
    /// Rebuilds the reference layout on an editor-authored canvas. This is
    /// intentionally explicit because it resets panel/control anchors to the
    /// project defaults; normal runtime startup never calls it for a baked UI.
    /// </summary>
    public void RebuildDefaultLayout(HUDUI hud, CardHandUI cardHand)
    {
        RectTransform root = GetComponent<RectTransform>();
        if (root == null)
            return;

        applied = false;
        UsesAuthoredLayout = false;
        BuildDefaultLayout(root, hud, cardHand);
    }

    private void BuildDefaultLayout(RectTransform root, HUDUI hud, CardHandUI cardHand)
    {

        TMP_FontAsset font = FindFont(root);

        RectTransform backdrop = CreatePanel("RaceScreenBackdrop", root,
            Vector2.zero, Vector2.one, new Color(0.018f, 0.026f, 0.045f, 0f));
        operationPanel = CreatePanel("OperationPanel", root,
            new Vector2(0.015f, 0.355f), new Vector2(0.145f, 0.985f), PanelColor);
        scoreboardPanel = CreatePanel("ScoreboardPanel", root,
            new Vector2(0.155f, 0.355f), new Vector2(0.305f, 0.985f), PanelColorAlt);
        trackFrame = CreatePanel("TrackFrame", root,
            new Vector2(0.315f, 0.355f), new Vector2(0.985f, 0.985f), TrackColor);
        deckPanel = CreatePanel("DeckTablePanel", root,
            new Vector2(0.015f, 0.015f), new Vector2(0.985f, 0.335f), BottomColor);

        Image trackImage = trackFrame.GetComponent<Image>();
        if (trackImage != null)
            trackImage.color = new Color(1f, 1f, 1f, 0f);
        Outline trackOutline = trackFrame.GetComponent<Outline>();
        if (trackOutline != null)
            trackOutline.useGraphicAlpha = true;

        operationPanel.SetAsFirstSibling();
        scoreboardPanel.SetAsFirstSibling();
        trackFrame.SetAsFirstSibling();
        deckPanel.SetAsFirstSibling();

        // CreatePanel puts new objects at the front for nested layering. The
        // full-screen backdrop is the exception and must stay below every HUD
        // panel and control, including the original prefab children.
        backdrop.SetAsFirstSibling();

        BuildOperationPanel(hud, cardHand, font);
        BuildScoreboardPanel(hud, font);
        BuildDeckPanel(cardHand, font);

        if (hud != null && hud.gameOverPanel != null)
            Dock(hud.gameOverPanel.transform, trackFrame, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));

        applied = true;
    }

    private bool TryBindAuthoredLayout(Transform root)
    {
        operationPanel = FindRect(root, "OperationPanel");
        scoreboardPanel = FindRect(root, "ScoreboardPanel");
        trackFrame = FindRect(root, "TrackFrame");
        deckPanel = FindRect(root, "DeckTablePanel");

        return operationPanel != null
            && scoreboardPanel != null
            && trackFrame != null
            && deckPanel != null;
    }

    private static void BindAuthoredReferences(HUDUI hud, CardHandUI cardHand)
    {
        if (hud != null)
        {
            if (hud.weatherText == null)
                hud.weatherText = FindComponent<TMP_Text>(hud.transform, "WeatherText");
            if (hud.standingsText == null)
                hud.standingsText = FindComponent<TMP_Text>(hud.transform, "StandingsText");
        }

        if (cardHand == null)
            return;

        if (cardHand.drawPileText == null)
            cardHand.drawPileText = FindComponent<TMP_Text>(cardHand.transform.root, "DrawPileInfo");
        if (cardHand.enginePileText == null)
            cardHand.enginePileText = cardHand.deckInfoText;
        if (cardHand.discardPileText == null)
            cardHand.discardPileText = FindComponent<TMP_Text>(cardHand.transform.root, "DiscardPileInfo");
    }

    private static void ApplyAuthoredButtonStyles(HUDUI hud, CardHandUI cardHand)
    {
        if (hud != null)
        {
            ModernUIStyle.ApplyButton(hud.gear1Button, ModernUIStyle.AccentBlue);
            ModernUIStyle.ApplyButton(hud.gear2Button, ModernUIStyle.AccentCyan);
            ModernUIStyle.ApplyButton(hud.gear3Button, ModernUIStyle.AccentGold);
            ModernUIStyle.ApplyButton(hud.gear4Button, ModernUIStyle.AccentPurple);
            ModernUIStyle.ApplyButton(hud.confirmGearButton, ModernUIStyle.AccentBlue, true);
            ModernUIStyle.ApplyButton(hud.driverSkillButton, ModernUIStyle.AccentPurple);
            ModernUIStyle.ApplyButton(hud.resetButton, ModernUIStyle.AccentGold);
            ModernUIStyle.ApplyButton(hud.returnToMenuButton, ModernUIStyle.AccentRed);
            ModernUIStyle.ApplyButton(hud.backToMenuButton, ModernUIStyle.AccentRed);
        }

        if (cardHand != null)
            ModernUIStyle.ApplyButton(cardHand.playCardsButton, ModernUIStyle.AccentGreen, true);
    }

    private void BuildOperationPanel(HUDUI hud, CardHandUI cardHand, TMP_FontAsset font)
    {
        CreateText("OperationTitle", operationPanel, "其他操作菜单", font, 18, TextAlignmentOptions.Center,
            new Vector2(0.04f, 0.91f), new Vector2(0.96f, 0.985f), AccentColor);

        if (hud != null)
        {
            Transform gearButtons = FindDeep(hud.transform, "GearButtons");
            if (gearButtons != null)
            {
                Dock(gearButtons, operationPanel, GearGridAnchorMin, GearGridAnchorMax);
                LayoutGroup group = gearButtons.GetComponent<LayoutGroup>();
                if (group != null)
                    group.enabled = false;

                // The original horizontal group was wider than the narrow
                // operation column. Use a compact 2x2 gear grid instead.
                DockButton(hud.gear1Button, gearButtons, new Vector2(0.02f, 0.52f), new Vector2(0.48f, 0.98f));
                DockButton(hud.gear2Button, gearButtons, new Vector2(0.52f, 0.52f), new Vector2(0.98f, 0.98f));
                DockButton(hud.gear3Button, gearButtons, new Vector2(0.02f, 0.02f), new Vector2(0.48f, 0.48f));
                DockButton(hud.gear4Button, gearButtons, new Vector2(0.52f, 0.02f), new Vector2(0.98f, 0.48f));
            }

            DockButton(hud.confirmGearButton, operationPanel, PrimaryActionAnchorMin, PrimaryActionAnchorMax);
            DockButton(hud.driverSkillButton, operationPanel, DriverSkillAnchorMin, DriverSkillAnchorMax);
            DockButton(hud.resetButton, operationPanel, ResetAnchorMin, ResetAnchorMax);
            DockButton(hud.returnToMenuButton, operationPanel, ReturnToMenuAnchorMin, ReturnToMenuAnchorMax);
            DockText(hud.statusText, operationPanel, 0.27f, 0.36f, 12);
            DockText(hud.logText, operationPanel, 0.17f, 0.26f, 10);
        }

        if (cardHand != null)
        {
            DockButton(cardHand.playCardsButton, operationPanel, PrimaryActionAnchorMin, PrimaryActionAnchorMax);
            if (cardHand.gearSelectionPanel != null)
                Dock(cardHand.gearSelectionPanel.transform, operationPanel, new Vector2(0.06f, 0.58f), new Vector2(0.94f, 0.65f));
        }

        CreatePanel("OperationPromptPanel", operationPanel,
            PromptPanelAnchorMin, PromptPanelAnchorMax, new Color(0.02f, 0.03f, 0.055f, 0.35f));
        EnsureLogScrollView(hud);
    }

    private void ArrangeOperationControls(HUDUI hud, CardHandUI cardHand)
    {
        if (operationPanel == null)
            return;

        Transform gearButtons = hud != null ? FindDeep(hud.transform, "GearButtons") : null;
        if (gearButtons != null)
        {
            Dock(gearButtons, operationPanel, GearGridAnchorMin, GearGridAnchorMax);
            LayoutGroup group = gearButtons.GetComponent<LayoutGroup>();
            if (group != null)
                group.enabled = false;
            DockButton(hud.gear1Button, gearButtons, new Vector2(0.02f, 0.52f), new Vector2(0.48f, 0.98f));
            DockButton(hud.gear2Button, gearButtons, new Vector2(0.52f, 0.52f), new Vector2(0.98f, 0.98f));
            DockButton(hud.gear3Button, gearButtons, new Vector2(0.02f, 0.02f), new Vector2(0.48f, 0.48f));
            DockButton(hud.gear4Button, gearButtons, new Vector2(0.52f, 0.02f), new Vector2(0.98f, 0.48f));
        }
        if (hud != null)
        {
            DockButton(hud.confirmGearButton, operationPanel, PrimaryActionAnchorMin, PrimaryActionAnchorMax);
            DockButton(hud.driverSkillButton, operationPanel, DriverSkillAnchorMin, DriverSkillAnchorMax);
            DockButton(hud.resetButton, operationPanel, ResetAnchorMin, ResetAnchorMax);
            DockButton(hud.returnToMenuButton, operationPanel, ReturnToMenuAnchorMin, ReturnToMenuAnchorMax);
            DockText(hud.statusText, operationPanel, 0.27f, 0.36f, 12);
            DockText(hud.logText, operationPanel, 0.17f, 0.26f, 10);
        }
        if (cardHand != null)
            DockButton(cardHand.playCardsButton, operationPanel, PrimaryActionAnchorMin, PrimaryActionAnchorMax);

        RectTransform promptPanel = FindRect(operationPanel, "OperationPromptPanel");
        if (promptPanel != null)
        {
            Dock(promptPanel, operationPanel, PromptPanelAnchorMin, PromptPanelAnchorMax);
            Image promptImage = promptPanel.GetComponent<Image>();
            if (promptImage != null)
                promptImage.raycastTarget = false;
            promptPanel.SetAsFirstSibling();
        }
        EnsureLogScrollView(hud);
    }

    private void EnsureLogScrollView(HUDUI hud)
    {
        if (operationPanel == null || hud == null || hud.logText == null)
            return;

        Transform existing = FindDeep(operationPanel, "OperationLogScroll");
        GameObject scrollObject = existing != null
            ? existing.gameObject
            : new GameObject("OperationLogScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        Dock(scrollObject.transform, operationPanel, LogScrollAnchorMin, LogScrollAnchorMax);

        Image background = scrollObject.GetComponent<Image>();
        background.color = new Color(0.01f, 0.02f, 0.04f, 0.52f);
        background.raycastTarget = true;

        Transform viewportTransform = FindDeep(scrollObject.transform, "Viewport");
        GameObject viewportObject = viewportTransform != null
            ? viewportTransform.gameObject
            : new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        Dock(viewportObject.transform, scrollObject.transform, Vector2.zero, Vector2.one);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;
        if (viewportObject.GetComponent<RectMask2D>() == null)
            viewportObject.AddComponent<RectMask2D>();

        RectTransform logRect = hud.logText.rectTransform;
        logRect.SetParent(viewportObject.transform, false);
        logRect.anchorMin = new Vector2(0f, 1f);
        logRect.anchorMax = new Vector2(1f, 1f);
        logRect.pivot = new Vector2(0.5f, 1f);
        logRect.anchoredPosition = Vector2.zero;
        logRect.sizeDelta = Vector2.zero;
        logRect.localScale = Vector3.one;
        hud.logText.fontSize = 10f;
        hud.logText.alignment = TextAlignmentOptions.TopLeft;
        hud.logText.enableWordWrapping = true;
        hud.logText.overflowMode = TextOverflowModes.Overflow;
        hud.logText.raycastTarget = false;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewportObject.GetComponent<RectTransform>();
        scroll.content = logRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.scrollSensitivity = 14f;
        hud.AttachLogScrollRect(scroll);
    }

    private void BuildScoreboardPanel(HUDUI hud, TMP_FontAsset font)
    {
        CreateText("ScoreboardTitle", scoreboardPanel, "排名和分站", font, 18, TextAlignmentOptions.Center,
            new Vector2(0.04f, 0.91f), new Vector2(0.96f, 0.985f), AccentColor);

        if (hud == null)
            return;

        DockText(hud.gearText, scoreboardPanel, 0.80f, 0.89f, 16);
        DockText(hud.heatText, scoreboardPanel, 0.70f, 0.79f, 15);
        DockText(hud.lapText, scoreboardPanel, 0.60f, 0.69f, 15);
        DockText(hud.positionText, scoreboardPanel, 0.50f, 0.59f, 14);
        DockText(hud.aiStatusText, scoreboardPanel, 0.40f, 0.49f, 13);

        if (hud.weatherText == null)
            hud.weatherText = CreateText("WeatherText", scoreboardPanel, "天气", font, 13,
                TextAlignmentOptions.Left, new Vector2(0.08f, 0.31f), new Vector2(0.92f, 0.39f), Color.white);
        else
            DockText(hud.weatherText, scoreboardPanel, 0.31f, 0.39f, 13);

        if (hud.standingsText == null)
            hud.standingsText = CreateText("StandingsText", scoreboardPanel, "等待排名…", font, 12,
                TextAlignmentOptions.TopLeft, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.29f), Color.white);
        else
            DockText(hud.standingsText, scoreboardPanel, 0.06f, 0.29f, 12);
    }

    private void BuildDeckPanel(CardHandUI cardHand, TMP_FontAsset font)
    {
        RectTransform drawPanel = CreatePanel("DrawPilePanel", deckPanel,
            new Vector2(0.015f, 0.08f), new Vector2(0.155f, 0.92f), PanelColorAlt);
        RectTransform enginePanel = CreatePanel("EnginePanel", deckPanel,
            new Vector2(0.165f, 0.08f), new Vector2(0.315f, 0.92f), PanelColorAlt);
        RectTransform handPanel = CreatePanel("HandPanel", deckPanel,
            new Vector2(0.33f, 0.08f), new Vector2(0.825f, 0.92f), PanelColorAlt);
        RectTransform discardPanel = CreatePanel("DiscardPilePanel", deckPanel,
            new Vector2(0.84f, 0.08f), new Vector2(0.985f, 0.92f), PanelColorAlt);

        CreateText("DeckTableTitle", deckPanel, "牌桌", font, 17, TextAlignmentOptions.Left,
            new Vector2(0.015f, 0.925f), new Vector2(0.20f, 0.995f), AccentColor);
        CreateText("DrawPileTitle", drawPanel, "抽牌堆", font, 15, TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.67f), new Vector2(0.95f, 0.94f), Color.white);
        CreateText("EnginePileTitle", enginePanel, "引擎库", font, 15, TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.67f), new Vector2(0.95f, 0.94f), Color.white);
        CreateText("HandTitle", handPanel, "手牌区", font, 15, TextAlignmentOptions.Center,
            new Vector2(0.03f, 0.90f), new Vector2(0.97f, 0.98f), AccentColor);
        CreateText("DiscardPileTitle", discardPanel, "弃牌堆", font, 15, TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.67f), new Vector2(0.95f, 0.94f), Color.white);

        RectMask2D handMask = handPanel.GetComponent<RectMask2D>();
        if (handMask == null)
            handMask = handPanel.gameObject.AddComponent<RectMask2D>();

        if (cardHand == null)
            return;

        // The reference layout reserves a finite bottom table width. Seven
        // cards at the old 170px size could overlap the discard pile, so use a
        // compact card size that still leaves room for card art and selection.
        cardHand.cardSizeOverride = new Vector2(112f, 180f);
        if (cardHand.handContainer != null)
            Dock(cardHand.handContainer, handPanel, new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.88f));
        if (cardHand.deckInfoText != null)
        {
            DockText(cardHand.deckInfoText, enginePanel, 0.16f, 0.62f, 12);
            cardHand.enginePileText = cardHand.deckInfoText;
        }

        cardHand.drawPileText = CreateText("DrawPileInfo", drawPanel, "抽牌堆\n-- 张", font, 17,
            TextAlignmentOptions.Center, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.64f), Color.white);
        cardHand.discardPileText = CreateText("DiscardPileInfo", discardPanel, "弃牌堆\n-- 张", font, 17,
            TextAlignmentOptions.Center, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.64f), Color.white);
    }

    private static RectTransform CreatePanel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        Transform existing = FindDeep(parent, name);
        GameObject panelObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Image image = panelObject.GetComponent<Image>();
        if (image == null)
            image = panelObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        Outline outline = panelObject.GetComponent<Outline>();
        if (outline == null)
            outline = panelObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.74f, 0.38f, 0.22f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
        panelObject.transform.SetAsFirstSibling();
        return rect;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, TMP_FontAsset font,
        float fontSize, TextAlignmentOptions alignment, Vector2 min, Vector2 max, Color color)
    {
        Transform existing = FindDeep(parent, name);
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        TMP_Text tmp = textObject.GetComponent<TMP_Text>();
        if (tmp == null)
            tmp = textObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void DockText(TMP_Text text, Transform parent, float minY, float maxY, float fontSize)
    {
        if (text == null)
            return;
        Dock(text.transform, parent, new Vector2(0.08f, minY), new Vector2(0.92f, maxY));
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
    }

    private static void DockButton(Button button, Transform parent, Vector2 min, Vector2 max)
    {
        if (button != null)
            Dock(button.transform, parent, min, max);
    }

    private static void Dock(Transform child, Transform parent, Vector2 min, Vector2 max)
    {
        if (child == null || parent == null)
            return;
        child.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        if (rect == null)
            return;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        child.SetAsLastSibling();
    }

    private static TMP_FontAsset FindFont(Transform root)
    {
        TMP_Text text = root.GetComponentInChildren<TMP_Text>(true);
        return text != null ? text.font : null;
    }

    private static Transform FindDeep(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (root.name == objectName)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }
        return null;
    }

    private static RectTransform FindRect(Transform root, string objectName)
    {
        Transform found = FindDeep(root, objectName);
        return found != null ? found.GetComponent<RectTransform>() : null;
    }

    private static T FindComponent<T>(Transform root, string objectName) where T : Component
    {
        Transform found = FindDeep(root, objectName);
        return found != null ? found.GetComponent<T>() : null;
    }
}
