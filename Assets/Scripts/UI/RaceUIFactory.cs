using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds the procedural race HUD used when a RaceCanvas prefab is unavailable.
/// The factory owns UI construction only; race state and callbacks remain in
/// <see cref="MVPGameManager"/>.
/// </summary>
public sealed class RaceUIFactory
{
    private readonly TMP_FontAsset fontAsset;

    public RaceUIFactory(TMP_FontAsset fontAsset)
    {
        this.fontAsset = fontAsset;
    }

    /// <summary>
    /// Creates the legacy fallback HUD and hand view when either reference is
    /// missing. Existing scene components are reused so authored layouts win.
    /// </summary>
    public void Build(
        Canvas canvas,
        ref HUDUI hudUI,
        ref CardHandUI cardHandUI,
        int totalNodes,
        GameObject cardUIPrefab,
        UnityAction<int> onGearSelected,
        UnityAction onConfirmGear,
        UnityAction onReset)
    {
        if (canvas == null)
            return;

        HideLegacyElements();

        Button autoPlayButton = null;
        if (hudUI == null)
            hudUI = BuildHud(canvas.transform, totalNodes, onGearSelected, onConfirmGear, onReset, out autoPlayButton);

        if (cardHandUI == null)
            cardHandUI = BuildCardHand(canvas.transform, cardUIPrefab, out autoPlayButton);

        if (autoPlayButton == null)
            autoPlayButton = FindButton("PlayBtn");

        if (cardHandUI != null && cardHandUI.playCardsButton == null)
            cardHandUI.SetPlayCardsButton(autoPlayButton);
    }

    private HUDUI BuildHud(
        Transform canvas,
        int totalNodes,
        UnityAction<int> onGearSelected,
        UnityAction onConfirmGear,
        UnityAction onReset,
        out Button playButton)
    {
        GameObject hudObject = new GameObject("HUD", typeof(RectTransform));
        hudObject.transform.SetParent(canvas, false);
        HUDUI hud = hudObject.AddComponent<HUDUI>();

        hud.statusText = CreateText(hudObject.transform, "StatusText", "选择档位 (1-4)", 22,
            new Vector2(-300, 180), new Vector2(420, 30));
        hud.gearText = CreateText(hudObject.transform, "GearText", "档位: 1", 18,
            new Vector2(-400, 150), new Vector2(150, 25));
        hud.heatText = CreateText(hudObject.transform, "HeatText", "引擎热量: 12", 18,
            new Vector2(-400, 125), new Vector2(280, 25));
        hud.lapText = CreateText(hudObject.transform, "LapText", "圈数: 0/3", 18,
            new Vector2(-400, 100), new Vector2(200, 25));
        hud.positionText = CreateText(hudObject.transform, "PositionText", $"位置: 0/{totalNodes}", 18,
            new Vector2(-400, 75), new Vector2(250, 25));
        hud.aiStatusText = CreateText(hudObject.transform, "AIStatusText", "AI: 就绪", 16,
            new Vector2(250, 50), new Vector2(200, 25));
        hud.weatherText = CreateText(hudObject.transform, "WeatherText", "晴天", 16,
            new Vector2(250, 180), new Vector2(200, 25));
        hud.standingsText = CreateText(hudObject.transform, "StandingsText", "", 14,
            new Vector2(250, 75), new Vector2(320, 100));
        hud.logText = CreateText(hudObject.transform, "LogText", "", 13,
            new Vector2(0, -160), new Vector2(750, 180));

        CreateGearButton(canvas, "Gear1Btn", "G1", new Vector2(-380, 220), 1, onGearSelected);
        CreateGearButton(canvas, "Gear2Btn", "G2", new Vector2(-290, 220), 2, onGearSelected);
        CreateGearButton(canvas, "Gear3Btn", "G3", new Vector2(-200, 220), 3, onGearSelected);
        CreateGearButton(canvas, "Gear4Btn", "G4", new Vector2(-110, 220), 4, onGearSelected);
        CreateActionButton(canvas, "ConfirmGearBtn", "确认", new Vector2(10, 220),
            new Color(0.4f, 0.7f, 1f), onConfirmGear);

        playButton = CreateActionButton(canvas, "PlayBtn", "出牌",
            new Vector2(-300, -185), Color.green, null);
        CreateActionButton(canvas, "ResetBtn", "重新开始", new Vector2(-150, -185), Color.yellow, onReset);

        GameObject gameOverObject = new GameObject("GameOverPanel", typeof(RectTransform));
        gameOverObject.transform.SetParent(canvas, false);
        hud.gameOverPanel = gameOverObject;
        hud.gameOverText = CreateText(gameOverObject.transform, "GameOverText", "", 28,
            Vector2.zero, new Vector2(500, 300));
        gameOverObject.SetActive(false);
        return hud;
    }

