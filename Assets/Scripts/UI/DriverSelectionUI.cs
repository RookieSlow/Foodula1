using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime-built driver selection overlay for the main menu.</summary>
public sealed class DriverSelectionUI : MonoBehaviour
{
    private static readonly Color OverlayColor = new Color(0.025f, 0.035f, 0.055f, 0.97f);
    private static readonly Color PanelColor = new Color(0.065f, 0.085f, 0.12f, 1f);
    private static readonly Color[] TeamColors =
    {
        new Color(0.27f, 0.52f, 0.82f), new Color(0.35f, 0.35f, 0.38f),
        new Color(0.75f, 0.22f, 0.18f), new Color(0.70f, 0.35f, 0.12f),
        new Color(0.20f, 0.55f, 0.45f), new Color(0.63f, 0.30f, 0.68f)
    };

    private Action<string> onDriverSelected;
    private readonly Dictionary<string, Image> cardImages = new Dictionary<string, Image>();
    private GameObject overlay;
    private TMP_Text selectedText;
    private TMP_FontAsset font;
    private bool initialized;

    public void Initialize(Action<string> selectionCallback)
    {
        onDriverSelected = selectionCallback;
        if (initialized) return;
        font = GetComponentInChildren<TMP_Text>(true)?.font;
        BuildOverlay();
        initialized = true;
    }

    public void Show()
    {
        if (!initialized) Initialize(null);
        RefreshVisuals();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (overlay != null) overlay.SetActive(false);
    }

    private void BuildOverlay()
    {
        overlay = CreateObject("DriverSelectionOverlay", transform);
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.AddComponent<Image>().color = OverlayColor;

        GameObject panel = CreateObject("DriverSelectionPanel", overlay.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1420f, 930f);
        panel.AddComponent<Image>().color = PanelColor;

        CreateText(panel.transform, "Title", "选择车手", 46f, new Vector2(0f, 418f), new Vector2(900f, 66f), FontStyles.Bold);
        CreateText(panel.transform, "Hint", "每位车手拥有独立风格、天赋和一组被动/主动技能", 20f,
            new Vector2(0f, 375f), new Vector2(1000f, 36f), FontStyles.Normal, new Color(0.62f, 0.72f, 0.84f));

        GameObject grid = CreateObject("DriverGrid", panel.transform);
        RectTransform gridRect = grid.GetComponent<RectTransform>();
        gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = new Vector2(0f, 5f);
        gridRect.sizeDelta = new Vector2(1320f, 700f);
        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(305f, 205f);
        layout.spacing = new Vector2(20f, 18f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 4;
        layout.childAlignment = TextAnchor.MiddleCenter;

        IReadOnlyList<DriverProfile> drivers = DriverSelectionState.AvailableDrivers;
        for (int i = 0; i < drivers.Count; i++) CreateCard(grid.transform, drivers[i], i);

        selectedText = CreateText(panel.transform, "SelectedDriver", string.Empty, 19f,
            new Vector2(0f, -420f), new Vector2(950f, 32f), FontStyles.Normal, new Color(0.45f, 0.82f, 1f));
        Button backButton = CreateButton(panel.transform, "BackButton", "返回",
            new Vector2(-570f, -420f), new Vector2(180f, 52f), new Color(0.24f, 0.28f, 0.34f), 22f);
        backButton.onClick.AddListener(Hide);
        overlay.SetActive(false);
    }

    private void CreateCard(Transform parent, DriverProfile driver, int index)
    {
        GameObject card = CreateObject("Driver_" + driver.Id, parent);
        Image background = card.AddComponent<Image>();
        background.color = TeamColors[(int)driver.Team % TeamColors.Length];
        cardImages[driver.Id] = background;
        Button button = card.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(background.color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(background.color, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        string id = driver.Id;
        button.onClick.AddListener(() => SelectDriver(id));

        AddTeamLogo(card.transform, driver.Team, new Vector2(-108f, 70f), new Vector2(48f, 48f));

        CreateText(card.transform, "Name", driver.DisplayName, 22f, new Vector2(22f, 72f), new Vector2(225f, 34f), FontStyles.Bold);
        CreateText(card.transform, "Meta", $"{driver.Team} · {driver.Style} · XP {driver.TalentMultiplier:0.0}x", 16f,
            new Vector2(0f, 42f), new Vector2(285f, 26f), FontStyles.Normal, new Color(0.92f, 0.95f, 1f));
        CreateText(card.transform, "Passive", $"被动：{driver.PassiveName}\n{driver.PassiveSummary}", 14f,
            new Vector2(0f, -4f), new Vector2(282f, 58f), FontStyles.Normal, new Color(0.98f, 0.82f, 0.42f));
        CreateText(card.transform, "Active", $"主动：{driver.ActiveName}\n{driver.ActiveSummary}", 14f,
            new Vector2(0f, -62f), new Vector2(282f, 58f), FontStyles.Normal, new Color(0.62f, 0.88f, 1f));
    }

    private static void AddTeamLogo(Transform parent, TeamId team, Vector2 position, Vector2 size)
    {
        Sprite sprite = BrandArtResources.LoadTeamLogo(team);
        if (sprite == null) return;
        GameObject logoObject = CreateObject("TeamLogo", parent);
        RectTransform rect = logoObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = logoObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void SelectDriver(string driverId)
    {
        if (!DriverSelectionState.TrySelect(driverId))
        {
            Debug.LogError("[DriverSelectionUI] Unknown driver ID: " + driverId);
            return;
        }

        RefreshVisuals();
        onDriverSelected?.Invoke(driverId);
    }

    private void RefreshVisuals()
    {
        string selectedId = DriverSelectionState.SelectedDriverId;
        foreach (KeyValuePair<string, Image> entry in cardImages)
        {
            if (!DriverCatalog.TryGet(entry.Key, out DriverProfile driver)) continue;
            Color baseColor = TeamColors[(int)driver.Team % TeamColors.Length];
            entry.Value.color = entry.Key == selectedId ? Color.Lerp(baseColor, Color.white, 0.22f) : baseColor;
        }

        if (selectedText != null)
        {
            DriverProfile selected = DriverSelectionState.ResolveDriver(TeamId.CN);
            selectedText.text = $"当前车手：{selected.DisplayName}（{selected.Team}，{selected.Style}）";
        }
    }

    private TMP_Text CreateText(Transform parent, string objectName, string content, float size,
        Vector2 position, Vector2 dimensions, FontStyles style, Color? color = null)
    {
        GameObject textObject = CreateObject(objectName, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color ?? Color.white;
        text.raycastTarget = false;
        if (font != null) text.font = font;
        return text;
    }

    private Button CreateButton(Transform parent, string objectName, string label, Vector2 position,
        Vector2 dimensions, Color color, float size)
    {
        GameObject buttonObject = CreateObject(objectName, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        Image image = buttonObject.AddComponent<Image>();
        image.color = color;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ButtonClickAnimation.Attach(button);
        TMP_Text text = CreateText(buttonObject.transform, "Label", label, size, Vector2.zero, dimensions, FontStyles.Bold);
        Stretch(text.rectTransform);
        return button;
    }

    private static GameObject CreateObject(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
