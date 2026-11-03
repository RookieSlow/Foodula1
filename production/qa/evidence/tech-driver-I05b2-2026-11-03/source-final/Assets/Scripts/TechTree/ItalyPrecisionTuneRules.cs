using System;
using System.Collections.Generic;

/// <summary>Locked IT L3B, Word paragraphs 300-312: a separate -1 after source values, once per race.</summary>
public static class ItalyPrecisionTuneRules
{
    public const int Reduction = 1;
    public const int MinimumContribution = 2;

    public static int GetSourceContribution(PlayerState player, CardData card)
        => card == null ? 0 : CardPlayRules.GetCommittedSpeedCardValue(card) +
            DriverSkillRules.GetSpeedPerCardBonus(player?.driverSkill);

    public static bool IsEligibleTarget(PlayerState player, IReadOnlyList<CardData> selected,
        CardData target, CardData driverSlowdownCard = null)
    {
        if (player?.deck == null || target == null || !target.IsSpeed || target.isTemp ||
            !string.IsNullOrEmpty(target.trickId) || ReferenceEquals(target, driverSlowdownCard) ||
            !player.deck.ContainsInHand(target) || GetSourceContribution(player, target) < MinimumContribution ||
            selected == null || selected.Count == 0) return false;
        int matches = 0;
        var unique = new HashSet<CardData>();
        foreach (CardData card in selected)
        {
            if (card == null || !unique.Add(card)) return false;
            if (ReferenceEquals(card, target)) matches++;
        }
        return matches == 1;
    }

    /// <summary>The original card remains shared. Only this owner's committed instance receives the delta.</summary>
    public static int GetCommittedReduction(PlayerState player)
    {
        ItalyPrecisionTuneRuntime runtime = player?.techState?.precisionTune;
        return runtime != null && runtime.IsCommittedFor(player) ? Reduction : 0;
    }
}
