using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// TrackDataLoader 单元测试 — JSON 解析、ConfigToNodes 转换、坐标映射、弯道映射。
/// 使用 Resources/Configs/Tracks/ 下的真实赛道数据。
/// </summary>
public class TrackDataLoaderTest
{
    [Test]
    public void test_load_config_silverstone_parses_core_fields()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("silverstone_afternoon_tea");

        Assert.IsNotNull(cfg, "silverstone_afternoon_tea.json 应存在");
        Assert.AreEqual("silverstone_afternoon_tea", cfg.trackId);
        Assert.IsTrue(cfg.cells.Length > 0);
        Assert.AreEqual(cfg.gameCellCount, cfg.cells.Length);
        Assert.IsTrue(cfg.laps > 0);
    }

    [Test]
    public void test_load_config_missing_track_returns_null()
    {
        LogAssert.Expect(LogType.Error,
            "[TrackDataLoader] Track config not found: Resources/Configs/Tracks/no_such_track.json");
        TrackConfig cfg = TrackDataLoader.LoadConfig("no_such_track");
        Assert.IsNull(cfg);
    }

    [Test]
    public void test_get_available_track_ids_returns_all()
    {
        string[] ids = TrackDataLoader.GetAvailableTrackIds();
        Assert.IsTrue(ids.Length >= 6, $"应有至少 6 条赛道，实际 {ids.Length}");
        Assert.Contains("silverstone_afternoon_tea", ids);
        Assert.Contains("suzuka_sushi", ids);
    }

    [Test]
    public void test_config_to_nodes_converts_every_cell()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("silverstone_afternoon_tea");
        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(cfg);

        Assert.AreEqual(cfg.cells.Length, nodes.Count);
        // 节点索引连续
        for (int i = 0; i < nodes.Count; i++)
            Assert.AreEqual(i, nodes[i].nodeIndex);
    }

    [Test]
    public void test_config_to_nodes_maps_start_finish_flag()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("silverstone_afternoon_tea");
        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(cfg);

        int startFinishCount = 0;
        foreach (var node in nodes)
            if (node.isStartFinish) startFinishCount++;

        Assert.AreEqual(1, startFinishCount, "赛道应恰有 1 个起点/终点节点");
    }

    [Test]
    public void test_config_to_nodes_maps_corners_with_unique_ids()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("suzuka_sushi");
        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(cfg);

        var cornerIds = new HashSet<int>();
        int cornerNodeCount = 0;
        foreach (var node in nodes)
        {
            if (node.cornerId > 0)
            {
                cornerIds.Add(node.cornerId);
                cornerNodeCount++;
            }
        }

        Assert.IsTrue(cornerIds.Count >= 3, $"铃鹿应有多条弯道，实际 {cornerIds.Count}");
        Assert.IsTrue(cornerNodeCount >= cornerIds.Count);
        // 弯道节点有真实限速
        foreach (var node in nodes)
            if (node.cornerId > 0 && node.isApex)
                Assert.IsTrue(node.speedLimit < 99);
    }

    [Test]
    public void test_config_to_nodes_maps_pit_flags_when_present()
    {
        // 用最小配置验证映射契约；真实赛道是否启用维修区由各自 JSON 决定。
        var config = new TrackConfig
        {
            cells = new[]
            {
                new CellData { index = 0, type = "start_finish", name = "Start" },
                new CellData { index = 1, type = "pit_entry", name = "Pit Entry" },
                new CellData { index = 2, type = "pit_exit", name = "Pit Exit" }
            }
        };
        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(config);
        Assert.IsTrue(PitLaneRules.HasPitLane(nodes), "含 pit_entry/pit_exit 的配置应识别为维修区");
        Assert.IsTrue(PitLaneRules.FindPitEntry(nodes) >= 0);
        Assert.IsTrue(PitLaneRules.FindPitExit(nodes) >= 0);
    }

    [Test]
    public void test_config_to_world_positions_scales_center()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("silverstone_afternoon_tea");
        Vector2[] positions = TrackDataLoader.ConfigToWorldPositions(cfg, 30f, 20f);

        Assert.AreEqual(cfg.cells.Length, positions.Length);
        // 中心点 (0.5, 0.5) → (0, 0)
        Vector2 center = positions[cfg.cells.Length / 2];
        Assert.IsTrue(Mathf.Abs(center.x) <= 15f);
        Assert.IsTrue(Mathf.Abs(center.y) <= 10f);
        // 范围: x ∈ [-15, 15], y ∈ [-10, 10]
        foreach (var pos in positions)
        {
            Assert.IsTrue(pos.x >= -15.001f && pos.x <= 15.001f);
            Assert.IsTrue(pos.y >= -10.001f && pos.y <= 10.001f);
        }
    }

    [Test]
    public void test_config_to_world_positions_respects_origin()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("monza_pasta");
        Vector2[] positions = TrackDataLoader.ConfigToWorldPositions(cfg, 30f, 30f, new Vector2(10f, 20f));

        Assert.AreEqual(cfg.cells.Length, positions.Length);
        // 坐标整体偏移 (10, 20)
        foreach (var pos in positions)
        {
            Assert.IsTrue(pos.x >= -5.001f && pos.x <= 25.001f);
            Assert.IsTrue(pos.y >= 5.001f && pos.y <= 35.001f);
        }
    }

    [Test]
    public void test_all_tracks_preserve_authored_landmarks_when_sampling()
    {
        string[] trackIds = TrackDataLoader.GetAvailableTrackIds();
        Assert.IsTrue(trackIds.Length >= 8, "应覆盖所有可选赛道和回退赛道");

        foreach (string trackId in trackIds)
        {
            TrackConfig cfg = TrackDataLoader.LoadConfig(trackId);
            Assert.IsNotNull(cfg, $"赛道配置缺失: {trackId}");

            float width = trackId == TrackManager.FallbackTrackId ? 31.9f : 30f;
            float height = trackId == TrackManager.FallbackTrackId ? 12.3f : 16.875f;
            Vector2[] authored = TrackDataLoader.ConfigToWorldPositionsRaw(cfg, width, height);
            Vector2[] sampled = TrackDataLoader.ConfigToWorldPositions(cfg, width, height);

            for (int i = 0; i < cfg.cells.Length; i++)
            {
                CellData cell = cfg.cells[i];
                bool isLandmark = cell.IsCorner || cell.IsStartFinish || cell.IsPitEntry || cell.IsPitExit;
                if (isLandmark)
                {
                    Assert.AreEqual(
                        authored[i],
                        sampled[i],
                        $"{trackId} 节点 {i} 的弯道/地标坐标不应因采样漂移");
                }
            }

            float authoredLength = TrackLayoutRules.CalculateClosedPathLength(authored);
            float sampledLength = TrackLayoutRules.CalculateClosedPathLength(sampled);
            Assert.LessOrEqual(
                sampledLength,
                authoredLength + 0.001f,
                $"{trackId} 重采样不应制造超出原始中心线的路径长度");
        }
    }

    [Test]
    public void test_anchored_sampling_preserves_a_single_landmark()
    {
        Vector2[] authored =
        {
            new Vector2(0f, 0f),
            new Vector2(3f, 0f),
            new Vector2(6f, 0f),
            new Vector2(6f, 3f),
            new Vector2(0f, 3f)
        };
        bool[] anchors = { true, false, false, false, false };

        Vector2[] sampled = TrackLayoutRules.ResampleAnchoredPath(authored, anchors);

        Assert.AreEqual(authored[0], sampled[0], "单个起点锚点也不得因重采样漂移");
        Assert.AreEqual(authored.Length, sampled.Length);
    }

    [Test]
    public void test_build_corner_maps_assigns_limits_and_names()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("suzuka_sushi");
        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(cfg);
        TrackDataLoader.BuildCornerMaps(cfg, nodes, out var limits, out var names);

        Assert.AreEqual(limits.Count, names.Count);
        Assert.IsTrue(limits.Count >= 3);
        foreach (var kv in limits)
        {
            Assert.IsTrue(kv.Key > 0);
            Assert.IsTrue(kv.Value < 99);
            Assert.IsTrue(!string.IsNullOrEmpty(names[kv.Key]));
        }
    }

    // ===== fallback_42（统一硬编码赛道为 JSON，roadmap P1 #9） =====

    [Test]
    public void test_fallback_42_parses_with_42_cells()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig(TrackManager.FallbackTrackId);

        Assert.IsNotNull(cfg, "fallback_42.json 应存在");
        Assert.AreEqual(42, cfg.cells.Length);
        Assert.AreEqual(42, cfg.gameCellCount);
        Assert.AreEqual(3, cfg.laps);
        Assert.IsFalse(cfg.hasPitLane);
    }

    [Test]
    public void test_fallback_42_matches_original_hardcoded_layout()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig(TrackManager.FallbackTrackId);
        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(cfg);

        // 起点线在节点 0
        Assert.IsTrue(nodes[0].isStartFinish);
        Assert.AreEqual(1, nodes.FindAll(n => n.isStartFinish).Count);

        // 5 个弯道 apex 与原 DefineApex 一致：索引 9/17/25/31/37，限速 3/2/4/2/3
        int[] apexIndexes = { 9, 17, 25, 31, 37 };
        int[] apexLimits = { 3, 2, 4, 2, 3 };
        for (int i = 0; i < apexIndexes.Length; i++)
        {
            Assert.IsTrue(nodes[apexIndexes[i]].isApex, $"节点 {apexIndexes[i]} 应为 apex");
            Assert.IsTrue(nodes[apexIndexes[i]].cornerId > 0);
            Assert.AreEqual(apexLimits[i], nodes[apexIndexes[i]].speedLimit,
                $"节点 {apexIndexes[i]} 限速应等于原硬编码值");
        }

        // 弯段总数 5，直道无限速
        var cornerIds = new HashSet<int>();
        foreach (var n in nodes)
            if (n.cornerId > 0) cornerIds.Add(n.cornerId);
        Assert.AreEqual(5, cornerIds.Count);
        Assert.AreEqual(99, nodes[1].speedLimit);
    }

    [Test]
    public void test_fallback_42_coordinates_are_normalized()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig(TrackManager.FallbackTrackId);

        foreach (var cell in cfg.cells)
        {
            Assert.IsTrue(cell.position.x >= -0.001f && cell.position.x <= 1.001f,
                $"节点 {cell.index} x 越界: {cell.position.x}");
            Assert.IsTrue(cell.position.y >= -0.001f && cell.position.y <= 1.001f,
                $"节点 {cell.index} y 越界: {cell.position.y}");
        }

        // 世界坐标映射：包围盒 31.9 × 12.3（TrackManager.FallbackWorldWidth/Height 私有，用已知值断言）
        Vector2[] world = TrackDataLoader.ConfigToWorldPositions(cfg, 31.9f, 12.3f);
        Assert.AreEqual(cfg.cells.Length, world.Length);
        foreach (var pos in world)
        {
            Assert.IsTrue(Mathf.Abs(pos.x) <= 16f);
            Assert.IsTrue(Mathf.Abs(pos.y) <= 6.2f);
        }
    }
}
