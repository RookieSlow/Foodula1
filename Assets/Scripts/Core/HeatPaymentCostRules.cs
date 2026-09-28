using System;

public readonly struct HeatPaymentCost
{
    public HeatPaymentCost(int amount, int beforeBread, bool breadWasArmed)
    {
        Amount = amount;
        BeforeBread = beforeBread;
        BreadWasArmed = breadWasArmed;
    }
    public int Amount { get; }
    public int BeforeBread { get; }
    public bool BreadWasArmed { get; }
}

/// <summary>One-shot cost resolution, without paying cards or executing spin/presentation effects.</summary>
public static class HeatPaymentCostRules
{
    /// <summary>Consumes passive/bread discounts in the existing order. Non-positive requests consume nothing.</summary>
    public static HeatPaymentCost Resolve(PlayerState player, int requested)
    {
        if (requested <= 0) return default;
        int amount = DriverSkillRules.ApplyHeatMultiplier(player?.driverSkill, requested);
        if (player?.driverSkill != null)
            amount = Math.Max(0, amount - player.driverSkill.ConsumePassiveHeatDiscount());
        int beforeBread = amount;
        bool breadArmed = player.trickState.schwarzbrotActive;
        amount = TrickCardRules.ApplySchwarzbrot(player.trickState, amount);
        return new HeatPaymentCost(amount, beforeBread, breadArmed);
    }
}
