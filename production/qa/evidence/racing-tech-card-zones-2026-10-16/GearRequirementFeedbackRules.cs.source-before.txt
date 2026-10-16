using System;
using System.Collections.Generic;

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
    /// <summary>Formats the existing gear/extra-capacity label without reading scene state.</summary>
    public static string FormatRequirementLabel(
        TeamId team, int gear, TeamGearRules.SpeedCardRequirement requirement)
    {
        string gearName = TeamGearRules.GetDisplayName(team, gear);
        return requirement.ExtraCardCount > 0
            ? $"{gearName} 档（基础 {requirement.BaseCardCount} + 额外 {requirement.ExtraCardCount}）"
            : $"{gearName} 档";
    }

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

    /// <summary>Formats the pending speed-card selection without reading UI or scene state.</summary>
    public static string FormatPendingSpeedStatus(string requirementStatus, IReadOnlyList<CardData> selected)
    {
        return $"{requirementStatus}\n待确认：{selected.Count} 张速度牌（速度总和 {CardPlayRules.SumCommittedSpeedCardValues(selected)}）";
    }

    public static string FormatSpeedCommitFailure(SpeedCardCommitResult result, int maxCards)
        => result == SpeedCardCommitResult.SpeedLimitReached
            ? $"速度牌已达上限：{maxCards} 张（本次选择未提交）"
            : "出牌失败：手牌状态已变化";

    public static string FormatCommittedSpeedLog(string playerName, IReadOnlyList<CardData> cards)
        => $"{playerName} 确认 {cards.Count} 张速度牌（速度总和 {CardPlayRules.SumCommittedSpeedCardValues(cards)}）。";

    public static string FormatCommittedSpeedStatus(int committedCards, int maxCards)
        => $"已打出 {committedCards}/{maxCards} 张速度牌；可继续多选或结束出牌";

    public static string FormatPendingTrickStatus(string requirementStatus, string trickName)
        => $"{requirementStatus}\n待确认：{trickName}（确认后立即发动）";

    /// <summary>Preserves the action label and space shortcut for discard, end, trick and speed selection.</summary>
    public static string FormatActionButtonLabel(
        bool discardMode, int selectedCount, bool selectedTrick, GearRequirementFeedback feedback)
    {
        string label;
        if (discardMode)
            label = "弃置所选牌";
        else if (selectedCount == 0)
            label = feedback.RequiredCards > 0 ? FormatEndButtonLabel(feedback) : "结束出牌";
        else if (selectedTrick)
            label = "打出特技牌";
        else
            label = selectedCount == 1 ? "打出速度牌" : $"打出速度牌 ({selectedCount})";

        return label + (discardMode || selectedCount > 0 ? "  [空格]" : "");
    }
}
