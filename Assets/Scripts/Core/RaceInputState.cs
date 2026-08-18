/// <summary>
/// Turn-scoped input gate shared by the race coroutine and UI callbacks.
/// It stores no scene references and is reset when a race is initialized.
/// </summary>
public sealed class RaceInputState
{
    /// <summary>Whether the gear confirmation gate is currently open.</summary>
    public bool WaitingForGear { get; private set; }

    /// <summary>Whether the speed/trick card gate is currently open.</summary>
    public bool WaitingForCards { get; private set; }

    /// <summary>Whether the discard confirmation gate is currently open.</summary>
    public bool WaitingForDiscard { get; private set; }

    /// <summary>Gear currently highlighted by the player.</summary>
    public int PendingGear { get; private set; }

    /// <summary>Gear committed by the player when the gear gate closes.</summary>
    public int PlayerGearChoice { get; private set; }

    /// <summary>Clears all gates and restores neutral choices.</summary>
    public void Reset()
    {
        WaitingForGear = false;
        WaitingForCards = false;
        WaitingForDiscard = false;
        PendingGear = 0;
        PlayerGearChoice = 0;
    }

    /// <summary>Opens gear selection and uses the current gear as the default choice.</summary>
    public void BeginGearSelection(int currentGear)
    {
        WaitingForGear = true;
        WaitingForCards = false;
        WaitingForDiscard = false;
        PendingGear = currentGear;
        PlayerGearChoice = currentGear;
    }

    /// <summary>Highlights a gear value while the gear gate is open.</summary>
    public bool SelectGear(int gear)
    {
        if (!WaitingForGear)
            return false;

        PendingGear = gear;
        return true;
    }

    /// <summary>Commits the highlighted gear and closes the gear gate.</summary>
    public bool ConfirmGear()
    {
        if (!WaitingForGear)
            return false;

        PlayerGearChoice = PendingGear;
        WaitingForGear = false;
        return true;
    }

    /// <summary>Opens the card-play gate and closes unrelated input gates.</summary>
    public void BeginCardSelection()
    {
        WaitingForGear = false;
        WaitingForCards = true;
        WaitingForDiscard = false;
    }

    /// <summary>Closes the card-play gate.</summary>
    public void EndCardSelection()
    {
        WaitingForCards = false;
    }

    /// <summary>Opens the discard gate and closes unrelated input gates.</summary>
    public void BeginDiscardSelection()
    {
        WaitingForGear = false;
        WaitingForCards = false;
        WaitingForDiscard = true;
    }

    /// <summary>Closes the discard gate.</summary>
    public void EndDiscardSelection()
    {
        WaitingForDiscard = false;
    }
}
