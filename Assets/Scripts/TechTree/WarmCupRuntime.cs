/// <summary>
/// CN L2 温杯's per-racer, race-only declaration window.
/// The declaration is free; the engine heat payment is committed only during
/// ordered turn cleanup after ordinary movement, fees and cooling have settled.
/// </summary>
public sealed class WarmCupRuntime
{
    public int TurnId { get; private set; } = -1;
    public int Version { get; private set; }
    public bool IsOpen { get; private set; }
    public bool Declared { get; private set; }
    public bool UsedThisTurn { get; private set; }

    public void BeginTurn(int globalTurn)
    {
        CloseInput();
        UsedThisTurn = false;
        TurnId = globalTurn;
        IsOpen = globalTurn >= 0;
    }

    public bool TryDeclare(int globalTurn)
    {
        if (!IsOpen || TurnId != globalTurn || Declared || UsedThisTurn)
            return false;
        Declared = true;
        Version++;
        return true;
    }

    public bool TryConsume(int globalTurn)
    {
        if (!IsOpen || TurnId != globalTurn || !Declared || UsedThisTurn)
            return false;
        UsedThisTurn = true;
        Declared = false;
        Version++;
        return true;
    }

    public void CancelDeclaration(int globalTurn)
    {
        if (IsOpen && TurnId == globalTurn && !UsedThisTurn)
        {
            Declared = false;
            Version++;
        }
    }

    public void CloseInput()
    {
        IsOpen = false;
        Declared = false;
        Version++;
    }

    public void ResetForRace()
    {
        CloseInput();
        TurnId = -1;
        UsedThisTurn = false;
    }

    public WarmCupRuntime CreatePresentationCopy()
    {
        return (WarmCupRuntime)MemberwiseClone();
    }
}
