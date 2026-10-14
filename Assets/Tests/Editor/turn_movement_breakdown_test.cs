using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class TurnMovementBreakdownTests
{
    [Test]
    public void InstantMovementSurvivesPlanningAndDoesNotLightUntraversedCards()
    {
        var ledger = new TurnMovementBreakdown();
        ledger.RecordInstant("阴阳茶", 1, TurnMovementKind.Technology);
        ledger.BeginBasePlan();
        ledger.AddPlan("速度牌", 4, TurnMovementKind.Card);
        ledger.AddPlan("技能", 2, TurnMovementKind.Driver);
        ledger.ReconcileBaseTotal(6);
        Assert.That(ledger.TotalCells, Is.EqualTo(7));
        Assert.That(ledger.ActualCells, Is.EqualTo(1));
        Assert.That(ledger.Entries[1].ActualCells, Is.Zero);
        ledger.Advance(5);
        Assert.That(ledger.Entries[1].ActualCells, Is.EqualTo(4));
        Assert.That(ledger.Entries[2].ActualCells, Is.EqualTo(1));
        Assert.That(ledger.ActualCells, Is.EqualTo(6));
    }

    [TestCase(0)]
    [TestCase(2)]
    [TestCase(9)]
    public void SignedCorrectionsAgreeWithAuthoritativeMovement(int total)
    {
        var ledger = new TurnMovementBreakdown();
        ledger.BeginBasePlan();
        ledger.AddPlan("卡牌", 4, TurnMovementKind.Card);
        ledger.AddPlan("技能", 2, TurnMovementKind.Driver);
        ledger.AddPlan("修正", -1, TurnMovementKind.Other);
        ledger.ReconcileBaseTotal(total);
        Assert.That(ledger.TotalCells, Is.EqualTo(total));
        Assert.That(ledger.Entries.Sum(e => e.Cells), Is.EqualTo(total));
        ledger.Advance(total);
        Assert.That(ledger.ActualCells, Is.EqualTo(total));
        Assert.That(ledger.Entries.All(e => e.ActualCells <= e.EffectiveCells), Is.True);
    }

    [Test]
    public void LateTailwindRemainsDimUntilItsOwnMovementAndRewindIsSeparate()
    {
        var ledger = new TurnMovementBreakdown();
        ledger.BeginBasePlan();
        ledger.AddPlan("速度牌", 3, TurnMovementKind.Card);
        ledger.Advance(3);
        ledger.AddPlan("尾流", 2, TurnMovementKind.Other);
        Assert.That(ledger.TotalCells, Is.EqualTo(5));
        Assert.That(ledger.ActualCells, Is.EqualTo(3));
        Assert.That(ledger.Entries.Last().ActualCells, Is.Zero);
        ledger.Advance(2);
        ledger.RecordRewind(4);
        Assert.That(ledger.ActualCells, Is.EqualTo(5));
        Assert.That(ledger.RewindCells, Is.EqualTo(4));
    }

    [Test]
    public void PlayerTurnResetClearsAllTelemetry()
    {
        var player = new PlayerState("driver", false, 7, 2);
        player.turnMovement.RecordInstant("科技", 2, TurnMovementKind.Technology);
        player.turnMovement.RecordRewind(1);
        new RaceSession().BeginTurn(player);
        Assert.That(player.turnMovement.TotalCells, Is.Zero);
        Assert.That(player.turnMovement.ActualCells, Is.Zero);
        Assert.That(player.turnMovement.RewindCells, Is.Zero);
        Assert.That(player.turnMovement.HasBasePlan, Is.False);
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void TeamCardContributionsMatchExistingRulesWithoutConsumingAgain(TeamId team)
    {
        var player = new PlayerState("driver", false, 0, 2) { teamId = team };
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 1));
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 3));
        player.hotpotAttackAppliedThisTurn = true;
        player.hotpotAttackCardValueThisTurn = 3;
        player.trickMoveBonusThisTurn = 2;
        player.italyCornerExitBoostReady = true;
        var session = new RaceSession();
        int expected = RaceMovementRules.ComputeCornerSpeed(player, null, false) +
            RaceMovementRules.ComputeCardAndDriverMovementBonus(player, false) +
            session.ComputeMovementBonus(player, false);
        player.turnMovement.BeginBasePlan();
        RaceMovementRules.RecordCardAndDriverMovementPlan(player, false);
        int bonus = session.ComputeMovementBonusWithBreakdown(player, false, player.turnMovement);
        Assert.That(bonus, Is.EqualTo(session.ComputeMovementBonus(player, false)));
        Assert.That(player.turnMovement.Entries.Sum(e => e.Cells), Is.EqualTo(expected));
        Assert.That(player.turnMovement.Entries.Count(e => e.Kind == TurnMovementKind.Card), Is.GreaterThanOrEqualTo(4));
        Assert.That(player.italyCornerExitBoostReady, Is.True);
        Assert.That(player.hotpotAttackAppliedThisTurn, Is.True);
        Assert.That(player.deck.HandCount, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RealMissingCarTraversalCountsWrapAndSkippedPresentation(bool skip)
    {
        const BindingFlags privateFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = new GameObject("Movement ledger traversal");
        host.SetActive(false);
        var manager = host.AddComponent<MVPGameManager>();
        var track = host.AddComponent<TrackManager>();
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        try
        {
            manager.config = config;
            manager.trackManager = track;
            config.totalLaps = 3;
            config.enableWeather = false;
            config.enablePitLane = false;
            var nodes = new List<TrackNode>();
            for (int i = 0; i < 8; i++) nodes.Add(new TrackNode(i, 0, isStartFinish: i == 0));
            typeof(TrackManager).GetField("nodes", privateFlags).SetValue(track, nodes);
            var player = new PlayerState("driver", false, 6, 2);
            var session = new RaceSession();
            session.Players.Add(player);
            typeof(MVPGameManager).GetField("session", privateFlags).SetValue(manager, session);
            player.turnMovement.BeginBasePlan();
            player.turnMovement.AddPlan("卡牌", 4, TurnMovementKind.Card);
            var state = (RacePresentationSkipState)typeof(MVPGameManager)
                .GetField("presentationSkipState", privateFlags).GetValue(manager);
            state.Begin();
            if (skip) manager.RequestPresentationSkip();
            IEnumerator movement = (IEnumerator)typeof(MVPGameManager)
                .GetMethod("AnimateMovementByAmount", privateFlags)
                .Invoke(manager, new object[] { player, -1, 4, false });
            Drain(movement);
            Assert.That(player.position, Is.EqualTo(2));
            Assert.That(player.lap, Is.EqualTo(1));
            Assert.That(player.turnMovement.ActualCells, Is.EqualTo(4));
            Assert.That(player.turnMovement.TotalCells, Is.EqualTo(4));
            Assert.That(player.turnMovement.Entries.Count, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(config); }
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void ActualManagerPlansAllContributionsOnceAndLeavesInstantStepSettled(TeamId team)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = new GameObject("Movement ledger planner");
        host.SetActive(false);
        var manager = host.AddComponent<MVPGameManager>();
        var track = host.AddComponent<TrackManager>();
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        try
        {
            manager.config = config;
            manager.trackManager = track;
            config.enableTechTree = false;
            var nodes = new List<TrackNode>();
            for (int i = 0; i < 20; i++) nodes.Add(new TrackNode(i, 0));
            typeof(TrackManager).GetField("nodes", flags).SetValue(track, nodes);
            var player = new PlayerState("driver", false, 0, 2) { teamId = team };
            player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 1));
            player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 2));
            player.turnMovement.RecordInstant("即时科技", 1, TurnMovementKind.Technology);
            player.italyCornerExitBoostReady = true;
            var session = new RaceSession();
            session.Players.Add(player);
            typeof(MVPGameManager).GetField("session", flags).SetValue(manager, session);
            int expected = 3 + session.ComputeMovementBonus(player, false) +
                (team == TeamId.IT ? TeamVehicleRules.GetCornerExitBonus(team) : 0);
            typeof(MVPGameManager).GetMethod("ComputeMovements", flags)
                .Invoke(manager, new object[] { session.Players, new HashSet<PlayerState>() });
            Assert.That(player.totalMovementThisTurn, Is.EqualTo(expected));
            Assert.That(player.turnMovement.TotalCells, Is.EqualTo(expected + 1));
            Assert.That(player.turnMovement.ActualCells, Is.EqualTo(1));
            Assert.That(player.turnMovement.Entries.Where(e => e.Kind == TurnMovementKind.Card)
                .All(e => e.ActualCells == 0), Is.True);
            Assert.That(player.italyCornerExitBoostReady, Is.EqualTo(team != TeamId.IT));
            Assert.That(player.gear, Is.EqualTo(2));
            Assert.That(player.position, Is.Zero);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(config); }
    }

    [Test]
    public void ActiveDriverContributionIsPurpleWithoutSpendingAnotherUse()
    {
        var player = new PlayerState("driver", false, 0, 2);
        Assert.That(DriverCatalog.TryGet("us_tony_stewart", out DriverProfile profile), Is.True);
        player.driverId = profile.Id;
        player.driverSkill.Initialize(profile, 5, true);
        Assert.That(player.driverSkill.TryActivate(profile,
            new DriverSkillActivationContext(true, 0, 3, 10, 10, 0), out _), Is.True);
        int uses = player.driverSkill.UsesRemaining;
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 2));
        player.turnMovement.BeginBasePlan();
        RaceMovementRules.RecordCardAndDriverMovementPlan(player, false);
        Assert.That(player.turnMovement.Entries.Where(e => e.Kind == TurnMovementKind.Driver).Sum(e => e.Cells),
            Is.EqualTo(DriverSkillRules.GetMovementBonus(player.driverSkill, false)).And.GreaterThan(0));
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
        Assert.That(player.driverSkill.ActiveTurnsRemaining, Is.EqualTo(3));
    }

    [Test]
    public void ReadingDetailsDefersNextTurnBeforeItsLedgerCanBeCleared()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = new GameObject("Movement details turn boundary");
        host.SetActive(false);
        var manager = host.AddComponent<MVPGameManager>();
        var hud = host.AddComponent<HUDUI>();
        var panel = new GameObject("Details reading");
        panel.transform.SetParent(host.transform, false);
        try
        {
            manager.hudUI = hud;
            typeof(HUDUI).GetField("telemetryDetailsPanel", flags).SetValue(hud, panel);
            var session = new RaceSession();
            var player = new PlayerState("driver", false, 4, 2);
            session.Players.Add(player);
            player.turnMovement.RecordInstant("科技", 3, TurnMovementKind.Technology);
            typeof(MVPGameManager).GetField("session", flags).SetValue(manager, session);
            ((RacePhaseState)typeof(MVPGameManager).GetField("phaseState", flags).GetValue(manager)).BeginAnimation();
            IEnumerator loop = (IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", flags).Invoke(manager, null);
            Assert.That(loop.MoveNext(), Is.True);
            Assert.That(loop.Current, Is.InstanceOf<WaitWhile>());
            var waiting = (WaitWhile)loop.Current;
            Assert.That(waiting.keepWaiting, Is.True);
            Assert.That(player.turnMovement.ActualCells, Is.EqualTo(3));
            Assert.That(typeof(MVPGameManager).GetField("raceTurnNumber", flags).GetValue(manager), Is.Zero);
            panel.SetActive(false);
            Assert.That(waiting.keepWaiting, Is.False);
            (loop as System.IDisposable)?.Dispose();
        }
        finally { Object.DestroyImmediate(host); }
    }

    private static void Drain(IEnumerator routine)
    {
        int guard = 0;
        while (routine.MoveNext())
        {
            Assert.That(++guard, Is.LessThan(1000));
            if (routine.Current is IEnumerator nested) Drain(nested);
        }
    }
}
