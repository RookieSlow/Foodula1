using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime-built encyclopedia reader opened from the settings overlay.</summary>
public sealed class GameEncyclopediaUI : MonoBehaviour
{
    private GameObject overlay;
    private EncyclopediaCatalogData catalog;
    private int entryIndex;
    private TMP_Text categoryValue;
    private TMP_Text titleValue;
    private TMP_Text summaryValue;
    private TMP_Text bodyValue;
    private TMP_Text pageValue;
    private Button previousButton;
    private Button nextButton;

    public void Initialize()
    {
        if (overlay == null) Build();
        Hide();
    }

    public void Show()
    {
        if (overlay == null) Build();
        catalog = EncyclopediaCatalog.LoadDefault();
        List<string> errors = EncyclopediaCatalog.Validate(catalog);
        entryIndex = 0;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();

        if (errors.Count > 0)
        {
            ShowLoadFailure(errors);
            return;
        }

        RefreshEntry();
    }

    public void Hide()
    {
        if (overlay != null) overlay.SetActive(false);
    }

    private void Build()
    {
        TMP_FontAsset font = GetComponentInChildren<TMP_Text>(true)?.font;
        var factory = new RaceUIFactory(font);

        overlay = new GameObject("EncyclopediaOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(transform, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0.01f, 0.02f, 0.035f, 0.985f);

        GameObject panel = new GameObject("EncyclopediaPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(overlay.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1120f, 840f);
        panel.GetComponent<Image>().color = new Color(0.04f, 0.07f, 0.115f, 1f);

        TMP_Text heading = factory.CreateText(panel.transform, "EncyclopediaHeading", "游戏百科", 34,
            new Vector2(0f, 365f), new Vector2(900f, 48f));
        heading.alignment = TextAlignmentOptions.Center;
        heading.fontStyle = FontStyles.Bold;
        heading.color = new Color(0.35f, 0.82f, 1f);

        categoryValue = factory.CreateText(panel.transform, "EncyclopediaCategory", "", 16,
            new Vector2(0f, 320f), new Vector2(900f, 30f));
        categoryValue.alignment = TextAlignmentOptions.Center;
        categoryValue.color = new Color(0.55f, 0.8f, 0.95f);

        titleValue = factory.CreateText(panel.transform, "EncyclopediaTitle", "", 28,
            new Vector2(0f, 276f), new Vector2(940f, 44f));
        titleValue.alignment = TextAlignmentOptions.Center;
        titleValue.fontStyle = FontStyles.Bold;

        summaryValue = factory.CreateText(panel.transform, "EncyclopediaSummary", "", 18,
            new Vector2(0f, 226f), new Vector2(940f, 54f));
        summaryValue.alignment = TextAlignmentOptions.Center;
        summaryValue.color = new Color(0.78f, 0.86f, 0.92f);
        summaryValue.enableWordWrapping = true;

        BuildScrollBody(factory, panel.transform);

        previousButton = factory.CreateActionButton(panel.transform, "EncyclopediaPrevious", "上一条",
            new Vector2(-330f, -346f), new Color(0.2f, 0.43f, 0.62f), PreviousEntry);
        previousButton.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 50f);

        pageValue = factory.CreateText(panel.transform, "EncyclopediaPage", "", 17,
            new Vector2(0f, -346f), new Vector2(240f, 44f));
        pageValue.alignment = TextAlignmentOptions.Center;

        nextButton = factory.CreateActionButton(panel.transform, "EncyclopediaNext", "下一条",
            new Vector2(330f, -346f), new Color(0.2f, 0.43f, 0.62f), NextEntry);
        nextButton.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 50f);

        Button close = factory.CreateActionButton(panel.transform, "EncyclopediaClose", "返回设置",
            new Vector2(0f, -392f), new Color(0.5f, 0.25f, 0.25f), Hide);
        close.GetComponent<RectTransform>().sizeDelta = new Vector2(210f, 42f);
    }

    private void BuildScrollBody(RaceUIFactory factory, Transform parent)
    {
        GameObject scrollObject = new GameObject(
            "EncyclopediaBodyScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObject.transform.SetParent(parent, false);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = scrollRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRectTransform.anchoredPosition = new Vector2(0f, -36f);
        scrollRectTransform.sizeDelta = new Vector2(960f, 440f);
        scrollObject.GetComponent<Image>().color = new Color(0.02f, 0.038f, 0.065f, 0.95f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(26f, 18f);
        viewportRect.offsetMax = new Vector2(-26f, -18f);

        bodyValue = factory.CreateText(viewport.transform, "EncyclopediaBody", "", 19,
            Vector2.zero, new Vector2(880f, 400f));
        RectTransform bodyRect = bodyValue.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = Vector2.zero;
        bodyRect.sizeDelta = new Vector2(0f, 400f);
        bodyValue.alignment = TextAlignmentOptions.TopLeft;
        bodyValue.enableWordWrapping = true;
        bodyValue.overflowMode = TextOverflowModes.Overflow;
        bodyValue.color = new Color(0.9f, 0.93f, 0.96f);
        ContentSizeFitter fitter = bodyValue.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = bodyRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
    }

    private void PreviousEntry()
    {
        if (catalog?.entries == null || catalog.entries.Length == 0) return;
        entryIndex = (entryIndex - 1 + catalog.entries.Length) % catalog.entries.Length;
        RefreshEntry();
    }

    private void NextEntry()
    {
        if (catalog?.entries == null || catalog.entries.Length == 0) return;
        entryIndex = (entryIndex + 1) % catalog.entries.Length;
        RefreshEntry();
    }

    private void RefreshEntry()
    {
        EncyclopediaEntry entry = catalog.entries[entryIndex];
        categoryValue.text = entry.category;
        titleValue.text = entry.title;
        summaryValue.text = entry.summary;
        bodyValue.text = entry.body;
        bodyValue.rectTransform.anchoredPosition = Vector2.zero;
        pageValue.text = $"{entryIndex + 1} / {catalog.entries.Length}";
        previousButton.interactable = catalog.entries.Length > 1;
        nextButton.interactable = catalog.entries.Length > 1;
    }

    private void ShowLoadFailure(List<string> errors)
    {
        categoryValue.text = "数据校验失败";
        titleValue.text = "百科暂不可用";
        summaryValue.text = "百科数据不完整，已安全停止显示。";
        bodyValue.text = string.Join("\n", errors);
        pageValue.text = "—";
        previousButton.interactable = false;
        nextButton.interactable = false;
        Debug.LogError("[Encyclopedia] " + string.Join(" | ", errors));
    }
}
