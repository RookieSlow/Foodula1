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
    public void test_silverstone_nodes_are_evenly_distributed_after_realistic_straight_scaling()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("silverstone_afternoon_tea");
        Vector2[] positions = TrackDataLoader.ConfigToWorldPositions(cfg, 30f, 16.875f);
        float median = TrackPresentationRules.CalculateMedianNeighborDistance(positions);
        float maxSpacing = 0f;
        for (int i = 0; i < positions.Length; i++)
            maxSpacing = Mathf.Max(
                maxSpacing,
                Vector2.Distance(positions[i], positions[(i + 1) % positions.Length]));

        Assert.AreEqual(77, cfg.cells.Length);
        Assert.AreEqual(3, cfg.laps);
        Assert.AreEqual(20, System.Array.FindAll(cfg.cells, cell => cell.IsCorner).Length);
        Assert.AreEqual(13, System.Array.FindAll(cfg.cells, cell => cell.isApex).Length);
        Assert.That(cfg.cells[0].position.x, Is.EqualTo(0.676042f).Within(0.000001f));
        Assert.That(cfg.cells[0].position.y, Is.EqualTo(0.129630f).Within(0.000001f));
        Assert.LessOrEqual(
            maxSpacing,
            median * 1.5f,
            $"银石最大格距 {maxSpacing:F3} 不应显著大于全图中位数 {median:F3}");

        CellData[] maggotts = System.Array.FindAll(
            cfg.cells,
            cell => cell.cornerId == "maggotts_becketts_chapel");
        Assert.AreEqual(5, maggotts.Length, "银石五连 S 弯应保持五个连续弯道格");
        for (int i = 1; i < maggotts.Length; i++)
            Assert.AreEqual(maggotts[i - 1].index + 1, maggotts[i].index);
        Assert.IsTrue(maggotts[maggotts.Length - 1].isApex);
    }

    [Test]
    public void test_all_selectable_tracks_have_a_twelve_cell_major_straight()
    {
        string[] trackIds =
        {
            "silverstone_afternoon_tea",
            "nurburgring_bier",
            "monza_pasta",
            "indianapolis_burger",
            "shanghai_dim_sum",
            "suzuka_sushi",
            "nurburgring_24h_endurance",
            "le_mans_old_mulsanne"
        };

        foreach (string trackId in trackIds)
        {
            TrackConfig cfg = TrackDataLoader.LoadConfig(trackId);
            int longestStraight = CalculateLongestCircularStraight(cfg.cells);
            Assert.GreaterOrEqual(
                longestStraight,
                12,
                $"{trackId} 至少应有一段可完整执行 4+3+3+2（12 格）冲刺的大直道");
        }

        Assert.AreEqual(77, TrackDataLoader.LoadConfig("silverstone_afternoon_tea").cells.Length);
        Assert.AreEqual(63, TrackDataLoader.LoadConfig("monza_pasta").cells.Length);
        Assert.AreEqual(59, TrackDataLoader.LoadConfig("nurburgring_bier").cells.Length);

        TrackConfig silverstone = TrackDataLoader.LoadConfig("silverstone_afternoon_tea");
        Assert.AreEqual(
            12,
            System.Array.FindAll(silverstone.cells, cell => cell.name == "机库直道").Length,
            "银石机库直道应保留 12 格完整冲刺容量");
    }

    [Test]
    public void test_real_track_straight_proportions_distinguish_major_medium_and_short_runs()
    {
        CollectionAssert.AreEqual(
            new[] { 12, 10, 10, 9, 7, 4, 3, 1, 1 },
            CalculateCircularStraightLengths(TrackDataLoader.LoadConfig("silverstone_afternoon_tea").cells));
        CollectionAssert.AreEqual(
            new[] { 15, 12, 11, 8, 1, 1 },
            CalculateCircularStraightLengths(TrackDataLoader.LoadConfig("monza_pasta").cells));
        CollectionAssert.AreEqual(
            new[] { 15, 13, 3, 3 },
            CalculateCircularStraightLengths(TrackDataLoader.LoadConfig("indianapolis_burger").cells));
        CollectionAssert.AreEqual(
            new[] { 12, 10, 3, 2, 2, 2, 2, 1 },
            CalculateCircularStraightLengths(TrackDataLoader.LoadConfig("suzuka_sushi").cells));
    }

    [Test]
    public void test_real_world_flat_and_high_speed_corners_use_fast_limits()
    {
        AssertApexLimit("shanghai_dim_sum", "yin_exit", 6);
        AssertApexLimit("shanghai_dim_sum", "long_straight_right_turn", 6);
        AssertApexLimit("silverstone_afternoon_tea", "abbey_tea", 6);
        AssertApexLimit("silverstone_afternoon_tea", "copse_espresso", 6);
        AssertApexLimit("nurburgring_bier", "schumacher_s", 6);
        AssertApexLimit("monza_pasta", "grande_lasagna", 6);
        AssertApexLimit("suzuka_sushi", "unagi_200r", 6);
        AssertApexLimit("suzuka_sushi", "wagyu_130r", 6);
        AssertApexLimit("le_mans_old_mulsanne", "porsche_curves", 5);
        AssertApexLimit("nurburgring_24h_endurance", "schwedenkreuz", 6);
    }

    [Test]
    public void test_le_mans_mulsanne_kink_is_a_fast_apex_at_cell_60()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("le_mans_old_mulsanne");

        Assert.IsNotNull(cfg, "勒芒旧慕尚配置应存在");
        Assert.AreEqual(142, cfg.cells.Length);
        CellData kink = System.Array.Find(
            cfg.cells,
            cell => cell.cornerId == "mulsanne_kink" && cell.isApex);

        Assert.IsNotNull(kink, "勒芒应包含慕尚高速偏弯弯心");
        Assert.AreEqual(60, kink.index);
        Assert.IsTrue(kink.IsCorner);
        Assert.AreEqual(1, kink.cornerLevel);
        Assert.AreEqual(6, kink.cornerLimit);
        Assert.AreEqual(24, System.Array.FindAll(cfg.cells, cell => cell.IsCorner).Length);
        Assert.AreEqual(9, System.Array.FindAll(cfg.cells, cell => cell.isApex).Length);
    }

    private static int CalculateLongestCircularStraight(CellData[] cells)
    {
        int longest = 0;
        for (int start = 0; start < cells.Length; start++)
        {
            int previous = (start - 1 + cells.Length) % cells.Length;
            if (cells[start].IsCorner || !cells[previous].IsCorner)
                continue;

            int length = 0;
            while (length < cells.Length && !cells[(start + length) % cells.Length].IsCorner)
                length++;
            longest = Mathf.Max(longest, length);
        }
        return longest;
    }

    private static List<int> CalculateCircularStraightLengths(CellData[] cells)
    {
        var lengths = new List<int>();
        for (int start = 0; start < cells.Length; start++)
        {
            int previous = (start - 1 + cells.Length) % cells.Length;
            if (cells[start].IsCorner || !cells[previous].IsCorner)
                continue;

            int length = 0;
            while (length < cells.Length && !cells[(start + length) % cells.Length].IsCorner)
                length++;
            lengths.Add(length);
        }
        lengths.Sort((left, right) => right.CompareTo(left));
        return lengths;
    }

    private static void AssertApexLimit(string trackId, string cornerId, int expectedLimit)
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig(trackId);
        CellData apex = System.Array.Find(cfg.cells, cell => cell.cornerId == cornerId && cell.isApex);
        Assert.IsNotNull(apex, $"{trackId} 缺少 {cornerId} 弯心");
        Assert.AreEqual(expectedLimit, apex.cornerLimit, $"{trackId}/{cornerId} 现实速度等级不匹配");
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
    public void test_monza_corner_landmarks_follow_visible_turn_sections()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("monza_pasta");
        int[] expectedCornerIndexes = { 8, 9, 11, 12, 13, 29, 30, 32, 33, 45, 46, 47, 56, 57, 58 };
        int[] misplacedStraightIndexes = { 5, 6, 17, 18, 34, 35, 43, 44, 52, 53 };

        foreach (int index in expectedCornerIndexes)
            Assert.IsTrue(cfg.cells[index].IsCorner, $"蒙扎节点 {index} 应属于可见弯道段");

        foreach (int index in misplacedStraightIndexes)
            Assert.IsFalse(cfg.cells[index].IsCorner, $"蒙扎节点 {index} 位于直道，不应显示弯道蒙版");

        Assert.AreEqual(7, new HashSet<string>(System.Array.ConvertAll(
            expectedCornerIndexes,
            index => cfg.cells[index].cornerId)).Count);
    }

    [Test]
    public void test_shanghai_corner_landmarks_follow_visible_turn_sections()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("shanghai_dim_sum");
        int[] expectedCornerIndexes =
        {
            6, 7, 8, 9, 12, 15, 19, 20, 23, 24, 27, 28, 32, 33, 34, 35, 40,
            56, 57, 58, 59, 61
        };
        int[] misplacedStraightIndexes = { 51, 52, 53 };

        foreach (int index in expectedCornerIndexes)
            Assert.IsTrue(cfg.cells[index].IsCorner, $"上海节点 {index} 应属于可见弯道段");

        foreach (int index in misplacedStraightIndexes)
            Assert.IsFalse(cfg.cells[index].IsCorner, $"上海节点 {index} 位于长直道，不应显示弯道蒙版");

        Assert.AreEqual(11, new HashSet<string>(System.Array.ConvertAll(
            expectedCornerIndexes,
            index => cfg.cells[index].cornerId)).Count);
        Assert.IsTrue(cfg.cells[40].isApex, "上海龙须糖超长直道末端右弯的弯心应落在节点 40");
        Assert.IsTrue(cfg.cells[57].isApex, "上海 T14 的弯心应落在发卡弯的折返点");
        Assert.IsTrue(cfg.cells[61].isApex, "上海最后一弯的弯心应落在起点前的可见转向处");
    }

    [Test]
    public void test_indianapolis_corner_landmarks_follow_visible_oval_turns()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("indianapolis_burger");
        int[] expectedCornerIndexes = { 6, 7, 11, 12, 28, 29, 33, 34 };
        int[] misplacedStraightIndexes = { 17, 18 };

        foreach (int index in expectedCornerIndexes)
            Assert.IsTrue(cfg.cells[index].IsCorner, $"印第节点 {index} 应属于椭圆弯道段");

        foreach (int index in misplacedStraightIndexes)
            Assert.IsFalse(cfg.cells[index].IsCorner, $"印第节点 {index} 位于后直道，不应显示弯道蒙版");

        Assert.AreEqual(4, new HashSet<string>(System.Array.ConvertAll(
            expectedCornerIndexes,
            index => cfg.cells[index].cornerId)).Count);
        Assert.IsTrue(cfg.cells[7].isApex, "印第第一弯的弯心应位于右侧弯道入口");
        Assert.IsTrue(cfg.cells[12].isApex, "印第第二弯的弯心应位于右侧弯道出口");
    }

    [Test]
    public void test_nurburgring_gp_corner_landmarks_follow_visible_turn_sections()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("nurburgring_bier");
        int[] expectedCornerIndexes = { 41, 42 };
        int[] misplacedStraightIndexes = { 43, 44 };

        foreach (int index in expectedCornerIndexes)
            Assert.IsTrue(cfg.cells[index].IsCorner, $"纽北 GP 节点 {index} 应属于维多尔弯的可见转向段");

        foreach (int index in misplacedStraightIndexes)
            Assert.IsFalse(cfg.cells[index].IsCorner, $"纽北 GP 节点 {index} 位于维多尔弯出口直道，不应显示弯道蒙版");

        Assert.AreEqual("veedol_gurken", cfg.cells[41].cornerId);
        Assert.IsTrue(cfg.cells[41].isApex, "纽北 GP 维多尔弯的弯心应落在右上方回头点");
    }

    [Test]
    public void test_nurburgring_gp_final_sector_has_no_overlapping_or_reversing_nodes()
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig("nurburgring_bier");
        Vector2[] positions = TrackDataLoader.ConfigToWorldPositions(cfg, 30f, 16.875f);

        for (int index = 46; index <= 58; index++)
        {
            int next = (index + 1) % positions.Length;
            int following = (index + 2) % positions.Length;
            float spacing = Vector2.Distance(positions[index], positions[next]);
            float directionChange = Vector2.Angle(
                positions[next] - positions[index],
                positions[following] - positions[next]);

            Assert.GreaterOrEqual(
                spacing,
                0.5f,
                $"纽博格林 GP 节点 {index}->{next} 不应近距离重叠");
            Assert.Less(
                directionChange,
                120f,
                $"纽博格林 GP 节点 {index}->{next}->{following} 不应瞬间折返");
        }

        for (int first = 0; first < positions.Length; first++)
        {
            int firstNext = (first + 1) % positions.Length;
            for (int second = first + 2; second < positions.Length; second++)
            {
                int secondNext = (second + 1) % positions.Length;
                if (first == secondNext || firstNext == second)
                    continue;

                Assert.IsFalse(
                    SegmentsProperlyIntersect(
                        positions[first],
                        positions[firstNext],
                        positions[second],
                        positions[secondNext]),
                    $"纽博格林 GP 线路 {first}->{firstNext} 不应与 {second}->{secondNext} 自相交");
            }
        }
    }

    private static bool SegmentsProperlyIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float abC = Cross(b - a, c - a);
        float abD = Cross(b - a, d - a);
        float cdA = Cross(d - c, a - c);
        float cdB = Cross(d - c, b - c);
        return abC * abD < 0f && cdA * cdB < 0f;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
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
