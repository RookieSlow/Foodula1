using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TutorialOverlayAuthoring))]
public sealed class TutorialOverlayAuthoringEditor : Editor
{
    private List<string> validationIssues;

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
            validationIssues = null;

        TutorialOverlayAuthoring authoring =
            (TutorialOverlayAuthoring)target;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("手工预览与校验", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "先在上方选择 Preview Step，再用下方按钮把文案显示到 Prefab。" +
            "预览与校验都不会推进教程状态或覆盖 Steps 中的文本。",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("预览所选教程步骤"))
                Preview(authoring, () => authoring.PreviewStep(authoring.PreviewStepId));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("预览自由练习"))
                Preview(authoring, () => authoring.PreviewPractice(false));
            if (GUILayout.Button("预览练习完成"))
                Preview(authoring, () => authoring.PreviewPractice(true));
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("校验 16 步文案与引用"))
            validationIssues = authoring.CollectValidationIssues();

        DrawValidationResult();
    }

    private static void Preview(
        TutorialOverlayAuthoring authoring,
        System.Func<bool> previewAction)
    {
        Undo.RegisterFullObjectHierarchyUndo(
            authoring.gameObject,
            "Preview Tutorial Overlay");
        if (!previewAction())
        {
            Debug.LogWarning(
                "[TUTORIAL_AUTHORING] 无法预览：请检查 Guide 引用及所选步骤。",
                authoring);
        }
        SceneView.RepaintAll();
    }

    private void DrawValidationResult()
    {
        if (validationIssues == null)
            return;

        if (validationIssues.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "16 步文案、ID 与 Prefab 引用校验通过。",
                MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            $"发现 {validationIssues.Count} 个问题。校验只报告，不会自动修复。",
            MessageType.Error);
        int visibleCount = Mathf.Min(validationIssues.Count, 10);
        for (int i = 0; i < visibleCount; i++)
            EditorGUILayout.LabelField($"• {validationIssues[i]}", EditorStyles.wordWrappedLabel);
        if (validationIssues.Count > visibleCount)
            EditorGUILayout.LabelField($"另有 {validationIssues.Count - visibleCount} 个问题……");
    }
}
