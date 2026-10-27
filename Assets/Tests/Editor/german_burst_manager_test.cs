using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Declared AI policy and real cleanup adapters; no ordinary AI driver skills or complete-race claim.</summary>
public class GermanBurstManagerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private MVPGameManager manager;
    private GameConfigSO config;
    private RaceSession race;
    private PlayerState player;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Owned burst manager adapter"); host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>(); manager.trackManager = host.AddComponent<TrackManager>();
        var nodes = new List<TrackNode>(); for (int i = 0; i < 60; i++) nodes.Add(new TrackNode(i, 99));
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);
        config = ScriptableObject.CreateInstance<GameConfigSO>(); config.enableTechTree = true; config.gearOneCooldown = 0;
        manager.config = config;
        race = new RaceSession(new SystemRandomSource(17)) { TeamVehicleBonusesEnabled = false };
        player = new PlayerState("AI burst", true, 0, 1) { teamId = TeamId.DE, techState = new TechTreeState(TeamId.DE) };
        player.techState.activeNodeIds.UnionWith(new[] { "de-l1-schwarzbier-fuel", "de-l3-grill-spezial" });
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 2) }, new HeatPool(10));
        player.deck.DrawToHand(4); race.Players.Add(player); race.BeginTurn(player, 1);
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, race);
        typeof(MVPGameManager).GetField("raceTurnNumber", Private).SetValue(manager, 1);
    }

    [TearDown]
    public void TearDown() { Object.DestroyImmediate(host); Object.DestroyImmediate(config); }
    private void Invoke(string name, params object[] args) => typeof(MVPGameManager).GetMethod(name, Private).Invoke(manager, args);

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void ActualAiDeclarationPrecedesGearPaymentAndOnlyDeclaredReceiptsReturn(int blocked)
    {
        player.deck.DrawHeatFromPool(1, HeatPaymentDestination.Hand);
        CardData old = player.deck.Hand[player.deck.Hand.Count - 1];
        int before = player.deck.heatPool.remaining;
        if (blocked == 1) config.enableTechTree = false;
        if (blocked == 2) typeof(MVPGameManager).GetField("tutorialScenario", Private).SetValue(manager, TutorialScenarioDefinition.CreateLeMansUk());
        if (blocked == 3) player.isAI = false;
        if (blocked == 4) player.techState.activeNodeIds.Remove("de-l3-grill-spezial");
        Invoke("PrepareAiGermanBurst", player);
        Assert.That(player.techState.germanBurst.Declared, Is.EqualTo(blocked == 0));
        Assert.That(player.techState.grillSpezialUsed, Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(before));
        if (blocked != 0) return;
        Invoke("ApplyGearShift", player, 4);
        Assert.That(player.techState.grillSpezialUsed, Is.True);
        Assert.That(player.techState.germanBurst.Locked, Is.True);
        Assert.That(player.heatPaidCardsThisTurn.Count, Is.GreaterThan(0));
        var speed = new List<CardData>(); foreach (var card in player.deck.Hand) if (card.IsSpeed) speed.Add(card);
        Assert.That(CardPlayRules.CommitSpeedCards(player, speed, 4, race, 60), Is.EqualTo(SpeedCardCommitResult.Success));
        Invoke("ComputeMovements", new List<PlayerState> { player }, new HashSet<PlayerState>());
        Assert.That(player.techState.germanBurst.FuelUsesThisTurn, Is.EqualTo(1), "Existing AI fuel choice remains one actual use");
        Invoke("ReactStep", player); Invoke("CleanupTurn", player);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(before));
        Assert.That(player.deck.ContainsInHand(old), Is.True);
        Assert.That(player.techState.germanBurst.Declared, Is.False);
    }

    [TestCase(false)] [TestCase(true)]
    public void UndeclaredPaymentNeverObtainsOldAutomaticRefund(bool ai)
    {
        player.isAI = ai;
        Assert.That(manager.TryPayHeat(player, 1, 0, "undeclared payment"), Is.True);
        var paid = player.heatPaidCardsThisTurn[0];
        Assert.That(race.GetGrillSpezialCooldown(player), Is.Zero);
        Invoke("CleanupTurn", player);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(9));
        Assert.That(player.deck.ContainsInHand(paid), Is.True);
        Assert.That(player.techState.grillSpezialUsed, Is.False);
    }

    [Test]
    public void ActualRefundReportsCapacityRetirementSeparatelyFromNetEngineRestoration()
    {
        player.deck.heatPool = new HeatPool(1); player.deck.SetRegionalCapacityBonus(1);
        Assert.That(race.TryDeclareGermanBurst(player, true, 1), Is.True);
        Assert.That(manager.TryPayHeat(player, 2, 0, "window payment"), Is.True);
        player.deck.SetRegionalCapacityBonus(0);
        Assert.That(player.deck.RegionalHeatPendingRetirement, Is.EqualTo(1));
        var logs = new List<string>(); manager.hudUI = host.AddComponent<HUDUI>(); manager.hudUI.SetLogSink(logs.Add);
        Invoke("CleanupTurn", player);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(1));
        Assert.That(player.deck.RegionalHeatPendingRetirement, Is.Zero);
        Assert.That(player.deck.CountPermanentHeatOutsideEngine(), Is.Zero);
        Assert.That(logs.Exists(x => x.Contains("净回引擎1") && x.Contains("容量退役1")), Is.True);
        Invoke("CleanupTurn", player); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(1));
    }
}
