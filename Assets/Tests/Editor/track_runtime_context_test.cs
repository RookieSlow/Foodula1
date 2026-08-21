using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class TrackRuntimeContextTests
{
    [Test]
    public void test_context_owns_track_metadata_without_mutating_source_arrays()
    {
        var config = new TrackConfig
        {
            trackId = "indianapolis_burger",
            trackName = "Indianapolis Burger",
            country = "US",
            laps = 2,
            defaultWeather = "sunny",
            weatherPool = new[] { "sunny", "cloudy" }
        };
        var nodes = new List<TrackNode>
        {
            new TrackNode(0, 99, "Start", isStartFinish: true),
            new TrackNode(1, 4, "Turn", cornerId: 1, isApex: true)
        };
        var weatherPool = config.weatherPool;
        var context = new TrackRuntimeContext(
            config.trackId,
            config,
            nodes,
            new[] { Vector2.zero, Vector2.right },
            new[] { -0.5f, 0.5f },
            new Dictionary<int, int> { { 1, 4 } },
            new Dictionary<int, string> { { 1, "Turn" } },
            3);

        string[] copiedWeatherPool = context.WeatherPool;
        copiedWeatherPool[0] = "hot";
        nodes[1].speedLimit = 99;
        config.laneCornerSpeedLimits = new[] { 1, 1, 1, 1 };

        Assert.That(context.TotalLaps, Is.EqualTo(2));
        Assert.That(context.TrackName, Is.EqualTo("Indianapolis Burger"));
        Assert.That(context.Country, Is.EqualTo("US"));
        Assert.That(context.WeatherPool[0], Is.EqualTo(weatherPool[0]));
        Assert.That(context.TotalNodes, Is.EqualTo(2));
        Assert.That(context.GetCornerSpeedLimit(1, 3), Is.EqualTo(7));
        Assert.That(context.GetNode(1).speedLimit, Is.EqualTo(4));
    }

    [Test]
    public void test_context_preserves_traversal_and_lane_speed_boundaries()
    {
        var config = new TrackConfig
        {
            trackId = "indianapolis_burger",
            laps = 2,
            laneCornerSpeedLimits = new[] { 4, 5, 6, 7 },
            allowStartFinishLaneChange = true
        };
        var nodes = new List<TrackNode>
        {
            new TrackNode(0, 99, isStartFinish: true),
            new TrackNode(1, 4, cornerId: 1, isApex: true),
            new TrackNode(2, 99)
        };
        var context = new TrackRuntimeContext(
            config.trackId,
            config,
            nodes,
            new[] { new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(4f, 0f) },
            new[] { -0.42f, -0.14f, 0.14f, 0.42f },
            new Dictionary<int, int> { { 1, 4 } },
            new Dictionary<int, string> { { 1, "Burger Turn" } },
            3);

        HashSet<int> crossedCorners = context.GetUniqueCornersCrossed(0, 4);

        Assert.That(context.StartFinishNodeIndex, Is.EqualTo(0));
        Assert.That(context.AllowsStartFinishLaneChange, Is.True);
        Assert.That(crossedCorners, Is.EquivalentTo(new[] { 1 }));
        Assert.That(context.GetCornerSpeedLimit(1, 0), Is.EqualTo(4));
        Assert.That(context.GetCornerSpeedLimit(1, 3), Is.EqualTo(7));
        Assert.That(context.GetCornerName(1), Is.EqualTo("Burger Turn"));
        Assert.That(context.GetNode(-1).nodeIndex, Is.EqualTo(2));
    }

    [Test]
    public void test_context_normalizes_positions_and_applies_lane_offset()
    {
        var nodes = new List<TrackNode>
        {
            new TrackNode(0, 99),
            new TrackNode(1, 99),
            new TrackNode(2, 99),
            new TrackNode(3, 99)
        };
        var context = new TrackRuntimeContext(
            "silverstone_afternoon_tea",
            null,
            nodes,
            new[]
            {
                new Vector2(0f, 0f),
                new Vector2(2f, 0f),
                new Vector2(2f, 2f),
                new Vector2(0f, 2f)
            },
            new[] { -0.25f, 0.25f },
            null,
            null,
            3);

        Vector3 centerline = context.GetNodePosition(4);
        Vector3 innerLane = context.GetNodePosition(4, 0);
        Vector3 outerLane = context.GetNodePosition(-4, 1);

        Assert.That(centerline, Is.EqualTo(new Vector3(0f, 0f, 0f)));
        Assert.That(innerLane, Is.Not.EqualTo(centerline));
        Assert.That(outerLane, Is.Not.EqualTo(centerline));
        Assert.That(context.GetLaneTowardsInside(0), Is.EqualTo(0));
        Assert.That(context.GetLaneTowardsOutside(1), Is.EqualTo(1));
    }

    [Test]
    public void test_context_supports_direct_adapter_lookahead_and_path_cache_queries()
    {
        var nodes = new List<TrackNode>
        {
            new TrackNode(0, 99, isStartFinish: true),
            new TrackNode(1, 99),
            new TrackNode(2, 3, cornerId: 7, isApex: true),
            new TrackNode(3, 99),
            new TrackNode(4, 99)
        };
        var context = new TrackRuntimeContext(
            "silverstone_afternoon_tea",
            null,
            nodes,
            new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(2f, 0f),
                new Vector2(3f, 0f),
                new Vector2(4f, 0f)
            },
            null,
            new Dictionary<int, int> { { 7, 3 } },
            new Dictionary<int, string> { { 7, "Apex" } },
            3);

        HashSet<int> lookAheadCorners = context.GetUniqueCornersCrossed(1, 7);

        Assert.That(lookAheadCorners, Is.EquivalentTo(new[] { 7 }));
        Assert.That(context.GetNode(7).nodeIndex, Is.EqualTo(2));
        Assert.That(context.GetNodePosition(7), Is.EqualTo(new Vector3(2f, 0f, 0f)));
        Assert.That(context.GetCornerSpeedLimit(7, 0), Is.EqualTo(3));
    }

    [Test]
    public void test_context_supports_vehicle_spawn_and_teleport_path_queries()
    {
        var config = new TrackConfig
        {
            trackId = "indianapolis_burger",
            laneCornerSpeedLimits = new[] { 4, 5, 6, 7 },
            allowStartFinishLaneChange = true
        };
        var nodes = new List<TrackNode>
        {
            new TrackNode(0, 99, "Start/Finish", isStartFinish: true),
            new TrackNode(1, 99, "Straight"),
            new TrackNode(2, 99, "Exit")
        };
        var context = new TrackRuntimeContext(
            config.trackId,
            config,
            nodes,
            new[]
            {
                new Vector2(0f, 0f),
                new Vector2(2f, 0f),
                new Vector2(2f, 2f)
            },
            new[] { -0.42f, -0.14f, 0.14f, 0.42f },
            null,
            null,
            3);

        int playerLane = context.GetDefaultLaneIndex(false);
        int aiLane = context.GetDefaultLaneIndex(true);
        Vector3 playerStart = context.GetNodePosition(context.StartFinishNodeIndex, playerLane);
        Vector3 playerNext = context.GetNodePosition(context.StartFinishNodeIndex + 1, playerLane);

        Assert.That(context.StartFinishNodeIndex, Is.EqualTo(0));
        Assert.That(playerLane, Is.EqualTo(1));
        Assert.That(aiLane, Is.EqualTo(2));
        Assert.That(playerStart, Is.Not.EqualTo(playerNext));
        Assert.That(context.GetNode(3).nodeIndex, Is.EqualTo(0));
        Assert.That(context.AllowsStartFinishLaneChange, Is.True);
    }

    [Test]
    public void test_context_debug_overlay_queries_preserve_node_metadata_and_positions()
    {
        var nodes = new List<TrackNode>
        {
            new TrackNode(0, 99, "Start", isStartFinish: true),
            new TrackNode(1, 4, "Apex", cornerId: 2, isApex: true),
            new TrackNode(2, 99, "Exit")
        };
        var context = new TrackRuntimeContext(
            "silverstone_afternoon_tea",
            null,
            nodes,
            new[]
            {
                new Vector2(-1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 0f)
            },
            null,
            new Dictionary<int, int> { { 2, 4 } },
            new Dictionary<int, string> { { 2, "Apex" } },
            3);

        Assert.That(context.TotalNodes, Is.EqualTo(3));
        Assert.That(context.GetNode(4).nodeIndex, Is.EqualTo(1));
        Assert.That(context.GetNode(4).isApex, Is.True);
        Assert.That(context.GetNodePosition(-1), Is.EqualTo(new Vector3(1f, 0f, 0f)));
        Assert.That(context.GetNodePosition(1), Is.Not.EqualTo(Vector3.zero));
    }

    [Test]
    public void test_context_exposes_ordered_crossed_nodes_for_multi_lap_events()
    {
        var context = new TrackRuntimeContext(
            "silverstone_afternoon_tea",
            null,
            new List<TrackNode>
            {
                new TrackNode(0, 99, "Start", isStartFinish: true),
                new TrackNode(1, 99, "Straight"),
                new TrackNode(2, 99, "Pit Entry", isPitEntry: true),
                new TrackNode(3, 99, "Pit Exit", isPitExit: true)
            },
            null,
            null,
            null,
            null,
            3);

        Assert.That(context.GetCrossedNodeIndices(1, 9), Is.EqualTo(new[] { 2, 3, 0, 1, 2, 3, 0, 1 }));
        TrackTraversalEvents events = context.GetTraversalEvents(1, 9);
        Assert.That(events.CrossedStartFinishNodeIndices, Is.EqualTo(new[] { 0, 0 }));
        Assert.That(events.CrossedPitEntry, Is.True);
    }

    [Test]
    public void test_context_supports_movement_plan_and_corner_resolution_queries()
    {
        var config = new TrackConfig
        {
            trackId = "indianapolis_burger",
            laneCornerSpeedLimits = new[] { 4, 5, 6, 7 }
        };
        var nodes = new List<TrackNode>
        {
            new TrackNode(0, 99, "Start", isStartFinish: true),
            new TrackNode(1, 99, "Straight"),
            new TrackNode(2, 4, "Burger Turn", cornerId: 3, isApex: true),
            new TrackNode(3, 99, "Exit")
        };
        var context = new TrackRuntimeContext(
            config.trackId,
            config,
            nodes,
            new[]
            {
                new Vector2(0f, 0f),
                new Vector2(2f, 0f),
                new Vector2(4f, 1f),
                new Vector2(4f, 3f)
            },
            new[] { -0.42f, -0.14f, 0.14f, 0.42f },
            new Dictionary<int, int> { { 3, 4 } },
            new Dictionary<int, string> { { 3, "Burger Turn" } },
            3);

        HashSet<int> crossedCorners = context.GetUniqueCornersCrossed(1, 6);

        Assert.That(context.TotalNodes, Is.EqualTo(4));
        Assert.That(crossedCorners, Is.EquivalentTo(new[] { 3 }));
        Assert.That(context.GetCornerSpeedLimit(3, 3), Is.EqualTo(7));
        Assert.That(context.GetCornerName(3), Is.EqualTo("Burger Turn"));
        Assert.That(context.GetNode(6).nodeIndex, Is.EqualTo(2));
    }

    [Test]
    public void test_context_exposes_pit_path_snapshot_for_pit_rules()
    {
        var nodes = new List<TrackNode>
        {
            new TrackNode(0, 99, "Start", isStartFinish: true),
            new TrackNode(1, 99, "Straight"),
            new TrackNode(2, 99, "Pit Entry", isPitEntry: true),
            new TrackNode(3, 99, "Pit Lane"),
            new TrackNode(4, 99, "Pit Exit", isPitExit: true),
            new TrackNode(5, 99, "Finish Straight")
        };
        var context = new TrackRuntimeContext(
            "shanghai_dim_sum",
            null,
            nodes,
            null,
            null,
            null,
            null,
            3);

        nodes[2].isPitEntry = false;
        nodes[4].isPitExit = false;

        Assert.That(PitLaneRules.HasPitLane(context.Nodes), Is.True);
        Assert.That(PitLaneRules.FindPitEntry(context.Nodes), Is.EqualTo(2));
        Assert.That(PitLaneRules.FindPitExit(context.Nodes), Is.EqualTo(4));
        Assert.That(PitLaneRules.CrossedPitEntry(1, 3, context.Nodes), Is.True);
        Assert.That(PitLaneRules.GetPitExitPosition(context.Nodes), Is.EqualTo(1));
        Assert.That(context.GetNode(2).isPitEntry, Is.True);
        Assert.That(context.GetNode(4).isPitExit, Is.True);
    }
}
