using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure sizing rules that keep track visuals legible across different cell densities.
/// </summary>
public static class TrackPresentationRules
{
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

