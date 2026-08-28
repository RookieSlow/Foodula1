using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Non-interactive spotlight for one authored tutorial concept. Four dimming
/// panels leave a clear hole around the live target, while a border and short
/// callout explain that region without taking ownership of gameplay input.
/// </summary>
public sealed class TutorialFocusHighlightUI : MonoBehaviour
{
    private Canvas canvas;
    private MVPGameManager manager;
    [Header("手动高光布局")]
    [SerializeField, Min(0f)] private float targetPadding = 10f;
    [SerializeField, Min(1f)] private float borderThickness = 4f;
    [SerializeField] private Vector2 calloutOffset = Vector2.zero;
    [SerializeField, Min(180f)] private float calloutWidth = 360f;
    [SerializeField, Min(40f)] private float calloutHeight = 58f;
    [SerializeField, Range(0f, 0.8f)] private float outsideDimAlpha = 0.38f;

    [Header("Prefab 引用")]
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private Image[] dimmers = new Image[4];
    [SerializeField] private Image[] borders = new Image[4];
    private readonly Dictionary<string, RectTransform> namedTargets =
        new Dictionary<string, RectTransform>();
    [SerializeField] private RectTransform calloutRect;
    [SerializeField] private TMP_Text calloutText;
    private RectTransform cachedCardTarget;
    private string cachedCardTrickId;
    private TutorialFocusTarget focusTarget;
    private string focusIntroduction;

    public static TutorialFocusHighlightUI Create(
        Canvas canvas,
        MVPGameManager manager,
        TMP_FontAsset font)
    {
        if (canvas == null || manager == null)
            return null;

        Transform existing = canvas.transform.Find("TutorialFocusHighlight");
        GameObject root = existing != null
            ? existing.gameObject
            : new GameObject(
                "TutorialFocusHighlight",
                typeof(RectTransform),
                typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        TutorialFocusHighlightUI highlight = root.GetComponent<TutorialFocusHighlightUI>();
        if (highlight == null)
            highlight = root.AddComponent<TutorialFocusHighlightUI>();
        highlight.canvas = canvas;
        highlight.manager = manager;
        highlight.rootRect = rect;
        highlight.Build(font);
        highlight.Bind(canvas, manager);
        root.SetActive(false);
        return highlight;
    }

    public void Bind(Canvas targetCanvas, MVPGameManager targetManager)
    {
        canvas = targetCanvas;
        manager = targetManager;
        if (rootRect == null)
            rootRect = transform as RectTransform;
        CacheNamedTargets();
    }

    public void Show(TutorialStepDefinition step)
    {
        if (step == null)
        {
            Hide();
            return;
        }

        Show(step.focusTarget, step.focusIntroduction);
    }

    public void Show(TutorialFocusTarget target, string introduction)
    {
        focusTarget = target;
        focusIntroduction = introduction ?? string.Empty;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        RefreshFocus();
    }

    public void Hide()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        RefreshFocus();
    }

    private void Build(TMP_FontAsset font)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        for (int i = 0; i < dimmers.Length; i++)
            dimmers[i] = CreateImage($"FocusDimmer{i}", new Color(0.01f, 0.02f, 0.04f, outsideDimAlpha));
        for (int i = 0; i < borders.Length; i++)
            borders[i] = CreateImage($"FocusBorder{i}", new Color(1f, 0.76f, 0.25f, 0.95f));

