using System.Collections.Generic;

/// <summary>Result of committing one or more speed cards during the human card-play phase.</summary>
public enum SpeedCardCommitResult
{
    Success,
    InvalidCard,
    CardNotInHand,
    SpeedLimitReached
}

/// <summary>
/// Pure card-play mutations shared by UI orchestration and EditMode tests.
/// Movement is resolved later; this class only moves a confirmed speed card
/// from hand into the current turn's played-card area.
/// </summary>
public static class CardPlayRules
{
    public static bool CanUseHeatAsSpeed(PlayerState player, RaceSession session, int totalCells)
    {
        return player != null && session != null && totalCells > 0 &&
            session.HasSmokedBBQAtPosition(player, player.position, totalCells);
    }

    /// <summary>Read-only sorted candidates; heat remains its original instance.</summary>
    public static List<CardData> GetSpeedCandidates(CardDeck deck, bool heatAsSpeed)
    {
        if (!heatAsSpeed) return deck.GetSpeedCardsSortedDesc();
        var cards = new List<CardData>();
        foreach (CardData card in deck.Hand)
            if (card != null && (card.IsSpeed || card.IsHeat)) cards.Add(card);
        cards.Sort((a, b) => GetCommittedSpeedCardValue(b).CompareTo(GetCommittedSpeedCardValue(a)));
        return cards;
    }

    /// <summary>
    /// Full recovery (spin/pit) must retire committed heat before cooling. Remove
    /// its played references so later cleanup cannot reinsert already cooled heat.
    /// Ordinary speed references and their existing cleanup timing stay intact.
    /// </summary>
    public static void RetireCommittedHeatForRecovery(PlayerState player)
    {
        var heat = new List<CardData>();
        for (int i = player.playedSpeedCardsThisTurn.Count - 1; i >= 0; i--)
            if (player.playedSpeedCardsThisTurn[i].IsHeat)
            {
                heat.Add(player.playedSpeedCardsThisTurn[i]);
                player.playedSpeedCardsThisTurn.RemoveAt(i);
            }
        if (heat.Count > 0) player.deck.DiscardPlayedSpeedCards(heat);
    }
    /// <summary>Read-only current-turn requirement; optional capacity never raises mandatory cards.</summary>
    public static TeamGearRules.SpeedCardRequirement GetSpeedCardRequirement(PlayerState player)
    {
        return TeamGearRules.GetSpeedCardRequirement(
            player.teamId, player.gear, player.chinaConsecutiveGearCount,
            GetOptionalSpeedCardSlots(player));
    }

    /// <summary>
    /// Returns real optional card slots. A pending Hotpot ATTACK deliberately
    /// does not participate because it empowers a normal slot instead.
    /// </summary>
    public static int GetOptionalSpeedCardSlots(PlayerState player)
    {
        return player == null ? 0 : System.Math.Max(0, player.extraCardSlotsThisTurn);
    }

    /// <summary>Commits one exact speed card from hand to the current turn's played area.</summary>
    public static SpeedCardCommitResult CommitSpeedCard(
        PlayerState player,
        CardData card,
        int maxSpeedCards)
    {
        return CommitSpeedCards(player, new List<CardData> { card }, maxSpeedCards);
    }

    /// <summary>
    /// Commits a selected group of exact speed-card instances atomically.
    /// The UI may use this for multi-select play, while a one-card selection
    /// follows the same path and therefore has identical validation.
    /// </summary>
    public static SpeedCardCommitResult CommitSpeedCards(
        PlayerState player,
        IReadOnlyList<CardData> cards,
        int maxSpeedCards)
    {
        return CommitSpeedCardsInternal(player, cards, maxSpeedCards, false);
    }

