using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure geometry rules used by the race camera and minimap.
/// </summary>
public static class RaceCameraRules
{
    /// <summary>
    /// Wraps an index into the valid range for a closed track.
    /// </summary>
    public static int WrapIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        int wrapped = index % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }

    /// <summary>
    /// Finds the track position closest to a world-space point.
    /// </summary>
    public static int FindClosestPositionIndex(
        IReadOnlyList<Vector3> positions,
        Vector3 worldPosition)
    {
        if (positions == null || positions.Count == 0)
        {
            return -1;
        }

        int closestIndex = 0;
        float closestDistance = (positions[0] - worldPosition).sqrMagnitude;

        for (int i = 1; i < positions.Count; i++)
        {
            float distance = (positions[i] - worldPosition).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    /// <summary>
    /// Calculates bounds for a wrapped window around a track position.
    /// </summary>
    public static Bounds CalculateWindowBounds(
        IReadOnlyList<Vector3> positions,
        int centerIndex,
        int cellsBehind,
        int cellsAhead)
    {
        if (positions == null || positions.Count == 0)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        int safeBehind = Mathf.Max(0, cellsBehind);
        int safeAhead = Mathf.Max(0, cellsAhead);
        Vector3 first = positions[WrapIndex(centerIndex - safeBehind, positions.Count)];
        Bounds bounds = new Bounds(first, Vector3.zero);

        for (int offset = -safeBehind + 1; offset <= safeAhead; offset++)
        {
            bounds.Encapsulate(positions[WrapIndex(centerIndex + offset, positions.Count)]);
        }

        return bounds;
    }

    /// <summary>
    /// Calculates bounds containing every supplied track position.
    /// </summary>
    public static Bounds CalculateTrackBounds(IReadOnlyList<Vector3> positions)
    {
        if (positions == null || positions.Count == 0)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Bounds bounds = new Bounds(positions[0], Vector3.zero);
        for (int i = 1; i < positions.Count; i++)
        {
            bounds.Encapsulate(positions[i]);
        }

        return bounds;
    }

    /// <summary>
    /// Calculates the orthographic half-height required to fit bounds.
    /// </summary>
    public static float CalculateOrthographicSize(
        Bounds bounds,
        float aspect,
        float paddingMultiplier,
        float minimumSize)
    {
        float safeAspect = Mathf.Max(0.01f, aspect);
        float safePadding = Mathf.Max(1f, paddingMultiplier);
        float halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / safeAspect);
        return Mathf.Max(Mathf.Max(0.01f, minimumSize), halfHeight * safePadding);
    }

    /// <summary>
    /// Converts a screen-space pointer drag into an opposite world-space camera offset.
    /// </summary>
    public static Vector3 CalculateDragWorldOffset(
        Vector2 screenDelta,
        float orthographicSize,
        float viewportPixelHeight,
        float sensitivity)
    {
        float safeHeight = Mathf.Max(1f, viewportPixelHeight);
        float worldUnitsPerPixel = Mathf.Max(0.01f, orthographicSize) * 2f / safeHeight;
        float safeSensitivity = Mathf.Max(0.01f, sensitivity);
        return new Vector3(
            -screenDelta.x * worldUnitsPerPixel * safeSensitivity,
            -screenDelta.y * worldUnitsPerPixel * safeSensitivity,
            0f);
    }

    /// <summary>
    /// Applies exponential mouse-wheel zoom and clamps it to the playable range.
    /// Positive wheel input zooms in.
    /// </summary>
    public static float CalculateScrolledOrthographicSize(
        float currentSize,
        float scrollDelta,
        float sensitivity,
        float minimumSize,
        float maximumSize)
    {
        float safeMinimum = Mathf.Max(0.01f, minimumSize);
        float safeMaximum = Mathf.Max(safeMinimum, maximumSize);
        float factor = Mathf.Exp(-scrollDelta * Mathf.Max(0.01f, sensitivity));
        return Mathf.Clamp(currentSize * factor, safeMinimum, safeMaximum);
    }
}

/// <summary>
/// Turn-scoped camera ownership state. Manual input blocks automatic focus until
/// the next race turn begins.
/// </summary>
public sealed class RaceCameraFocusState
{
    public bool ManualOverrideThisTurn { get; private set; }

    public bool AllowsAutomaticFocus => !ManualOverrideThisTurn;

    public void BeginTurn()
    {
        ManualOverrideThisTurn = false;
    }

    public void TakeManualControl()
    {
        ManualOverrideThisTurn = true;
    }
}
