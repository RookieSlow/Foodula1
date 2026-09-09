using TMPro;
using UnityEngine;

/// <summary>Clickable world-space limit label owned by TrackManager.</summary>
public sealed class CornerSpeedLimitLabel : MonoBehaviour
{
    private TrackManager trackManager;
    private TextMeshPro label;

    public int CornerId { get; private set; }
    public int LaneIndex { get; private set; }
    public int BaseLimit { get; private set; }

    public void Initialize(
        TrackManager owner,
        TextMeshPro target,
        int cornerId,
        int laneIndex,
        int baseLimit)
    {
        trackManager = owner;
        label = target;
        CornerId = cornerId;
        LaneIndex = laneIndex;
        BaseLimit = baseLimit;
    }

    public void SetDisplayedLimit(int limit)
    {
        if (label != null)
            label.text = limit.ToString();
    }

    private void OnMouseUpAsButton()
    {
        trackManager?.HandleCornerLimitLabelClicked(CornerId, LaneIndex);
    }
}
