using System;

/// <summary>Stateless translation of successful heat operations into ordered tutorial signals.</summary>
/// <remarks>The coordinator calls these only for human, positive, completed operations.
/// Each signal must still pass the live Director gate; rejection does not suppress later signals.</remarks>
public static class TutorialHeatFeedbackRules
{
    public static void ReportPayment(TeamId team, HeatPaymentCost cost, int paid,
        HeatPaymentDestination destination, string reason, Func<TutorialAction, string, bool> report)
    {
        if (team == TeamId.DE && cost.BreadWasArmed &&
            reason == "engine failure" && cost.BeforeBread == 2 && paid == 1)
            report(TutorialAction.ResolveDeSchwarzbrot,
                $"requested:{cost.BeforeBread},paid:{paid},discount:1");
        report(TutorialAction.PayHeat, $"amount:{paid},destination:{destination},reason:{reason}");
    }

    public static void ReportCooling(PlayerState player, int cooled, int requested,
        Func<TutorialAction, string, bool> report)
    {
        report(TutorialAction.CoolHeatCard, $"cooled:{cooled},requested:{requested}");
        // Re-read live state after the generic signal, as the existing adapter did.
        if (player.teamId == TeamId.CN && player.gear == ChinaGearShiftRules.RecoverGear)
            report(TutorialAction.CompleteChinaRecover, $"cooled:{cooled},consecutive:{player.chinaConsecutiveGearCount}");
    }
}
