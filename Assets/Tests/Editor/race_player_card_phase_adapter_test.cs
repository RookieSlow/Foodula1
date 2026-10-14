using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Exercises the real human card-phase coordinator without starting a scene or race coroutine.</summary>
public class RacePlayerCardPhaseAdapterTests
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private GameObject host;
    private MVPGameManager manager;
    private GameConfigSO config;
    private RaceInputState input;
    private PlayerState player;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Card phase adapter test");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        manager.cardHandUI = host.AddComponent<CardHandUI>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        manager.config = config;

        var session = new RaceSession(new SystemRandomSource(1));
        player = new PlayerState("driver", false, 8, 1) { teamId = TeamId.UK };
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 3) }, new HeatPool(6));
        session.Players.Add(player);
        typeof(MVPGameManager).GetField("session", PrivateInstance).SetValue(manager, session);

        input = (RaceInputState)typeof(MVPGameManager).GetField("inputState", PrivateInstance).GetValue(manager);
        input.BeginCardSelection();
    }

    [TearDown]
    public void TearDown()
    {
        if (host != null) Object.DestroyImmediate(host);
        if (config != null) Object.DestroyImmediate(config);
    }

    private void Finish()
    {
        MethodInfo method = typeof(MVPGameManager).GetMethod("FinishPlayerCardPhase", PrivateInstance);
        Assert.IsNotNull(method, "Card-phase coordinator changed; update this adapter fixture.");
        method.Invoke(manager, new object[] { player });
    }

    private RacePhaseState Phase => (RacePhaseState)typeof(MVPGameManager)
        .GetField("phaseState", PrivateInstance).GetValue(manager);

    private List<CardUI> DisplayedCards => (List<CardUI>)typeof(CardHandUI)
        .GetField("cardUIs", PrivateInstance).GetValue(manager.cardHandUI);

    private CardData[] ShowExactHand(int count)
    {
        var cards = new CardData[count];
        for (int i = 0; i < count; i++) cards[i] = new CardData(CardType.Speed, 3);
        player.deck.InitializeExactOrder(cards, new HeatPool(6));
        player.deck.DrawToHand(count);
        var container = new GameObject("Card phase hand", typeof(RectTransform));
        container.transform.SetParent(host.transform, false);
        var template = new GameObject("Card phase template", typeof(RectTransform));
        template.SetActive(false);
        template.transform.SetParent(host.transform, false);
        template.AddComponent<CardUI>();
        manager.cardHandUI.handContainer = container.transform;
        manager.cardHandUI.cardPrefab = template;
        Phase.BeginCardSelection();
        manager.cardHandUI.ShowHand(manager, player);
        Assert.AreEqual(count, DisplayedCards.Count);
        return cards;
    }

    // Selection is assigned without pointer/audio effects. The real UI query,
    // atomic commit, hand rebuild and public end callback are exercised below.
    [TestCase(TeamId.UK, 1)]
    [TestCase(TeamId.UK, 2)]
    [TestCase(TeamId.DE, 1)]
    [TestCase(TeamId.DE, 2)]
    [TestCase(TeamId.IT, 1)]
    [TestCase(TeamId.IT, 2)]
    [TestCase(TeamId.US, 1)]
    [TestCase(TeamId.US, 2)]
    [TestCase(TeamId.CN, 1)]
    [TestCase(TeamId.CN, 2)]
    [TestCase(TeamId.JP, 1)]
    [TestCase(TeamId.JP, 2)]
    public void PublicSelectedGroupCommitsExactEqualValueInstancesBeforeExplicitEnd(
        TeamId team, int count)
    {
        player.teamId = team;
        player.extraCardSlotsThisTurn = 1;
        var cards = ShowExactHand(3);
        // Reverse selection order deliberately; commitment follows displayed order.
        for (int i = count - 1; i >= 0; i--) DisplayedCards[i].SetSelectedWithoutNotify(true);

        manager.OnPlayCardsButtonClicked();

        Assert.IsTrue(input.WaitingForCards, "Confirming cards must not implicitly end the phase.");
        Assert.AreEqual(count, player.playedSpeedCardsThisTurn.Count);
        for (int i = 0; i < count; i++) Assert.AreSame(cards[i], player.playedSpeedCardsThisTurn[i]);
        Assert.AreEqual(3 - count, player.deck.Hand.Count);
        for (int i = count; i < cards.Length; i++)
        {
            Assert.AreSame(cards[i], player.deck.Hand[i - count]);
            Assert.AreSame(cards[i], DisplayedCards[i - count].cardData);
        }
        Assert.IsEmpty(manager.cardHandUI.GetSelectedPlayCards(), "Refresh clears stale selection.");
        Assert.AreEqual(6, player.deck.heatPool.remaining);

        manager.OnPlayCardsButtonClicked();
        Assert.IsFalse(input.WaitingForCards);
        manager.OnPlayCardsButtonClicked();
        Assert.AreEqual(count, player.playedSpeedCardsThisTurn.Count);
        Assert.AreEqual(3 - count, player.deck.Hand.Count);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.IsEmpty(player.heatPaidCardsThisTurn);
        Assert.AreEqual(0, player.spinCounter);
    }

    [TestCase(2)]
    [TestCase(3)]
    public void PublicOversizedSelectedGroupIsRejectedAtomically(int count)
    {
        var cards = ShowExactHand(count); // Gear one, no optional slots.
        foreach (CardUI ui in DisplayedCards) ui.SetSelectedWithoutNotify(true);

        manager.OnPlayCardsButtonClicked();
        manager.OnPlayCardsButtonClicked();

        Assert.IsTrue(input.WaitingForCards);
        CollectionAssert.AreEqual(cards, player.deck.Hand);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.AreEqual(count, manager.cardHandUI.GetSelectedPlayCards().Count);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.IsEmpty(player.heatPaidCardsThisTurn);
    }

    [TestCase(0)]
    [TestCase(1)]
    public void PublicStaleEqualValueSelectionCannotPartiallyCommitValidCard(int staleIndex)
    {
        player.extraCardSlotsThisTurn = 1;
        var cards = ShowExactHand(2);
        DisplayedCards[staleIndex].cardData = new CardData(CardType.Speed, 3);
        foreach (CardUI ui in DisplayedCards) ui.SetSelectedWithoutNotify(true);

        manager.OnPlayCardsButtonClicked();

        Assert.IsTrue(input.WaitingForCards);
        CollectionAssert.AreEqual(cards, player.deck.Hand);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.IsEmpty(manager.cardHandUI.GetSelectedPlayCards());
        for (int i = 0; i < cards.Length; i++) Assert.AreSame(cards[i], DisplayedCards[i].cardData);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.IsEmpty(player.heatPaidCardsThisTurn);
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void PublicFinishKeepsOptionalSlotsOptionalAndIgnoresRepeatedClicks(TeamId team)
    {
        player.teamId = team;
        player.extraCardSlotsThisTurn = 2;
        var played = new CardData(CardType.Speed, 3);
        player.playedSpeedCardsThisTurn.Add(played);
        Phase.BeginCardSelection();

        manager.OnPlayCardsButtonClicked();
        Assert.IsFalse(input.WaitingForCards);
        manager.OnPlayCardsButtonClicked();
        manager.OnPlayCardsButtonClicked();

        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.IsEmpty(player.heatPaidCardsThisTurn);
        Assert.AreEqual(0, player.deck.CountHeatInHand());
        Assert.AreEqual(0, player.spinCounter);
        CollectionAssert.AreEqual(new[] { played }, player.playedSpeedCardsThisTurn);
        Assert.AreEqual(GamePhase.WaitingForCards, Phase.Current,
            "The callback closes input; the coroutine owns the next phase.");
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void PublicEmptyFinishPaysOneMissingCardOnlyOnce(TeamId team)
    {
        player.teamId = team;
        Phase.BeginCardSelection();

        manager.OnPlayCardsButtonClicked();
        Assert.IsFalse(input.WaitingForCards);
        Assert.AreEqual(1, player.heatPaidCardsThisTurn.Count);
        var paid = player.heatPaidCardsThisTurn[0];
        CollectionAssert.Contains(player.deck.Hand, paid);
        manager.OnPlayCardsButtonClicked();

        Assert.AreEqual(5, player.deck.heatPool.remaining);
        Assert.AreEqual(1, player.deck.CountHeatInHand());
        CollectionAssert.AreEqual(new[] { paid }, player.heatPaidCardsThisTurn);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.AreEqual(0, player.spinCounter);
    }

    [TestCase(0)]
    [TestCase(1)]
    public void PublicInsufficientEngineRecoversPartialPaymentAndDoesNotSpinTwice(int available)
    {
        player.gear = 3;
        player.deck.DrawHeatFromPool(6 - available);
        var played = new CardData(CardType.Speed, 3);
        player.playedSpeedCardsThisTurn.Add(played);
        player.playedHeatCardsThisTurn.Add(CardData.CreateTempHeat());
        Phase.BeginCardSelection();

        manager.OnPlayCardsButtonClicked();
        Assert.IsFalse(input.WaitingForCards);
        manager.OnPlayCardsButtonClicked();

        Assert.AreEqual(1, player.spinCounter);
        Assert.IsTrue(player.skipNextTurn);
        Assert.AreEqual(1, player.gear);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountHeatInHand());
        Assert.IsEmpty(player.playedHeatCardsThisTurn);
        CollectionAssert.AreEqual(new[] { played }, player.playedSpeedCardsThisTurn);
    }

    [TestCase(GamePhase.WaitingForGear)]
    [TestCase(GamePhase.Animating)]
    [TestCase(GamePhase.GameOver)]
    public void PublicFinishRejectsCallbacksOutsideCardPhase(GamePhase phase)
    {
        if (phase == GamePhase.WaitingForGear) Phase.BeginGearSelection();
        else if (phase == GamePhase.Animating) Phase.BeginAnimation();
        else Phase.CompleteGame();

        manager.OnPlayCardsButtonClicked();

        Assert.IsTrue(input.WaitingForCards);
        Assert.AreEqual(phase, Phase.Current);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountHeatInHand());
        Assert.IsEmpty(player.heatPaidCardsThisTurn);
        Assert.AreEqual(0, player.spinCounter);
    }

    [Test]
    public void RequiredCardPlayedClosesInputWithoutChargingHeatAndKeepsPlayedOwnership()
    {
        var played = new CardData(CardType.Speed, 3);
        player.playedSpeedCardsThisTurn.Add(played);
        player.extraCardSlotsThisTurn = 1; // Optional slots do not raise the minimum.

        Finish();

        Assert.IsFalse(input.WaitingForCards);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.IsEmpty(player.heatPaidCardsThisTurn);
        CollectionAssert.AreEqual(new[] { played }, player.playedSpeedCardsThisTurn);
        Assert.AreEqual(0, player.spinCounter);
    }

    [TestCase(2, 1, 5)]
    [TestCase(3, 2, 4)]
    public void MissingRequiredCardsChargeExactHeatAndCloseInput(int gear, int missing, int remaining)
    {
        player.gear = gear;
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 3));

        Finish();

        Assert.IsFalse(input.WaitingForCards);
        Assert.AreEqual(remaining, player.deck.heatPool.remaining);
        Assert.AreEqual(missing, player.deck.CountHeatInHand());
        Assert.AreEqual(missing, player.heatPaidCardsThisTurn.Count);
        Assert.AreEqual(1, player.playedSpeedCardsThisTurn.Count);
        Assert.AreEqual(0, player.spinCounter);
    }

    [Test]
    public void EmptyEngineSpinsButLeavesCommittedSpeedForTurnCleanup()
    {
        player.gear = 2;
        player.deck.DrawHeatFromPool(6);
        var played = new CardData(CardType.Speed, 3);
        player.playedSpeedCardsThisTurn.Add(played);
        player.playedHeatCardsThisTurn.Add(CardData.CreateTempHeat());

        Finish();

        Assert.IsFalse(input.WaitingForCards);
        Assert.AreEqual(1, player.spinCounter);
        Assert.IsTrue(player.skipNextTurn);
        Assert.AreEqual(1, player.gear);
        Assert.IsEmpty(player.playedHeatCardsThisTurn);
        CollectionAssert.AreEqual(new[] { played }, player.playedSpeedCardsThisTurn);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
    }
}
