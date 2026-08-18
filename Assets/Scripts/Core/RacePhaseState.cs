/// <summary>The presentation-facing phases of a race turn.</summary>
public enum GamePhase
{
    WaitingForGear,
    WaitingForCards,
    Animating,
    GameOver
}

/// <summary>
/// Owns race-phase transitions and input acceptance without scene references.
/// The coroutine coordinator remains responsible for the work performed in each phase.
/// </summary>
public sealed class RacePhaseState
{
    /// <summary>The current race phase.</summary>
    public GamePhase Current { get; private set; } = GamePhase.GameOver;

    /// <summary>Whether the main race loop should continue running.</summary>
    public bool IsRunning => Current != GamePhase.GameOver;

    /// <summary>Starts or resets a race at gear selection.</summary>
    public void ResetForRace()
    {
        Current = GamePhase.WaitingForGear;
    }

    /// <summary>Moves to the gear-selection phase.</summary>
    public void BeginGearSelection()
    {
        Current = GamePhase.WaitingForGear;
    }

    /// <summary>Moves to the card-play phase.</summary>
    public void BeginCardSelection()
    {
        Current = GamePhase.WaitingForCards;
    }

    /// <summary>Moves to the movement/resolution phase.</summary>
    public void BeginAnimation()
    {
        Current = GamePhase.Animating;
    }

    /// <summary>Closes the race loop and shows the game-over state.</summary>
    public void CompleteGame()
    {
        Current = GamePhase.GameOver;
    }

    /// <summary>Checks whether gear callbacks belong to the active phase and gate.</summary>
    public bool CanAcceptGear(RaceInputState inputState)
    {
        return Current == GamePhase.WaitingForGear &&
               inputState != null &&
               inputState.WaitingForGear;
    }

    /// <summary>Checks whether card callbacks belong to the active phase and gate.</summary>
    public bool CanAcceptCards(RaceInputState inputState)
    {
        return Current == GamePhase.WaitingForCards &&
               inputState != null &&
               inputState.WaitingForCards;
    }
}
