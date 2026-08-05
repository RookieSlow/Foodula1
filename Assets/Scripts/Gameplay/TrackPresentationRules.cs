using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure sizing rules that keep track visuals legible across different cell densities.
/// </summary>
public static class TrackPresentationRules
{
    public const string IndianapolisTrackId = "indianapolis_burger";

    public static int GetLaneCount(string trackId)
    {
        return trackId == IndianapolisTrackId ? 4 : 2;
    }

    public static bool IsIndianapolis(string trackId)
    {
        return trackId == IndianapolisTrackId;
    }

    /// <summary>
    /// Returns the lane used by default on ordinary clockwise layouts. The
    /// generated normals put the inside of those layouts on lane 0.
    /// Indianapolis uses the same clockwise winding in Unity world
    /// coordinates, so its inside lane is also lane 0.
    /// </summary>
    public static int GetInnerLaneIndex(string trackId)
    {
        return 0;
    }

    public static int GetOuterLaneIndex(string trackId)
    {
        return GetLaneCount(trackId) - 1;
    }

    /// <summary>
    /// Ordinary tracks keep all cars on the inside unless two cars share a
    /// track cell. The trailing car then uses the outside lane to render a
    /// side-by-side pass.
    /// </summary>
    public static int GetStandardTrafficLaneIndex(string trackId, bool isTrailingInParallel)
    {
        return isTrailingInParallel
            ? GetOuterLaneIndex(trackId)
            : GetInnerLaneIndex(trackId);
    }

    public static bool AllowsStartFinishLaneChange(string trackId)
    {
        return IsIndianapolis(trackId);
    }

    /// <summary>
    /// Converts the renderer's lane index to an inner-to-outer rank. The
    /// Indianapolis path is clockwise in Unity world coordinates, and its
    /// centered lane offsets therefore place lane 0 inside and lane 3 outside.
    /// </summary>
    public static int GetLaneRankFromInside(string trackId, int laneIndex)
    {
        int laneCount = GetLaneCount(trackId);
        int safeLane = Mathf.Clamp(laneIndex, 0, laneCount - 1);
        return trackId == IndianapolisTrackId ? safeLane : 0;
    }

    public static int GetLaneAdjustedCornerSpeedLimit(
        string trackId,
        int baseLimit,
        int laneIndex)
    {
        if (trackId != IndianapolisTrackId)
            return baseLimit;

        int laneCount = GetLaneCount(trackId);
        int outerLaneLimit = Mathf.Max(1, baseLimit + (laneCount - 1));
        int innerLaneLimit = Mathf.Max(1, outerLaneLimit - (laneCount - 1));
        return innerLaneLimit + GetLaneRankFromInside(trackId, laneIndex);
    }

    public static float[] CalculateCenteredLaneOffsets(int laneCount, float laneSpacing)
    {
        int safeLaneCount = Mathf.Max(1, laneCount);
        float safeSpacing = Mathf.Max(0f, laneSpacing);
        float[] offsets = new float[safeLaneCount];
        float center = (safeLaneCount - 1) * 0.5f;
        for (int i = 0; i < safeLaneCount; i++)
        {
            offsets[i] = (i - center) * safeSpacing;
        }
        return offsets;
    }

    /// <summary>
    /// Maps track-node gameplay semantics to their presentation color.
    /// Start/finish remains a separate landmark; apex nodes are the only
    /// corner nodes shown in red because they trigger corner resolution.
    /// </summary>
    public static Color GetNodeColor(
        TrackNode node,
        Color straightColor,
        Color cornerColor,
        Color apexColor,
        Color startFinishColor)
    {
        if (node == null)
        {
            return straightColor;
        }

        if (node.isStartFinish)
        {
            return startFinishColor;
        }

        if (node.cornerId <= 0)
        {
            return straightColor;
        }

        return node.isApex ? apexColor : cornerColor;
    }
    /// <summary>
    /// Returns the median distance between neighboring positions on a closed track.
    /// </summary>
    public static float CalculateMedianNeighborDistance(IReadOnlyList<Vector2> positions)
    {
        if (positions == null || positions.Count < 2)
        {
            return 0f;
        }

        var distances = new List<float>(positions.Count);
        for (int i = 0; i < positions.Count; i++)
        {
            int next = (i + 1) % positions.Count;
            distances.Add(Vector2.Distance(positions[i], positions[next]));
        }

        distances.Sort();
        int middle = distances.Count / 2;
        if (distances.Count % 2 == 1)
        {
            return distances[middle];
        }

        return (distances[middle - 1] + distances[middle]) * 0.5f;
    }

    /// <summary>
    /// Calculates a scale multiplier that limits a visual to part of the cell spacing.
    /// </summary>
    public static float CalculateNodeScaleMultiplier(
        float medianSpacing,
        float currentVisualDiameter,
        float spacingFillRatio,
        float minimumMultiplier)
    {
        if (medianSpacing <= 0f || currentVisualDiameter <= 0f)
        {
            return 1f;
        }

        float desiredDiameter = medianSpacing * Mathf.Clamp01(spacingFillRatio);
        float multiplier = desiredDiameter / currentVisualDiameter;
        return Mathf.Clamp(multiplier, Mathf.Clamp01(minimumMultiplier), 1f);
    }
}
