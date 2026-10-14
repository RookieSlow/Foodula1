using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation-only shell; existing HUD labels remain the live data bindings.</summary>
public sealed class RacingTechHudShellUI : MonoBehaviour
{
    private TMP_Text standings;
    private ScrollRect standingsScroll;
    private bool sizing;
    private bool subscribed;
    private string measuredText;
    private TMP_FontAsset measuredFont;
    private Vector2 measuredViewport;
    private float measuredFontSize;
    private bool measured;

    public ScrollRect StandingsScroll => standingsScroll;

    public static RacingTechHudShellUI Attach(Transform root, HUDUI hud)
    {
        Transform scoreboard = Find(root, "ScoreboardPanel");
        if (scoreboard == null) return null;
        RacingTechHudShellUI shell = scoreboard.GetComponent<RacingTechHudShellUI>();
        if (shell == null) shell = scoreboard.gameObject.AddComponent<RacingTechHudShellUI>();
        StylePanel(Find(root, "OperationPanel"));
        StylePanel(scoreboard);
        StylePanel(Find(root, "DeckTablePanel"));
        StyleHeading(Find(root, "OperationTitle"));
        StyleHeading(Find(root, "ScoreboardTitle"));
        StyleHeading(Find(root, "DeckTableTitle"));
        if (hud != null) shell.Bind(hud);
        return shell;
    }

