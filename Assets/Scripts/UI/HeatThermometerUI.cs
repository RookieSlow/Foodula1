using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compact ten-segment vertical heat gauge. It is authored into RaceCanvas by
/// the builder and can also attach itself to older canvases at runtime.
/// </summary>
public sealed class HeatThermometerUI : MonoBehaviour
{
    public const int SegmentTotal = 10;

    private static readonly Color ColdColor = new Color(0.22f, 0.68f, 1f, 1f);
    private static readonly Color WarmColor = new Color(0.97f, 0.58f, 0.24f, 1f);
    private static readonly Color HotColor = new Color(0.97f, 0.28f, 0.25f, 1f);
    private static readonly Color InactiveColor = new Color(0.18f, 0.23f, 0.31f, 0.55f);

    [SerializeField] private Image[] segments = new Image[SegmentTotal];
    [SerializeField] private TMP_Text percentText;
    [SerializeField] private CanvasGroup pulseGroup;

    private HeatGaugeState currentState;
    private bool initialized;

    public HeatGaugeState CurrentState => currentState;
    public int SegmentCount => segments != null ? segments.Length : 0;

    private void Awake()
    {
        // RaceCanvas.prefab serializes the thermometer children directly. The
        // runtime-created fallback calls Build(), but authored instances must
        // also become refreshable before HUDUI's first Refresh call.
        TryInitializeSerializedLayout();
    }

    private void Update()
    {
        if (pulseGroup == null)
            return;

        pulseGroup.alpha = currentState.WarningLevel == HeatWarningLevel.Critical
            ? 0.78f + Mathf.PingPong(Time.unscaledTime * 0.45f, 0.22f)
            : 1f;
    }

    private void OnDisable()
    {
        if (pulseGroup != null)
            pulseGroup.alpha = 1f;
    }

    public static HeatThermometerUI Attach(TMP_Text sourceText)
    {
        if (sourceText == null || sourceText.transform.parent == null)
            return null;

        Transform parent = sourceText.transform.parent;
        Transform existing = parent.Find("HeatThermometer");
        GameObject root = existing != null
            ? existing.gameObject
            : new GameObject("HeatThermometer", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);

        HeatThermometerUI thermometer = root.GetComponent<HeatThermometerUI>();
        if (thermometer == null)
            thermometer = root.AddComponent<HeatThermometerUI>();
        thermometer.Build(sourceText);
        return thermometer;
    }

