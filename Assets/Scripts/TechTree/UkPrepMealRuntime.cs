using System.Collections.Generic;

/// <summary>Per-racer, race-only UK L1 input sources. No profile or UI state is stored here.</summary>
public sealed class UkPrepMealRuntime
{
    private List<CardData> oldHandHeat = new List<CardData>();
    private List<CardData> oldDiscardHeat = new List<CardData>();

    public int LastUsedLap { get; private set; } = -1;
    public int InputTurn { get; private set; } = -1;
    public int Version { get; private set; }
    public bool IsOpen { get; private set; }
    public IReadOnlyList<CardData> OldHandHeat => oldHandHeat.AsReadOnly();
    public IReadOnlyList<CardData> OldDiscardHeat => oldDiscardHeat.AsReadOnly();

    /// <summary>Freeze once after draw/breakfast. Refreshes in the same input cannot add later payments.</summary>
    public bool BeginInput(CardDeck deck, int globalTurn)
    {
        if (deck == null || globalTurn < 0 || InputTurn == globalTurn) return false;
        CloseInput();
        InputTurn = globalTurn;
        IsOpen = true;
        Capture(deck.Hand, oldHandHeat);
        Capture(deck.DiscardPile, oldDiscardHeat);
        return true;
    }

    private static void Capture(IReadOnlyList<CardData> pile, List<CardData> target)
    {
        foreach (CardData card in pile)
            if (card != null && card.IsHeat && !card.isTemp && !target.Contains(card)) target.Add(card);
    }

    /// <summary>Read currently owned frozen sources, in hand-before-discard order. Draw is excluded.</summary>
    public List<CardData> GetRecoverableSources(CardDeck deck, int limit)
    {
        var result = new List<CardData>();
        if (!IsOpen || deck == null || limit <= 0) return result;
        Append(deck.Hand, deck, limit, result);
        Append(deck.DiscardPile, deck, limit, result);
        return result;
    }

    private void Append(IReadOnlyList<CardData> pile, CardDeck deck, int limit, List<CardData> result)
    {
        foreach (CardData card in pile)
        {
            if (result.Count == limit) return;
            if (card == null || !card.IsHeat || card.isTemp || result.Contains(card)) continue;
            if ((oldHandHeat.Contains(card) || oldDiscardHeat.Contains(card)) &&
                deck.IsUniquePermanentHeatInHandOrDiscard(card)) result.Add(card);
        }
    }

    /// <summary>Closes only the input, retaining the lap use and the deduplication turn.</summary>
    public void CloseInput()
    {
        IsOpen = false;
        Version++;
        oldHandHeat.Clear();
        oldDiscardHeat.Clear();
    }

    internal void CommitValidatedUse(int lap)
    {
        LastUsedLap = lap;
        CloseInput();
    }

    /// <summary>A new race clears uses as well as source references.</summary>
    public void ResetForRace()
    {
        CloseInput();
        LastUsedLap = -1;
        InputTurn = -1;
    }

    /// <summary>Forecast owns new containers; card identities remain immutable shared inputs.</summary>
    public UkPrepMealRuntime CreatePresentationCopy()
    {
        var copy = (UkPrepMealRuntime)MemberwiseClone();
        copy.oldHandHeat = new List<CardData>(oldHandHeat);
        copy.oldDiscardHeat = new List<CardData>(oldDiscardHeat);
        return copy;
    }
}
