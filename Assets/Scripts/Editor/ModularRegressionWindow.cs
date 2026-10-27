using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-only module picker; does not save scenes or change game/project settings.</summary>
public sealed class ModularRegressionWindow : EditorWindow
{
    private readonly HashSet<string> selected = new HashSet<string> { "tech", "drivers" };
    private ModularRegressionCatalog.Catalog catalog;
    private ModularRegressionCatalog.Plan plan;
    private string[] names;
    private string problem = "";
    private bool includeCore = true, loading;
    private int mode;
    private Vector2 scroll;
    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    [NonSerialized] private string displayedResultPath;
    [NonSerialized] private ModularRegressionRunner.Result displayedResult;

    [MenuItem("Tools/Foodula1/模块化回归")]
    public static void Open() => GetWindow<ModularRegressionWindow>("模块化回归");
    private void OnEnable()
    {
        minSize = new Vector2(540, 470);
        selected.Clear();
        selected.UnionWith(SessionState.GetString("Foodula1.ModularRegression.WindowModules", "tech,drivers").Split(',').Where(x => x.Length > 0));
        mode = SessionState.GetInt("Foodula1.ModularRegression.WindowMode", 0);
        includeCore = SessionState.GetBool("Foodula1.ModularRegression.WindowCore", true);
        Reload();
    }
    private void OnInspectorUpdate()
    {
        if (!loading && !ModularRegressionRunner.IsBusy && (catalog == null || names == null || counts.Count == 0)) Reload();
        Repaint();
    }

    private void Reload()
    {
        if (loading) return;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || ModularRegressionRunner.NativeTestsRunning())
            { problem = "等待编辑器和 Test Runner 空闲后刷新清单。"; return; }
            catalog = ModularRegressionCatalog.Load();
            loading = true;
            ModularRegressionRunner.Discover(discovered =>
            {
                loading = false;
                if (this == null) return;
                names = discovered;
                counts.Clear();
                foreach (var module in catalog.modules)
                    counts[module.id] = names.Count(n => ModularRegressionCatalog.OwnerFixture(n, module.fixtures) != null);
                Recalculate(); Repaint();
            });
        }
        catch (Exception exception) { loading = false; problem = exception.Message; }
    }
    private string Request()
    {
        if (mode == 1) return "fast-all";
        if (mode == 2) return "full";
        var ids = selected.ToList();
        if (includeCore) ids.Add("core");
        return string.Join(",", ids);
    }
    private void Recalculate()
    {
        try { plan = ModularRegressionCatalog.Select(catalog, names, Request().Split(',')); problem = ""; }
        catch (Exception exception) { plan = null; problem = exception.Message; }
    }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Foodula1 模块化回归", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("按修改范围选择模块。短集仍包含模块内原有原生/协程测试；长比赛单列。发布或核心流程变化时保留全量验证。", MessageType.Info);
        using (new EditorGUI.DisabledScope(loading || ModularRegressionRunner.IsBusy))
            if (GUILayout.Button(loading ? "正在发现测试…" : "刷新测试清单")) Reload();
        if (catalog == null || names == null) { EditorGUILayout.HelpBox(problem, MessageType.Warning); return; }
        EditorGUI.BeginChangeCheck();
        mode = GUILayout.Toolbar(mode, new[] { "选择模块", "全部短集", "全量（含长比赛）" });
        scroll = EditorGUILayout.BeginScrollView(scroll);
        using (new EditorGUI.DisabledScope(mode != 0 || ModularRegressionRunner.IsBusy))
        {
            includeCore = EditorGUILayout.ToggleLeft("同时验证比赛基础与结算模块", includeCore);
            foreach (var module in catalog.modules)
            {
                bool active = selected.Contains(module.id);
                counts.TryGetValue(module.id, out int count);
                bool next = EditorGUILayout.ToggleLeft(module.label + "  · " + count + " 项" + (module.longRunning ? "  [长流程]" : ""), active);
                if (next) selected.Add(module.id); else selected.Remove(module.id);
            }
        }
        EditorGUILayout.EndScrollView();
        if (EditorGUI.EndChangeCheck())
        {
            SessionState.SetString("Foodula1.ModularRegression.WindowModules", string.Join(",", selected.OrderBy(x => x)));
            SessionState.SetInt("Foodula1.ModularRegression.WindowMode", mode);
            SessionState.SetBool("Foodula1.ModularRegression.WindowCore", includeCore);
            Recalculate();
        }
        if (!string.IsNullOrEmpty(problem)) EditorGUILayout.HelpBox(problem, MessageType.Error);
        if (plan != null)
        {
            EditorGUILayout.LabelField("发现 " + plan.discovered + " · 将运行 " + plan.selected + " · 本次不执行 " + plan.excluded);
            EditorGUILayout.LabelField("包含长流程 " + plan.longSelected + " 项。未执行部分不计为通过。");
            if (plan.missingFixtures.Length > 0 || plan.staleFixtures.Length > 0)
                EditorGUILayout.HelpBox("全量会包含未分组测试；请更新模块清单以恢复定向运行。", MessageType.Warning);
        }
        bool busy = ModularRegressionRunner.IsBusy;
        using (new EditorGUI.DisabledScope(plan == null || busy || loading))
            if (GUILayout.Button("运行当前选择"))
            {
                try { ModularRegressionRunner.Run(Request()); }
                catch (Exception exception) { problem = exception.Message; }
            }
        if (ModularRegressionRunner.IsPreparing) EditorGUILayout.HelpBox("正在发现并核对本次测试清单…", MessageType.Info);
        else if (busy) EditorGUILayout.HelpBox("已结束 " + ModularRegressionRunner.Completed + "/" + ModularRegressionRunner.SelectedCount + "\n" + ModularRegressionRunner.CurrentTest, MessageType.Info);
        if (!string.IsNullOrEmpty(ModularRegressionRunner.Error)) EditorGUILayout.HelpBox(ModularRegressionRunner.Error, MessageType.Error);
        string resultPath = ModularRegressionRunner.LastResultPath;
        if (displayedResultPath != resultPath)
        {
            displayedResultPath = resultPath;
            displayedResult = File.Exists(resultPath) ? JsonUtility.FromJson<ModularRegressionRunner.Result>(File.ReadAllText(resultPath)) : null;
        }
        if (displayedResult != null)
        {
            var result = displayedResult;
            EditorGUILayout.LabelField("本次原生结果 " + result.state + " · " + result.passed + "/" + result.total + " · " + result.seconds.ToString("F2") + " 秒");
            if (GUILayout.Button("查看本次结果文件")) EditorUtility.RevealInFinder(ModularRegressionRunner.LastResultPath);
        }
    }
}
