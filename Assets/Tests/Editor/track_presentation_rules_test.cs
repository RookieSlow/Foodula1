using System.Collections.Generic;
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
        Assert.That(normalOffsets, Is.EqualTo(new[] { -0.14f, 0.14f }).Within(0.0001f));

        float[] ovalOffsets = TrackPresentationRules.CalculateCenteredLaneOffsets(4, 0.28f);
        Assert.That(ovalOffsets, Is.EqualTo(new[] { -0.42f, -0.14f, 0.14f, 0.42f }).Within(0.0001f));
    }

    [Test]
    public void test_standard_tracks_keep_inner_lane_until_parallel()
    {
        Assert.That(TrackPresentationRules.GetInnerLaneIndex("silverstone_afternoon_tea"), Is.EqualTo(0));
        Assert.That(TrackPresentationRules.GetOuterLaneIndex("silverstone_afternoon_tea"), Is.EqualTo(1));
        Assert.That(
            TrackPresentationRules.GetStandardTrafficLaneIndex("silverstone_afternoon_tea", false),
            Is.EqualTo(0));
        Assert.That(
            TrackPresentationRules.GetStandardTrafficLaneIndex("silverstone_afternoon_tea", true),
            Is.EqualTo(1));
    }

    [Test]
    public void test_indianapolis_inside_lane_is_distinct_and_lane_selection_is_preserved()
    {
        Assert.That(TrackPresentationRules.IsIndianapolis("indianapolis_burger"), Is.True);
        Assert.That(TrackPresentationRules.GetInnerLaneIndex("indianapolis_burger"), Is.EqualTo(0));
        Assert.That(TrackPresentationRules.GetOuterLaneIndex("indianapolis_burger"), Is.EqualTo(3));
        Assert.That(TrackPresentationRules.GetStandardTrafficLaneIndex("indianapolis_burger", false), Is.EqualTo(0));
    }

    [Test]
    public void test_indianapolis_corner_limits_rise_from_inside_to_outside()
    {
        Assert.That(TrackPresentationRules.AllowsStartFinishLaneChange("indianapolis_burger"), Is.True);
        Assert.That(TrackPresentationRules.GetLaneRankFromInside("indianapolis_burger", 0), Is.EqualTo(0));
        Assert.That(TrackPresentationRules.GetLaneRankFromInside("indianapolis_burger", 3), Is.EqualTo(3));

        Assert.That(TrackPresentationRules.GetLaneAdjustedCornerSpeedLimit("indianapolis_burger", 4, 0), Is.EqualTo(4));
        Assert.That(TrackPresentationRules.GetLaneAdjustedCornerSpeedLimit("indianapolis_burger", 4, 1), Is.EqualTo(5));
        Assert.That(TrackPresentationRules.GetLaneAdjustedCornerSpeedLimit("indianapolis_burger", 4, 2), Is.EqualTo(6));
        Assert.That(TrackPresentationRules.GetLaneAdjustedCornerSpeedLimit("indianapolis_burger", 4, 3), Is.EqualTo(7));
        Assert.That(TrackPresentationRules.GetLaneAdjustedCornerSpeedLimit("silverstone_afternoon_tea", 3, 0), Is.EqualTo(3));
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

    [Test]
    public void test_player_cell_text_is_one_based_and_wraps()
    {
        Assert.That(TrackPresentationRules.FormatCellPosition(0, 60), Is.EqualTo("格 1/60"));
        Assert.That(TrackPresentationRules.FormatCellPosition(59, 60), Is.EqualTo("格 60/60"));
        Assert.That(TrackPresentationRules.FormatCellPosition(60, 60), Is.EqualTo("格 1/60"));
        Assert.That(TrackPresentationRules.FormatCellPosition(-1, 60), Is.EqualTo("格 60/60"));
    }

    [Test]
    public void test_local_cell_labels_sample_dense_tracks_but_keep_center_and_edges()
    {
        Assert.That(TrackPresentationRules.CalculateLocalLabelStride(0.9f), Is.EqualTo(1));
        Assert.That(TrackPresentationRules.CalculateLocalLabelStride(0.34f), Is.EqualTo(3));
        Assert.That(TrackPresentationRules.ShouldShowLocalCellLabel(0, 3, 6), Is.True);
        Assert.That(TrackPresentationRules.ShouldShowLocalCellLabel(6, 3, 6), Is.True);
        Assert.That(TrackPresentationRules.ShouldShowLocalCellLabel(-3, 3, 6), Is.True);
        Assert.That(TrackPresentationRules.ShouldShowLocalCellLabel(2, 3, 6), Is.False);
    }

    [Test]
    public void test_smooth_corner_path_preserves_authored_endpoints_without_mutating_nodes()
    {
        Vector2[] path =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(2f, 1f),
            new Vector2(2f, 2f),
            new Vector2(1f, 3f),
            new Vector2(0f, 3f)
        };
        Vector2[] original = (Vector2[])path.Clone();

        Vector2[] smooth = TrackPresentationRules.BuildSmoothCornerPath(
            path,
            new[] { 1, 2, 3 },
            4);

        Assert.That(smooth.Length, Is.EqualTo(13));
        Assert.That(smooth[0], Is.EqualTo(path[1]));
        Assert.That(smooth[smooth.Length - 1], Is.EqualTo(path[4]));
        Assert.That(path, Is.EqualTo(original));
        Assert.That(smooth[5], Is.Not.EqualTo(path[2]));
    }

    [Test]
    public void test_corner_level_and_closest_node_rules_match_visual_semantics()
    {
        Assert.That(TrackPresentationRules.InferCornerLevel(4), Is.EqualTo(1));
        Assert.That(TrackPresentationRules.InferCornerLevel(3), Is.EqualTo(2));
        Assert.That(TrackPresentationRules.InferCornerLevel(2), Is.EqualTo(3));

        Vector2[] positions =
        {
            new Vector2(0f, 0f),
            new Vector2(2f, 0f),
            new Vector2(4f, 0f)
        };
        Assert.That(
            TrackPresentationRules.FindClosestNodeIndex(positions, new Vector2(2.3f, 0.1f)),
            Is.EqualTo(1));
    }

    [Test]
    public void test_all_authored_tracks_build_valid_smooth_corner_paths()
    {
        string[] trackIds =
        {
            "silverstone_afternoon_tea",
            "nurburgring_bier",
            "monza_pasta",
            "indianapolis_burger",
            "shanghai_dim_sum",
            "suzuka_sushi",
            "le_mans_old_mulsanne",
            "nurburgring_24h_endurance",
            "fallback_42"
        };

        foreach (string trackId in trackIds)
        {
            TrackConfig config = TrackDataLoader.LoadConfig(trackId);
            Assert.That(config, Is.Not.Null, $"Track config missing: {trackId}");
            Vector2[] positions = TrackDataLoader.ConfigToWorldPositions(config, 30f, 18f);
            List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(config);
            var corners = new Dictionary<int, List<int>>();

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].cornerId <= 0)
                    continue;
                if (!corners.TryGetValue(nodes[i].cornerId, out List<int> indices))
                {
                    indices = new List<int>();
                    corners.Add(nodes[i].cornerId, indices);
                }
                indices.Add(i);
            }

            Assert.That(corners.Count, Is.GreaterThan(0), $"No corners found: {trackId}");
            foreach (KeyValuePair<int, List<int>> corner in corners)
            {
                Vector2[] smooth = TrackPresentationRules.BuildSmoothCornerPath(
                    positions,
                    corner.Value,
                    5);
                Assert.That(smooth.Length, Is.EqualTo(corner.Value.Count * 5 + 1),
                    $"Unexpected sample count: {trackId}, corner {corner.Key}");
                for (int i = 0; i < smooth.Length; i++)
                {
                    Assert.That(float.IsNaN(smooth[i].x) || float.IsNaN(smooth[i].y), Is.False,
                        $"NaN sample: {trackId}, corner {corner.Key}");
                    Assert.That(float.IsInfinity(smooth[i].x) || float.IsInfinity(smooth[i].y), Is.False,
                        $"Infinite sample: {trackId}, corner {corner.Key}");
                }
            }
        }
    }
}
