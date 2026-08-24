using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CardPilePreviewTests
{
    [Test]
    public void DrawPilePreviewUsesActualDrawOrder()
    {
        CardData heat = new CardData(CardType.Heat, 0);
        CardData first = new CardData(CardType.Speed, 1);
        CardData second = new CardData(CardType.Speed, 2);
        CardData fourth = new CardData(CardType.Speed, 4);
        List<CardData> pile = new List<CardData> { heat, first, second, fourth };

        List<CardData> visible = CardPilePreviewRules.SelectVisibleCards(pile, 3, false);

        Assert.That(visible, Is.EqualTo(new[] { heat, first, second }));
    }

    [Test]
    public void DiscardPilePreviewStartsWithMostRecentlyDiscardedCard()
    {
        CardData first = new CardData(CardType.Speed, 1);
        CardData second = new CardData(CardType.Speed, 2);
        CardData trick = CardData.CreateTrick("attack");
        List<CardData> pile = new List<CardData> { first, second, trick };

        List<CardData> visible = CardPilePreviewRules.SelectVisibleCards(pile, 3, true);

        Assert.That(visible, Is.EqualTo(new[] { trick, second, first }));
    }

    [Test]
    public void PreviewClampsToConfiguredCountAndSkipsNullEntries()
    {
        CardData first = new CardData(CardType.Speed, 1);
        CardData second = new CardData(CardType.Speed, 2);
        List<CardData> pile = new List<CardData> { null, first, null, second };

        List<CardData> visible = CardPilePreviewRules.SelectVisibleCards(pile, 1, false);

        Assert.That(visible.Count, Is.EqualTo(1));
        Assert.That(visible[0], Is.SameAs(first));
    }

    [Test]
    public void RuntimePreviewRefreshTracksVisibleCardsAndPileCount()
    {
        GameObject previewObject = new GameObject("CardPilePreviewTest");
        try
        {
            CardPilePreviewUI preview = previewObject.AddComponent<CardPilePreviewUI>();
            preview.Configure(null, null, null, null, null, false);
            preview.Refresh(new List<CardData>
            {
                new CardData(CardType.Speed, 1),
                new CardData(CardType.Speed, 2),
                new CardData(CardType.Speed, 3),
                new CardData(CardType.Speed, 4)
            }, 4);

            Assert.That(preview.DisplayedCardCount, Is.EqualTo(3));
            Assert.That(preview.DisplayedPileCount, Is.EqualTo(4));
        }
        finally
        {
            Object.DestroyImmediate(previewObject);
        }
    }
}
