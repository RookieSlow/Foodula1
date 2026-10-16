using System.Collections.Generic;

/// <summary>Pure wrapping navigation for the playable cards in a rendered hand.</summary>
public static class CardKeyboardNavigationRules
{
    public static int FindNextIndex(IReadOnlyList<bool> playable, int currentIndex, int direction)
    {
        if (playable == null || playable.Count == 0 || direction == 0)
            return -1;

        int step = direction < 0 ? -1 : 1;
        int index = currentIndex;
        for (int i = 0; i < playable.Count; i++)
        {
            index = (index + step + playable.Count) % playable.Count;
            if (playable[index])
                return index;
        }
        return -1;
    }
}

public enum CardActionShortcutIntent
{
    None,
    SubmitSelection,
    ConfirmDiscard
}

/// <summary>
/// Maps Space to a selected-card action, separate from ending an empty play phase.
/// </summary>
public static class CardActionShortcutRules
{
    public static CardActionShortcutIntent Resolve(bool isDiscardMode, int selectedCardCount)
    {
        if (isDiscardMode)
            return CardActionShortcutIntent.ConfirmDiscard;
        return selectedCardCount > 0
            ? CardActionShortcutIntent.SubmitSelection
            : CardActionShortcutIntent.None;
    }
}

/// <summary>Builds the compact effect copy shown for the selected hand card.</summary>
public static class CardSelectionEffectRules
{
    public static string Format(CardData card, TrickCardDatabase database)
    {
        if (card == null)
            return string.Empty;

        string title = CardPileInspectorRules.GetCardTitle(card, database);
        string effect = CardPileInspectorRules.GetCardEffect(card, database);
        return $"<color=#FFD27A><b>{title}</b></color>　{effect}";
    }
}
