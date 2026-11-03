using System;
using System.Collections.Generic;

/// <summary>Locked D05 Word 343-362: source-first -1, then one actually earned safe-apex exit tail.</summary>
public static class AscariPrecisionRules
{
    public const int Reduction = 1;
    public static int GetExitReward(int tier) => tier <= 0 ? 0 : tier == 1 ? 2 : 3;

    public static bool IsEligibleTarget(PlayerState player, IReadOnlyList<CardData> selected,
        CardData target, CardData technologyTarget = null)
        => ItalyPrecisionTuneRules.IsEligibleTarget(player, selected, target, technologyTarget);

    public static int GetCommittedReduction(PlayerState player)
        => player?.driverSkill?.Precision.IsCommittedFor(player) == true ? Reduction : 0;

    /// <summary>Ordered actual node path; the first apex cannot be chosen by HashSet enumeration.</summary>
    public static bool TryGetFirstBaseApex(IReadOnlyList<TrackNode> nodes, int start,
        int speed, int lapsRemaining, out int cornerId, out int distance)
    {
        cornerId = 0; distance = 0;
        if (nodes == null || nodes.Count == 0 || speed <= 0 || lapsRemaining <= 0) return false;
        int end = TrackRules.EndBeforeFinalFinish(nodes, start, start + speed, lapsRemaining);
        for (int p = start + 1; p <= end; p++)
        {
            int index = (p % nodes.Count + nodes.Count) % nodes.Count;
            TrackNode node = nodes[index];
            if (node != null && node.isApex && node.cornerId > 0)
            { cornerId = node.cornerId; distance = p - start; return true; }
        }
        return false;
    }

    /// <summary>Only selected ordinary card contributions form this reachability route; no bonus is borrowed.</summary>
    public static int SelectedBaseSpeed(PlayerState player, IReadOnlyList<CardData> selected,
        bool reduceTechnology)
    {
        int speed = SumOrdinarySources(player, player.playedSpeedCardsThisTurn) + SumOrdinarySources(player, selected);
        return Math.Max(0, speed - Reduction - (reduceTechnology ? ItalyPrecisionTuneRules.Reduction : 0));
    }

    /// <summary>ATTACK's ordinary card body still moves; only Hotpot's additional +1 is excluded from this route.</summary>
    public static int GetCommittedBaseSpeed(PlayerState player)
        => player == null ? 0 : Math.Max(0, SumOrdinarySources(player, player.playedSpeedCardsThisTurn) -
            GetCommittedReduction(player) - ItalyPrecisionTuneRules.GetCommittedReduction(player));

    private static int SumOrdinarySources(PlayerState player, IReadOnlyList<CardData> cards)
    {
        int speed = 0, bonus = DriverSkillRules.GetSpeedPerCardBonus(player.driverSkill);
        if (cards != null) foreach (CardData card in cards)
            if (card != null && card.IsSpeed) speed += Math.Max(0, card.value + bonus);
        return speed;
    }
}
