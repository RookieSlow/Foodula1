using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Player-facing card zones used by the lightweight table transition FX.</summary>
public enum CardVisualZone
{
    Hand,
    DrawPile,
    DiscardPile,
    Engine
}

/// <summary>
/// Runtime-only overlay that gives card ownership changes a short physical
/// transition. It never delays or changes rules resolution.
/// </summary>
public sealed class CardZoneTransitionUI : MonoBehaviour
{
    public float duration = 0.32f;
    public float stagger = 0.045f;
    public float arcHeight = 76f;
    public int maxBatchVisuals = 6;
    public Vector2 visualSize = new Vector2(54f, 76f);

    private RectTransform overlayRect;
    private Canvas canvas;
    private Sprite speedBackground;
    private Sprite heatBackground;
    private Sprite[] speedNumbers;
    private Sprite heatIcon;
    private TMP_FontAsset font;

    public int ActiveTransitionCount { get; private set; }

    public void Configure(
        Canvas ownerCanvas,
        Sprite speedBg,
        Sprite heatBg,
        Sprite[] numberSprites,
        Sprite heatIconSprite,
        TMP_FontAsset fontAsset)
    {
        canvas = ownerCanvas;
        speedBackground = speedBg;
        heatBackground = heatBg;
        speedNumbers = numberSprites;
        heatIcon = heatIconSprite;
        font = fontAsset;
        overlayRect = transform as RectTransform;
    }

    public void Play(
        IReadOnlyList<CardData> cards,
        RectTransform source,
        RectTransform destination)
    {
        if (cards == null || cards.Count == 0 || source == null || destination == null)
            return;

        int visuals = CardZoneTransitionRules.GetVisualCount(cards.Count, maxBatchVisuals);
        for (int i = 0; i < visuals; i++)
        {
            int represented = i == visuals - 1 ? cards.Count - visuals + 1 : 1;
            CardData card = cards[Mathf.Min(i, cards.Count - 1)];
            StartCoroutine(PlayOne(card, represented, source, destination, i * stagger));
        }
    }

    public void PlayHeat(int count, RectTransform source, RectTransform destination)
    {
        if (count <= 0)
            return;

        List<CardData> heatCards = new List<CardData>(count);
        for (int i = 0; i < count; i++)
            heatCards.Add(new CardData(CardType.Heat, 0));
        Play(heatCards, source, destination);
    }

    private IEnumerator PlayOne(
        CardData card,
        int representedCount,
        RectTransform source,
        RectTransform destination,
        float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        if (overlayRect == null || canvas == null)
            yield break;

        Vector2 start = ToOverlayPosition(source);
        Vector2 end = ToOverlayPosition(destination);
        GameObject visual = CreateVisual(card, representedCount);
        RectTransform visualRect = visual.GetComponent<RectTransform>();
        CanvasGroup group = visual.GetComponent<CanvasGroup>();
        visualRect.anchoredPosition = start;
        visualRect.localScale = Vector3.one * 0.88f;
        ActiveTransitionCount++;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = CardZoneTransitionRules.Smooth(t);
            visualRect.anchoredPosition = CardZoneTransitionRules.EvaluateArc(
                start, end, arcHeight, eased);
            float scale = t < 0.55f
                ? Mathf.Lerp(0.88f, 1.08f, t / 0.55f)
                : Mathf.Lerp(1.08f, 0.76f, (t - 0.55f) / 0.45f);
            visualRect.localScale = Vector3.one * scale;
            group.alpha = t < 0.78f ? 1f : Mathf.InverseLerp(1f, 0.78f, t);
            yield return null;
        }

        ActiveTransitionCount = Mathf.Max(0, ActiveTransitionCount - 1);
        Destroy(visual);
    }

    private Vector2 ToOverlayPosition(RectTransform target)
    {
        Camera cameraForCanvas = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cameraForCanvas, target.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            overlayRect, screen, cameraForCanvas, out Vector2 local);
        return local;
    }

    private GameObject CreateVisual(CardData card, int representedCount)
    {
        GameObject root = new GameObject(
            "CardTransition",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image));
        root.transform.SetParent(overlayRect, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = visualSize;

        Image background = root.GetComponent<Image>();
        background.raycastTarget = false;
        background.preserveAspect = true;
        background.sprite = card != null && card.IsHeat ? heatBackground : speedBackground;
        if (background.sprite == null)
            background.color = card != null && card.IsHeat
                ? new Color(0.88f, 0.34f, 0.22f, 1f)
                : new Color(0.24f, 0.56f, 0.91f, 1f);
        else if (card != null && card.IsTrick)
            background.color = new Color(1f, 0.84f, 0.38f, 1f);

        Sprite iconSprite = null;
        if (card != null && card.IsHeat)
            iconSprite = heatIcon;
        else if (card != null && card.IsSpeed && speedNumbers != null &&
                 card.value >= 1 && card.value <= speedNumbers.Length)
            iconSprite = speedNumbers[card.value - 1];

        if (iconSprite != null)
        {
            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(root.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.53f);
            iconRect.sizeDelta = visualSize * 0.58f;
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }
        else if (card != null)
        {
            string fallback = card.IsTrick ? "特" : card.IsHeat ? "热" : card.value.ToString();
            CreateLabel(root.transform, "CardLabel", fallback, 20f, Vector2.zero, Vector2.one);
        }

        if (representedCount > 1)
        {
            TMP_Text multiplier = CreateLabel(
                root.transform,
                "Multiplier",
                "×" + representedCount,
                13f,
                new Vector2(0.50f, 0.02f),
                new Vector2(0.98f, 0.28f));
            multiplier.alignment = TextAlignmentOptions.Center;
            multiplier.color = new Color(1f, 0.86f, 0.42f, 1f);
        }

        return root;
    }

    private TMP_Text CreateLabel(
        Transform parent,
        string name,
        string value,
        float size,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = font != null ? font : TMP_Settings.defaultFontAsset;
        label.text = value;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }
}

/// <summary>Pure presentation rules for card-zone transitions.</summary>
public static class CardZoneTransitionRules
{
    public static int GetVisualCount(int eventCardCount, int maxVisuals)
    {
        if (eventCardCount <= 0 || maxVisuals <= 0)
            return 0;
        return Mathf.Min(eventCardCount, maxVisuals);
    }

    public static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    public static Vector2 EvaluateArc(Vector2 start, Vector2 end, float height, float t)
    {
        t = Mathf.Clamp01(t);
        Vector2 linear = Vector2.LerpUnclamped(start, end, t);
        linear.y += Mathf.Max(0f, height) * 4f * t * (1f - t);
        return linear;
    }
}