        GameObject callout = new GameObject(
            "FocusIntroduction",
            typeof(RectTransform),
            typeof(Image));
        callout.transform.SetParent(transform, false);
        calloutRect = callout.GetComponent<RectTransform>();
        Image background = callout.GetComponent<Image>();
        background.color = new Color(0.055f, 0.09f, 0.14f, 0.97f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject(
            "FocusIntroductionText",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(callout.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 7f);
        textRect.offsetMax = new Vector2(-12f, -7f);
        calloutText = textObject.GetComponent<TMP_Text>();
        calloutText.font = font;
        calloutText.fontSize = 15f;
        calloutText.fontStyle = FontStyles.Bold;
        calloutText.alignment = TextAlignmentOptions.MidlineLeft;
        calloutText.enableWordWrapping = true;
        calloutText.color = new Color(1f, 0.87f, 0.56f);
        calloutText.raycastTarget = false;
    }

    private Image CreateImage(string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(transform, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void RefreshFocus()
    {
        if (rootRect == null || !gameObject.activeInHierarchy)
            return;

        RectTransform target = ResolveTarget(focusTarget);
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            SetVisualsActive(false);
            return;
        }

        SetVisualsActive(true);
        Rect focus = GetTargetRect(target);
        Rect bounds = rootRect.rect;
        focus.xMin = Mathf.Clamp(focus.xMin - targetPadding, bounds.xMin, bounds.xMax);
        focus.xMax = Mathf.Clamp(focus.xMax + targetPadding, bounds.xMin, bounds.xMax);
        focus.yMin = Mathf.Clamp(focus.yMin - targetPadding, bounds.yMin, bounds.yMax);
        focus.yMax = Mathf.Clamp(focus.yMax + targetPadding, bounds.yMin, bounds.yMax);

        Color dimColor = new Color(0.01f, 0.02f, 0.04f, outsideDimAlpha);
        for (int i = 0; i < dimmers.Length; i++)
            dimmers[i].color = dimColor;

        LayoutDimmer(dimmers[0].rectTransform,
            new Rect(bounds.xMin, focus.yMax, bounds.width, bounds.yMax - focus.yMax));
        LayoutDimmer(dimmers[1].rectTransform,
            new Rect(bounds.xMin, bounds.yMin, bounds.width, focus.yMin - bounds.yMin));
        LayoutDimmer(dimmers[2].rectTransform,
            new Rect(bounds.xMin, focus.yMin, focus.xMin - bounds.xMin, focus.height));
        LayoutDimmer(dimmers[3].rectTransform,
            new Rect(focus.xMax, focus.yMin, bounds.xMax - focus.xMax, focus.height));

        float pulse = GameSettingsRuntime.Current.reduceMotion
            ? 1f
            : 0.82f + Mathf.Sin(Time.unscaledTime * 4f) * 0.18f;
        Color borderColor = new Color(1f, 0.76f, 0.25f, pulse);
        for (int i = 0; i < borders.Length; i++)
            borders[i].color = borderColor;

        LayoutRect(borders[0].rectTransform,
            new Rect(focus.xMin, focus.yMax - borderThickness, focus.width, borderThickness));
        LayoutRect(borders[1].rectTransform,
            new Rect(focus.xMin, focus.yMin, focus.width, borderThickness));
        LayoutRect(borders[2].rectTransform,
            new Rect(focus.xMin, focus.yMin, borderThickness, focus.height));
        LayoutRect(borders[3].rectTransform,
            new Rect(focus.xMax - borderThickness, focus.yMin, borderThickness, focus.height));

        if (calloutText.text != focusIntroduction)
            calloutText.text = focusIntroduction;
        float resolvedCalloutWidth = Mathf.Min(
            calloutWidth,
            Mathf.Max(250f, bounds.width - 32f));
        float resolvedCalloutHeight = calloutHeight;
        float calloutX = Mathf.Clamp(
            focus.center.x + calloutOffset.x,
            bounds.xMin + resolvedCalloutWidth * 0.5f + 8f,
            bounds.xMax - resolvedCalloutWidth * 0.5f - 8f);
        bool placeBelow = focus.yMin - resolvedCalloutHeight - 12f >= bounds.yMin;
        float calloutY = placeBelow
            ? focus.yMin - resolvedCalloutHeight * 0.5f - 8f
            : focus.yMax + resolvedCalloutHeight * 0.5f + 8f;
        calloutY += calloutOffset.y;
        calloutY = Mathf.Clamp(
            calloutY,
            bounds.yMin + resolvedCalloutHeight * 0.5f + 8f,
            bounds.yMax - resolvedCalloutHeight * 0.5f - 8f);
        calloutRect.anchoredPosition = new Vector2(calloutX, calloutY);
        calloutRect.sizeDelta = new Vector2(resolvedCalloutWidth, resolvedCalloutHeight);
    }

    private RectTransform ResolveTarget(TutorialFocusTarget target)
    {
        switch (target)
        {
            case TutorialFocusTarget.RaceStatus:
                return ResolveNamedTarget("ScoreboardPanel") ??
                       RectOf(manager.hudUI != null ? manager.hudUI.statusText : null);
            case TutorialFocusTarget.TurnPrompt:
                return ResolveNamedTarget("OperationPromptPanel") ??
                       RectOf(manager.hudUI != null ? manager.hudUI.statusText : null);
            case TutorialFocusTarget.GearControls:
                return manager.hudUI != null && manager.hudUI.gear1Button != null
                    ? manager.hudUI.gear1Button.transform.parent as RectTransform
                    : ResolveNamedTarget("GearButtons");
            case TutorialFocusTarget.Hand:
                return manager.cardHandUI != null
                    ? manager.cardHandUI.handContainer as RectTransform
                    : ResolveNamedTarget("HandPanel");
            case TutorialFocusTarget.CardPiles:
                return ResolveNamedTarget("DeckTablePanel");
            case TutorialFocusTarget.EngineHeat:
                return manager.cardHandUI != null && manager.cardHandUI.enginePileText != null
                    ? manager.cardHandUI.enginePileText.transform.parent as RectTransform
                    : ResolveNamedTarget("EnginePanel");
            case TutorialFocusTarget.ActionButton:
                return manager.cardHandUI != null && manager.cardHandUI.playCardsButton != null
                    ? manager.cardHandUI.playCardsButton.transform as RectTransform
                    : ResolveNamedTarget("OperationPanel");
            case TutorialFocusTarget.Track:
                return ResolveNamedTarget("TrackFrame");
            case TutorialFocusTarget.Weather:
                return RectOf(manager.hudUI != null ? manager.hudUI.weatherText : null);
            case TutorialFocusTarget.PitChoice:
                return ResolveNamedTarget("PitChoicePanel") ?? ResolveNamedTarget("TrackFrame");
            case TutorialFocusTarget.UkSconeCard:
                return ResolveCard("uk-scone") ?? ResolveNamedTarget("HandPanel");
            case TutorialFocusTarget.UkTeaCard:
                return ResolveCard("uk-english-breakfast-tea") ?? ResolveNamedTarget("HandPanel");
            case TutorialFocusTarget.Review:
                return ResolveNamedTarget("TrackFrame") ?? ResolveNamedTarget("ScoreboardPanel");
            default:
                return null;
        }
    }

    private RectTransform ResolveNamedTarget(string objectName)
    {
        if (namedTargets.TryGetValue(objectName, out RectTransform cached))
            return cached != null && cached.gameObject.activeInHierarchy ? cached : null;

        RectTransform[] candidates = canvas.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].name != objectName)
                continue;
            namedTargets[objectName] = candidates[i];
            return candidates[i].gameObject.activeInHierarchy ? candidates[i] : null;
        }
        return null;
    }

    private RectTransform ResolveCard(string trickId)
    {
        if (manager.cardHandUI == null)
            return null;

        if (cachedCardTrickId == trickId && cachedCardTarget != null &&
            cachedCardTarget.gameObject.activeInHierarchy)
            return cachedCardTarget;

        CardUI[] cards = manager.cardHandUI.GetComponentsInChildren<CardUI>(true);
        for (int i = 0; i < cards.Length; i++)
        {
            CardData card = cards[i].cardData;
            if (card != null && card.trickId == trickId && cards[i].gameObject.activeInHierarchy)
            {
                cachedCardTrickId = trickId;
                cachedCardTarget = cards[i].transform as RectTransform;
                return cachedCardTarget;
            }
        }
        return null;
    }

    private void CacheNamedTargets()
    {
        namedTargets.Clear();
        RectTransform[] candidates = canvas.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            string candidateName = candidates[i].name;
            if (!namedTargets.ContainsKey(candidateName))
                namedTargets.Add(candidateName, candidates[i]);
        }
    }

    private Rect GetTargetRect(RectTransform target)
    {
        var worldCorners = new Vector3[4];
        target.GetWorldCorners(worldCorners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldCorners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootRect, screenPoint, camera, out Vector2 localPoint);
            min = Vector2.Min(min, localPoint);
            max = Vector2.Max(max, localPoint);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void SetVisualsActive(bool active)
    {
        for (int i = 0; i < dimmers.Length; i++)
            dimmers[i].gameObject.SetActive(active);
        for (int i = 0; i < borders.Length; i++)
            borders[i].gameObject.SetActive(active);
        calloutRect.gameObject.SetActive(active);
    }

    private static RectTransform RectOf(Component component)
    {
        return component != null ? component.transform as RectTransform : null;
    }

    private static void LayoutDimmer(RectTransform rect, Rect area)
    {
        LayoutRect(rect, new Rect(
            area.x,
            area.y,
            Mathf.Max(0f, area.width),
            Mathf.Max(0f, area.height)));
    }

    private static void LayoutRect(RectTransform rect, Rect area)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = area.center;
        rect.sizeDelta = area.size;
    }
}
