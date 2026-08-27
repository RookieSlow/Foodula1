using System;
using System.Collections.Generic;

/// <summary>
/// One-shot runtime commands authored for a tutorial step. The race adapter
/// decides when to apply the command; normal race rules never inspect it.
/// </summary>
public sealed class TutorialCheckpointCue
{
    public TutorialWeatherCue Weather { get; }
    public TutorialOpponentCue Opponent { get; }
    public TutorialPlayerCheckpoint Player { get; }
    public bool HasAny => Weather != null || Opponent != null || Player != null;

    public TutorialCheckpointCue(
        TutorialWeatherCue weather,
        TutorialOpponentCue opponent,
        TutorialPlayerCheckpoint player)
    {
        Weather = weather;
        Opponent = opponent;
        Player = player;
    }
}

/// <summary>
/// Runtime-facing tutorial orchestrator. It owns progression, exposes newly
/// emitted log records exactly once, and turns authored step scripts into
/// one-shot checkpoint cues without depending on Unity or race internals.
/// </summary>
public sealed class TutorialRuntimeDirector
{
    private readonly TutorialScenarioDefinition scenario;
    private readonly TutorialStateMachine stateMachine;
    private int emittedEventCount;
    private TutorialCheckpointCue pendingCue;

    public TutorialRunPhase Phase => stateMachine.Phase;
    public TutorialStepDefinition CurrentStep => stateMachine.CurrentStep;
    public int CompletedStepCount => stateMachine.CompletedStepCount;
    public int StepCount => scenario.steps.Count;
    public bool BlocksRaceInput =>
        Phase == TutorialRunPhase.Guided &&
        CurrentStep != null &&
        CurrentStep.allowManualAdvance;

    public TutorialRuntimeDirector(
        TutorialScenarioDefinition scenario,
        bool startInPractice = false)
    {
        this.scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        stateMachine = new TutorialStateMachine(scenario, startInPractice);
        pendingCue = BuildCue(CurrentStep != null ? CurrentStep.id : (TutorialStepId?)null);
    }

    public bool IsExpecting(TutorialAction action)
    {
        return Phase == TutorialRunPhase.Guided &&
               CurrentStep != null &&
               CurrentStep.requiredAction == action;
    }

    public bool TryPerform(TutorialAction action, out string failureReason)
    {
        if (!stateMachine.TryPerform(action, out failureReason))
            return false;

        pendingCue = BuildCue(CurrentStep != null ? CurrentStep.id : (TutorialStepId?)null);
        return true;
    }

    public void SkipGuidedSection()
    {
        stateMachine.SkipGuidedSection();
        pendingCue = null;
    }

    public void RestartGuidedSection()
    {
        stateMachine.RestartGuidedSection();
        pendingCue = BuildCue(CurrentStep != null ? CurrentStep.id : (TutorialStepId?)null);
    }

    public void RestartPracticeLap()
    {
        stateMachine.RestartPracticeLap();
        pendingCue = null;
    }

    public bool CompletePracticeLap(out string failureReason)
    {
        return stateMachine.TryPerform(TutorialAction.CompletePracticeLap, out failureReason);
    }

    public void ExitTutorial()
    {
        stateMachine.ExitTutorial();
        pendingCue = null;
    }

    public TutorialCheckpointCue TakePendingCue()
    {
        TutorialCheckpointCue cue = pendingCue;
        pendingCue = null;
        return cue;
    }

    public IReadOnlyList<TutorialEventRecord> DrainNewEvents()
    {
        IReadOnlyList<TutorialEventRecord> allEvents = stateMachine.Events;
        if (emittedEventCount >= allEvents.Count)
            return Array.Empty<TutorialEventRecord>();

        var result = new List<TutorialEventRecord>(allEvents.Count - emittedEventCount);
        for (int i = emittedEventCount; i < allEvents.Count; i++)
            result.Add(allEvents[i]);
        emittedEventCount = allEvents.Count;
        return result;
    }

    private TutorialCheckpointCue BuildCue(TutorialStepId? step)
    {
        if (!step.HasValue)
            return null;

        TutorialWeatherCue weather = null;
        for (int i = 0; i < scenario.weatherScript.Count; i++)
        {
            if (scenario.weatherScript[i].step == step.Value)
            {
                weather = scenario.weatherScript[i];
                break;
            }
        }

        TutorialOpponentCue opponent = null;
        for (int i = 0; i < scenario.opponentScript.Count; i++)
        {
            if (scenario.opponentScript[i].step == step.Value)
            {
                opponent = scenario.opponentScript[i];
                break;
            }
        }

        TutorialPlayerCheckpoint player = null;
        for (int i = 0; i < scenario.playerCheckpoints.Count; i++)
        {
            if (scenario.playerCheckpoints[i].step == step.Value)
            {
                player = scenario.playerCheckpoints[i];
                break;
            }
        }

        return weather != null || opponent != null || player != null
            ? new TutorialCheckpointCue(weather, opponent, player)
            : null;
    }
}