    private CardHandUI BuildCardHand(Transform canvas, GameObject cardUIPrefab, out Button playButton)
    {
        Transform handContainer = canvas.Find("HandContainer");
        if (handContainer == null)
        {
            GameObject handObject = new GameObject("HandContainer", typeof(RectTransform));
            handObject.transform.SetParent(canvas, false);
            handContainer = handObject.transform;
        }

        GameObject cardHandObject = new GameObject("CardHand", typeof(RectTransform));
        cardHandObject.transform.SetParent(canvas, false);
        CardHandUI hand = cardHandObject.AddComponent<CardHandUI>();
        hand.handContainer = handContainer;
        hand.deckInfoText = CreateText(cardHandObject.transform, "DeckInfo",
            "牌堆: 12速 + 3热", 14, new Vector2(-300, -100), new Vector2(200, 25));

        hand.cardPrefab = cardUIPrefab != null ? cardUIPrefab : LoadDefaultCardPrefab();
        if (hand.cardPrefab == null)
            Debug.LogWarning("Could not auto-load CardPrefab. Set it manually on CardHand.");

        playButton = FindButton("PlayBtn");
        return hand;
    }

    public TMP_Text CreateText(Transform parent, string name, string text, int fontSize,
        Vector2 anchoredPosition, Vector2 size)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Left;
        if (fontAsset != null)
            label.font = fontAsset;
        return label;
    }

    private void CreateGearButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        int gear,
        UnityAction<int> onGearSelected)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(80, 40);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(1, 1, 1, 0.8f);
        Button button = buttonObject.AddComponent<Button>();
        ButtonClickAnimation.Attach(button);
        int capturedGear = gear;
        if (onGearSelected != null)
            button.onClick.AddListener(() => onGearSelected(capturedGear));
        CreateCenteredLabel(buttonObject.transform, label, 18, Color.black);
    }

    public Button CreateActionButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        Color color,
        UnityAction callback)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(120, 40);

        Image image = buttonObject.AddComponent<Image>();
        image.color = color;
        Button button = buttonObject.AddComponent<Button>();
        ButtonClickAnimation.Attach(button);
        if (callback != null)
            button.onClick.AddListener(callback);
        TMP_Text actionLabel = CreateCenteredLabel(buttonObject.transform, label, 20, Color.black);
        actionLabel.fontStyle = FontStyles.Bold;
        return button;
    }

    private TMP_Text CreateCenteredLabel(Transform parent, string text, int fontSize, Color color)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        if (fontAsset != null)
            label.font = fontAsset;
        else if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        return label;
    }

    private static Button FindButton(string name)
    {
        GameObject buttonObject = GameObject.Find(name);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    private static void HideLegacyElements()
    {
        string[] names = { "StatusTextTMP", "ReadyStepsText", "NextRound", "Reset" };
        for (int i = 0; i < names.Length; i++)
        {
            GameObject legacy = GameObject.Find(names[i]);
            if (legacy != null)
                legacy.SetActive(false);
        }
    }

    private static GameObject LoadDefaultCardPrefab()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CardPrefab.prefab");
#else
        return null;
#endif
    }
}
