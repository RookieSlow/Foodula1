/// <summary>
/// Tracks whether the current race presentation window may be skipped.
/// Gameplay rules never depend on this state; it only shortens visual waits.
/// </summary>
public sealed class RacePresentationSkipState
{
    /// <summary>Whether a movement or tailwind presentation window is active.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Whether the player has requested the current presentation skip.</summary>
    public bool IsSkipRequested { get; private set; }

    /// <summary>Starts a fresh presentation window and clears its previous request.</summary>
    public void Begin()
    {
        IsActive = true;
        IsSkipRequested = false;
    }

    /// <summary>Requests a skip only while a presentation window is active.</summary>
    public bool RequestSkip()
    {
        if (!IsActive)
            return false;

        IsSkipRequested = true;
        return true;
    }

    /// <summary>Closes the window and clears the one-shot request.</summary>
    public void End()
    {
        IsActive = false;
        IsSkipRequested = false;
    }
}
