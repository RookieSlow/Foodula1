using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class HeatRecoverySourcesTests
{
    private static List<CardData> Zone(CardDeck deck, string name) =>
        (List<CardData>)typeof(CardDeck).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(deck);

    [TestCase(0, 0, 0, 0)]
    [TestCase(2, 0, 0, 1)]
    [TestCase(0, 1, 2, 0)]
    [TestCase(2, 1, 2, 1)]
    [TestCase(0, 0, 0, 2)]
    public void ActualRecoverySourcesPreserveNonHeatAndDoNotRefundTemporaryCards(int hand, int draw, int discard, int temporary)
    {
        var deck = new CardDeck();
        var speed = new CardData(CardType.Speed, 3);
        deck.InitializeExactOrder(new[] { speed }, new HeatPool(6));
        deck.DrawHeatFromPool(hand, HeatPaymentDestination.Hand);
        deck.DrawHeatFromPool(discard, HeatPaymentDestination.Discard);
        for (int i = 0; i < draw; i++)
        {
            Zone(deck, "drawPile").Add(new CardData(CardType.Heat, 0));
            deck.heatPool.remaining--;
        }
        for (int i = 0; i < temporary; i++)
        {
            deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
            Zone(deck, "drawPile").Add(CardData.CreateTempHeat());
            Zone(deck, "discardPile").Add(CardData.CreateTempHeat());
        }
        HeatCoolingResult result = deck.RecoverAllHeatWithSources();
        Assert.AreEqual(hand + temporary, result.FromHand);
        Assert.AreEqual(draw + temporary, result.FromDraw);
        Assert.AreEqual(discard + temporary, result.FromDiscard);
        Assert.AreEqual(hand + draw + discard + 3 * temporary, result.Total);
        Assert.AreEqual(6, deck.heatPool.remaining);
        Assert.AreEqual(0, deck.CountPermanentHeatOutsideEngine());
        Assert.AreEqual(0, deck.HandCount);
        Assert.AreEqual(0, deck.DiscardPile.Count);
        CollectionAssert.AreEqual(new[] { speed }, deck.DrawPile);
        Assert.AreEqual(0, deck.RecoverAllHeatWithSources().Total);
        Assert.AreEqual(6, deck.heatPool.remaining);
    }

    [Test]
    public void LegacyVoidApiStillFullyRecoversAndIsRepeatable()
    {
        var deck = new CardDeck();
        deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 1) }, new HeatPool(6));
        deck.DrawHeatFromPool(2, HeatPaymentDestination.Hand);
        deck.DrawHeatFromPool(2, HeatPaymentDestination.Discard);
        deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
        deck.RecoverAllHeatToPool();
        deck.RecoverAllHeatToPool();
        Assert.AreEqual(6, deck.heatPool.remaining);
        Assert.AreEqual(0, deck.CountHeatInHand());
        Assert.AreEqual(0, deck.CountHeatInDiscardPile());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NullPoolRetainsExistingPermanentVsTemporaryRecoveryContract(bool permanent)
    {
        var deck = new CardDeck();
        var heat = permanent ? new CardData(CardType.Heat, 0) : CardData.CreateTempHeat();
        Zone(deck, "hand").Add(heat); // Inject malformed legacy state; public API rejects permanent heat without payment.
        Assert.IsNull(deck.heatPool);
        if (permanent)
        {
            Assert.Throws<NullReferenceException>(() => deck.RecoverAllHeatWithSources());
            Assert.AreSame(heat, deck.Hand[0]);
        }
        else
        {
            Assert.AreEqual(1, deck.RecoverAllHeatWithSources().FromHand);
            Assert.AreEqual(0, deck.HandCount);
        }
    }
}

/// <summary>Actual full-recovery/pit adapters, without scene startup, FX or storage.</summary>
public class HeatRecoveryBoundaryIntegrationTests
{
    private GameObject host;
    private GameConfigSO config;
    private MVPGameManager manager;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Heat recovery integration");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.minGear = 2;
        config.pitExitMoveBonus = 1;
        manager.config = config;
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, new RaceSession(new SystemRandomSource(1)));
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(host);
        UnityEngine.Object.DestroyImmediate(config);
    }

    private PlayerState Player(bool ai, TeamId team)
    {
        var player = new PlayerState("driver", ai, 10, 4) { teamId = team, chinaConsecutiveGearCount = 3 };
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 2) }, new HeatPool(6));
        player.deck.DrawHeatFromPool(2, HeatPaymentDestination.Hand, player.heatPaidCardsThisTurn);
        player.deck.DrawHeatFromPool(2, HeatPaymentDestination.Discard, player.heatPaidCardsThisTurn);
        player.deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
        return player;
    }

    private void Invoke(string method, PlayerState player) =>
        typeof(MVPGameManager).GetMethod(method, Private).Invoke(manager, new object[] { player });

    [TestCase(false)]
    [TestCase(true)]
    public void RealRecoveryAdapterKeepsReceiptLifetimeAndDoesNotChangeDrivingState(bool ai)
    {
        var player = Player(ai, TeamId.US);
        Invoke("RecoverAllHeatWithPresentation", player);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountPermanentHeatOutsideEngine());
        Assert.AreEqual(0, player.deck.CountHeatInHand());
        Assert.AreEqual(4, player.heatPaidCardsThisTurn.Count);
        Assert.AreEqual(10, player.position);
        Assert.AreEqual(4, player.gear);
        Invoke("RecoverAllHeatWithPresentation", player);
        Invoke("RecoverAllHeatWithPresentation", null);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
    }

    [TestCase(false, TeamId.CN, true)]
    [TestCase(true, TeamId.US, true)]
    [TestCase(false, TeamId.US, false)]
    [TestCase(true, TeamId.CN, false)]
    public void RealScheduledPitRecoversOnlyAfterSuccessfulEntry(bool ai, TeamId team, bool hasPit)
    {
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 40; i++)
            nodes.Add(new TrackNode(i, 99, "node", isPitEntry: hasPit && i == 10, isPitExit: hasPit && i == 15));
        typeof(MVPGameManager).GetField("tutorialPitRuleNodes", Private).SetValue(manager, nodes);
        var player = Player(ai, team);
        player.pitStopScheduled = true;
        player.skipNextTurn = true;
        player.pitChoiceResolvedThisLap = true;
        Invoke("ExecuteScheduledPitStop", player);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.IsFalse(player.skipNextTurn);
        Assert.AreEqual(hasPit ? 6 : 2, player.deck.heatPool.remaining);
        Assert.AreEqual(hasPit ? 0 : 4, player.deck.CountPermanentHeatOutsideEngine());
        Assert.AreEqual(hasPit ? 0 : 3, player.deck.CountHeatInHand());
        Assert.AreEqual(hasPit ? 16 : 10, player.position);
        Assert.AreEqual(hasPit ? (team == TeamId.CN ? ChinaGearShiftRules.RecoverGear : 2) : 4, player.gear);
        Assert.AreEqual(hasPit ? 0 : 3, player.chinaConsecutiveGearCount);
        Assert.AreEqual(!hasPit, player.pitChoiceResolvedThisLap);
        Assert.AreEqual(4, player.heatPaidCardsThisTurn.Count);
    }
}