    /// <summary>
    /// Explicit BBQ-aware entry. Eligibility is read at commitment time, using
    /// the current position and effective technology; the ordinary entry stays
    /// speed-only. The original heat instance/type/value is never converted.
    /// </summary>
    public static SpeedCardCommitResult CommitSpeedCards(
        PlayerState player,
        IReadOnlyList<CardData> cards,
        int maxSpeedCards,
        RaceSession session,
        int totalCells)
    {
        bool heatAsSpeed = CanUseHeatAsSpeed(player, session, totalCells);
        return CommitSpeedCardsInternal(player, cards, maxSpeedCards, heatAsSpeed);
    }

    /// <summary>
    /// Value of an instance in the committed speed area, not a general hand
    /// value. A legally committed BBQ heat remains speed 2 after leaving its zone.
    /// </summary>
    public static int GetCommittedSpeedCardValue(CardData card)
    {
        return card.IsHeat ? 2 : card.value;
    }

    public static int SumCommittedSpeedCardValues(IReadOnlyList<CardData> cards)
    {
        int total = 0;
        if (cards != null)
            for (int i = 0; i < cards.Count; i++)
                total += GetCommittedSpeedCardValue(cards[i]);
        return total;
    }

    private static SpeedCardCommitResult CommitSpeedCardsInternal(
        PlayerState player,
        IReadOnlyList<CardData> cards,
        int maxSpeedCards,
        bool heatAsSpeed)
    {
        if (player == null || player.deck == null || cards == null || cards.Count == 0)
            return SpeedCardCommitResult.InvalidCard;

        int remaining = maxSpeedCards - player.playedSpeedCardsThisTurn.Count;
        if (cards.Count > remaining)
            return SpeedCardCommitResult.SpeedLimitReached;

        // Validate the entire selection before mutating the deck. This keeps
        // a stale UI selection from consuming only part of the group.
        var unique = new HashSet<CardData>();
        for (int i = 0; i < cards.Count; i++)
        {
            CardData card = cards[i];
            if (card == null || (!card.IsSpeed && !(heatAsSpeed && card.IsHeat)))
                return SpeedCardCommitResult.InvalidCard;
            if (!unique.Add(card) || !player.deck.ContainsInHand(card))
                return SpeedCardCommitResult.CardNotInHand;
        }

        List<CardData> removed = player.deck.RemoveFromHand(new List<CardData>(cards));
        if (removed.Count != cards.Count)
            return SpeedCardCommitResult.CardNotInHand;

        for (int i = 0; i < cards.Count; i++)
            player.playedSpeedCardsThisTurn.Add(cards[i]);

        // Hotpot does not add a slot. It turns the first speed card in the next
        // successful commit into ATTACK, so a failed/stale selection never
        // consumes the effect. Players can confirm one card first when they
        // want to choose the exact card that receives the marker.
        if (TrickCardRules.ConsumeHotpotAttack(player.trickState))
        {
            player.hotpotAttackAppliedThisTurn = true;
            player.hotpotAttackCardValueThisTurn = GetCommittedSpeedCardValue(cards[0]);
        }
        return SpeedCardCommitResult.Success;
    }

    /// <summary>
    /// Returns the part of the ATTACK card that is moved out of corner speed,
    /// plus Hotpot's +1 movement. Adding this to the reduced corner speed keeps
    /// total movement equal to the original card total +1.
    /// </summary>
    public static int GetHotpotMovementContribution(PlayerState player, int speedPerCardBonus = 0)
    {
        if (player == null || !player.hotpotAttackAppliedThisTurn)
            return 0;

        return GetHotpotCornerExclusion(player, speedPerCardBonus) +
            TrickCardRules.GetHotpotSpeedBonus();
    }

    /// <summary>Returns the full effective ATTACK-card speed excluded from corner checks.</summary>
    public static int GetHotpotCornerExclusion(PlayerState player, int speedPerCardBonus = 0)
    {
        if (player == null || !player.hotpotAttackAppliedThisTurn)
            return 0;

        return System.Math.Max(0, player.hotpotAttackCardValueThisTurn + speedPerCardBonus);
    }
}
