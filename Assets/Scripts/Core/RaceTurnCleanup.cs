using System;

/// <summary>Ordered turn-end card ownership cleanup with injected runtime effect adapters.</summary>
public static class RaceTurnCleanup
{
    /// <summary>
    /// Requires a participant with a deck. Played references remain available to effects until completion.
    /// Adapters retain skill, presentation and technology behavior; exceptions propagate as before.
    /// </summary>
    public static void Execute(
        PlayerState player,
        Action<PlayerState> resolveSkills,
        Action<PlayerState, int> reportTemporaryCards,
        Action<PlayerState> resolveTechnology)
    {
        int playedTemporaryHeat = player.deck.DiscardPlayedSpeedCards(player.playedSpeedCardsThisTurn);
        resolveSkills?.Invoke(player);
        int removed = playedTemporaryHeat + player.deck.RemoveTempCardsFromHand();
        if (removed > 0) reportTemporaryCards?.Invoke(player, removed);

        // Evaluate after skill effects, not from a stale start-of-cleanup snapshot.
        if (!RaceTurnRules.IsTerminal(player) && player.techState != null)
            resolveTechnology?.Invoke(player);

        player.playedSpeedCardsThisTurn.Clear();
    }
}
