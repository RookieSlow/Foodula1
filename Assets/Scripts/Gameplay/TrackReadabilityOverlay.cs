using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Runtime-only track information around the human player's car. The overlay
/// follows rendered node positions and never mutates track or race state.
/// </summary>
public sealed class TrackReadabilityOverlay : MonoBehaviour
{
    [Header("局部格数")]
    [Range(5, 7)] public int localCellRadius = 6;
    [Min(0.05f)] public float tickHalfLength = 0.20f;
    [Min(0.05f)] public float labelRoadClearance = 0.30f;
    [Min(0.1f)] public float minimumLabelSpacing = 0.72f;

    [Header("玩家高亮")]
    public Color playerHighlightColor = new Color(0.345f, 0.651f, 1f, 1f);
    [Min(0.05f)] public float haloRadius = 0.52f;
    [Min(0f)] public float haloPulseAmount = 0.055f;
    [Min(0f)] public float haloPulseSpeed = 2.4f;

    private sealed class CellMarker
    {
        public GameObject root;
        public LineRenderer tick;
        public SpriteRenderer badge;
        public TextMeshPro label;
    }

    private readonly List<CellMarker> markers = new List<CellMarker>();
    private TrackManager trackManager;
    private Transform playerCar;
    private Vector2[] trackPositions = new Vector2[0];
    private LineRenderer halo;
    private TextMeshPro positionLabel;
    private TMP_FontAsset fontAsset;
    private Material lineMaterial;
    private int currentNodeIndex = -1;

    public int CurrentNodeIndex => currentNodeIndex;
    public int VisibleMarkerCount => markers.Count;

    public void Configure(TrackManager manager, TMP_FontAsset preferredFont = null)
    {
        trackManager = manager;
        fontAsset = preferredFont != null ? preferredFont : TMP_Settings.defaultFontAsset;
        CacheTrackPositions();
        EnsureVisuals();
    }

    public void BindPlayer(Transform playerTransform)
    {
        playerCar = playerTransform;
        currentNodeIndex = -1;
        SetVisible(playerCar != null && trackManager != null && trackManager.TotalNodes > 0);
        RefreshNearestNode(true);
    }

    private void LateUpdate()
    {
        if (playerCar == null || trackManager == null || trackPositions.Length == 0)
            return;

        UpdatePlayerHighlight();
        RefreshNearestNode(false);
    }

    private void CacheTrackPositions()
    {
        if (trackManager == null || trackManager.TotalNodes <= 0)
        {
            trackPositions = new Vector2[0];
            return;
        }

        trackPositions = new Vector2[trackManager.TotalNodes];
        for (int i = 0; i < trackPositions.Length; i++)
            trackPositions[i] = trackManager.GetNodePosition(i);
    }

    private void EnsureVisuals()
    {
        if (lineMaterial == null)
            lineMaterial = new Material(Shader.Find("Sprites/Default"));

        if (halo == null)
        {
            GameObject haloObject = new GameObject("PlayerPositionHalo");
            haloObject.transform.SetParent(transform, false);
            halo = haloObject.AddComponent<LineRenderer>();
            halo.useWorldSpace = true;
            halo.loop = true;
            halo.positionCount = 48;
            halo.startWidth = 0.075f;
            halo.endWidth = 0.075f;
            halo.numCornerVertices = 6;
            halo.material = lineMaterial;
            halo.sortingOrder = 12;
        }

        if (positionLabel == null)
        {
            GameObject labelObject = new GameObject("PlayerCellPosition");
            labelObject.transform.SetParent(transform, false);
            positionLabel = labelObject.AddComponent<TextMeshPro>();
            positionLabel.alignment = TextAlignmentOptions.Center;
            positionLabel.fontSize = 2.7f;
            positionLabel.fontStyle = FontStyles.Bold;
            positionLabel.color = Color.white;
            positionLabel.outlineWidth = 0.24f;
            positionLabel.outlineColor = new Color(0.025f, 0.04f, 0.06f, 0.98f);
            positionLabel.sortingOrder = 13;
            if (fontAsset != null)
                positionLabel.font = fontAsset;
        }

        int markerCount = localCellRadius * 2 + 1;
        while (markers.Count < markerCount)
            markers.Add(CreateMarker(markers.Count));
        for (int i = 0; i < markers.Count; i++)
            markers[i].root.SetActive(i < markerCount);
    }

