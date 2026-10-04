using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Shares the inactive manager, input, track and recording animator fixture.
// Instant technology currently settles laps without offering a lane choice.
// These tests guard its handoff to traversal, not that unresolved player-facing
// choice, Unity frame scheduling, rendered clicks or a complete race.
public partial class LaneChoiceBoundaryIntegrationTests
{
    [TestCase(TeamId.UK, 1, 0)]
    [TestCase(TeamId.DE, 0, 1)]
    [TestCase(TeamId.IT, -1, 2)]
    [TestCase(TeamId.US, 1, 0)]
    [TestCase(TeamId.CN, -1, 2)]
    [TestCase(TeamId.JP, 0, 1)]
    public void InstantCrossingHandsNextTraversalToOneLaneWait(
        TeamId team, int direction, int selectedLane)
    {
        using (new InstantLaneAudioSilencer())
        {
            TargetRecorder animator = ConfigureAuthoredInstantTrack(out GameObject car);
            GameObject panel = CreateTraversalPanel();
            List<string> logs = CaptureInstantLaneLogs();
            manager.Player.teamId = team;
            AdvanceInstantLane(manager.Player, 1);
            AssertInstantLaneCrossing(manager.Player, panel, 1, false);
            Assert.That(animator.Targets, Is.Empty, "Instant movement does not invoke the node animator.");
            Assert.That(car.transform.position, Is.EqualTo(track.GetNodePosition(0, 1)));

            IEnumerator traversal = Traverse(manager.Player, car, track.TotalNodes + 2);
            IEnumerator choice = SeekInstantLaneWait(traversal, out WaitWhile waiting);

            Assert.That(manager.Player.lap, Is.EqualTo(2));
            Assert.That(manager.Player.hasFinished, Is.False);
            Assert.That(animator.Targets.Count, Is.EqualTo(track.TotalNodes));
            Assert.That(animator.Targets.Last(), Is.EqualTo(track.GetNodePosition(0, 1)));
            Assert.That(waiting.keepWaiting, Is.True);
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(logs.Count(message => message.Contains("完成第")), Is.EqualTo(2));
            Assert.That(logs.Any(message => message.Contains("印地起点换道")), Is.False);

            manager.ChooseIndianapolisLaneChange(direction);
            Assert.That(waiting.keepWaiting, Is.False);
            Assert.That(choice.MoveNext(), Is.False);
            Drain(traversal);
            manager.ChooseIndianapolisLaneChange(-direction); // Late input is inert.

            Assert.That(animator.Targets.Skip(track.TotalNodes), Is.EqualTo(new[]
                { track.GetNodePosition(1, selectedLane), track.GetNodePosition(2, selectedLane) }));
            Assert.That(lanes, Is.EqualTo(new[] { selectedLane, 3 }));
            Assert.That(manager.Player.position, Is.Zero, "Only the outer movement adapter commits final position.");
            Assert.That(manager.Player.lap, Is.EqualTo(2));
            Assert.That(manager.Player.finishOrder, Is.Zero);
            Assert.That(input.WaitingForLaneChange, Is.False);
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(logs.Count(message => message.Contains("印地起点换道")), Is.EqualTo(1));
            Assert.That(logs.Last(), Is.EqualTo(LaneChoicePresentationRules.GetResolvedLog(1, selectedLane)));
        }
    }

