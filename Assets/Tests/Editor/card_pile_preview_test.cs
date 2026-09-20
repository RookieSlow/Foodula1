using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
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
            Assert.That(preview.DisplayedStackLayerCount, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(previewObject);
        }
    }

    [TestCase(0, 0)]
    [TestCase(1, 1)]
    [TestCase(3, 1)]
    [TestCase(4, 2)]
    [TestCase(18, 6)]
    [TestCase(30, 7)]
    public void StackThicknessScalesWithPileCountAndClamps(int pileCount, int expectedLayers)
    {
        Assert.That(CardPilePreviewRules.GetStackLayerCount(pileCount, 7, 3),
            Is.EqualTo(expectedLayers));
    }

    [Test]
    public void DrawPileInspectorGroupsCompositionWithoutRevealingOrder()
    {
        var database = TrickCardDatabaseFactory.CreateDefault();
        var pile = new List<CardData>
        {
            new CardData(CardType.Speed, 3),
            CardData.CreateTrick("cn-hotpot-base"),
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 3)
        };

        List<CardPileEntry> entries = CardPileInspectorRules.BuildEntries(
            pile, newestCardIsAtEnd: false, concealOrder: true, database);

        Assert.That(entries.Count, Is.EqualTo(3));
        Assert.That(entries[0].Title, Is.EqualTo("速度牌 1"));
        Assert.That(entries[1].Title, Is.EqualTo("速度牌 3"));
        Assert.That(entries[1].Count, Is.EqualTo(2));
        Assert.That(entries[2].Effect, Does.Contain("下一张正常打出的速度牌"));
    }

    [Test]
    public void DiscardInspectorShowsNewestCardFirstWithConcreteEffects()
    {
        var database = TrickCardDatabaseFactory.CreateDefault();
        var pile = new List<CardData>
        {
            new CardData(CardType.Speed, 2),
            new CardData(CardType.Heat, 0),
            CardData.CreateTrick("uk-scone")
        };

        List<CardPileEntry> entries = CardPileInspectorRules.BuildEntries(
            pile, newestCardIsAtEnd: true, concealOrder: false, database);

        Assert.That(entries.Select(entry => entry.Title), Is.EqualTo(new[]
        {
            "司康 · 进攻", "热量牌", "速度牌 2"
        }));
        Assert.That(entries[0].Effect, Does.Contain("前进+2格"));
        Assert.That(entries[1].Effect, Does.Contain("冷却返回引擎"));
        Assert.That(entries[2].Effect, Does.Contain("2 点基础移动"));
    }

    [Test]
    public void SelectedCardEffectReusesConcretePileInspectorCopy()
    {
        TrickCardDatabase database = TrickCardDatabaseFactory.CreateDefault();

        string speed = CardSelectionEffectRules.Format(
            new CardData(CardType.Speed, 4), database);
        string trick = CardSelectionEffectRules.Format(
            CardData.CreateTrick("uk-scone"), database);

        Assert.That(speed, Does.Contain("速度牌 4"));
        Assert.That(speed, Does.Contain("4 点基础移动"));
        Assert.That(trick, Does.Contain("司康"));
        Assert.That(trick, Does.Contain("前进+2格"));
    }

    [Test]
    public void ClickingRuntimePileCreatesScrollableVisualInspector()
    {
        GameObject canvasObject = new GameObject(
            "PileInspectorTestCanvas", typeof(Canvas));
        GameObject pileObject = new GameObject(
            "DrawPilePanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        pileObject.transform.SetParent(canvasObject.transform, false);
        try
        {
            CardPilePreviewUI preview = pileObject.AddComponent<CardPilePreviewUI>();
            preview.Configure(null, null, null, null, null, false);
            preview.ConfigureInspection(
                TrickCardDatabaseFactory.CreateDefault(), "抽牌堆", concealOrder: true);
            preview.Refresh(new List<CardData>
            {
                new CardData(CardType.Speed, 1),
                new CardData(CardType.Speed, 2),
                CardData.CreateTrick("uk-scone")
            }, 3);

            pileObject.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            Transform overlay = canvasObject.transform.Find("CardPileInspectorOverlay");
            Assert.That(overlay, Is.Not.Null);
            Assert.That(
                overlay.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true),
                Is.Not.Null);
            Assert.That(
                overlay.GetComponentsInChildren<TMP_Text>(true)
                    .Any(text => text.text.Contains("抽牌堆 · 3 张")),
                Is.True);
            Assert.That(
                overlay.GetComponentsInChildren<TMP_Text>(true)
                    .Any(text => text.text.Contains("前进+2格")),
                Is.True);
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }
}
