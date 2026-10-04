using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Exercises the real optional-discard coroutine and confirm callback without starting a scene.</summary>
public class RaceDiscardStepAdapterTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private MVPGameManager manager;
    private CardHandUI hand;
    private RaceInputState input;
    private PlayerState player;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Optional discard regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        hand = host.AddComponent<CardHandUI>();
        manager.cardHandUI = hand;
        hand.handContainer = new GameObject("Hand", typeof(RectTransform)).transform;
        hand.handContainer.SetParent(host.transform, false);
        hand.cardPrefab = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(CardUI));
        hand.cardPrefab.transform.SetParent(host.transform, false);
        input = (RaceInputState)typeof(MVPGameManager).GetField("inputState", Private).GetValue(manager);
        player = new PlayerState("driver", false, 0, 2);
        var session = new RaceSession();
        session.Players.Add(player);
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, session);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(host);

    [TestCase(false, false, false, 0)]
    [TestCase(true, false, false, 1)]
    [TestCase(false, true, false, 1)]
    [TestCase(true, true, false, 2)]
    [TestCase(false, false, true, 0)]
    [TestCase(true, false, true, 1)]
    public void ConfirmationDiscardsOnlySelectedPlayableInstances(
        bool selectSpeed, bool selectTrick, bool forceSelectHeat, int expectedDiscarded)
    {
        CardData speed = new CardData(CardType.Speed, 2);
        CardData trick = CardData.CreateTrick("uk-scone");
        CardData heat = CardData.CreateTempHeat();
        player.deck.AddCardsToHand(new[] { speed, trick, heat });
        IEnumerator step = BeginDiscardStep();
        SetSelected(speed, selectSpeed);
        SetSelected(trick, selectTrick);
        SetSelected(heat, forceSelectHeat); // Simulates a stale UI selection; heat remains in hand.
        CardUI speedUi = GetCardUI(speed);
        CardUI trickUi = GetCardUI(trick);

        Assert.That(player.deck.ContainsInHand(speed), Is.True);
        Assert.That(player.deck.ContainsInHand(trick), Is.True);
        Assert.That(input.WaitingForDiscard, Is.True);

        manager.OnPlayCardsButtonClicked();
        Assert.That(input.WaitingForDiscard, Is.False);
        Assert.That(step.MoveNext(), Is.False);

        Assert.That(player.deck.ContainsInHand(speed), Is.EqualTo(!selectSpeed));
        Assert.That(player.deck.ContainsInHand(trick), Is.EqualTo(!selectTrick));
        Assert.That(player.deck.ContainsInHand(heat), Is.True);
        Assert.That(player.deck.DiscardPileCount, Is.EqualTo(expectedDiscarded));
        Assert.That(speedUi == null, Is.EqualTo(selectSpeed));
        Assert.That(trickUi == null, Is.EqualTo(selectTrick));
        if (!selectSpeed) Assert.That(speedUi.gameObject.activeSelf, Is.True);
        if (!selectTrick) Assert.That(trickUi.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void StaleSelectedPresentationDoesNotMoveACardThatIsNotInHand()
    {
        CardData present = new CardData(CardType.Speed, 2);
        CardData stale = new CardData(CardType.Speed, 4);
        player.deck.AddCardsToHand(new[] { present });
        IEnumerator step = BeginDiscardStep();
        CardUI staleUi = new GameObject("Stale", typeof(RectTransform), typeof(Image), typeof(CardUI))
            .GetComponent<CardUI>();
        staleUi.transform.SetParent(hand.handContainer, false);
        staleUi.SetupCard(stale, null);
        staleUi.isSelected = true;
        var cardUIs = (List<CardUI>)typeof(CardHandUI).GetField("cardUIs", Private).GetValue(hand);
        cardUIs.Add(staleUi);

        manager.OnPlayCardsButtonClicked();
        Assert.That(step.MoveNext(), Is.False);

        Assert.That(player.deck.ContainsInHand(present), Is.True);
        Assert.That(player.deck.DiscardPileCount, Is.Zero);
        Assert.That(staleUi.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void LateConfirmationAfterDiscardGateClosesDoesNotChangeCardZones()
    {
        CardData speed = new CardData(CardType.Speed, 3);
        player.deck.AddCardsToHand(new[] { speed });
        IEnumerator step = BeginDiscardStep();
        SetSelected(speed, true);

        manager.OnPlayCardsButtonClicked();
        Assert.That(step.MoveNext(), Is.False);
        manager.OnPlayCardsButtonClicked();

        Assert.That(player.deck.DiscardPileCount, Is.EqualTo(1));
        Assert.That(player.deck.ContainsInHand(speed), Is.False);
    }

    private IEnumerator BeginDiscardStep()
    {
        IEnumerator step = (IEnumerator)typeof(MVPGameManager)
            .GetMethod("DiscardStep", Private).Invoke(manager, null);
        Assert.That(step.MoveNext(), Is.True);
        Assert.That(input.WaitingForDiscard, Is.True);
        return step;
    }

    private CardUI GetCardUI(CardData card)
    {
        foreach (CardUI ui in hand.handContainer.GetComponentsInChildren<CardUI>(true))
            if (ui.cardData == card) return ui;
        throw new AssertionException("Card presentation was not created for the hand instance.");
    }

    private void SetSelected(CardData card, bool selected) => GetCardUI(card).isSelected = selected;
}
