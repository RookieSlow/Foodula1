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
        return
            $"{goal}\n\n" +
            $"<color=#8BD7FF><b>现在场上</b></color>　{currentState}\n" +
            $"<color=#FFD27A><b>轮到你了</b></color>　{actionPrompt}\n" +
            $"<color=#8FE0A6><b>完成后</b></color>　{successSignal}\n" +
            $"<size=90%><color=#B9C7D8>没反应？{recoveryHint}</color></size>";
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
