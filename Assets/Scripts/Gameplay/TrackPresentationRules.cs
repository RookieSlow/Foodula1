using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure sizing rules that keep track visuals legible across different cell densities.
/// </summary>
public static class TrackPresentationRules
{
    public const string IndianapolisTrackId = "indianapolis_burger";

    public static int WrapNodeIndex(int index, int totalNodes)
    {
        if (totalNodes <= 0)
            return 0;

        int wrapped = index % totalNodes;
        return wrapped < 0 ? wrapped + totalNodes : wrapped;
    }

    public static string FormatCellPosition(int zeroBasedIndex, int totalNodes)
    {
        if (totalNodes <= 0)
            return "格 0/0";

        return $"格 {WrapNodeIndex(zeroBasedIndex, totalNodes) + 1}/{totalNodes}";
    }

    public static int CalculateLocalLabelStride(float medianSpacing, float minimumLabelSpacing = 0.72f)
    {
        if (medianSpacing <= Mathf.Epsilon || minimumLabelSpacing <= Mathf.Epsilon)
            return 1;

        return Mathf.Clamp(Mathf.CeilToInt(minimumLabelSpacing / medianSpacing), 1, 3);
    }

    public static bool ShouldShowLocalCellLabel(int relativeOffset, int stride, int radius)
    {
        int safeStride = Mathf.Max(1, stride);
        return relativeOffset == 0 || Mathf.Abs(relativeOffset) == Mathf.Max(0, radius)
            || Mathf.Abs(relativeOffset) % safeStride == 0;
    }

    public static int FindClosestNodeIndex(IReadOnlyList<Vector2> positions, Vector2 point)
    {
        if (positions == null || positions.Count == 0)
            return -1;

        int closest = 0;
        float bestDistance = (positions[0] - point).sqrMagnitude;
        for (int i = 1; i < positions.Count; i++)
        {
            float distance = (positions[i] - point).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = i;
            }
        }

        return closest;
    }

    public static int InferCornerLevel(int speedLimit)
    {
        if (speedLimit <= 2)
            return 3;
        if (speedLimit == 3)
            return 2;
        return 1;
    }

    public static Color GetCornerLevelColor(
        int cornerLevel,
        Color levelOne,
        Color levelTwo,
        Color levelThree)
    {
        if (cornerLevel >= 3)
            return levelThree;
        if (cornerLevel == 2)
            return levelTwo;
        return levelOne;
    }

    /// <summary>
    /// Builds a Catmull-Rom render path through a contiguous corner range.
    /// Gameplay nodes remain untouched; the returned samples are presentation-only.
    /// </summary>
    public static Vector2[] BuildSmoothCornerPath(
        IReadOnlyList<Vector2> closedPath,
        IReadOnlyList<int> cornerIndices,
        int subdivisionsPerSegment)
    {
        if (closedPath == null || closedPath.Count < 2 || cornerIndices == null || cornerIndices.Count == 0)
            return new Vector2[0];

        int subdivisions = Mathf.Clamp(subdivisionsPerSegment, 1, 8);
        var samples = new List<Vector2>(cornerIndices.Count * subdivisions + 1);

        for (int i = 0; i < cornerIndices.Count; i++)
        {
            int current = WrapNodeIndex(cornerIndices[i], closedPath.Count);
            int next = i + 1 < cornerIndices.Count
                ? WrapNodeIndex(cornerIndices[i + 1], closedPath.Count)
                : WrapNodeIndex(current + 1, closedPath.Count);
            Vector2 p0 = closedPath[WrapNodeIndex(current - 1, closedPath.Count)];
            Vector2 p1 = closedPath[current];
            Vector2 p2 = closedPath[next];
            Vector2 p3 = closedPath[WrapNodeIndex(next + 1, closedPath.Count)];

            for (int step = 0; step < subdivisions; step++)
            {
                float t = step / (float)subdivisions;
                Vector2 sample = EvaluateCatmullRom(p0, p1, p2, p3, t);
                float padding = Vector2.Distance(p1, p2) * 0.2f;
                sample.x = Mathf.Clamp(sample.x, Mathf.Min(p1.x, p2.x) - padding, Mathf.Max(p1.x, p2.x) + padding);
                sample.y = Mathf.Clamp(sample.y, Mathf.Min(p1.y, p2.y) - padding, Mathf.Max(p1.y, p2.y) + padding);
                samples.Add(sample);
            }
        }

        int last = WrapNodeIndex(cornerIndices[cornerIndices.Count - 1], closedPath.Count);
        samples.Add(closedPath[WrapNodeIndex(last + 1, closedPath.Count)]);
        return samples.ToArray();
    }

    private static Vector2 EvaluateCatmullRom(
        Vector2 p0,
        Vector2 p1,
        Vector2 p2,
        Vector2 p3,
        float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

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