    [TestCase(TeamId.CN, 3, false)]
    [TestCase(TeamId.CN, 1, true)]
    [TestCase(TeamId.US, 3, false)]
    [TestCase(TeamId.US, 1, true)]
    public void ActualTechnologyCrossingPreservesLaneAndCurrentCardGate(
        TeamId team, int requiredLaps, bool finished)
    {
        using (new InstantLaneAudioSilencer())
        {
            ConfigureAuthoredInstantTrack(out GameObject car);
            crossingConfig.totalLaps = requiredLaps;
            GameObject panel = CreateTraversalPanel();
            List<string> logs = CaptureInstantLaneLogs();
            PlayerState player = manager.Player;
            player.teamId = team;
            player.techState = new TechTreeState(team);
            player.positionAtTurnStart = player.position;
            player.deck.heatPool = new HeatPool(2);
            input.BeginCardSelection();
            if (team == TeamId.CN)
            {
                typeof(MVPGameManager).GetMethod("ApplyYinYang", Private)
                    .Invoke(manager, new object[] { player, YinYangResult.Yin });
                Assert.That(player.deck.heatPool.remaining, Is.EqualTo(1));
                Assert.That(player.deck.CountHeatInDiscardPile(), Is.EqualTo(1));
            }
            else
            {
                player.techState.landmark2PassCount = 2;
                player.techState.totalRepairs = 2;
                Assert.That(player.deck.DrawHeatFromPoolToHand(1), Is.EqualTo(1));
                typeof(MVPGameManager).GetMethod("ResolveMotherRoadPass", Private)
                    .Invoke(manager, new object[] { player, 1, player.position });
                Assert.That(player.deck.heatPool.remaining, Is.EqualTo(2));
                Assert.That(player.deck.CountHeatInHand(), Is.Zero);
                Assert.That(player.techState.landmark2UltUsed, Is.True);
            }

            AssertInstantLaneCrossing(player, panel, 1, finished);
            Assert.That(input.WaitingForCards, Is.True, "Synchronous settlement does not steal the card gate.");
            Assert.That(car.transform.position, Is.EqualTo(track.GetNodePosition(0, 1)));
            Assert.That(logs.Count(message => message.Contains("完成第")), Is.EqualTo(1));
            Assert.That(logs.Count(message => message.Contains("完赛！")), Is.EqualTo(finished ? 1 : 0));
            Assert.That(logs.Last(), Does.Contain(team == TeamId.CN ? "阴阳茶(阴)" : "母亲之路(复兴)"));
        }
    }

