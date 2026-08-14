/// <summary>Result of committing one speed card during the human card-play phase.</summary>
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
