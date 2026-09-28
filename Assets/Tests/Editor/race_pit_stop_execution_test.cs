using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RacePitStopExecutionTests
{
    public static List<TrackNode> Track(bool hasPit = true)
    {
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 20; i++)
            nodes.Add(new TrackNode(i, 99, "node", isPitEntry: hasPit && i == 10, isPitExit: hasPit && i == 19));
        return nodes;
    }

    public static PlayerState Player(TeamId team = TeamId.US)
    {
        var player = new PlayerState("pit driver", false, 10, 4)
        {
            teamId = team, pitStopScheduled = true, skipNextTurn = true,
            pitChoiceResolvedThisLap = true, pitStopRequested = true,
            chinaConsecutiveGearCount = 3, lap = 2, spinCounter = 1,
            totalMovementThisTurn = 8
        };
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 2) }, new HeatPool(6));
        player.deck.DrawHeatFromPool(2, HeatPaymentDestination.Hand, player.heatPaidCardsThisTurn);
        player.deck.DrawHeatFromPool(2, HeatPaymentDestination.Discard, player.heatPaidCardsThisTurn);
        return player;
    }

    [TestCase(TeamId.CN)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.JP)]
    public void SuccessfulStopKeepsOrderedLiveStateAndOnlyResetsExitFields(TeamId team)
    {
        var player = Player(team);
        var calls = new List<string>();
        PitStopResult result = RacePitStopExecution.Execute(player, p =>
        {
            Assert.IsFalse(p.pitStopScheduled);
            Assert.IsFalse(p.skipNextTurn);
            calls.Add("exit");
            return PitLaneRules.EnterPit(p, Track(), 2);
        }, p =>
        {
            Assert.AreEqual(1, p.position);
            Assert.AreEqual(4, p.gear);
            Assert.IsTrue(p.pitChoiceResolvedThisLap);
            calls.Add("heat");
            p.deck.RecoverAllHeatToPool();
        }, p =>
        {
            Assert.AreEqual(6, p.deck.heatPool.remaining);
            Assert.AreEqual(3, p.chinaConsecutiveGearCount);
            calls.Add("gear");
            return TeamGearRules.IsChina(p.teamId) ? ChinaGearShiftRules.RecoverGear : 2;
        });
        Assert.IsTrue(result.success);
        Assert.AreEqual(19, result.pitExitPosition);
        Assert.AreEqual(1, result.exitPosition);
        CollectionAssert.AreEqual(new[] { "exit", "heat", "gear" }, calls);
        Assert.AreEqual(team == TeamId.CN ? 1 : 2, player.gear);
        Assert.AreEqual(0, player.chinaConsecutiveGearCount);
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
        Assert.IsTrue(player.pitStopRequested); // Do not broaden cleanup beyond the old adapter.
        Assert.AreEqual(2, player.lap);
        Assert.AreEqual(1, player.spinCounter);
        Assert.AreEqual(8, player.totalMovementThisTurn);
        Assert.AreEqual(4, player.heatPaidCardsThisTurn.Count);
    }

    [TestCase("missing-pit")]
    [TestCase("finished")]
    [TestCase("blown")]
    public void RejectedStopConsumesSchedulingButKeepsAllExitState(string reason)
    {
        var player = Player();
        player.hasFinished = reason == "finished";
        player.isBlown = reason == "blown";
        PitStopResult result = RacePitStopExecution.Execute(player,
            p => PitLaneRules.EnterPit(p, Track(reason != "missing-pit")),
            p => Assert.Fail("Rejected stop must not recover heat"),
            p => { Assert.Fail("Rejected stop must not restore gear"); return 1; });
        Assert.IsFalse(result.success);
        Assert.AreEqual("Cannot enter pit", result.message);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.IsFalse(player.skipNextTurn);
        Assert.AreEqual(10, player.position);
        Assert.AreEqual(4, player.gear);
        Assert.AreEqual(2, player.deck.heatPool.remaining);
        Assert.AreEqual(4, player.deck.CountPermanentHeatOutsideEngine());
        Assert.AreEqual(3, player.chinaConsecutiveGearCount);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void AdapterExceptionPropagatesAtTheSameMutationBoundary(int stage)
    {
        var player = Player();
        var failure = new InvalidOperationException("adapter failure");
        int callbacks = 0;
        var caught = Assert.Throws<InvalidOperationException>(() => RacePitStopExecution.Execute(player, p =>
        {
            callbacks++;
            if (stage == 0) throw failure;
            return PitLaneRules.EnterPit(p, Track());
        }, p =>
        {
            callbacks++;
            if (stage == 1) throw failure;
            p.deck.RecoverAllHeatToPool();
        }, p =>
        {
            callbacks++;
            throw failure;
        }));
        Assert.AreSame(failure, caught);
        Assert.AreEqual(stage + 1, callbacks);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.IsFalse(player.skipNextTurn);
        Assert.AreEqual(stage == 0 ? 10 : 0, player.position);
        Assert.AreEqual(stage == 2 ? 6 : 2, player.deck.heatPool.remaining);
        Assert.AreEqual(4, player.gear);
        Assert.AreEqual(3, player.chinaConsecutiveGearCount);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
    }

    [Test]
    public void NullParticipantDoesNotCallAdapters()
    {
        Assert.IsFalse(RacePitStopExecution.Execute(null, null, null, null).success);
    }
}

