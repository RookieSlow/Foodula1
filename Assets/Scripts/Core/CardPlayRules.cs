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
    /// <summary>Commits one exact speed card from hand to the current turn's played area.</summary>
    public static SpeedCardCommitResult CommitSpeedCard(
        PlayerState player,
        CardData card,
        int maxSpeedCards)
    {
        if (player == null || player.deck == null || card == null || !card.IsSpeed)
            return SpeedCardCommitResult.InvalidCard;

        if (!player.deck.ContainsInHand(card))
            return SpeedCardCommitResult.CardNotInHand;

        if (player.playedSpeedCardsThisTurn.Count >= maxSpeedCards)
            return SpeedCardCommitResult.SpeedLimitReached;

        if (player.deck.RemoveFromHand(new System.Collections.Generic.List<CardData> { card }).Count != 1)
            return SpeedCardCommitResult.CardNotInHand;

        player.playedSpeedCardsThisTurn.Add(card);
        return SpeedCardCommitResult.Success;
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
            if (card == null || !card.IsSpeed)
                return SpeedCardCommitResult.InvalidCard;
            if (!unique.Add(card) || !player.deck.ContainsInHand(card))
                return SpeedCardCommitResult.CardNotInHand;
        }

        List<CardData> removed = player.deck.RemoveFromHand(new List<CardData>(cards));
        if (removed.Count != cards.Count)
            return SpeedCardCommitResult.CardNotInHand;

        for (int i = 0; i < cards.Count; i++)
            player.playedSpeedCardsThisTurn.Add(cards[i]);
        return SpeedCardCommitResult.Success;
    }

    /// <summary>
    /// Returns the Hotpot movement bonus only when the player actually used its
    /// additional ATTACK-card slot beyond gear and Kanto Oden slots.
    /// </summary>
    public static int GetHotpotMovementBonus(PlayerState player)
    {
        if (player == null || !TrickCardRules.HasHotpotAttack(player.trickState))
            return 0;

        int normalSlots = player.gear + player.extraCardSlotsThisTurn;
        return player.playedSpeedCardsThisTurn.Count > normalSlots
            ? TrickCardRules.GetHotpotSpeedBonus()
            : 0;
    }
}
