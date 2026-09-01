using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-only team and rank marker rendered above a race car.
///
/// The visual GDD ultimately calls for a flag and driver avatar. No authored
/// flag/avatar textures are currently available, so this component provides a
/// readable team-code fallback without changing the scene or car prefab. The
/// badge is deliberately non-interactive and stays upright while the car turns.
/// </summary>
public sealed class RaceCarBadgeUI : MonoBehaviour
{
    private const float BadgeWidth = 130f;
    private const float BadgeHeight = 30f;
    private const float BadgeWorldHeight = 0.14f;
    private const float BadgeTopPadding = 0.06f;

    private Canvas badgeCanvas;
    private RectTransform badgeRect;
    private Image badgeBackground;
    private Image badgeLogo;
    private TextMeshProUGUI badgeLabel;
    private SpriteRenderer carRenderer;
    private TeamId teamId;
    private bool initialized;

    /// <summary>Creates the world-space badge and assigns its team identity.</summary>
    public void Initialize(TeamId team)
    {
        teamId = team;
        EnsureView();
        ApplyTeamStyle();
        initialized = true;
        UpdateBadgeTransform();
    }

    /// <summary>Refreshes the displayed rank after standings change.</summary>
    public void Refresh(PlayerState player, int rank)
    {
        if (player == null)
            return;

        if (!initialized || player.teamId != teamId)
            Initialize(player.teamId);

        badgeLabel.text = badgeLogo != null && badgeLogo.sprite != null
            ? string.Format("P{0}", Mathf.Max(1, rank))
            : string.Format("{0}  P{1}", TeamCarPresentationRules.GetBadgeCode(teamId), Mathf.Max(1, rank));
        UpdateBadgeTransform();
    }

    private void LateUpdate()
    {
        if (initialized)
            UpdateBadgeTransform();
    }

    private void EnsureView()
    {
        if (badgeCanvas != null)
            return;

        carRenderer = GetComponent<SpriteRenderer>();

        GameObject canvasObject = new GameObject(
            "TeamBadgeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        badgeCanvas = canvasObject.GetComponent<Canvas>();
        badgeCanvas.renderMode = RenderMode.WorldSpace;
        badgeCanvas.worldCamera = Camera.main;
        badgeCanvas.overrideSorting = true;
        badgeCanvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        scaler.referencePixelsPerUnit = 100f;

        badgeRect = canvasObject.GetComponent<RectTransform>();
        badgeRect.sizeDelta = new Vector2(BadgeWidth, BadgeHeight);

        GameObject backgroundObject = new GameObject(
            "TeamBadgeBackground",
            typeof(RectTransform),
            typeof(Image),
            typeof(Outline));
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        badgeBackground = backgroundObject.GetComponent<Image>();
        badgeBackground.raycastTarget = false;
        Outline outline = backgroundObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject logoObject = new GameObject(
            "TeamBadgeLogo", typeof(RectTransform), typeof(Image));
        logoObject.transform.SetParent(canvasObject.transform, false);
        RectTransform logoRect = logoObject.GetComponent<RectTransform>();
        logoRect.anchorMin = logoRect.anchorMax = new Vector2(0f, 0.5f);
        logoRect.pivot = new Vector2(0f, 0.5f);
        logoRect.anchoredPosition = new Vector2(5f, 0f);
        logoRect.sizeDelta = new Vector2(30f, 24f);
        badgeLogo = logoObject.GetComponent<Image>();
        badgeLogo.preserveAspect = true;
        badgeLogo.raycastTarget = false;

        GameObject labelObject = new GameObject(
            "TeamBadgeLabel",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(37f, 0f);
        labelRect.offsetMax = new Vector2(-5f, 0f);

        badgeLabel = labelObject.GetComponent<TextMeshProUGUI>();
        badgeLabel.alignment = TextAlignmentOptions.Center;
        badgeLabel.fontStyle = FontStyles.Bold;
        if (TMP_Settings.defaultFontAsset != null)
            badgeLabel.font = TMP_Settings.defaultFontAsset;
        badgeLabel.fontSize = 20f;
        badgeLabel.enableAutoSizing = true;
        badgeLabel.fontSizeMin = 10f;
        badgeLabel.fontSizeMax = 20f;
        badgeLabel.enableWordWrapping = false;
        badgeLabel.raycastTarget = false;
        badgeLabel.outlineWidth = 0.18f;
        badgeLabel.outlineColor = new Color(0f, 0f, 0f, 0.75f);
    }

    private void ApplyTeamStyle()
    {
        if (badgeBackground == null || badgeLabel == null)
            return;

        badgeBackground.color = TeamCarPresentationRules.GetBadgeColor(teamId);
        badgeLogo.sprite = BrandArtResources.LoadTeamLogo(teamId);
        badgeLogo.enabled = badgeLogo.sprite != null;
        badgeLabel.rectTransform.offsetMin = new Vector2(badgeLogo.enabled ? 37f : 5f, 0f);
        badgeLabel.color = TeamCarPresentationRules.GetBadgeTextColor(teamId);
        badgeLabel.text = badgeLogo.enabled ? string.Empty : TeamCarPresentationRules.GetBadgeCode(teamId);
    }

    private void UpdateBadgeTransform()
    {
        if (badgeCanvas == null || badgeRect == null)
            return;

        float parentScale = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        float canvasScale = BadgeWorldHeight / (BadgeHeight * parentScale);
        badgeRect.localScale = Vector3.one * canvasScale;

        if (carRenderer == null)
            carRenderer = GetComponent<SpriteRenderer>();

        float verticalOffset = carRenderer != null
            ? carRenderer.bounds.extents.y + BadgeTopPadding
            : 0.34f;
        badgeRect.position = transform.position + Vector3.up * verticalOffset + Vector3.back * 0.03f;
        badgeRect.rotation = Quaternion.identity;
    }
}
