/// <summary>Semantic origin for an actual ordinary payment; display reason strings carry no authority.</summary>
public enum HeatPaymentSource { Other, OrdinaryCorner }

/// <summary>Data-side result; spin, presentation and tutorial feedback remain with the coordinator.</summary>
public readonly struct RaceHeatPaymentResult
{
    public RaceHeatPaymentResult(int drawn, bool sufficient, bool usedFishAndChips)
    {
        Drawn = drawn;
        Sufficient = sufficient;
        UsedFishAndChips = usedFishAndChips;
    }

    public int Drawn { get; }
    public bool Sufficient { get; }
    public bool UsedFishAndChips { get; }
}

/// <summary>Executes an already-resolved heat cost without scene, audio or tutorial dependencies.</summary>
public static class RaceHeatPaymentExecution
{
    public static RaceHeatPaymentResult Pay(PlayerState player, RaceSession session,
        int amount, HeatPaymentDestination destination, HeatPaymentSource source = HeatPaymentSource.Other)
    {
        if (amount <= 0) return new RaceHeatPaymentResult(0, true, false);

        int drawn = player.deck.DrawHeatFromPool(amount, destination, player.heatPaidCardsThisTurn);
        // Track actual payment before recovery, including insufficient partial payments.
        if (session != null)
        {
            session.TrackHeatPaid(player, drawn);
            if (source == HeatPaymentSource.OrdinaryCorner)
                session.RecordOrdinaryCornerPayment(player, drawn);
        }
        if (drawn < amount)
        {
            return new RaceHeatPaymentResult(drawn, false, false);
        }

        if (player.techState != null)
            TechTreeRules.TrackDimSumCombo(player.techState, false, false, true);
        return new RaceHeatPaymentResult(drawn, true, false);
    }
}
