using System.Collections.Generic;

/// <summary>Pure card-selection policy used by <see cref="AIController"/>.</summary>
public static class AIPlanner
{
    public static List<CardData> ChooseSpeedCards(
        CardDeck deck,
        int gear,
        float heatRatio,
        bool hasCornerRisk,
        float heatWarningThreshold,
        float cautiousHeatThreshold,
        float variationChance,
        IRandomSource randomSource)
    {
        bool chooseLowCards =
            heatRatio >= heatWarningThreshold ||
            (hasCornerRisk && heatRatio >= cautiousHeatThreshold);

        List<CardData> chosen = chooseLowCards
            ? deck.GetBottomNSpeedCards(gear)
            : deck.GetTopNSpeedCards(gear);

        if (chosen.Count > 1 &&
            variationChance > 0f &&
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
