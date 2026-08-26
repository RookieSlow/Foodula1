using System.Collections.Generic;

/// <summary>Pure card-selection policy used by <see cref="AIController"/>.</summary>
public static class AIPlanner
{
    /// <summary>
    /// Calculates the movement needed to finish one cell behind a nearby
    /// opponent after accounting for the opponent's estimated movement.
    /// Returns false when the opponent is not in the configured planning
    /// window or is not ahead on the circular track.
    /// </summary>
    public static bool TryGetSlipstreamTargetMovement(
        int followerPosition,
        int leaderPosition,
        int leaderPlannedMovement,
        int totalNodes,
        int planningRange,
        out int targetMovement)
    {
        targetMovement = 0;
        if (totalNodes <= 0 || planningRange <= 0 || leaderPlannedMovement < 0)
            return false;

        int gap = RaceSession.ForwardDistance(followerPosition, leaderPosition, totalNodes);
        // A one-cell gap is already a valid slipstream setup. Keep it in the
        // planner so the follower can match the leader's expected movement
        // instead of accidentally overtaking when the leader accelerates.
        // Same-cell cars need the end-of-turn arrival resolver, not this plan.
        if (gap <= 0 || gap > totalNodes / 2 || gap > planningRange)
            return false;

        // The runtime trigger range is one cell.  With a current gap of G
        // and a leader expected to move L, the follower should move G+L-1.
        targetMovement = gap + leaderPlannedMovement - 1;
        return targetMovement > 0;
    }

    /// <summary>
    /// Finds exactly <paramref name="cardCount"/> speed cards whose values
    /// sum to <paramref name="targetMovement"/>. Card instances are returned
    /// in descending value order and remain owned by the deck until the
    /// caller commits them. Returns false when no exact combination exists.
    /// </summary>
    public static bool TryFindExactSpeedCards(
        CardDeck deck,
        int cardCount,
        int targetMovement,
        out List<CardData> result)
    {
        result = null;
        if (deck == null || cardCount <= 0 || targetMovement <= 0)
            return false;

        List<CardData> candidates = deck.GetSpeedCardsSortedDesc();
        if (candidates.Count < cardCount)
            return false;

        var selected = new List<CardData>(cardCount);
        if (!FindExactSpeedCards(candidates, 0, cardCount, targetMovement, selected))
            return false;

        result = selected;
        return true;
    }

    public static List<CardData> ChooseSpeedCards(
        CardDeck deck,
        int maxCards,
        float heatRatio,
        bool hasCornerRisk,
        float heatWarningThreshold,
        float cautiousHeatThreshold,
        float variationChance,
        IRandomSource randomSource)
    {
        // A known corner risk takes priority over aggression even when the
        // engine is still cool.  Choosing the highest cards in that state
        // creates avoidable overspeed spins and makes cross-team benchmarks
        // measure reckless card selection instead of vehicle identity.
        bool chooseLowCards =
            hasCornerRisk ||
            heatRatio >= heatWarningThreshold ||
            (hasCornerRisk && heatRatio >= cautiousHeatThreshold);

        List<CardData> chosen = chooseLowCards
            ? deck.GetBottomNSpeedCards(maxCards)
            : deck.GetTopNSpeedCards(maxCards);

        if (chosen.Count > 1 &&
            variationChance > 0f &&
            randomSource != null &&
            randomSource.NextDouble() < variationChance)
        {
            Shuffle(chosen, randomSource);
        }

        return chosen;
    }

    private static bool FindExactSpeedCards(
        IReadOnlyList<CardData> candidates,
        int startIndex,
        int remainingCards,
        int remainingMovement,
        List<CardData> selected)
    {
        if (remainingCards == 0)
            return remainingMovement == 0;
        if (remainingMovement <= 0 || candidates.Count - startIndex < remainingCards)
            return false;

        for (int i = startIndex; i <= candidates.Count - remainingCards; i++)
        {
            CardData card = candidates[i];
            if (card == null || !card.IsSpeed || card.value <= 0 || card.value > remainingMovement)
                continue;

            selected.Add(card);
            if (FindExactSpeedCards(
                candidates,
                i + 1,
                remainingCards - 1,
                remainingMovement - card.value,
                selected))
            {
                return true;
            }
            selected.RemoveAt(selected.Count - 1);
        }

        return false;
    }

    private static void Shuffle<T>(IList<T> list, IRandomSource randomSource)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = randomSource.NextInt(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
