/// <summary>
/// Turn-scoped input gate shared by the race coroutine and UI callbacks.
/// It stores no scene references and is reset when a race is initialized.
/// </summary>
public sealed class RaceInputState
{
    private enum InputGate
    {
        None,
        Gear,
        Cards,
        Discard,
        LaneChange,
        PitChoice
    }

    private InputGate activeGate;

    /// <summary>Whether the gear confirmation gate is currently open.</summary>
    public bool WaitingForGear => activeGate == InputGate.Gear;

    /// <summary>Whether the speed/trick card gate is currently open.</summary>
    public bool WaitingForCards => activeGate == InputGate.Cards;

    /// <summary>Whether the discard confirmation gate is currently open.</summary>
    public bool WaitingForDiscard => activeGate == InputGate.Discard;

    /// <summary>Whether the Indianapolis lane-choice gate is currently open.</summary>
    public bool WaitingForLaneChange => activeGate == InputGate.LaneChange;

    /// <summary>Whether the pit-entry choice gate is currently open.</summary>
    public bool WaitingForPitChoice => activeGate == InputGate.PitChoice;

    /// <summary>Gear currently highlighted by the player.</summary>
    public int PendingGear { get; private set; }

    /// <summary>Gear committed by the player when the gear gate closes.</summary>
    public int PlayerGearChoice { get; private set; }

    /// <summary>Clears all gates and restores neutral choices.</summary>
    public void Reset()
    {
        CloseAllGates();
        PendingGear = 0;
        PlayerGearChoice = 0;
    }

    private void CloseAllGates()
    {
        activeGate = InputGate.None;
    }

    private void CloseGate(InputGate gate)
    {
        if (activeGate == gate)
            activeGate = InputGate.None;
    }

    /// <summary>Opens gear selection and uses the current gear as the default choice.</summary>
    public void BeginGearSelection(int currentGear)
    {
        CloseAllGates();
        activeGate = InputGate.Gear;
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
        CloseGate(InputGate.Gear);
        return true;
    }

    /// <summary>Opens the card-play gate and closes unrelated input gates.</summary>
    public void BeginCardSelection()
    {
        CloseAllGates();
        activeGate = InputGate.Cards;
    }

    /// <summary>Closes the card-play gate.</summary>
    public void EndCardSelection()
    {
        CloseGate(InputGate.Cards);
    }

    /// <summary>Opens the discard gate and closes unrelated input gates.</summary>
    public void BeginDiscardSelection()
    {
        CloseAllGates();
        activeGate = InputGate.Discard;
    }

    /// <summary>Closes the discard gate.</summary>
    public void EndDiscardSelection()
    {
        CloseGate(InputGate.Discard);
    }

    /// <summary>Opens the Indianapolis lane-choice gate.</summary>
    public void BeginLaneChangeSelection()
    {
        CloseAllGates();
        activeGate = InputGate.LaneChange;
    }

    /// <summary>Closes the Indianapolis lane-choice gate.</summary>
    public void EndLaneChangeSelection()
    {
        CloseGate(InputGate.LaneChange);
    }

    /// <summary>Opens the pit-entry choice gate.</summary>
    public void BeginPitChoice()
    {
        CloseAllGates();
        activeGate = InputGate.PitChoice;
    }

    /// <summary>Closes the pit-entry choice gate.</summary>
    public void EndPitChoice()
    {
        CloseGate(InputGate.PitChoice);
    }
}
