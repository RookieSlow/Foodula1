using System;
using System.Collections.Generic;

public sealed class TutorialEventRecord
{
    public int sequence { get; }
    public string eventId { get; }
    public TutorialStepId? step { get; }
    public string detail { get; }

    public TutorialEventRecord(int sequence, string eventId, TutorialStepId? step, string detail)
    {
        this.sequence = sequence;
        this.eventId = eventId;
        this.step = step;
        this.detail = detail ?? string.Empty;
    }

    public override string ToString()
    {
        string stepValue = step.HasValue ? step.Value.ToString() : "none";
        return $"[TUTORIAL] seq={sequence} event={eventId} step={stepValue} detail={detail}";
    }
}

/// <summary>
/// Pure tutorial progression state. UI and race systems report completed
/// actions; invalid or out-of-order actions leave the current step unchanged.
/// </summary>
public sealed class TutorialStateMachine
{
    private readonly TutorialScenarioDefinition scenario;
    private readonly List<TutorialEventRecord> events = new List<TutorialEventRecord>();
    private int stepIndex;
    private int activeStepIndex;
    private readonly HashSet<int> completedSteps = new HashSet<int>();
    private int eventSequence;

    public TutorialRunPhase Phase { get; private set; }
    public TutorialStepDefinition CurrentStep =>
        Phase == TutorialRunPhase.Guided && stepIndex < scenario.steps.Count
            ? scenario.steps[stepIndex]
            : null;
    public int CompletedStepCount => completedSteps.Count;
    public int CurrentStepIndex => stepIndex;
    public TutorialStepDefinition ActiveStep => Phase == TutorialRunPhase.Guided
        ? scenario.steps[activeStepIndex] : null;
    public bool IsReviewing => Phase == TutorialRunPhase.Guided && stepIndex < activeStepIndex;
    public bool IsCurrentStepComplete => completedSteps.Contains(stepIndex);
    public bool IsActiveStepComplete => completedSteps.Contains(activeStepIndex);
    public bool CanGoPrevious => Phase == TutorialRunPhase.Guided && stepIndex > 0;
    public bool CanGoNext => CurrentStep != null &&
        (IsReviewing || IsCurrentStepComplete || CurrentStep.allowManualAdvance);
    public TutorialStepDefinition LastCompletedStep { get; private set; }
    public IReadOnlyList<TutorialEventRecord> Events => events;

    public TutorialStateMachine(
        TutorialScenarioDefinition scenario,
        bool startInPractice = false)
    {
        this.scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        if (startInPractice)
        {
            stepIndex = scenario.steps.Count;
            for (int i = 0; i < scenario.steps.Count; i++) completedSteps.Add(i);
            Phase = TutorialRunPhase.Practice;
            AddEvent("practice_started", null, scenario.id);
        }
        else
        {
            Phase = TutorialRunPhase.Guided;
            AddEvent("guided_started", CurrentStep?.id, scenario.id);
        }
    }

    public bool TryPerform(TutorialAction action, out string failureReason)
    {
        failureReason = null;

        if (Phase == TutorialRunPhase.Practice)
        {
            if (action != TutorialAction.CompletePracticeLap)
            {
                failureReason = "practice_lap_not_complete";
                AddEvent("action_rejected", null, failureReason);
                return false;
            }

            Phase = TutorialRunPhase.Completed;
            AddEvent("practice_completed", null, scenario.id);
            return true;
        }

        if (Phase != TutorialRunPhase.Guided || CurrentStep == null)
        {
            failureReason = "tutorial_not_accepting_actions";
            AddEvent("action_rejected", null, failureReason);
            return false;
        }

        if (ActiveStep.requiredAction != action)
        {
            failureReason = $"expected_{ActiveStep.requiredAction}";
            AddEvent("action_rejected", CurrentStep.id, failureReason);
            return false;
        }

        if (!completedSteps.Add(activeStepIndex))
        {
            failureReason = "step_already_complete";
            return false;
        }
        TutorialStepId completed = ActiveStep.id;
        LastCompletedStep = ActiveStep;
        AddEvent("step_completed", completed, action.ToString());
        return true;
    }

    /// <summary>Reviews a visited lesson without changing the live race or action latch.</summary>
    public bool TryPrevious()
    {
        if (!CanGoPrevious) return false;
        stepIndex--;
        AddEvent("step_reviewed", CurrentStep.id, CurrentStep.instructionKey);
        return true;
    }

    /// <summary>Only explicit navigation starts another lesson or the practice lap.</summary>
    public bool TryNext(out string failureReason)
    {
        failureReason = null;
        if (!CanGoNext)
        {
            failureReason = "step_action_not_complete";
            return false;
        }
        if (IsReviewing)
        {
            stepIndex++;
            AddEvent("step_reviewed", CurrentStep.id, CurrentStep.instructionKey);
            return true;
        }
        if (!IsActiveStepComplete)
            TryPerform(ActiveStep.requiredAction, out failureReason);
        stepIndex++;
        activeStepIndex = stepIndex;

        if (stepIndex >= scenario.steps.Count)
        {
            Phase = TutorialRunPhase.Practice;
            AddEvent("practice_started", null, scenario.id);
        }
        else
        {
            AddEvent("step_started", CurrentStep.id, CurrentStep.instructionKey);
        }

        return true;
    }

    public void SkipGuidedSection()
    {
        if (Phase != TutorialRunPhase.Guided) return;
        LastCompletedStep = null;
        stepIndex = scenario.steps.Count;
        for (int i = 0; i < scenario.steps.Count; i++) completedSteps.Add(i);
        Phase = TutorialRunPhase.Practice;
        AddEvent("guided_skipped", null, scenario.id);
        AddEvent("practice_started", null, scenario.id);
    }

    public void RestartGuidedSection()
    {
        stepIndex = 0;
        activeStepIndex = 0;
        completedSteps.Clear();
        LastCompletedStep = null;
        Phase = TutorialRunPhase.Guided;
        AddEvent("guided_restarted", CurrentStep?.id, scenario.id);
    }

    public void RestartPracticeLap()
    {
        if (Phase != TutorialRunPhase.Practice && Phase != TutorialRunPhase.Completed) return;
        Phase = TutorialRunPhase.Practice;
        AddEvent("practice_restarted", null, scenario.id);
    }

    public void ExitTutorial()
    {
        Phase = TutorialRunPhase.Exited;
        AddEvent("tutorial_exited", CurrentStep?.id, scenario.id);
    }

    private void AddEvent(string eventId, TutorialStepId? step, string detail)
    {
        events.Add(new TutorialEventRecord(++eventSequence, eventId, step, detail));
    }
}
