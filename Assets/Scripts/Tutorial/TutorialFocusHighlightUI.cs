using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialFocusDismissState
{
    private bool hasStep;
    private string operationKey;

    public bool IsVisible { get; private set; }
    public bool IsWaitingForPointerRelease { get; private set; }

    public void Show(TutorialStepId nextStepId, bool pointerHeld)
    {
        Show(nextStepId.ToString(), pointerHeld);
    }

    public void Show(string nextOperation, bool pointerHeld)
    {
        if (hasStep && operationKey == nextOperation)
            return;

        hasStep = true;
        operationKey = nextOperation;
        IsVisible = true;
        IsWaitingForPointerRelease = pointerHeld;
    }

    public bool Update(bool pointerHeld, bool pointerPressedThisFrame)
    {
        if (!IsVisible)
            return false;

        if (IsWaitingForPointerRelease)
        {
            if (!pointerHeld)
                IsWaitingForPointerRelease = false;
            return false;
        }

        if (!pointerPressedThisFrame)
            return false;

        IsVisible = false;
        return true;
    }

    public void Hide()
    {
        IsVisible = false;
        IsWaitingForPointerRelease = false;
    }
}

public enum TutorialFocusOperation
{
    None, Gear, SelectCards, ConfirmPlay, EndCards, Discard, ConfirmDiscard, Pit, Lane
}

/// <summary>Input gates take precedence over the lesson's explanatory subject.</summary>
public static class TutorialFocusOperationRules
{
    public static TutorialFocusOperation Resolve(string phase, int selectedCount, bool canEndCards)
    {
        switch (phase)
        {
            case "gear": return TutorialFocusOperation.Gear;
            case "cards": return selectedCount > 0 ? TutorialFocusOperation.ConfirmPlay :
                canEndCards ? TutorialFocusOperation.EndCards : TutorialFocusOperation.SelectCards;
            case "discard": return selectedCount > 0 ? TutorialFocusOperation.ConfirmDiscard : TutorialFocusOperation.Discard;
            case "pit": return TutorialFocusOperation.Pit;
            case "lane": return TutorialFocusOperation.Lane;
            default: return TutorialFocusOperation.None;
        }
    }
}

/// <summary>
/// Non-interactive spotlight for one authored tutorial concept. Four dimming
/// panels leave a clear hole around the live target, while a border and short
/// callout explain that region without taking ownership of gameplay input.
/// </summary>
public sealed class TutorialFocusHighlightUI : MonoBehaviour
{
    private Canvas canvas;
    private MVPGameManager manager;
    private CanvasGroup canvasGroup;
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
    private TutorialFocusTarget lessonTarget;
    private string lessonIntroduction;
    private bool presentationRequested;
    private TutorialFocusOperation operation;
    private int operationSequence;
    private bool operationEncounteredForLesson;
    private bool hasDisplayedLesson;
    private TutorialStepId displayedLesson;
    private readonly TutorialFocusDismissState operationDismissState = new TutorialFocusDismissState();
    private TutorialFocusDismissState ActiveDismissState =>
        operation != TutorialFocusOperation.None || operationEncounteredForLesson
            ? operationDismissState
            : dismissState;
    private readonly TutorialFocusDismissState dismissState =
        new TutorialFocusDismissState();

    public static TutorialFocusHighlightUI Create(
        Canvas canvas,
        MVPGameManager manager,
        TMP_FontAsset font)
    {
        if (canvas == null || manager == null)
            return null;

        TutorialFocusHighlightUI authored = canvas.GetComponentInChildren<TutorialFocusHighlightUI>(true);
        if (authored != null)
        {
            authored.Bind(canvas, manager);
            authored.Hide();
            return authored;
        }
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
        if (group == null) group = root.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        TutorialFocusHighlightUI highlight = root.GetComponent<TutorialFocusHighlightUI>();
        if (highlight == null)
            highlight = root.AddComponent<TutorialFocusHighlightUI>();
        highlight.canvas = canvas;
        highlight.manager = manager;
        highlight.canvasGroup = group;
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
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (rootRect == null)
            rootRect = transform as RectTransform;
        RepairCalloutHierarchy();
        EnsureCalloutMask();
        CacheNamedTargets();
    }

    public void Show(TutorialStepDefinition step)
    {
        if (step == null)
        {
            Hide();
            return;
        }

        Show(step.id, step.focusTarget, step.focusIntroduction);
    }

    public void Show(TutorialFocusTarget target, string introduction)
    {
        Show(default, target, introduction);
    }