    private void Bind(HUDUI hud)
    {
        if (hud.statusText != null)
        {
            Stretch(hud.statusText.rectTransform, new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.29f));
            hud.statusText.color = RacingTechTheme.Primary;
        }
        StyleStat(hud.gearText, 0.84f, 0.90f);
        StyleStat(hud.heatText, 0.73f, 0.84f);
        StyleStat(hud.lapText, 0.67f, 0.73f);
        StyleStat(hud.positionText, 0.61f, 0.67f);
        StyleStat(hud.aiStatusText, 0.53f, 0.61f, true);
        StyleStat(hud.weatherText, 0.46f, 0.53f, true);
        if (hud.standingsText == null) return;
        standings = hud.standingsText;
        if (isActiveAndEnabled) SubscribeOnce();
        measured = false;
        RectTransform scrollRect = Child(transform, "RacingTechStandings", typeof(Image), typeof(ScrollRect));
        Stretch(scrollRect, new Vector2(0.06f, 0.045f), new Vector2(0.94f, 0.43f));
        Image surface = scrollRect.GetComponent<Image>();
        surface.color = RacingTechTheme.Graphite;
        surface.raycastTarget = true;
        RectTransform viewport = Child(scrollRect, "Viewport", typeof(Image), typeof(RectMask2D));
        Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.offsetMin = new Vector2(6f, 4f);
        viewport.offsetMax = new Vector2(-16f, -4f);
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;
        RectTransform content = standings.rectTransform;
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, content.sizeDelta.y);
        content.localScale = Vector3.one;
        standings.color = RacingTechTheme.Primary;
        standings.alignment = TextAlignmentOptions.TopLeft;
        standings.enableWordWrapping = true;
        standings.overflowMode = TextOverflowModes.Overflow;
        standings.raycastTarget = false;
        standings.margin = Vector4.zero;
        standingsScroll = scrollRect.GetComponent<ScrollRect>();
        bool firstBinding = standingsScroll.content == null;
        standingsScroll.viewport = viewport;
        standingsScroll.content = content;
        standingsScroll.horizontal = false;
        standingsScroll.vertical = true;
        standingsScroll.movementType = ScrollRect.MovementType.Clamped;
        standingsScroll.inertia = false;
        standingsScroll.scrollSensitivity = 24f;
        standingsScroll.verticalScrollbar = BuildScrollbar(scrollRect);
        standingsScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        RefreshStandingsSize();
        if (firstBinding) standingsScroll.verticalNormalizedPosition = 1f;
    }

    private void StyleStat(TMP_Text label, float minY, float maxY, bool secondary = false)
    {
        if (label == null) return;
        label.transform.SetParent(transform, false);
        Stretch(label.rectTransform, new Vector2(0.08f, minY), new Vector2(0.92f, maxY));
        label.color = secondary ? RacingTechTheme.Secondary : RacingTechTheme.Primary;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
    }

    private static Scrollbar BuildScrollbar(RectTransform parent)
    {
        RectTransform bar = Child(parent, "Scrollbar", typeof(Image), typeof(Scrollbar));
        Stretch(bar, new Vector2(1f, 0f), Vector2.one);
        bar.offsetMin = new Vector2(-9f, 4f);
        bar.offsetMax = new Vector2(-3f, -4f);
        Image rail = bar.GetComponent<Image>();
        rail.color = RacingTechTheme.Raised;
        rail.raycastTarget = true;
        bool newHandle = bar.Find("Handle") == null;
        RectTransform handle = Child(bar, "Handle", typeof(Image));
        if (newHandle) Stretch(handle, Vector2.zero, Vector2.one);
        Image thumb = handle.GetComponent<Image>();
        thumb.color = RacingTechTheme.Secondary;
        thumb.raycastTarget = true;
        Scrollbar scrollbar = bar.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = thumb;
        scrollbar.transition = Selectable.Transition.None;
        Navigation navigation = scrollbar.navigation;
        navigation.mode = Navigation.Mode.None;
        scrollbar.navigation = navigation;
        return scrollbar;
    }

    private void OnEnable()
    {
        SubscribeOnce();
        RefreshStandingsSize();
    }
    private void SubscribeOnce()
    {
        if (subscribed) return;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        subscribed = true;
    }
    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();
    private void Unsubscribe()
    {
        if (subscribed) TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        subscribed = false;
    }
    private void OnRectTransformDimensionsChange() => RefreshStandingsSize();
    private void OnTextChanged(Object changed)
    {
        // Plain MonoBehaviours in controlled editor previews may miss teardown callbacks.
        if (this == null) { Unsubscribe(); return; }
        if (!isActiveAndEnabled) return;
        if (changed != standings) return;
        // Geometry notifications can also represent changed wrapping/spacing.
        measured = false;
        RefreshStandingsSize();
    }

    /// <summary>Runs only for binding, text notifications and panel resize, never in Update.</summary>
    public void RefreshStandingsSize()
    {
        if (sizing || standings == null || standingsScroll == null || standingsScroll.viewport == null) return;
        sizing = true;
        try
        {
            RectTransform viewport = standingsScroll.viewport;
            float width = Mathf.Max(1f, viewport.rect.width);
            Vector2 viewportSize = viewport.rect.size;
            if (measured && measuredText == standings.text && measuredFont == standings.font
                && measuredFontSize == standings.fontSize && measuredViewport == viewportSize) return;
            float preferred = standings.font != null
                ? standings.GetPreferredValues(standings.text, width, Mathf.Infinity).y : 0f;
            float height = Mathf.Max(viewport.rect.height, Mathf.Ceil(preferred) + 4f);
            if (Mathf.Abs(standings.rectTransform.sizeDelta.y - height) >= 0.5f)
                standings.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            measuredText = standings.text;
            measuredFont = standings.font;
            measuredFontSize = standings.fontSize;
            measuredViewport = viewportSize;
            measured = true;
        }
        finally { sizing = false; }
    }

    private static void StylePanel(Transform panel)
    {
        if (panel == null) return;
        RacingTechTheme.ApplySurface(panel.GetComponent<Image>());
        DisableOutline(panel);
        RectTransform divider = Child(panel, "RacingTechDivider", typeof(Image));
        Stretch(divider, new Vector2(0.04f, 0.905f), new Vector2(0.96f, 0.905f));
        divider.offsetMin = new Vector2(0f, -1f);
        Image image = divider.GetComponent<Image>();
        image.color = RacingTechTheme.Separator;
        image.raycastTarget = false;
    }

    private static void DisableOutline(Transform target)
    {
        Outline outline = target.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    private static void StyleHeading(Transform target)
    {
        TMP_Text text = target != null ? target.GetComponent<TMP_Text>() : null;
        if (text == null) return;
        text.color = RacingTechTheme.Primary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
    }

    private static RectTransform Child(Transform parent, string name, params System.Type[] components)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return (RectTransform)existing;
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        foreach (System.Type type in components) child.AddComponent(type);
        return child.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Transform Find(Transform root, string name)
    {
        if (root == null || root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = Find(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
