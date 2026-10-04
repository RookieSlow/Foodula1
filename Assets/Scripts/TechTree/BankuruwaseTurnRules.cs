/// <summary>JP L3 turn-boundary state changes, independent of HUD presentation.</summary>
public enum BankuruwaseTurnTransition
{
    None,
    Activated,
    Continued,
    Expired
}

public static class BankuruwaseTurnRules
{
    /// <summary>
    /// An active rotor consumes one turn even after its rank improves. An
    /// expiration cannot trigger it again until a later turn boundary.
    /// </summary>
    public static BankuruwaseTurnTransition Advance(
        TechTreeState state, TechTreeDatabase db, int currentRank, int totalPlayers)
    {
        if (state == null)
            return BankuruwaseTurnTransition.None;

        if (state.bankuruwaseActive)
            return TechTreeRules.TickBankuruwase(state)
                ? BankuruwaseTurnTransition.Continued
                : BankuruwaseTurnTransition.Expired;

        if (!TechTreeRules.ShouldTriggerBankuruwase(state, db, currentRank, totalPlayers))
            return BankuruwaseTurnTransition.None;

        TechTreeRules.ActivateBankuruwase(state);
        return BankuruwaseTurnTransition.Activated;
    }
}