    public void Show(
        TutorialStepId stepId,
        TutorialFocusTarget target,
        string introduction)
    {
        if (!hasDisplayedLesson || displayedLesson != stepId)
        {
            hasDisplayedLesson = true;
            displayedLesson = stepId;
            operation = TutorialFocusOperation.None;
            operationEncounteredForLesson = false;
            operationDismissState.Hide();
        }
        presentationRequested = true;
        lessonTarget = target;
        lessonIntroduction = introduction ?? string.Empty;
        dismissState.Show(stepId, Input.GetMouseButton(0));
        RefreshOperation();
        if (!ActiveDismissState.IsVisible && operation == TutorialFocusOperation.None && manager == null)
        {
            // Refreshes for the same tutorial step are common while the HUD is
            // rebuilding. A dismissed spotlight must actively clear itself on
            // every such refresh, otherwise TMP may submit its callout again.
            DeactivateVisuals();
            return;
        }

        if (presentationRequested)
        {
            SetCanvasVisible(true);
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            RefreshFocus();
        }
    }

    public void Hide()
    {
        presentationRequested = false;
        dismissState.Hide();
        operationDismissState.Hide();
        operation = TutorialFocusOperation.None;
        DeactivateVisuals();
    }

    private void LateUpdate()
    {
        if (!presentationRequested) return;
        RefreshOperation();
        if (ActiveDismissState.Update(
                Input.GetMouseButton(0),
                Input.GetMouseButtonDown(0)))
        {
            SetCalloutActive(false);
        }
        RefreshFocus();
    }

    private void RefreshOperation()
    {
        string phase = manager != null ? manager.TutorialInputPhase : "none";
        CardHandUI hand = manager != null ? manager.cardHandUI : null;
        int selected = hand == null ? 0 : phase == "discard"
            ? hand.GetSelectedCards().Count : hand.GetSelectedPlayCards().Count;
        PlayerState player = manager != null ? manager.Player : null;
        bool canEnd = player != null && player.deck != null &&
            (player.playedSpeedCardsThisTurn.Count >= manager.GetMaxSpeedCardsThisTurn(player) ||
             player.deck.CountSpeedInHand() == 0);
        TutorialFocusOperation next = TutorialFocusOperationRules.Resolve(phase, selected, canEnd);
        if (operation != next)
        {
            operation = next;
            operationSequence++;
            if (next != TutorialFocusOperation.None)
            {
                operationEncounteredForLesson = true;
                operationDismissState.Show(operationSequence.ToString(), Input.GetMouseButton(0));
            }
        }
        focusTarget = lessonTarget;
        focusIntroduction = lessonIntroduction;
        switch (operation)
        {
            case TutorialFocusOperation.Gear:
                focusTarget = TutorialFocusTarget.GearControls;
                focusIntroduction = "选择本回合档位，然后点击确认档位。"; break;
            case TutorialFocusOperation.SelectCards:
                focusTarget = TutorialFocusTarget.Hand;
                focusIntroduction = "点击手牌选择速度牌或一张特技牌，再确认出牌。"; break;
            case TutorialFocusOperation.ConfirmPlay:
                focusTarget = TutorialFocusTarget.ActionButton;
                focusIntroduction = "已选好牌，点击确认出牌；也可以先调整选择。"; break;
            case TutorialFocusOperation.EndCards:
                focusTarget = TutorialFocusTarget.ActionButton;
                focusIntroduction = "本次可以结束出牌，点击结束出牌继续。"; break;
            case TutorialFocusOperation.Discard:
                focusTarget = TutorialFocusTarget.Hand;
                focusIntroduction = "选择要弃掉的非热量牌，再点击确认弃牌；不弃牌可直接确认。"; break;
            case TutorialFocusOperation.ConfirmDiscard:
                focusTarget = TutorialFocusTarget.ActionButton;
                focusIntroduction = "点击确认弃牌，完成本次弃牌操作。"; break;
            case TutorialFocusOperation.Pit:
                focusTarget = TutorialFocusTarget.PitChoice;
                focusIntroduction = "选择预定进站或继续比赛。"; break;
            case TutorialFocusOperation.Lane:
                focusIntroduction = "选择本次通过终点后的车道。"; break;
        }
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
        calloutText.overflowMode = TextOverflowModes.Ellipsis;
        calloutText.color = new Color(1f, 0.87f, 0.56f);
        calloutText.raycastTarget = false;
        EnsureCalloutMask();
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

        RectTransform target = operation == TutorialFocusOperation.Lane
            ? ResolveNamedTarget("IndianapolisLaneChangePanel") : ResolveTarget(focusTarget);
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            SetVisualsActive(false);
            return;
        }

        SetVisualsActive(true);
        Rect focus = GetTargetRect(target);
        if (operation == TutorialFocusOperation.Gear && manager.hudUI != null &&
            manager.hudUI.confirmGearButton != null)
            focus = Union(focus, GetTargetRect(manager.hudUI.confirmGearButton.transform as RectTransform));
        if (operation == TutorialFocusOperation.Discard && manager.cardHandUI != null &&
            manager.cardHandUI.playCardsButton != null)
            focus = Union(focus, GetTargetRect(manager.cardHandUI.playCardsButton.transform as RectTransform));
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

