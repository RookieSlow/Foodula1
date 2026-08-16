using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation-safe sampling rules for closed track centerlines.
/// Track JSON stores authored landmark positions, while gameplay benefits from
/// even placement inside each straight run. Re-sampling is constrained to
/// those runs so authored corners, apexes and pit landmarks stay on the
/// background artwork.
/// </summary>
public static class TrackLayoutRules
{
    /// <summary>
    /// Re-samples only the non-landmark runs between authored anchors. Corner,
    /// start/finish and pit nodes remain exactly at their JSON coordinates.
    /// </summary>
    public static Vector2[] ResampleAnchoredPath(
        IReadOnlyList<Vector2> source,
        IReadOnlyList<bool> anchors)
    {
        if (source == null || source.Count <= 2 || anchors == null || anchors.Count != source.Count)
            return Copy(source);

        Vector2[] result = Copy(source);
        List<int> anchorIndices = new List<int>();
        for (int i = 0; i < anchors.Count; i++)
        {
            if (anchors[i])
                anchorIndices.Add(i);
        }

        if (anchorIndices.Count == 0)
            return ResampleClosedPath(source);

        for (int anchorOrdinal = 0; anchorOrdinal < anchorIndices.Count; anchorOrdinal++)
        {
            int start = anchorIndices[anchorOrdinal];
            int end = anchorIndices[(anchorOrdinal + 1) % anchorIndices.Count];
            int interiorCount = (end - start - 1 + source.Count) % source.Count;
            if (interiorCount <= 0)
                continue;

            int pointCount = interiorCount + 2;
            Vector2[] run = new Vector2[pointCount];
            for (int i = 0; i < pointCount; i++)
                run[i] = source[(start + i) % source.Count];

            float[] lengths = new float[pointCount - 1];
            float[] cumulative = new float[pointCount];
            float total = 0f;
            for (int i = 0; i < lengths.Length; i++)
            {
                lengths[i] = Vector2.Distance(run[i], run[i + 1]);
                total += lengths[i];
                cumulative[i + 1] = total;
            }

            if (total <= Mathf.Epsilon)
                continue;

            for (int interior = 1; interior <= interiorCount; interior++)
            {
                float target = total * interior / (interiorCount + 1);
                int segment = 0;
                while (segment < lengths.Length - 1 && cumulative[segment + 1] < target)
                    segment++;

                float t = lengths[segment] > Mathf.Epsilon
                    ? (target - cumulative[segment]) / lengths[segment]
                    : 0f;
                result[(start + interior) % source.Count] = Vector2.Lerp(
                    run[segment], run[segment + 1], Mathf.Clamp01(t));
            }
        }

        return result;
    }

    public static Vector2[] ResampleClosedPath(IReadOnlyList<Vector2> source)
    {
        if (source == null || source.Count <= 2)
            return Copy(source);

        int count = source.Count;
        float[] segmentLengths = new float[count];
        float[] cumulative = new float[count + 1];
        float totalLength = 0f;

        for (int i = 0; i < count; i++)
        {
            Vector2 from = source[i];
            Vector2 to = source[(i + 1) % count];
            float length = Vector2.Distance(from, to);
            segmentLengths[i] = length;
            totalLength += length;
            cumulative[i + 1] = totalLength;
        }

        if (totalLength <= Mathf.Epsilon)
            return Copy(source);

        Vector2[] result = new Vector2[count];
        float spacing = totalLength / count;
        int segmentIndex = 0;

        for (int i = 0; i < count; i++)
        {
            float targetDistance = spacing * i;
            while (segmentIndex < count - 1 && cumulative[segmentIndex + 1] < targetDistance)
                segmentIndex++;

            float segmentLength = segmentLengths[segmentIndex];
            float t = segmentLength > Mathf.Epsilon
                ? (targetDistance - cumulative[segmentIndex]) / segmentLength
                : 0f;
            t = Mathf.Clamp01(t);

            Vector2 from = source[segmentIndex];
            Vector2 to = source[(segmentIndex + 1) % count];
            result[i] = Vector2.Lerp(from, to, t);
        }

        return result;
    }

    public static float CalculateClosedPathLength(IReadOnlyList<Vector2> points)
    {
        if (points == null || points.Count < 2)
            return 0f;

        float total = 0f;
        for (int i = 0; i < points.Count; i++)
            total += Vector2.Distance(points[i], points[(i + 1) % points.Count]);
        return total;
    }

    public static float CalculateMaxClosedSegmentLength(IReadOnlyList<Vector2> points)
    {
        if (points == null || points.Count < 2)
            return 0f;

        float max = 0f;
        for (int i = 0; i < points.Count; i++)
            max = Mathf.Max(max, Vector2.Distance(points[i], points[(i + 1) % points.Count]));
        return max;
    }

    private static Vector2[] Copy(IReadOnlyList<Vector2> source)
    {
        if (source == null)
            return new Vector2[0];

        Vector2[] copy = new Vector2[source.Count];
        for (int i = 0; i < source.Count; i++)
            copy[i] = source[i];
        return copy;
    }
}
