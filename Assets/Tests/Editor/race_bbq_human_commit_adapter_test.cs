using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Protects the human BBQ commit adapter, without starting a race scene.</summary>
public class RaceBBQHumanCommitAdapterTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private MVPGameManager manager;
    private GameConfigSO config;
    private RaceSession session;
    private PlayerState player;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("BBQ human commit regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        manager.trackManager = host.AddComponent<TrackManager>();
        manager.cardHandUI = host.AddComponent<CardHandUI>();
        manager.cardHandUI.handContainer = new GameObject("Hand", typeof(RectTransform)).transform;
        manager.cardHandUI.handContainer.SetParent(host.transform, false);
        manager.cardHandUI.cardPrefab = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(CardUI));
        manager.cardHandUI.cardPrefab.transform.SetParent(host.transform, false);
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.enableTechTree = true;
        manager.config = config;
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 60; i++) nodes.Add(new TrackNode(i, 99));
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);

        session = new RaceSession(new SystemRandomSource(1));
        player = new PlayerState("BBQ human", false, 5, 2)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US)
        };
        player.techState.activeNodeIds.Add("us-l2-smoked-bbq");
        player.deck.heatPool = new HeatPool(6);
        session.Players.Add(player);
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, session);
    }

    [TearDown]
    public void TearDown()
    {
        if (host != null) Object.DestroyImmediate(host);
        if (config != null) Object.DestroyImmediate(config);
    }

    [TestCase(TeamId.US, false)]
    [TestCase(TeamId.US, true)]
    [TestCase(TeamId.UK, false)]
    [TestCase(TeamId.UK, true)]
    public void EligibleHumanCommitKeepsExactHeatInstanceInPlayedArea(TeamId team, bool temporary)
    {
        ConfigureTeam(team);
        var speed = new CardData(CardType.Speed, 3);
        player.deck.AddCardsToHand(new[] { speed });
        Assert.That(player.deck.DrawHeatFromPoolToHand(1), Is.EqualTo(1));
        CardData permanent = player.deck.Hand[player.deck.Hand.Count - 1];
        CardData heat = temporary ? CardData.CreateTempHeat() : permanent;
        if (temporary) player.deck.AddCardsToHand(new[] { heat });

        Assert.That(manager.CanUseBBQHeatCards(player), Is.True);
        Assert.That(Commit(heat, speed), Is.True);

        CollectionAssert.AreEqual(new[] { heat, speed }, player.playedSpeedCardsThisTurn);
        Assert.That(player.deck.ContainsInHand(heat), Is.False);
        Assert.That(player.deck.ContainsInHand(speed), Is.False);
        Assert.That(player.deck.ContainsInHand(permanent), Is.EqualTo(temporary));
        Assert.That(player.deck.DiscardPileCount, Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(5));
        Assert.That(CardPlayRules.SumCommittedSpeedCardValues(player.playedSpeedCardsThisTurn), Is.EqualTo(5));
        Assert.That(heat.IsHeat, Is.True);
        Assert.That(heat.value, Is.Zero);
    }

    [TestCase(6, true, TeamId.US)]
    [TestCase(0, false, TeamId.US)]
    [TestCase(0, true, TeamId.DE)]
    [TestCase(0, true, TeamId.UK)]
    public void IneligibleHumanCommitLeavesHandAndPlayedAreaUntouched(
        int position, bool technologyEnabled, TeamId team)
    {
        ConfigureTeam(team);
        if (team == TeamId.UK) player.techState.sunNeverSetsTarget = TeamId.DE;
        config.enableTechTree = technologyEnabled;
        player.position = position;
        var speed = new CardData(CardType.Speed, 3);
        player.deck.AddCardsToHand(new[] { speed });
        Assert.That(player.deck.DrawHeatFromPoolToHand(1), Is.EqualTo(1));
        CardData heat = player.deck.Hand[player.deck.Hand.Count - 1];

        Assert.That(manager.CanUseBBQHeatCards(player), Is.False);
        Assert.That(Commit(heat, speed), Is.False);

        Assert.That(player.deck.ContainsInHand(heat), Is.True);
        Assert.That(player.deck.ContainsInHand(speed), Is.True);
        Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
        Assert.That(player.deck.DiscardPileCount, Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(5));
    }

    [Test]
    public void OverLimitMixedSelectionDoesNotCommitEitherCard()
    {
        var speed = new CardData(CardType.Speed, 3);
        player.deck.AddCardsToHand(new[] { speed });
        player.deck.DrawHeatFromPoolToHand(1);
        CardData heat = player.deck.Hand[player.deck.Hand.Count - 1];
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 2));

        Assert.That(Commit(heat, speed), Is.False);
        Assert.That(player.deck.ContainsInHand(heat), Is.True);
        Assert.That(player.deck.ContainsInHand(speed), Is.True);
        Assert.That(player.playedSpeedCardsThisTurn.Count, Is.EqualTo(1));
        Assert.That(player.deck.DiscardPileCount, Is.Zero);
    }

    [TestCase(TeamId.US, false)]
    [TestCase(TeamId.US, true)]
    [TestCase(TeamId.UK, false)]
    [TestCase(TeamId.UK, true)]
    public void HumanClickAndConfirmCommitsOnlySelectedHeat(TeamId team, bool temporary)
    {
        ConfigureTeam(team);
        var speed = new CardData(CardType.Speed, 3);
        player.deck.AddCardsToHand(new[] { speed });
        player.deck.DrawHeatFromPoolToHand(1);
        CardData permanent = player.deck.Hand[player.deck.Hand.Count - 1];
        CardData heat = temporary ? CardData.CreateTempHeat() : permanent;
        if (temporary) player.deck.AddCardsToHand(new[] { heat });
        BeginHumanSelection();

        Click(heat);
        CollectionAssert.AreEqual(new[] { heat }, manager.cardHandUI.GetSelectedPlayCards());
        manager.OnPlayCardsButtonClicked();

        CollectionAssert.AreEqual(new[] { heat }, player.playedSpeedCardsThisTurn);
        Assert.That(player.deck.ContainsInHand(heat), Is.False);
        Assert.That(player.deck.ContainsInHand(speed), Is.True);
        Assert.That(player.deck.ContainsInHand(permanent), Is.EqualTo(temporary));
        Assert.That(player.deck.DiscardPileCount, Is.Zero);
        Assert.That(CardPlayRules.SumCommittedSpeedCardValues(player.playedSpeedCardsThisTurn), Is.EqualTo(2));
        Assert.That(GetCardUI(speed), Is.Not.Null);
    }

    [TestCase(6, true, TeamId.US)]
    [TestCase(0, false, TeamId.US)]
    [TestCase(0, true, TeamId.DE)]
    [TestCase(0, true, TeamId.UK)]
    public void HumanClickCannotSelectHeatWithoutCurrentBBQEligibility(
        int position, bool technologyEnabled, TeamId team)
    {
        ConfigureTeam(team);
        if (team == TeamId.UK) player.techState.sunNeverSetsTarget = TeamId.DE;
        config.enableTechTree = technologyEnabled;
        player.position = position;
        player.deck.DrawHeatFromPoolToHand(1);
        CardData heat = player.deck.Hand[0];
        BeginHumanSelection();

        Click(heat);

        Assert.That(GetCardUI(heat).isSelected, Is.False);
        Assert.That(manager.cardHandUI.GetSelectedPlayCards(), Is.Empty);
        Assert.That(player.deck.ContainsInHand(heat), Is.True);
        Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
    }

    [Test]
    public void DiscardModeCannotSelectEligibleBBQHeatByClick()
    {
        player.deck.DrawHeatFromPoolToHand(1);
        CardData heat = player.deck.Hand[0];
        BeginHumanSelection();
        manager.cardHandUI.SetDiscardMode(true);

        Click(heat);

        Assert.That(GetCardUI(heat).isSelected, Is.False);
        Assert.That(manager.cardHandUI.GetSelectedCards(), Is.Empty);
        Assert.That(player.deck.ContainsInHand(heat), Is.True);
    }

    private void BeginHumanSelection()
    {
        ((RacePhaseState)typeof(MVPGameManager).GetField("phaseState", Private)
            .GetValue(manager)).BeginCardSelection();
        ((RaceInputState)typeof(MVPGameManager).GetField("inputState", Private)
            .GetValue(manager)).BeginCardSelection();
        manager.cardHandUI.ShowHand(manager, player);
    }

    private CardUI GetCardUI(CardData card)
    {
        foreach (CardUI ui in manager.cardHandUI.handContainer.GetComponentsInChildren<CardUI>(true))
            if (ui.cardData == card) return ui;
        throw new AssertionException("Expected hand presentation for the exact card instance.");
    }

    private void Click(CardData card) => typeof(CardHandUI).GetMethod("OnCardClicked", Private)
        .Invoke(manager.cardHandUI, new object[] { GetCardUI(card) });

    private void ConfigureTeam(TeamId team)
    {
        player.teamId = team;
        player.techState = new TechTreeState(team);
        if (team == TeamId.UK)
            player.techState.activeNodeIds.Add("uk-l3-sun-never-sets");
        else if (team == TeamId.US)
            player.techState.activeNodeIds.Add("us-l2-smoked-bbq");
        if (team == TeamId.UK) player.techState.sunNeverSetsTarget = TeamId.US;
    }

    private bool Commit(params CardData[] cards)
    {
        MethodInfo method = typeof(MVPGameManager).GetMethod("ConfirmPlayerSpeedCards", Private);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(manager, new object[] { player, cards });
    }
}
