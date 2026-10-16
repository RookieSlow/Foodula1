using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and controls the main-menu track selection overlay.
/// </summary>
public sealed class TrackSelectionUI : MonoBehaviour
{
    private static readonly Color[] TrackColors =
    {
        new Color(0.55f, 0.25f, 0.42f),
        new Color(0.72f, 0.43f, 0.12f),
        new Color(0.62f, 0.18f, 0.16f),
        new Color(0.18f, 0.38f, 0.65f),
        new Color(0.68f, 0.16f, 0.16f),
        new Color(0.20f, 0.50f, 0.42f),
        new Color(0.28f, 0.30f, 0.34f),
        new Color(0.16f, 0.38f, 0.55f)
    };

    private readonly Dictionary<string, Image> trackCardImages = new Dictionary<string, Image>();
    private Action<string> onTrackSelected;
    private GameObject overlay;
    private TMP_Text selectedTrackText;
    private TMP_FontAsset font;
    private bool isInitialized;

    /// <summary>
    /// Initializes the overlay and registers the callback invoked when a track is chosen.
    /// </summary>
    public void Initialize(Action<string> selectionCallback)
    {
        onTrackSelected = selectionCallback;
        if (isInitialized)
        {
            return;
        }

        font = GetComponentInChildren<TMP_Text>(true)?.font;
        BuildOverlay();
        isInitialized = true;
    }

    /// <summary>Shows the selection overlay and refreshes its current-selection state.</summary>
    public void Show()
    {
        if (!isInitialized)
        {
            Initialize(null);
        }

        RefreshSelectionVisuals();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    /// <summary>Closes the selection overlay without starting a race.</summary>
    public void Hide()
    {
        if (overlay != null)
        {
            overlay.SetActive(false);
        }
    }

    private void BuildOverlay()
    {
        overlay = CreateUIObject("TrackSelectionOverlay", transform);
        StretchToParent(overlay.GetComponent<RectTransform>());
        Image overlayImage = overlay.AddComponent<Image>();
        ModernUIStyle.ApplyOverlay(overlayImage);

        GameObject panel = CreateUIObject("TrackSelectionPanel", overlay.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1360f, 820f);
        panel.AddComponent<Image>();
        ModernUIStyle.ApplyPanel(panel, true);

        CreateText(panel.transform, "Title", MainMenuLabels.QuickRaceTrackTitle, 48f,
            new Vector2(0f, 335f), new Vector2(900f, 70f), FontStyles.Bold);
        CreateText(panel.transform, "Hint", "选择一条赛道后立即开始自由赛事", 22f,
            new Vector2(0f, 285f), new Vector2(900f, 40f), FontStyles.Normal,
            new Color(0.62f, 0.72f, 0.84f));

        selectedTrackText = CreateText(panel.transform, "SelectedTrack", string.Empty, 20f,
            new Vector2(0f, -320f), new Vector2(900f, 36f), FontStyles.Normal,
            new Color(0.45f, 0.82f, 1f));

        GameObject grid = CreateUIObject("TrackGrid", panel.transform);
        RectTransform gridRect = grid.GetComponent<RectTransform>();
        gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = new Vector2(0f, -5f);
        gridRect.sizeDelta = new Vector2(1240f, 440f);

        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(290f, 200f);
        layout.spacing = new Vector2(26f, 24f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 4;
        layout.childAlignment = TextAnchor.MiddleCenter;

        IReadOnlyList<TrackSelectionOption> tracks = TrackSelectionState.AvailableTracks;
        for (int i = 0; i < tracks.Count; i++)
        {
            CreateTrackCard(grid.transform, tracks[i], i);
        }

        Button backButton = CreateButton(panel.transform, "BackButton", "返回",
            new Vector2(-540f, -335f), new Vector2(210f, 56f),
            new Color(0.24f, 0.28f, 0.34f), 24f);
        backButton.onClick.AddListener(Hide);

        overlay.SetActive(false);
    }

    private void CreateTrackCard(Transform parent, TrackSelectionOption option, int index)
    {
        GameObject card = CreateUIObject($"Track_{option.TrackId}", parent);
        Image background = card.AddComponent<Image>();
        background.color = TrackColors[index % TrackColors.Length];
        trackCardImages[option.TrackId] = background;

        Button button = card.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(background.color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(background.color, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TrackSelectionOption capturedOption = option;
        button.onClick.AddListener(() => SelectTrack(capturedOption.TrackId));

        CreateText(card.transform, "TrackName", option.DisplayName, 27f,
            new Vector2(0f, 26f), new Vector2(260f, 70f), FontStyles.Bold);
        CreateText(card.transform, "Subtitle", option.Subtitle, 18f,
            new Vector2(0f, -38f), new Vector2(250f, 42f), FontStyles.Normal,
            new Color(0.92f, 0.95f, 1f));
        CreateText(card.transform, "Action", "点击开始", 15f,
            new Vector2(0f, -76f), new Vector2(220f, 28f), FontStyles.Normal,
            new Color(0.78f, 0.86f, 0.96f));
    }

    private void SelectTrack(string trackId)
    {
        if (!TrackSelectionState.TrySelect(trackId))
        {
            Debug.LogError($"[TrackSelectionUI] Unknown track ID: {trackId}");
            return;
        }

        RefreshSelectionVisuals();
        onTrackSelected?.Invoke(trackId);
    }

    private void RefreshSelectionVisuals()
    {
        string resolvedTrackId = TrackSelectionState.ResolveTrackId(string.Empty);
        foreach (KeyValuePair<string, Image> entry in trackCardImages)
        {
            int optionIndex = FindTrackIndex(entry.Key);
            Color baseColor = TrackColors[Mathf.Max(0, optionIndex) % TrackColors.Length];
            entry.Value.color = entry.Key == resolvedTrackId
                ? Color.Lerp(baseColor, Color.white, 0.22f)
                : baseColor;
        }

        TrackSelectionOption selected = FindTrack(resolvedTrackId);
        if (selectedTrackText != null)
        {
            selectedTrackText.text = $"当前选择：{selected.DisplayName}　{selected.Subtitle}";
        }
    }

    private static int FindTrackIndex(string trackId)
    {
        IReadOnlyList<TrackSelectionOption> tracks = TrackSelectionState.AvailableTracks;
        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].TrackId == trackId)
            {
                return i;
            }
        }

        return 0;
    }

    private static TrackSelectionOption FindTrack(string trackId)
    {
        IReadOnlyList<TrackSelectionOption> tracks = TrackSelectionState.AvailableTracks;
        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].TrackId == trackId)
            {
                return tracks[i];
            }
        }

        return tracks[0];
    }

    private TMP_Text CreateText(
        Transform parent,
        string objectName,
        string content,
        float fontSize,
        Vector2 anchoredPosition,
        Vector2 size,
        FontStyles fontStyle,
        Color? color = null)
    {
        GameObject textObject = CreateUIObject(objectName, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color ?? Color.white;
        text.raycastTarget = false;
        if (font != null)
        {
            text.font = font;
        }

        return text;
    }

    private Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color,
        float fontSize)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ModernUIStyle.ApplyButton(button, color);
        ButtonClickAnimation.Attach(button);

        TMP_Text buttonText = CreateText(buttonObject.transform, "Label", label, fontSize,
            Vector2.zero, size, FontStyles.Bold);
        StretchToParent(buttonText.rectTransform);

        return button;
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        var gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
