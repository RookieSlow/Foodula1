using System;

/// <summary>
/// Pure presentation data for the current speed-card requirement.
/// The values mirror the existing missing-card rule without changing its
/// gameplay result, so UI and EditMode tests can share one calculation.
/// </summary>
public readonly struct GearRequirementFeedback
{
    public GearRequirementFeedback(
        int requiredCards,
        int confirmedCards,
        int pendingCards,
        int speedCardsInHand,
        int engineHeat)
    {
        RequiredCards = Math.Max(0, requiredCards);
        ConfirmedCards = Math.Max(0, confirmedCards);
        PendingCards = Math.Max(0, pendingCards);
        DisplayedCards = ConfirmedCards + PendingCards;
        MissingCards = Math.Max(0, RequiredCards - DisplayedCards);
        PotentialCards = ConfirmedCards + Math.Max(0, speedCardsInHand);
        EngineHeat = Math.Max(0, engineHeat);
    }

    public int RequiredCards { get; }
    public int ConfirmedCards { get; }
    public int PendingCards { get; }
    public int DisplayedCards { get; }
    public int MissingCards { get; }
    public int PotentialCards { get; }
    public int EngineHeat { get; }

    public bool WillTriggerEngineFailure => MissingCards > 0;
    public bool HandCannotSatisfy => PotentialCards < RequiredCards;
    public bool MayTriggerSpin => MissingCards > EngineHeat;
}

public static class GearRequirementFeedbackRules
{
    public static GearRequirementFeedback Evaluate(
        int requiredCards,
        int confirmedCards,
        int pendingCards,
        int speedCardsInHand,
        int engineHeat)
    {
        return new GearRequirementFeedback(
            requiredCards,
            confirmedCards,
            pendingCards,
            speedCardsInHand,
            engineHeat);
    }

    public static string FormatStatus(string gearLabel, GearRequirementFeedback feedback)
    {
        string summary =
            $"{gearLabel}（要求 {feedback.RequiredCards} 张） | 本回合 {feedback.DisplayedCards}/{feedback.RequiredCards} 张速度牌";

        if (!feedback.WillTriggerEngineFailure)
            return summary + "\n<color=#64D987>已满足要求，可结束出牌</color>";

        string warning =
            $"<color=#FFB347>结束出牌将触发引擎故障：缺 {feedback.MissingCards} 张 → +{feedback.MissingCards} 热量</color>";
        if (feedback.HandCannotSatisfy)
            warning += "\n<color=#FFB347>当前手牌速度牌不足，无法补足要求</color>";
        if (feedback.MayTriggerSpin)
            warning += "\n<color=#FF6B6B>引擎热量不足，可能触发失控</color>";

        return summary + "\n" + warning;
    }

    public static string FormatEndButtonLabel(GearRequirementFeedback feedback)
    {
        if (!feedback.WillTriggerEngineFailure)
            return "结束出牌";
        if (feedback.MayTriggerSpin)
            return $"结束出牌 · 缺{feedback.MissingCards}，可能失控";
        return $"结束出牌 · 缺{feedback.MissingCards}→+{feedback.MissingCards}热";
    }
}
