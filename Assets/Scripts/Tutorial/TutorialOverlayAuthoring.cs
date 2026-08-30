using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class TutorialStepPresentation
{
    public TutorialStepId id;
    public string sectionLabel;
    public string title;
    [TextArea(2, 4)] public string goal;
    [TextArea(2, 4)] public string currentState;
    [TextArea(2, 4)] public string actionPrompt;
    [TextArea(2, 4)] public string successSignal;
    [TextArea(2, 4)] public string recoveryHint;
    [TextArea(2, 4)] public string focusIntroduction;
    public string manualAdvanceLabel;

    public string BuildGuideText()
    {
        return TutorialGuideTextBuilder.Build(
            goal,
            currentState,
            actionPrompt,
            successSignal,
            recoveryHint);
    }

    public static TutorialStepPresentation FromDefinition(TutorialStepDefinition step)
    {
        return new TutorialStepPresentation
        {
            id = step.id,
            sectionLabel = step.sectionLabel,
            title = step.title,
            goal = step.goal,
            currentState = step.currentState,
            actionPrompt = step.actionPrompt,
            successSignal = step.successSignal,
            recoveryHint = step.recoveryHint,
            focusIntroduction = step.focusIntroduction,
            manualAdvanceLabel = step.manualAdvanceLabel
        };
    }
}

public static class TutorialOverlayValidation
{
    public static List<string> CollectIssues(
        IReadOnlyList<TutorialStepPresentation> steps,
        TutorialGuideUI guide,
        TutorialFocusHighlightUI focusHighlight)
    {
        var issues = new List<string>();
        if (guide == null)
            issues.Add("缺少 TutorialGuideUI 引用。");
        if (focusHighlight == null)
            issues.Add("缺少 TutorialFocusHighlightUI 引用。");
        if (steps == null)
        {
            issues.Add("教程步骤列表为空。");
            return issues;
        }

        var seen = new HashSet<TutorialStepId>();
        for (int i = 0; i < steps.Count; i++)
        {
            TutorialStepPresentation step = steps[i];
            if (step == null)
            {
                issues.Add($"步骤列表第 {i + 1} 项为空。");
                continue;
            }

            if (!seen.Add(step.id))
                issues.Add($"步骤 ID 重复：{step.id}。");
            ValidateText(step.id, "章节", step.sectionLabel, issues);
            ValidateText(step.id, "标题", step.title, issues);
            ValidateText(step.id, "机制说明", step.goal, issues);
            ValidateText(step.id, "操作提示", step.actionPrompt, issues);
            ValidateText(step.id, "高光说明", step.focusIntroduction, issues);
        }

        foreach (TutorialStepId id in Enum.GetValues(typeof(TutorialStepId)))
        {
            if (!seen.Contains(id))
                issues.Add($"缺少步骤 ID：{id}。");
        }
        return issues;
    }

    private static void ValidateText(
        TutorialStepId id,
        string fieldName,
        string value,
        ICollection<string> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
            issues.Add($"步骤 {id} 的{fieldName}为空。");
    }
}

/// <summary>
/// Inspector-editable presentation source for the tutorial overlay prefab.
/// Gameplay actions and deterministic checkpoints remain in the scenario;
/// this component owns only player-facing copy and UI object references.
/// </summary>
public sealed class TutorialOverlayAuthoring : MonoBehaviour
{
    [Header("运行时组件")]
    [SerializeField] private TutorialGuideUI guide;
    [SerializeField] private TutorialFocusHighlightUI focusHighlight;

    [Header("16 步教程文本（可直接在 Inspector 修改）")]
    [SerializeField] private List<TutorialStepPresentation> steps =
        new List<TutorialStepPresentation>();

    [Header("Prefab 预览（仅改变编辑画面，不改变教程流程）")]
    [SerializeField] private TutorialStepId previewStep =
        TutorialStepId.ObjectiveAndInterface;