    public void Refresh(CardDeck deck)
    {
        TryInitializeSerializedLayout();
        if (!initialized)
            return;

        currentState = HeatGaugeRules.Evaluate(deck);
        int activeSegments = Mathf.CeilToInt(currentState.Fill01 * SegmentTotal);

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null)
                continue;
            segments[i].color = i < activeSegments ? GetSegmentColor(i) : InactiveColor;
        }

        if (percentText != null)
        {
            percentText.text = $"{currentState.Percent}%";
            percentText.color = currentState.WarningLevel == HeatWarningLevel.Critical
                ? HotColor
                : currentState.WarningLevel == HeatWarningLevel.Elevated
                    ? WarmColor
                    : ColdColor;
        }
    }

    private void TryInitializeSerializedLayout()
    {
        if (initialized)
            return;

        if (segments == null || segments.Length != SegmentTotal)
            segments = new Image[SegmentTotal];

        bool hasAllSegments = true;
        for (int i = 0; i < SegmentTotal; i++)
        {
            if (segments[i] == null)
            {
                Transform child = transform.Find($"Segment{i + 1:00}");
                if (child != null)
                    segments[i] = child.GetComponent<Image>();
            }

            if (segments[i] == null)
                hasAllSegments = false;
        }

        if (percentText == null)
        {
            Transform child = transform.Find("HeatPercent");
            if (child != null)
                percentText = child.GetComponent<TMP_Text>();
        }

        if (pulseGroup == null)
            pulseGroup = GetComponent<CanvasGroup>();

        // The segment references are the minimum contract. A percentage label
        // is optional so older compact gauges can still display their fill.
        initialized = hasAllSegments;
    }

    private void Build(TMP_Text sourceText)
    {
        RectTransform root = GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.82f, 0.675f);
        root.anchorMax = new Vector2(0.96f, 0.895f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
        transform.SetAsLastSibling();

        pulseGroup = GetComponent<CanvasGroup>();
        if (pulseGroup == null)
            pulseGroup = gameObject.AddComponent<CanvasGroup>();
        pulseGroup.blocksRaycasts = false;
        pulseGroup.interactable = false;

        Image background = GetOrCreateImage("GaugeBackground", transform);
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = new Vector2(0.2f, 0.12f);
        backgroundRect.anchorMax = new Vector2(0.8f, 0.88f);
        ResetRect(backgroundRect);
        background.color = new Color(0.025f, 0.04f, 0.07f, 0.9f);

        segments = new Image[SegmentTotal];
        const float gap = 0.012f;
        float segmentHeight = (0.72f - gap * (SegmentTotal - 1)) / SegmentTotal;
        for (int i = 0; i < SegmentTotal; i++)
        {
            Image segment = GetOrCreateImage($"Segment{i + 1:00}", transform);
            RectTransform rect = segment.rectTransform;
            float minY = 0.14f + i * (segmentHeight + gap);
            rect.anchorMin = new Vector2(0.27f, minY);
            rect.anchorMax = new Vector2(0.73f, minY + segmentHeight);
            ResetRect(rect);
            segment.color = InactiveColor;
            segments[i] = segment;
        }

        CreateTick("Tick50", 0.50f, WarmColor);
        CreateTick("Tick70", 0.70f, HotColor);

        percentText = GetOrCreateLabel("HeatPercent", transform, sourceText);
        RectTransform percentRect = percentText.rectTransform;
        percentRect.anchorMin = new Vector2(0f, 0.88f);
        percentRect.anchorMax = new Vector2(1f, 1f);
        ResetRect(percentRect);
        percentText.fontSize = 11f;
        percentText.alignment = TextAlignmentOptions.Center;
        percentText.raycastTarget = false;
        percentText.text = "0%";

        RectTransform sourceRect = sourceText.rectTransform;
        sourceRect.anchorMax = new Vector2(Mathf.Min(sourceRect.anchorMax.x, 0.79f), sourceRect.anchorMax.y);
        sourceText.fontSize = Mathf.Min(sourceText.fontSize, 13f);
        initialized = true;
    }

    private void CreateTick(string name, float normalizedHeat, Color color)
    {
        Image tick = GetOrCreateImage(name, transform);
        RectTransform rect = tick.rectTransform;
        float y = 0.14f + normalizedHeat * 0.72f;
        rect.anchorMin = new Vector2(0.72f, y);
        rect.anchorMax = new Vector2(0.94f, y);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 2f);
        tick.color = color;
    }

    private static Color GetSegmentColor(int index)
    {
        float t = (index + 0.5f) / SegmentTotal;
        return t < HeatGaugeRules.ElevatedThreshold
            ? Color.Lerp(ColdColor, WarmColor, t / HeatGaugeRules.ElevatedThreshold)
            : Color.Lerp(WarmColor, HotColor,
                (t - HeatGaugeRules.ElevatedThreshold) / (1f - HeatGaugeRules.ElevatedThreshold));
    }

    private static Image GetOrCreateImage(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        GameObject child = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text GetOrCreateLabel(string name, Transform parent, TMP_Text source)
    {
        Transform existing = parent.Find(name);
        GameObject child = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        TMP_Text label = child.GetComponent<TMP_Text>();
        if (source != null && source.font != null)
            label.font = source.font;
        return label;
    }

    private static void ResetRect(RectTransform rect)
    {
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
