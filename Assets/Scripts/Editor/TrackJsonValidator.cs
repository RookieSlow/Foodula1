using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 赛道 JSON schema 校验工具（roadmap P1 #6）。
/// 校验所有 Resources/Configs/Tracks/*.json：
///  - 可解析、cells 非空、gameCellCount 一致
///  - 节点索引连续（0..n-1）
///  - 恰有 1 个 start_finish
///  - 弯道：cornerId 非空、限速合理、每个弯段至少有 1 个 apex
///  - 维修区：pit_entry / pit_exit 成对出现（与 hasPitLane 一致）
///  - 坐标在 [0,1] 内、圈数 > 0、天气池可解析
/// 菜单：Foodula1 > Tools > Validate Track JSONs
/// </summary>
public static class TrackJsonValidator
{
    private const string TRACKS_PATH = "Assets/Resources/Configs/Tracks";

    [MenuItem("Foodula1/Tools/Validate Track JSONs")]
    public static void ValidateAllTracks()
    {
        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { TRACKS_PATH });
        if (guids.Length == 0)
        {
            Debug.LogWarning("[TrackValidator] 未找到赛道 JSON。");
            return;
        }

        int failed = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".json")) continue;

            var errors = new List<string>();
            TrackConfig cfg = TrackDataLoader.LoadConfig(System.IO.Path.GetFileNameWithoutExtension(path));
            if (cfg == null)
            {
                errors.Add("JSON 无法解析或 cells 为空");
            }
            else
            {
                ValidateConfig(cfg, errors);
            }

            if (errors.Count > 0)
            {
                failed++;
                Debug.LogError($"[TrackValidator] ❌ {path}:\n  - " + string.Join("\n  - ", errors));
            }
            else
            {
                Debug.Log($"[TrackValidator] ✅ {path}");
            }
        }

        int total = guids.Count(g => g != null);
        if (failed == 0)
            Debug.Log($"[TrackValidator] 全部 {total} 条赛道校验通过。");
        else
            Debug.LogError($"[TrackValidator] {failed}/{total} 条赛道存在错误，请修复后重新校验。");
    }

    private static void ValidateConfig(TrackConfig cfg, List<string> errors)
    {
        if (cfg.cells == null || cfg.cells.Length == 0)
        {
            errors.Add("cells 为空");
            return;
        }

        // 1. gameCellCount 一致性
        if (cfg.gameCellCount != cfg.cells.Length)
            errors.Add($"gameCellCount={cfg.gameCellCount} ≠ cells.Length={cfg.cells.Length}");

        // 2. 节点索引连续
        for (int i = 0; i < cfg.cells.Length; i++)
        {
            if (cfg.cells[i].index != i)
            {
                errors.Add($"cell[{i}].index={cfg.cells[i].index} 不连续");
                break;
            }
        }

        // 3. 恰有 1 个 start_finish
        int startFinishCount = cfg.cells.Count(c => c.IsStartFinish);
        if (startFinishCount != 1)
            errors.Add($"start_finish 数量={startFinishCount}（应为 1）");

        // 4. 弯道：cornerId 非空、限速合理、每个弯段有 apex
        var cornerLimits = new Dictionary<string, int>();
        var cornerHasApex = new Dictionary<string, bool>();
        foreach (var cell in cfg.cells.Where(c => c.IsCorner))
        {
            if (string.IsNullOrEmpty(cell.cornerId))
            {
                errors.Add($"cell[{cell.index}] 弯道缺少 cornerId");
                continue;
            }
            if (cell.cornerLimit <= 0 || cell.cornerLimit > 20)
                errors.Add($"cell[{cell.index}] cornerLimit={cell.cornerLimit} 超出合理范围 (1-20)");
            cornerLimits[cell.cornerId] = cell.cornerLimit;
            if (cell.isApex)
                cornerHasApex[cell.cornerId] = true;
        }
        foreach (var kv in cornerLimits)
        {
            if (!cornerHasApex.ContainsKey(kv.Key))
                errors.Add($"弯段 {kv.Key} 缺少 apex 节点");
        }

        // 5. 维修区成对
        int pitEntry = cfg.cells.Count(c => c.IsPitEntry);
        int pitExit = cfg.cells.Count(c => c.IsPitExit);
        if (cfg.hasPitLane && (pitEntry != 1 || pitExit != 1))
            errors.Add($"hasPitLane=true 但 pit_entry={pitEntry} / pit_exit={pitExit}（应各 1）");
        if (!cfg.hasPitLane && (pitEntry > 0 || pitExit > 0))
            errors.Add($"hasPitLane=false 但存在 pit_entry={pitEntry} / pit_exit={pitExit}");

        // 6. 坐标范围 [0,1]
        foreach (var cell in cfg.cells)
        {
            if (cell.position.x < -0.001f || cell.position.x > 1.001f ||
                cell.position.y < -0.001f || cell.position.y > 1.001f)
            {
                errors.Add($"cell[{cell.index}] 坐标 ({cell.position.x}, {cell.position.y}) 超出 [0,1]");
                break;
            }
        }

        // 7. 圈数
        if (cfg.laps <= 0)
            errors.Add($"laps={cfg.laps} 必须 > 0");

        // 8. 天气池可解析（sunny/cloudy/light_rain/heavy_rain/hot）
        if (cfg.weatherPool != null && cfg.weatherPool.Length > 0)
        {
            foreach (var w in cfg.weatherPool)
            {
                if (WeatherRules.ParseWeather(w) == null)
                {
                    errors.Add($"天气池包含未知天气: '{w}'");
                    break;
                }
            }
        }
        else
        {
            // Tracks may intentionally use a fixed default while weather
            // configuration is pending (e.g. endurance layouts).
            if (WeatherRules.ParseWeather(cfg.defaultWeather) == null)
                errors.Add("weatherPool 缺失或为空，且 defaultWeather 无法解析");
        }

        // 9. 布局元数据
        if (cfg.layout == null || !cfg.layout.pathClosed)
            errors.Add("layout.pathClosed 应为 true（环形赛道）");
    }

}