    [Header("练习圈文本")]
    [SerializeField] private string practiceTitle = "勒芒自由练习";
    [SerializeField] private string completedTitle = "练习圈完成";
    [TextArea(2, 3)] [SerializeField] private string practiceCompletion =
        "✓ 引导已结束，比赛状态已完整重置";
    [TextArea(2, 3)] [SerializeField] private string completedCompletion =
        "✓ 一整圈练习已经完成";
    [TextArea(3, 5)] [SerializeField] private string practiceInstruction =
        "状态已重置为 UK、零科技和教程精确牌组。自由完成一整圈；本圈使用脚本阴天，仍不写入正常奖励与进度。";
    [TextArea(3, 5)] [SerializeField] private string completedInstruction =
        "你已完成一整圈勒芒练习。该结果不会发放 RP、车手 XP、解锁或赛事进度。可以再练一圈、重播引导或退出。";

    public TutorialGuideUI Guide => guide;
    public TutorialFocusHighlightUI FocusHighlight => focusHighlight;
    public IReadOnlyList<TutorialStepPresentation> Steps => steps;
    public TutorialStepId PreviewStepId => previewStep;
    public string GetPracticeTitle(bool completed) => completed ? completedTitle : practiceTitle;
    public string GetPracticeCompletion(bool completed) =>
        completed ? completedCompletion : practiceCompletion;
    public string GetPracticeInstruction(bool completed) =>
        completed ? completedInstruction : practiceInstruction;

    public void Bind(MVPGameManager manager, Canvas canvas)
    {
        focusHighlight?.Bind(canvas, manager);
        guide?.Bind(manager, this, focusHighlight);
    }

    public TutorialStepPresentation Find(TutorialStepId id)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] != null && steps[i].id == id)
                return steps[i];
        }
        return null;
    }

    /// <summary>
    /// Copies one authored presentation into the visible Prefab text objects so
    /// designers can tune copy and RectTransforms together without entering Play Mode.
    /// This does not touch scenario actions, checkpoints, saves, or tutorial progress.
    /// </summary>
    public bool PreviewStep(TutorialStepId id)
    {
        TutorialStepPresentation presentation = Find(id);
        if (guide == null || presentation == null)
            return false;

        int index = steps.IndexOf(presentation);
        guide.PreviewAuthoredStep(presentation, index + 1, steps.Count);
        return true;
    }

    public bool PreviewPractice(bool completed)
    {
        if (guide == null)
            return false;

        guide.PreviewAuthoredPractice(
            GetPracticeTitle(completed),
            GetPracticeCompletion(completed),
            GetPracticeInstruction(completed),
            steps.Count,
            completed);
        return true;
    }

    public List<string> CollectValidationIssues()
    {
        return TutorialOverlayValidation.CollectIssues(steps, guide, focusHighlight);
    }

    [ContextMenu("校验教程文案配置")]
    private void ValidateAuthoring()
    {
        List<string> issues = CollectValidationIssues();
        if (issues.Count == 0)
        {
            Debug.Log("[TUTORIAL_AUTHORING] 16 步文案、ID 与 Prefab 引用校验通过。", this);
            return;
        }

        Debug.LogError(
            $"[TUTORIAL_AUTHORING] 发现 {issues.Count} 个问题：\n- " +
            string.Join("\n- ", issues),
            this);
    }

    [ContextMenu("预览所选教程步骤")]
    private void PreviewSelectedStep()
    {
        PreviewStep(previewStep);
    }

    [ContextMenu("预览自由练习")]
    private void PreviewPracticeState()
    {
        PreviewPractice(false);
    }

    [ContextMenu("预览练习完成")]
    private void PreviewCompletedState()
    {
        PreviewPractice(true);
    }

    public void Configure(
        TutorialGuideUI guideComponent,
        TutorialFocusHighlightUI focusComponent,
        TutorialScenarioDefinition scenario)
    {
        guide = guideComponent;
        focusHighlight = focusComponent;
        steps.Clear();
        if (scenario == null)
            return;
        for (int i = 0; i < scenario.steps.Count; i++)
            steps.Add(TutorialStepPresentation.FromDefinition(scenario.steps[i]));
    }

    [ContextMenu("恢复默认教程文本")]
    public void ResetToScenarioDefaults()
    {
        Configure(guide, focusHighlight, TutorialScenarioDefinition.CreateLeMansUk());
    }
}