        if (ActiveDismissState.IsVisible && calloutText != null && calloutText.text != focusIntroduction)
            calloutText.text = focusIntroduction;
        float resolvedCalloutWidth = Mathf.Min(
            calloutWidth,
            Mathf.Max(250f, bounds.width - 32f));
        float preferredTextHeight = calloutText != null
            ? calloutText.GetPreferredValues(
                focusIntroduction,
                Mathf.Max(1f, resolvedCalloutWidth - 24f),
                0f).y + 14f
            : calloutHeight;
        float resolvedCalloutHeight = Mathf.Clamp(
            Mathf.Max(calloutHeight, preferredTextHeight),
            calloutHeight,
            Mathf.Max(calloutHeight, bounds.height - 16f));
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
        if (manager == null) return null;
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
        {
            if (cached != null && cached.gameObject.activeInHierarchy) return cached;
            namedTargets.Remove(objectName);
        }

        RectTransform[] candidates = canvas.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].name != objectName || !candidates[i].gameObject.activeInHierarchy)
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
        {
            if (dimmers[i] != null)
                dimmers[i].gameObject.SetActive(active);
        }
        for (int i = 0; i < borders.Length; i++)
        {
            if (borders[i] != null)
                borders[i].gameObject.SetActive(active);
        }
        SetCalloutActive(active && ActiveDismissState.IsVisible);
    }

    private void SetCalloutActive(bool active)
    {
        if (calloutRect == null) return;
        if (!active)
        {
            foreach (TMP_Text text in calloutRect.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = string.Empty;
                text.ClearMesh();
                text.enabled = false;
                text.gameObject.SetActive(false);
            }
            foreach (CanvasRenderer renderer in calloutRect.GetComponentsInChildren<CanvasRenderer>(true))
            {
                renderer.Clear();
                renderer.cull = true;
            }
        }
        calloutRect.gameObject.SetActive(active);
        if (active && calloutText != null)
        {
            calloutText.gameObject.SetActive(true);
            calloutText.enabled = true;
            calloutText.text = focusIntroduction;
            calloutText.SetAllDirty();
        }
    }

    private void RepairCalloutHierarchy()
    {
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        // Keep the authored guide above the spotlight, and the entire overlay
        // above HUD siblings created later (pit/card panels included).
        TutorialOverlayAuthoring overlay = GetComponentInParent<TutorialOverlayAuthoring>();
        if (overlay != null)
        {
            overlay.transform.SetAsLastSibling();
            transform.SetAsFirstSibling();
        }
        else transform.SetAsLastSibling();
        if (calloutRect == null) calloutRect = transform.Find("FocusIntroduction") as RectTransform;
        if (calloutRect == null) return;
        calloutRect.SetParent(transform, false);
        calloutRect.SetAsLastSibling();
        if (calloutText == null) calloutText = calloutRect.GetComponentInChildren<TMP_Text>(true);
        if (calloutText != null && calloutText.transform.parent != calloutRect)
        {
            calloutText.transform.SetParent(calloutRect, false);
            RectTransform textRect = calloutText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 7f);
            textRect.offsetMax = new Vector2(-12f, -7f);
        }
        // Only this spotlight owns these named remnants. Never touch guide text.
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == calloutText) continue;
            text.text = string.Empty;
            text.ClearMesh();
            text.enabled = false;
            text.gameObject.SetActive(false);
        }
        foreach (Canvas nested in GetComponentsInChildren<Canvas>(true))
            nested.overrideSorting = false;
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
    }

    private static Rect Union(Rect a, Rect b)
    {
        return Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
            Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
    }

    private void DeactivateVisuals()
    {
        // Hide the parent group first, then explicitly clear every generated
        // mesh. TextMesh Pro fallback glyphs can own separate CanvasRenderers;
        // merely disabling their GameObjects may leave the last submitted mesh
        // visible for a frame in a standalone player.
        SetCanvasVisible(false);
        ClearRenderedGeometry();
        SetVisualsActive(false);
        cachedCardTarget = null;
        cachedCardTrickId = null;
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
        Canvas.ForceUpdateCanvases();
    }

    private void ClearRenderedGeometry()
    {
        if (calloutText != null)
        {
            calloutText.text = string.Empty;
            calloutText.ForceMeshUpdate(
                ignoreActiveState: true,
                forceTextReparsing: true);
            calloutText.ClearMesh();
            calloutText.enabled = false;
        }

        CanvasRenderer[] renderers =
            GetComponentsInChildren<CanvasRenderer>(includeInactive: true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].cull = true;
            renderers[i].Clear();
        }
    }

    private void EnsureCalloutMask()
    {
        if (calloutRect != null && calloutRect.GetComponent<RectMask2D>() == null)
            calloutRect.gameObject.AddComponent<RectMask2D>();
        if (calloutText != null)
            calloutText.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void SetCanvasVisible(bool visible)
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = visible ? 1f : 0f;
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