    [TestCase(0, 1)]
    [TestCase(3, -1)]
    public void EdgeChoiceAfterInstantCrossingKeepsTraversalSuspendedUntilKeep(int lane, int blockedDirection)
    {
        using (new InstantLaneAudioSilencer())
        {
            TargetRecorder animator = ConfigureAuthoredInstantTrack(out GameObject car);
            GameObject panel = CreateTraversalPanel();
            List<string> logs = CaptureInstantLaneLogs();
            lanes[0] = lane;
            AdvanceInstantLane(manager.Player, 1);
            IEnumerator traversal = Traverse(manager.Player, car, track.TotalNodes + 1, lane);
            IEnumerator choice = SeekInstantLaneWait(traversal, out WaitWhile waiting);

            manager.ChooseIndianapolisLaneChange(blockedDirection);
            input.EndCardSelection(); // A stale completion must not close this lane gate.
            Assert.That(waiting.keepWaiting, Is.True);
            Assert.That(animator.Targets.Count, Is.EqualTo(track.TotalNodes));
            Assert.That(lanes[0], Is.EqualTo(lane));
            Assert.That(logs.Any(message => message.Contains("印地起点换道")), Is.False);

            manager.ChooseIndianapolisLaneChange(0);
            Assert.That(waiting.keepWaiting, Is.False);
            Assert.That(choice.MoveNext(), Is.False);
            Drain(traversal);
            Assert.That(animator.Targets.Last(), Is.EqualTo(track.GetNodePosition(1, lane)));
            Assert.That(animator.Targets.Count, Is.EqualTo(track.TotalNodes + 1));
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(logs.Count(message => message.Contains("印地起点换道")), Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingPanelOrAiTraversalAfterInstantCrossingCannotOpenHumanWait(bool ai)
    {
        using (new InstantLaneAudioSilencer())
        {
            TargetRecorder animator = ConfigureAuthoredInstantTrack(out GameObject humanCar);
            GameObject panel = ai ? CreateTraversalPanel() : null;
            PlayerState participant = ai ? InstantLaneSession.Players[1] : manager.Player;
            GameObject car = humanCar;
            int lane = ai ? 3 : 1;
            if (ai)
            {
                car = new GameObject("Instant lane AI car");
                car.transform.SetParent(host.transform, false);
                ((List<GameObject>)typeof(MVPGameManager).GetField("carInstances", Private)
                    .GetValue(manager)).Add(car);
            }
            AdvanceInstantLane(participant, 1);
            Drain(Traverse(participant, car, track.TotalNodes + 1, lane));

            Assert.That(participant.lap, Is.EqualTo(2));
            Assert.That(participant.hasFinished, Is.False);
            Assert.That(animator.Targets.Count, Is.EqualTo(track.TotalNodes + 1));
            Assert.That(animator.Targets.Last(), Is.EqualTo(track.GetNodePosition(1, lane)));
            Assert.That(input.WaitingForLaneChange, Is.False);
            Assert.That(lanes, Is.EqualTo(new[] { 1, 3 }));
            if (panel != null) Assert.That(panel.activeSelf, Is.False);
            if (ai) Assert.That(manager.Player.lap, Is.Zero);
        }
    }

    [TestCase("gear")]
    [TestCase("cards")]
    [TestCase("discard")]
    public void InstantCrossingDoesNotReplaceAnotherInputGate(string gate)
    {
        using (new InstantLaneAudioSilencer())
        {
            ConfigureAuthoredInstantTrack(out _);
            GameObject panel = CreateTraversalPanel();
            if (gate == "gear") input.BeginGearSelection(2);
            else if (gate == "cards") input.BeginCardSelection();
            else input.BeginDiscardSelection();
            AdvanceInstantLane(manager.Player, 1);
            manager.ChooseIndianapolisLaneChange(-1);

            AssertInstantLaneCrossing(manager.Player, panel, 1, false);
            Assert.That(input.WaitingForGear, Is.EqualTo(gate == "gear"));
            Assert.That(input.WaitingForCards, Is.EqualTo(gate == "cards"));
            Assert.That(input.WaitingForDiscard, Is.EqualTo(gate == "discard"));
            Assert.That(input.WaitingForPitChoice, Is.False);
            if (gate == "gear")
            {
                Assert.That(input.PendingGear, Is.EqualTo(2));
                Assert.That(input.PlayerGearChoice, Is.EqualTo(2));
            }
        }
    }

    [Test]
    public void MultiWrapInstantFinishStopsRegistrationAndSuppressesSubsequentLaneOffer()
    {
        using (new InstantLaneAudioSilencer())
        {
            ConfigureAuthoredInstantTrack(out _);
            GameObject panel = CreateTraversalPanel();
            List<string> logs = CaptureInstantLaneLogs();
            AdvanceInstantLane(manager.Player, 1);
            AdvanceInstantLane(manager.Player, 4 * track.TotalNodes);

            AssertInstantLaneCrossing(manager.Player, panel, crossingConfig.totalLaps, true);
            Assert.That(InstantLaneSession.NextFinishOrder, Is.EqualTo(2));
            Assert.That(InstantLaneSession.IsRaceOver(), Is.False, "The unfinished AI still owns its race.");
            Assert.That(RegisterCrossing(manager.Player), Is.False);
            Assert.That(manager.Player.lap, Is.EqualTo(crossingConfig.totalLaps));
            Assert.That(InstantLaneSession.NextFinishOrder, Is.EqualTo(2));
            Assert.That(logs.Count(message => message.Contains("完成第")), Is.EqualTo(3));
            Assert.That(logs.Count(message => message.Contains("完赛！")), Is.EqualTo(1));
            Assert.That(logs.Any(message => message.Contains("印地起点换道")), Is.False);
        }
    }

    [TestCase("empty-nodes")]
    [TestCase("null-nodes")]
    [TestCase("missing-track")]
    [TestCase("null-player")]
    public void InstantCalculationFailurePrecedesAllStateAndPresentationEffects(string invalid)
    {
        TargetRecorder animator = ConfigureAuthoredInstantTrack(out GameObject car);
        List<string> logs = CaptureInstantLaneLogs();
        input.BeginCardSelection();
        PlayerState player = manager.Player;
        player.deck.heatPool = new HeatPool(2);
        int position = player.position;
        int heat = player.deck.heatPool.remaining;
        Vector3 carPosition = car.transform.position;
        if (invalid == "empty-nodes")
            typeof(TrackManager).GetField("nodes", Private).SetValue(track, new List<TrackNode>());
        if (invalid == "null-nodes")
            typeof(TrackManager).GetField("nodes", Private).SetValue(track, null);
        if (invalid == "missing-track") manager.trackManager = null;

        TargetInvocationException error = Assert.Throws<TargetInvocationException>(
            () => AdvanceInstantLane(invalid == "null-player" ? null : player, 1));

        Assert.That(error.InnerException, invalid == "empty-nodes"
            ? Is.TypeOf<DivideByZeroException>() : Is.TypeOf<NullReferenceException>());
        Assert.That(player.position, Is.EqualTo(position));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(heat));
        Assert.That(player.lap, Is.Zero);
        Assert.That(player.hasFinished, Is.False);
        Assert.That(InstantLaneSession.NextFinishOrder, Is.EqualTo(1));
        Assert.That(input.WaitingForCards, Is.True);
        Assert.That(input.WaitingForLaneChange, Is.False);
        Assert.That(lanes, Is.EqualTo(new[] { 1, 3 }));
        Assert.That(car.transform.position, Is.EqualTo(carPosition));
        Assert.That(animator.Targets, Is.Empty);
        Assert.That(logs, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InstantWrapSettlesLapBeforeRawEndPitReservationAndTeleport(bool finalLap)
    {
        using (new InstantLaneAudioSilencer())
        {
            TargetRecorder animator = ConfigureTraversal(out GameObject car);
            crossingConfig.totalLaps = finalLap ? 1 : 3;
            crossingConfig.enablePitLane = true;
            track.Nodes[1].isPitEntry = true;
            track.Nodes[2].isPitExit = true;
            PlayerState player = manager.Player; // Starts at cell 3 of four.
            player.pitStopRequested = true;
            player.pitChoiceResolvedThisLap = true;
            Vector3 initialCarPosition = car.transform.position;
            var observations = new List<string>();
            manager.hudUI = host.AddComponent<HUDUI>();
            typeof(HUDUI).GetField("logSink", Private).SetValue(manager.hudUI,
                (Action<string>)(message =>
                {
                    Assert.That(player.position, Is.EqualTo(1), "Commit wrapped position before settlement.");
                    Assert.That(car.transform.position, Is.EqualTo(initialCarPosition), "Teleport comes last.");
                    if (message.Contains("完成第"))
                    {
                        Assert.That(player.pitStopScheduled, Is.False, "Lap settlement precedes the pit reservation.");
                        observations.Add("lap");
                    }
                    else if (message.Contains("下一回合执行进站"))
                    {
                        Assert.That(player.lap, Is.EqualTo(1));
                        Assert.That(player.pitStopScheduled, Is.True);
                        observations.Add("pit");
                    }
                }));

            AdvanceInstantLane(player, 2); // Raw end 5 crosses both line 0 and entry 1.

            Assert.That(observations, Is.EqualTo(finalLap ? new[] { "lap" } : new[] { "lap", "pit" }));
            Assert.That(player.position, Is.EqualTo(1));
            Assert.That(player.lap, Is.EqualTo(1));
            Assert.That(player.hasFinished, Is.EqualTo(finalLap));
            Assert.That(player.finishOrder, Is.EqualTo(finalLap ? 1 : 0));
            Assert.That(player.pitStopRequested, Is.False);
            Assert.That(player.pitStopScheduled, Is.EqualTo(!finalLap));
            Assert.That(car.transform.position, Is.EqualTo(track.GetNodePosition(1, 1)));
            Assert.That(animator.Targets, Is.Empty);
            Assert.That(input.WaitingForLaneChange, Is.False);
        }
    }

    [TestCase(0, -1, -1)]
    [TestCase(1, 0, 1)]
    public void NonForwardInstantMovementKeepsSignedRemainderWithoutLapOrLaneOffer(
        int start, int movement, int expected)
    {
        TargetRecorder animator = ConfigureTraversal(out GameObject car);
        manager.Player.position = start;
        input.BeginCardSelection();

        AdvanceInstantLane(manager.Player, movement);

        Assert.That(manager.Player.position, Is.EqualTo(expected));
        Assert.That(manager.Player.lap, Is.Zero);
        Assert.That(manager.Player.hasFinished, Is.False);
        Assert.That(car.transform.position, Is.EqualTo(track.GetNodePosition(expected, 1)));
        Assert.That(animator.Targets, Is.Empty);
        Assert.That(input.WaitingForCards, Is.True);
        Assert.That(input.WaitingForLaneChange, Is.False);
    }

    private RaceSession InstantLaneSession => (RaceSession)typeof(MVPGameManager)
        .GetField("session", Private).GetValue(manager);

    private TargetRecorder ConfigureAuthoredInstantTrack(out GameObject car)
    {
        TargetRecorder animator = ConfigureTraversal(out car);
        TrackConfig authored = TrackDataLoader.LoadConfig(TrackPresentationRules.IndianapolisTrackId);
        Assert.That(authored.allowStartFinishLaneChange, Is.True);
        crossingConfig.totalLaps = authored.laps;
        crossingConfig.enablePitLane = false;
        crossingConfig.enableTechTree = false;
        typeof(TrackManager).GetProperty("LoadedTrackConfig").SetValue(track, authored);
        typeof(TrackManager).GetField("nodes", Private).SetValue(track, TrackDataLoader.ConfigToNodes(authored));
        typeof(TrackManager).GetField("worldPathCoordinates", Private).SetValue(track,
            TrackDataLoader.ConfigToWorldPositions(authored, 40f, 20f));
        foreach (PlayerState player in InstantLaneSession.Players)
        {
            player.position = track.TotalNodes - 1;
            Assert.That(player.lap, Is.Zero);
            Assert.That(player.hasFinished, Is.False);
            Assert.That(player.isBlown, Is.False);
        }
        Assert.That(track.StartFinishNodeIndex, Is.Zero);
        Assert.That(track.LaneCount, Is.EqualTo(4));
        return animator;
    }

    private List<string> CaptureInstantLaneLogs()
    {
        var hud = host.AddComponent<HUDUI>();
        manager.hudUI = hud;
        var logs = new List<string>();
        typeof(HUDUI).GetField("logSink", Private).SetValue(hud, (Action<string>)logs.Add);
        return logs;
    }

    private void AdvanceInstantLane(PlayerState player, int cells) => typeof(MVPGameManager)
        .GetMethod("AdvanceInstantTechnologyMovement", Private).Invoke(manager, new object[] { player, cells });

    private void AssertInstantLaneCrossing(PlayerState player, GameObject panel, int lap, bool finished)
    {
        Assert.That(player.position, Is.Zero);
        Assert.That(player.lap, Is.EqualTo(lap));
        Assert.That(player.hasFinished, Is.EqualTo(finished));
        Assert.That(player.finishOrder, Is.EqualTo(finished ? 1 : 0));
        Assert.That(lanes, Is.EqualTo(new[] { 1, 3 }));
        Assert.That(input.WaitingForLaneChange, Is.False);
        Assert.That(panel.activeSelf, Is.False);
    }

    private IEnumerator SeekInstantLaneWait(IEnumerator traversal, out WaitWhile waiting)
    {
        int steps = 0;
        while (traversal.MoveNext())
        {
            Assert.That(++steps, Is.LessThan(4 * track.TotalNodes));
            if (!(traversal.Current is IEnumerator step) || !step.MoveNext()) continue;
            if (step.Current is WaitWhile choiceWait)
            {
                Assert.That(input.WaitingForLaneChange, Is.True);
                waiting = choiceWait;
                return step;
            }
            Drain(step);
        }
        Assert.Fail("Traversal never reached its next unfinished human lane crossing.");
        waiting = null;
        return null;
    }

    private sealed class InstantLaneAudioSilencer : IDisposable
    {
        private static readonly FieldInfo Instance = typeof(AudioService)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly object previousInstance = Instance.GetValue(null);
        public InstantLaneAudioSilencer() => Instance.SetValue(null, null);
        public void Dispose() => Instance.SetValue(null, previousInstance);
    }
}
