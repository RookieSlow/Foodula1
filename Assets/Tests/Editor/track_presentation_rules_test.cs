using NUnit.Framework;
using UnityEngine;

public class TrackPresentationRulesTests
{
    [Test]
    public void test_lane_counts_and_offsets_match_track_classes()
    {
        Assert.That(TrackPresentationRules.GetLaneCount("silverstone_afternoon_tea"), Is.EqualTo(2));
        Assert.That(TrackPresentationRules.GetLaneCount("indianapolis_burger"), Is.EqualTo(4));

        float[] normalOffsets = TrackPresentationRules.CalculateCenteredLaneOffsets(2, 0.28f);
        Assert.That(normalOffsets, Is.EqualTo(new[] { -0.14f, 0.14f }));

        float[] ovalOffsets = TrackPresentationRules.CalculateCenteredLaneOffsets(4, 0.28f);
        Assert.That(ovalOffsets, Is.EqualTo(new[] { -0.42f, -0.14f, 0.14f, 0.42f }));
    }

    [Test]
    public void test_node_colors_distinguish_apex_corner_and_straight()
    {
        Color straight = Color.white;
        Color corner = new Color(1f, 0.5f, 0f, 1f);
        Color apex = Color.red;
        Color startFinish = Color.green;

        Assert.That(
            TrackPresentationRules.GetNodeColor(
                new TrackNode(0, 99),
                straight,
                corner,
                apex,
                startFinish),
            Is.EqualTo(straight));
        Assert.That(
            TrackPresentationRules.GetNodeColor(
                new TrackNode(1, 3, cornerId: 1),
                straight,
                corner,
                apex,
                startFinish),
            Is.EqualTo(corner));
        Assert.That(
            TrackPresentationRules.GetNodeColor(
                new TrackNode(2, 3, cornerId: 1, isApex: true),
                straight,
                corner,
                apex,
                startFinish),
            Is.EqualTo(apex));
        Assert.That(
            TrackPresentationRules.GetNodeColor(
                new TrackNode(3, 99, isStartFinish: true),
                straight,
                corner,
                apex,
                startFinish),
            Is.EqualTo(startFinish));
    }
    [Test]
    public void test_median_neighbor_distance_ignores_single_long_outlier()
    {
        Vector2[] positions =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(2f, 0f),
            new Vector2(3f, 0f),
            new Vector2(10f, 0f)
        };

        float result = TrackPresentationRules.CalculateMedianNeighborDistance(positions);

        Assert.That(result, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void test_node_scale_reduces_dense_visual_to_spacing_ratio()
    {
        float result = TrackPresentationRules.CalculateNodeScaleMultiplier(
            0.3f,
            0.7f,
            0.65f,
            0.2f);

        Assert.That(result, Is.EqualTo(0.27857f).Within(0.001f));
    }

    [Test]
    public void test_node_scale_preserves_existing_size_on_open_spacing()
    {
        float result = TrackPresentationRules.CalculateNodeScaleMultiplier(
            1.5f,
            0.7f,
            0.65f,
            0.2f);

        Assert.That(result, Is.EqualTo(1f));
    }
}