    private CellMarker CreateMarker(int index)
    {
        GameObject root = new GameObject($"LocalCellMarker_{index}");
        root.transform.SetParent(transform, false);

        LineRenderer tick = root.AddComponent<LineRenderer>();
        tick.useWorldSpace = true;
        tick.positionCount = 2;
        tick.startWidth = 0.035f;
        tick.endWidth = 0.035f;
        tick.material = lineMaterial;
        tick.sortingOrder = 4;

        GameObject badgeObject = new GameObject("CellBadge");
        badgeObject.transform.SetParent(root.transform, false);
        SpriteRenderer badge = badgeObject.AddComponent<SpriteRenderer>();
        badge.sprite = trackManager != null ? trackManager.NodeVisualSprite : null;
        badge.color = new Color(0.025f, 0.04f, 0.06f, 0.84f);
        badge.sortingOrder = 4;

        GameObject labelObject = new GameObject("CellNumber");
        labelObject.transform.SetParent(root.transform, false);
        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 2.15f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.outlineWidth = 0.18f;
        label.outlineColor = new Color(0.025f, 0.04f, 0.06f, 0.96f);
        label.sortingOrder = 5;
        if (fontAsset != null)
            label.font = fontAsset;

        return new CellMarker { root = root, tick = tick, badge = badge, label = label };
    }

    private void RefreshNearestNode(bool force)
    {
        if (playerCar == null || trackPositions.Length == 0)
            return;

        int nearest = TrackPresentationRules.FindClosestNodeIndex(trackPositions, playerCar.position);
        if (!force && nearest == currentNodeIndex)
            return;

        currentNodeIndex = nearest;
        RefreshLocalMarkers();
        if (positionLabel != null)
            positionLabel.text = TrackPresentationRules.FormatCellPosition(currentNodeIndex, trackPositions.Length);
    }

    private void RefreshLocalMarkers()
    {
        EnsureVisuals();
        int stride = TrackPresentationRules.CalculateLocalLabelStride(
            trackManager.MedianNodeSpacing,
            minimumLabelSpacing);
        float labelDistance = trackManager.RoadVisualWidth * 0.5f + labelRoadClearance;

        for (int markerIndex = 0; markerIndex < markers.Count; markerIndex++)
        {
            int relative = markerIndex - localCellRadius;
            int nodeIndex = TrackPresentationRules.WrapNodeIndex(
                currentNodeIndex + relative,
                trackManager.TotalNodes);
            Vector3 center = trackManager.GetNodePosition(nodeIndex);
            Vector2 normal = trackManager.GetNodeNormal(nodeIndex);
            CellMarker marker = markers[markerIndex];
            bool isCurrent = relative == 0;
            Color color = isCurrent
                ? playerHighlightColor
                : trackManager.GetNodeReadabilityColor(nodeIndex);

            float tickLength = isCurrent ? tickHalfLength * 1.45f : tickHalfLength;
            marker.tick.SetPosition(0, center - (Vector3)(normal * tickLength));
            marker.tick.SetPosition(1, center + (Vector3)(normal * tickLength));
            marker.tick.startWidth = marker.tick.endWidth = isCurrent ? 0.07f : 0.035f;
            marker.tick.startColor = marker.tick.endColor = color;

            Vector3 labelPosition = center + (Vector3)(normal * labelDistance);
            marker.badge.transform.position = labelPosition;
            marker.badge.transform.rotation = Quaternion.identity;
            marker.badge.transform.localScale = Vector3.one * (isCurrent ? 0.38f : 0.31f);
            marker.badge.color = isCurrent
                ? new Color(0.04f, 0.16f, 0.28f, 0.96f)
                : new Color(0.025f, 0.04f, 0.06f, 0.84f);

            marker.label.transform.position = labelPosition;
            marker.label.transform.rotation = Quaternion.identity;
            marker.label.text = (nodeIndex + 1).ToString();
            marker.label.color = color;
            marker.label.fontSize = isCurrent ? 2.65f : 2.15f;
            bool showLabel = TrackPresentationRules.ShouldShowLocalCellLabel(
                relative,
                stride,
                localCellRadius);
            marker.badge.gameObject.SetActive(showLabel && marker.badge.sprite != null);
            marker.label.gameObject.SetActive(showLabel);
        }
    }

    private void UpdatePlayerHighlight()
    {
        float pulse = haloPulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * haloPulseSpeed * Mathf.PI * 2f));
        float radius = haloRadius + pulse;
        Vector3 center = playerCar.position;

        for (int i = 0; i < halo.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / halo.positionCount;
            halo.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }

        float alpha = 0.72f + pulse * 2f;
        Color haloColor = new Color(
            playerHighlightColor.r,
            playerHighlightColor.g,
            playerHighlightColor.b,
            Mathf.Clamp01(alpha));
        halo.startColor = halo.endColor = haloColor;

        positionLabel.transform.position = center + Vector3.up * (radius + 0.42f);
        positionLabel.transform.rotation = Quaternion.identity;
    }

    private void SetVisible(bool visible)
    {
        if (halo != null)
            halo.gameObject.SetActive(visible);
        if (positionLabel != null)
            positionLabel.gameObject.SetActive(visible);
        for (int i = 0; i < markers.Count; i++)
            markers[i].root.SetActive(visible);
    }

    private void OnDestroy()
    {
        if (lineMaterial != null)
            Destroy(lineMaterial);
    }
}
