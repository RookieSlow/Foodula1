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

    private sealed class TeamLandmarkMarker
    {
        public GameObject root;
        public LineRenderer connector;
        public LineRenderer shield;
        public SpriteRenderer backing;
        public SpriteRenderer emblem;
        public TextMeshPro label;
        public int nodeIndex;
    }

    private readonly List<CellMarker> markers = new List<CellMarker>();
    private readonly List<TeamLandmarkMarker> teamLandmarkMarkers = new List<TeamLandmarkMarker>();
    private TrackManager trackManager;
    private Transform playerCar;
    private Vector2[] trackPositions = new Vector2[0];
    private LineRenderer halo;
    private TextMeshPro positionLabel;
    private TMP_FontAsset fontAsset;
    private Material lineMaterial;
    private int currentNodeIndex = -1;
    private bool unitedStatesTeamPresent;

    private static readonly Color UsNavy = new Color(0.035f, 0.07f, 0.16f, 0.98f);
    private static readonly Color UsRed = new Color(0.86f, 0.13f, 0.18f, 1f);
    private static readonly Color UsGold = new Color(1f, 0.76f, 0.20f, 1f);

    public int CurrentNodeIndex => currentNodeIndex;
    public int VisibleMarkerCount => markers.Count;
    public bool UnitedStatesLandmarksVisible => unitedStatesTeamPresent && teamLandmarkMarkers.Count > 0;
    public int UnitedStatesLandmarkMarkerCount => teamLandmarkMarkers.Count;

    public void Configure(TrackManager manager, TMP_FontAsset preferredFont = null)
    {
        trackManager = manager;
        fontAsset = preferredFont != null ? preferredFont : TMP_Settings.defaultFontAsset;
        CacheTrackPositions();
        EnsureVisuals();
        EnsureTeamLandmarkVisuals();
        RefreshTeamLandmarks();
        SetTeamLandmarksActive(unitedStatesTeamPresent);
    }

    public void BindPlayer(Transform playerTransform)
    {
        playerCar = playerTransform;
        currentNodeIndex = -1;
        SetVisible(playerCar != null && trackManager != null && trackManager.TotalNodes > 0);
        RefreshNearestNode(true);
    }

    public void SetUnitedStatesLandmarksVisible(bool visible)
    {
        unitedStatesTeamPresent = visible;
        EnsureTeamLandmarkVisuals();
        RefreshTeamLandmarks();
        SetTeamLandmarksActive(visible);
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
            halo.sharedMaterial = lineMaterial;
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
        tick.sharedMaterial = lineMaterial;
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

    private void EnsureTeamLandmarkVisuals()
    {
        if (trackManager == null || trackManager.TotalNodes <= 0)
            return;

        var indices = TeamLandmarkPresentationRules.GetUnitedStatesLandmarkNodeIndices(trackManager.TotalNodes);
        int[] landmarkNodes = { indices.startFinish, indices.midpoint };
        for (int i = 0; i < landmarkNodes.Length; i++)
        {
            if (landmarkNodes[i] < 0 || landmarkNodes[i] >= trackManager.TotalNodes)
                continue;

            TeamLandmarkMarker marker;
            if (i >= teamLandmarkMarkers.Count)
            {
                marker = CreateTeamLandmarkMarker(i);
                teamLandmarkMarkers.Add(marker);
            }
            else
            {
                marker = teamLandmarkMarkers[i];
            }

            marker.nodeIndex = landmarkNodes[i];
            marker.label.text = TeamLandmarkPresentationRules.GetUnitedStatesLandmarkLabel(i);
        }

        while (teamLandmarkMarkers.Count > landmarkNodes.Length)
        {
            TeamLandmarkMarker obsolete = teamLandmarkMarkers[teamLandmarkMarkers.Count - 1];
            teamLandmarkMarkers.RemoveAt(teamLandmarkMarkers.Count - 1);
            if (obsolete.root != null)
                Destroy(obsolete.root);
        }
    }

    private TeamLandmarkMarker CreateTeamLandmarkMarker(int index)
    {
        GameObject root = new GameObject($"USLandmark_{index + 1}");
        root.transform.SetParent(transform, false);
        root.SetActive(false);

        LineRenderer connector = CreateLandmarkLineRenderer(root.transform, "USLandmarkConnector");
        connector.useWorldSpace = true;
        connector.positionCount = 2;
        connector.startWidth = 0.07f;
        connector.endWidth = 0.035f;
        connector.sharedMaterial = lineMaterial;
        connector.startColor = UsGold;
        connector.endColor = UsRed;
        connector.sortingOrder = 14;

        LineRenderer shield = CreateLandmarkLineRenderer(root.transform, "USLandmarkShield");
        shield.useWorldSpace = true;
        shield.loop = true;
        shield.positionCount = 6;
        shield.startWidth = 0.085f;
        shield.endWidth = 0.085f;
        shield.sharedMaterial = lineMaterial;
        shield.startColor = shield.endColor = UsGold;
        shield.sortingOrder = 19;

        GameObject backingObject = new GameObject("USLandmarkBacking");
        backingObject.transform.SetParent(root.transform, false);
        SpriteRenderer backing = backingObject.AddComponent<SpriteRenderer>();
        backing.sprite = trackManager.NodeVisualSprite;
        backing.color = UsNavy;
        backing.sortingOrder = 17;

        GameObject emblemObject = new GameObject("USLandmarkEmblem");
        emblemObject.transform.SetParent(root.transform, false);
        SpriteRenderer emblem = emblemObject.AddComponent<SpriteRenderer>();
        emblem.sprite = BrandArtResources.LoadTeamLogo(TeamId.US);
        emblem.color = Color.white;
        emblem.sortingOrder = 18;

        GameObject labelObject = new GameObject("USLandmarkLabel");
        labelObject.transform.SetParent(root.transform, false);
        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 2.2f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.sortingOrder = 20;
        label.rectTransform.sizeDelta = new Vector2(4.2f, 0.72f);
        if (fontAsset != null)
            label.font = fontAsset;

        return new TeamLandmarkMarker
        {
            root = root,
            connector = connector,
            shield = shield,
            backing = backing,
            emblem = emblem,
            label = label
        };
    }

    private static LineRenderer CreateLandmarkLineRenderer(Transform parent, string objectName)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(parent, false);
        return lineObject.AddComponent<LineRenderer>();
    }

    private void RefreshTeamLandmarks()
    {
        if (trackManager == null || trackPositions.Length == 0)
            return;

        float diameter = Mathf.Clamp(trackManager.MedianNodeSpacing * 1.25f, 0.78f, 1.18f);
        float roadHalfWidth = trackManager.RoadVisualWidth * 0.5f;
        float outwardOffset = roadHalfWidth + Mathf.Max(labelRoadClearance, diameter * 0.72f);

        for (int i = 0; i < teamLandmarkMarkers.Count; i++)
        {
            TeamLandmarkMarker marker = teamLandmarkMarkers[i];
            if (marker == null || marker.root == null || marker.nodeIndex < 0 || marker.nodeIndex >= trackPositions.Length)
                continue;

            Vector3 trackPoint = trackManager.GetNodePosition(marker.nodeIndex);
            Vector2 normal = trackManager.GetNodeNormal(marker.nodeIndex);
            if (normal.sqrMagnitude <= Mathf.Epsilon)
                normal = Vector2.up;
            normal.Normalize();

            Vector3 outward = new Vector3(normal.x, normal.y, 0f);
            Vector3 center = trackPoint + outward * (outwardOffset + diameter * 0.56f);
            marker.root.transform.position = center;

            marker.connector.SetPosition(0, trackPoint + outward * roadHalfWidth);
            marker.connector.SetPosition(1, center - outward * (diameter * 0.48f));

            Vector3[] shieldPoints =
            {
                center + Vector3.up * (diameter * 0.72f),
                center + new Vector3(diameter * 0.62f, diameter * 0.28f, 0f),
                center + new Vector3(diameter * 0.48f, -diameter * 0.34f, 0f),
                center + Vector3.down * (diameter * 0.72f),
                center + new Vector3(-diameter * 0.48f, -diameter * 0.34f, 0f),
                center + new Vector3(-diameter * 0.62f, diameter * 0.28f, 0f)
            };
            marker.shield.SetPositions(shieldPoints);

            SetSpriteDiameter(marker.backing, diameter * 0.88f);
            SetSpriteDiameter(marker.emblem, diameter * 0.58f);
            marker.label.transform.position = center - Vector3.up * (diameter * 1.04f);
            marker.label.transform.rotation = Quaternion.identity;
            marker.label.fontSize = Mathf.Clamp(diameter * 2.35f, 1.8f, 2.8f);
        }
    }

    private static void SetSpriteDiameter(SpriteRenderer renderer, float diameter)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Vector2 size = renderer.sprite.bounds.size;
        float currentDiameter = Mathf.Max(size.x, size.y);
        if (currentDiameter > Mathf.Epsilon)
            renderer.transform.localScale = Vector3.one * (diameter / currentDiameter);
    }

    private void SetTeamLandmarksActive(bool active)
    {
        bool canShow = active && trackManager != null && trackManager.TotalNodes > 0;
        for (int i = 0; i < teamLandmarkMarkers.Count; i++)
        {
            if (teamLandmarkMarkers[i].root != null)
                teamLandmarkMarkers[i].root.SetActive(canShow);
        }
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