/// <summary>Real manager adapters on inactive objects; no startup, scene changes or persistence.</summary>
public class RacePitStopBoundaryIntegrationTests
{
    private GameObject host;
    private GameConfigSO config;
    private MVPGameManager manager;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Scheduled pit integration");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.minGear = 2;
        config.pitExitMoveBonus = 1;
        manager.config = config;
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, new RaceSession(new SystemRandomSource(1)));
        typeof(MVPGameManager).GetField("tutorialPitRuleNodes", Private).SetValue(manager, RacePitStopExecutionTests.Track());
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(host);
        UnityEngine.Object.DestroyImmediate(config);
    }

    private void Execute(PlayerState player) =>
        typeof(MVPGameManager).GetMethod("ExecuteScheduledPitStop", Private).Invoke(manager, new object[] { player });

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void ActualFastChargeAppliesOnlyWithTechnologyEnabled(bool ai, bool technology)
    {
        var player = RacePitStopExecutionTests.Player(TeamId.CN);
        player.isAI = ai;
        config.enableTechTree = technology;
        player.techState = new TechTreeState(TeamId.CN, 0);
        player.techState.activeNodeIds.Add("cn-l1-fast-charge");
        Execute(player);
        Assert.AreEqual(technology ? 1 : 0, player.position);
        Assert.AreEqual(ChinaGearShiftRules.RecoverGear, player.gear);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.IsFalse(player.skipNextTurn);
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(0, player.chinaConsecutiveGearCount);
        Assert.AreEqual(2, player.lap); // Existing teleport does not award a lap at wrapped exit.
        Assert.AreEqual(4, player.heatPaidCardsThisTurn.Count);
    }

    [TestCase(TeamId.CN, true)]
    [TestCase(TeamId.US, true)]
    [TestCase(TeamId.US, false)]
    public void MissingConfigPreservesExistingFallbackAndExceptionBoundary(TeamId team, bool hasPit)
    {
        manager.config = null;
        typeof(MVPGameManager).GetField("tutorialPitRuleNodes", Private).SetValue(manager, RacePitStopExecutionTests.Track(hasPit));
        var player = RacePitStopExecutionTests.Player(team);
        if (hasPit && team != TeamId.CN)
        {
            var error = Assert.Throws<TargetInvocationException>(() => Execute(player));
            Assert.IsInstanceOf<NullReferenceException>(error.InnerException);
        }
        else Execute(player);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.IsFalse(player.skipNextTurn);
        Assert.AreEqual(hasPit ? 0 : 10, player.position);
        Assert.AreEqual(hasPit ? 6 : 2, player.deck.heatPool.remaining);
        bool completed = hasPit && team == TeamId.CN;
        Assert.AreEqual(completed ? 1 : 4, player.gear);
        Assert.AreEqual(completed ? 0 : 3, player.chinaConsecutiveGearCount);
        Assert.AreEqual(!completed, player.pitChoiceResolvedThisLap);
        Execute(null);
    }
}
