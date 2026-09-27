using UnityEngine;

/// <summary>
/// Tracks the pointer-drag gesture used to take manual control of the race camera.
/// Input sampling stays in the Unity adapter; this small state boundary keeps
/// arm, held-delta, threshold, and release behavior independently testable.
/// </summary>
public sealed class RaceCameraPointerDragState
{
    private const float MinimumDragDeltaSquared = 0.25f;

    private Vector2 lastPointerPosition;

    public bool IsArmed { get; private set; }

    public void BeginDrag(Vector2 pointerPosition)
    {
        lastPointerPosition = pointerPosition;
        IsArmed = true;
    }

    /// <summary>
    /// Returns true only for a held, armed drag that exceeds the original
    /// quarter-pixel movement threshold. Sub-threshold samples still update the
    /// origin, matching the camera's prior per-frame input behavior.
    /// </summary>
    public bool TryGetDragDelta(
        Vector2 pointerPosition,
        bool pointerButtonHeld,
        out Vector2 dragDelta)
    {
        dragDelta = Vector2.zero;
        if (!IsArmed || !pointerButtonHeld)
            return false;

        Vector2 sampledDelta = pointerPosition - lastPointerPosition;
        lastPointerPosition = pointerPosition;
        if (sampledDelta.sqrMagnitude >= MinimumDragDeltaSquared)
        {
            dragDelta = sampledDelta;
            return true;
        }

        return false;
    }

    public void EndDrag()
    {
        IsArmed = false;
        lastPointerPosition = Vector2.zero;
    }
}
