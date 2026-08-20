using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Runtime node overlay for track authoring and alignment checks.
/// Press F8 during a race to show/hide numbered nodes without changing the
/// gameplay path, background, masks or lane presentation.
/// </summary>
public sealed class TrackDebugOverlay : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.F8;
    [SerializeField] private float markerScale = 0.42f;
    [SerializeField] private float labelOffset = 0.3f;

    private TrackManager trackManager;
    private readonly List<GameObject> markers = new List<GameObject>();
    private bool visible;

    public void Initialize(TrackManager manager)
    {
        trackManager = manager;
    }

    private void Awake()
    {
        if (trackManager == null)
            trackManager = GetComponent<TrackManager>();
    }

    private void Start()
    {
        if (trackManager != null)
            SetVisible(trackManager.showDebugTrackNodesInPlay);
    }

    private void Update()
    {
        if (!Application.isPlaying || trackManager == null)
            return;

        if (Input.GetKeyDown(toggleKey))
            SetVisible(!visible);
    }

    public void SetVisible(bool shouldShow)
    {
        visible = shouldShow;
        ClearMarkers();
        if (!visible || trackManager == null)
            return;

        TrackRuntimeContext track = trackManager.Runtime;
        if (track == null || track.TotalNodes <= 0)
            return;

        Sprite markerSprite = trackManager.nodePrefab != null
            ? trackManager.nodePrefab.GetComponent<SpriteRenderer>()?.sprite
            : null;
        TMP_FontAsset font = FindObjectOfType<TMP_Text>()?.font;

        for (int i = 0; i < track.TotalNodes; i++)
        {
            TrackNode node = track.GetNode(i);
            GameObject marker = new GameObject($"TrackDebugNode_{i}");
            marker.transform.position = track.GetNodePosition(i);
            marker.transform.localScale = Vector3.one * markerScale;

            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = markerSprite;
            renderer.color = TrackPresentationRules.GetNodeColor(
                node,
                trackManager.straightNodeColor,
                trackManager.cornerNodeColor,
                trackManager.apexNodeColor,
                trackManager.startFinishNodeColor);
            renderer.sortingOrder = 20;

            GameObject labelObject = new GameObject("Index");
            labelObject.transform.SetParent(marker.transform, false);
            labelObject.transform.localPosition = Vector3.up * labelOffset;
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = i.ToString();
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(1.5f, 0.85f);
            label.fontSize = 2.8f;
            label.fontStyle = FontStyles.Bold;
            label.enableWordWrapping = false;
            label.color = Color.black;
            label.outlineWidth = 0.22f;
            label.outlineColor = Color.white;
            label.sortingOrder = 21;
            if (font != null)
                label.font = font;

            markers.Add(marker);
        }
    }

    private void ClearMarkers()
    {
        foreach (GameObject marker in markers)
        {
            if (marker != null)
                Destroy(marker);
        }
        markers.Clear();
    }

    private void OnDestroy()
    {
        ClearMarkers();
    }
}
