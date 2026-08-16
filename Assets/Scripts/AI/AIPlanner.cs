using System.Collections.Generic;

/// <summary>Pure card-selection policy used by <see cref="AIController"/>.</summary>
public static class AIPlanner
{
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
