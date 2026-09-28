using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class LaneChoicePresentationTests
{
    [TestCase(1, "向内一格")]
    [TestCase(99, "向内一格")]
    [TestCase(-1, "向外一格")]
    [TestCase(-99, "向外一格")]
    [TestCase(0, "保持车道")]
    public void ChoiceCopyMatchesTheRuleDirection(int direction, string label) =>
        Assert.AreEqual(label, LaneChoicePresentationRules.GetChoiceLabel(direction));

    [TestCase(1, 0, "印地起点换道：向内一格（第 1 道）")]
    [TestCase(1, 2, "印地起点换道：向外一格（第 3 道）")]
    [TestCase(2, 1, "印地起点换道：向内一格（第 2 道）")]
    [TestCase(2, 3, "印地起点换道：向外一格（第 4 道）")]
    [TestCase(3, 3, "印地起点换道：保持当前车道（第 4 道）")]
    public void ResolvedCopyUsesTheActualLaneDelta(int oldLane, int selected, string expected) =>
        Assert.AreEqual(expected, LaneChoicePresentationRules.GetResolvedLog(oldLane, selected));
}

public class LaneChoiceRulesTests
{
    [TestCase(0, 4, 1, 0, false)]
    [TestCase(3, 4, -1, 3, false)]
    [TestCase(1, 4, 1, 0, true)]
    [TestCase(1, 4, -1, 2, true)]
    [TestCase(2, 4, 99, 1, true)]
    [TestCase(2, 4, -99, 3, true)]
    [TestCase(2, 4, 0, 2, true)]
    [TestCase(0, 1, 1, 0, false)]
    [TestCase(0, 1, -1, 0, false)]
    [TestCase(0, 1, 0, 0, true)]
    [TestCase(-1, 4, 1, 0, true)]
    [TestCase(5, 4, 1, 4, true)]
    [TestCase(5, 4, -1, 3, true)]
    [TestCase(5, 1, 0, 5, true)]
    public void SelectionPreservesOneStepAndExistingOutOfRangeSemantics(
        int lane, int count, int direction, int expected, bool accepted)
    {
        Assert.AreEqual(expected, RaceLaneRules.GetAdjacentLane(lane, count, direction));
        Assert.AreEqual(accepted, RaceLaneRules.TryChooseLane(lane, count, direction, out int selected));
        Assert.AreEqual(expected, selected);
    }
}

/// <summary>Real callbacks on inactive objects; no scene startup or visual teleport acceptance.</summary>
public class LaneChoiceBoundaryIntegrationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private MVPGameManager manager;
    private TrackManager track;
    private RaceInputState input;
    private List<int> lanes;
    private GameConfigSO crossingConfig;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Lane choice regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        track = host.AddComponent<TrackManager>();
        manager.trackManager = track;
        typeof(TrackManager).GetField("laneOffsets", Private).SetValue(track, new float[4]);
        input = (RaceInputState)typeof(MVPGameManager).GetField("inputState", Private).GetValue(manager);
        lanes = (List<int>)typeof(MVPGameManager).GetField("laneIndices", Private).GetValue(manager);
        lanes.Add(1);
        lanes.Add(3);
        var session = new RaceSession(new SystemRandomSource(1));
        session.Players.Add(new PlayerState("driver", false, 8, 2));
        session.Players.Add(new PlayerState("opponent", true, 8, 2));
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, session);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        if (crossingConfig != null) Object.DestroyImmediate(crossingConfig);
    }

    [TestCase(1, 0)]
    [TestCase(-1, 2)]
    [TestCase(0, 1)]
    [TestCase(100, 0)]
    [TestCase(-100, 2)]
    public void AcceptedInputChangesOnlyHumanLaneAndClosesGate(int direction, int expected)
    {
        input.BeginLaneChangeSelection();
        manager.ChooseIndianapolisLaneChange(direction);
        Assert.AreEqual(expected, lanes[0]);
        Assert.AreEqual(3, lanes[1]);
        Assert.IsFalse(input.WaitingForLaneChange);
        manager.ChooseIndianapolisLaneChange(-direction);
        Assert.AreEqual(expected, lanes[0]);
        Assert.AreEqual(8, manager.Player.position);
        Assert.AreEqual(2, manager.Player.gear);
    }

    [TestCase(0, 1)]
    [TestCase(3, -1)]
    public void BlockedEdgeKeepsGateOpenUntilKeepChoice(int lane, int direction)
    {
        lanes[0] = lane;
        input.BeginLaneChangeSelection();
        manager.ChooseIndianapolisLaneChange(direction);
        Assert.IsTrue(input.WaitingForLaneChange);
        Assert.AreEqual(lane, lanes[0]);
        manager.ChooseIndianapolisLaneChange(0);
        Assert.IsFalse(input.WaitingForLaneChange);
        Assert.AreEqual(lane, lanes[0]);
    }

    [TestCase("closed")]
    [TestCase("gear")]
    [TestCase("pit")]
    [TestCase("missing-track")]
    public void RejectedInputDoesNotConsumeOtherGates(string reason)
    {
        if (reason == "gear") input.BeginGearSelection(2);
        if (reason == "pit") input.BeginPitChoice();
        if (reason == "missing-track") { input.BeginLaneChangeSelection(); manager.trackManager = null; }
        manager.ChooseIndianapolisLaneChange(1);
        Assert.AreEqual(1, lanes[0]);
        Assert.AreEqual(reason == "gear", input.WaitingForGear);
        Assert.AreEqual(reason == "pit", input.WaitingForPitChoice);
        Assert.AreEqual(reason == "missing-track", input.WaitingForLaneChange);
    }

    [Test]
    public void TrackDirectionAdaptersUseTheSameLaneContract()
    {
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(RaceLaneRules.GetAdjacentLane(i, 4, 1), track.GetLaneTowardsInside(i));
            Assert.AreEqual(RaceLaneRules.GetAdjacentLane(i, 4, -1), track.GetLaneTowardsOutside(i));
        }
    }

    [TestCase(1, "印地起点换道：向内一格（第 1 道）")]
    [TestCase(-1, "印地起点换道：向外一格（第 3 道）")]
    [TestCase(0, "印地起点换道：保持当前车道（第 2 道）")]
    [TestCase(100, "印地起点换道：向内一格（第 1 道）")]
    [TestCase(-100, "印地起点换道：向外一格（第 3 道）")]
    public void AcceptedCallbackLogsActualDirectionOnlyOnce(int direction, string expected)
    {
        var hud = host.AddComponent<HUDUI>();
        manager.hudUI = hud;
        var logs = new List<string>();
        typeof(HUDUI).GetField("logSink", Private).SetValue(hud, (System.Action<string>)logs.Add);
        input.BeginLaneChangeSelection();
        manager.ChooseIndianapolisLaneChange(direction);
        manager.ChooseIndianapolisLaneChange(-direction);
        CollectionAssert.AreEqual(new[] { expected }, logs);
    }

    [TestCase(0, 1)]
    [TestCase(3, -1)]
    public void EdgeRejectionProducesNoFalseCompletionLog(int lane, int direction)
    {
        var hud = host.AddComponent<HUDUI>();
        manager.hudUI = hud;
        var logs = new List<string>();
        typeof(HUDUI).GetField("logSink", Private).SetValue(hud, (System.Action<string>)logs.Add);
        lanes[0] = lane;
        input.BeginLaneChangeSelection();
        manager.ChooseIndianapolisLaneChange(direction);
        Assert.IsEmpty(logs);
        Assert.IsTrue(input.WaitingForLaneChange);
        manager.ChooseIndianapolisLaneChange(0);
        CollectionAssert.AreEqual(new[] { $"印地起点换道：保持当前车道（第 {lane + 1} 道）" }, logs);
    }

    [TestCase(0, false, true)]
    [TestCase(1, true, true)]
    [TestCase(2, true, true)]
    [TestCase(3, true, false)]
    public void SelectionCoroutineDisablesOnlyTheBlockedDirectionAndClosesPanel(
        int lane, bool inward, bool outward)
    {
        lanes[0] = lane;
        var panel = new GameObject("Lane panel", typeof(RectTransform));
        panel.transform.SetParent(host.transform, false);
        panel.SetActive(false);
        typeof(MVPGameManager).GetField("laneChangePanel", Private).SetValue(manager, panel);
        Button inside = AddButton(panel, "laneInButton");
        Button outside = AddButton(panel, "laneOutButton");
        Button keep = AddButton(panel, "laneKeepButton");
        var routine = (IEnumerator)typeof(MVPGameManager).GetMethod("WaitForIndianapolisLaneChoice", Private)
            .Invoke(manager, null);
        Assert.IsTrue(routine.MoveNext());
        Assert.IsTrue(panel.activeSelf);
        Assert.AreEqual(inward, inside.interactable);
        Assert.AreEqual(outward, outside.interactable);
        Assert.IsTrue(keep.interactable);
        var waiting = (WaitWhile)routine.Current;
        Assert.IsTrue(waiting.keepWaiting);
        manager.ChooseIndianapolisLaneChange(0);
        Assert.IsFalse(waiting.keepWaiting);
        Assert.IsFalse(routine.MoveNext());
        Assert.IsFalse(panel.activeSelf);
        Assert.AreEqual(lane, lanes[0]);
    }

    [Test]
    public void MissingPanelDoesNotOpenAnUnfinishableInputGate()
    {
        var routine = (IEnumerator)typeof(MVPGameManager).GetMethod("WaitForIndianapolisLaneChoice", Private)
            .Invoke(manager, null);
        Assert.IsFalse(routine.MoveNext());
        Assert.IsFalse(input.WaitingForLaneChange);
        Assert.AreEqual(1, lanes[0]);
    }

    private Button AddButton(GameObject panel, string field)
    {
        var buttonHost = new GameObject(field, typeof(RectTransform), typeof(Button));
        buttonHost.transform.SetParent(panel.transform, false);
        var button = buttonHost.GetComponent<Button>();
        typeof(MVPGameManager).GetField(field, Private).SetValue(manager, button);
        return button;
    }

    [TestCase(TeamId.CN)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.JP)]
    public void HumanCrossingOffersLaneChoiceForEveryTeamWithoutChangingPosition(TeamId team)
    {
        ConfigureCrossings();
        manager.Player.teamId = team;
        Assert.IsTrue(RegisterCrossing(manager.Player));
        Assert.AreEqual(1, manager.Player.lap);
        Assert.IsFalse(manager.Player.hasFinished);
        Assert.AreEqual(8, manager.Player.position);
        Assert.AreEqual(2, manager.Player.gear);
        Assert.AreEqual(1, lanes[0]);
        Assert.IsFalse(input.WaitingForLaneChange); // Registration decides; the coroutine opens the gate.
    }

    [TestCase("ai")]
    [TestCase("ordinary-track")]
    [TestCase("already-finished")]
    public void NonChoiceCrossingsDoNotOpenTheLaneGate(string reason)
    {
        ConfigureCrossings();
        var session = (RaceSession)typeof(MVPGameManager).GetField("session", Private).GetValue(manager);
        PlayerState participant = reason == "ai" ? session.Players[1] : manager.Player;
        if (reason == "ordinary-track") crossingConfig.trackId = "silverstone";
        if (reason == "already-finished") { participant.hasFinished = true; participant.lap = 3; }
        Assert.IsFalse(RegisterCrossing(participant));
        Assert.AreEqual(reason == "already-finished" ? 3 : 1, participant.lap);
        Assert.IsFalse(input.WaitingForLaneChange);
        Assert.AreEqual(1, lanes[0]);
        Assert.AreEqual(3, lanes[1]);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FinalCrossingAssignsFinishBeforeRejectingLaneChoice(bool ai)
    {
        ConfigureCrossings();
        var session = (RaceSession)typeof(MVPGameManager).GetField("session", Private).GetValue(manager);
        PlayerState participant = ai ? session.Players[1] : manager.Player;
        participant.lap = 2;
        Assert.IsFalse(RegisterCrossing(participant));
        Assert.AreEqual(3, participant.lap);
        Assert.IsTrue(participant.hasFinished);
        Assert.AreEqual(1, participant.finishOrder);
        Assert.AreEqual(2, session.NextFinishOrder);
        Assert.IsFalse(RegisterCrossing(participant));
        Assert.AreEqual(3, participant.lap);
        Assert.AreEqual(2, session.NextFinishOrder);
        Assert.IsFalse(input.WaitingForLaneChange);
    }

    [TestCase(1, 0)]
    [TestCase(0, 1)]
    [TestCase(-1, 2)]
    public void TwoNonFinalCrossingsReopenChoiceThenFinalCrossingDoesNot(int direction, int selected)
    {
        ConfigureCrossings();
        var panel = new GameObject("Crossing lane panel", typeof(RectTransform));
        panel.transform.SetParent(host.transform, false);
        panel.SetActive(false);
        typeof(MVPGameManager).GetField("laneChangePanel", Private).SetValue(manager, panel);
        AddButton(panel, "laneInButton");
        AddButton(panel, "laneOutButton");
        AddButton(panel, "laneKeepButton");
        for (int lap = 1; lap <= 2; lap++)
        {
            Assert.IsTrue(RegisterCrossing(manager.Player));
            Assert.AreEqual(lap, manager.Player.lap);
            var routine = (IEnumerator)typeof(MVPGameManager).GetMethod("WaitForIndianapolisLaneChoice", Private)
                .Invoke(manager, null);
            Assert.IsTrue(routine.MoveNext());
            var waiting = (WaitWhile)routine.Current;
            Assert.IsTrue(waiting.keepWaiting);
            Assert.IsTrue(panel.activeSelf);
            manager.ChooseIndianapolisLaneChange(lap == 1 ? direction : 0);
            Assert.IsFalse(waiting.keepWaiting);
            Assert.IsFalse(routine.MoveNext());
            Assert.IsFalse(panel.activeSelf);
            Assert.AreEqual(selected, lanes[0]);
            Assert.AreEqual(8, manager.Player.position);
        }
        Assert.IsFalse(RegisterCrossing(manager.Player));
        Assert.IsTrue(manager.Player.hasFinished);
        Assert.AreEqual(3, manager.Player.lap);
        Assert.IsFalse(input.WaitingForLaneChange);
        Assert.IsFalse(panel.activeSelf);
        Assert.AreEqual(selected, lanes[0]);
    }

    private void ConfigureCrossings()
    {
        crossingConfig = ScriptableObject.CreateInstance<GameConfigSO>();
        crossingConfig.trackId = "indianapolis_burger";
        crossingConfig.totalLaps = 3;
        crossingConfig.enableWeather = false;
        manager.config = crossingConfig;
        track.config = crossingConfig;
    }

    [TestCase(1, 0)]
    [TestCase(0, 1)]
    [TestCase(-1, 2)]
    public void RemainingMovementTargetsFollowTheResolvedLane(int direction, int selected)
    {
        TargetRecorder animator = ConfigureTraversal(out GameObject car);
        var panel = new GameObject("Traversal lane panel", typeof(RectTransform));
        panel.transform.SetParent(host.transform, false);
        typeof(MVPGameManager).GetField("laneChangePanel", Private).SetValue(manager, panel);
        AddButton(panel, "laneInButton");
        AddButton(panel, "laneOutButton");
        AddButton(panel, "laneKeepButton");
        var traversal = (IEnumerator)typeof(MVPGameManager).GetMethod("TraverseMovementNodes", Private)
            .Invoke(manager, new object[] { manager.Player, car, 6, 1 });

        Assert.IsTrue(traversal.MoveNext());
        Drain((IEnumerator)traversal.Current);
        Assert.AreEqual(track.GetNodePosition(0, 1), animator.Targets[0]);
        Assert.IsTrue(traversal.MoveNext());
        var choice = (IEnumerator)traversal.Current;
        Assert.IsTrue(choice.MoveNext());
        var waiting = (WaitWhile)choice.Current;
        Assert.IsTrue(waiting.keepWaiting);
        Assert.AreEqual(1, manager.Player.lap);
        Assert.AreEqual(1, animator.Targets.Count); // No remaining node is visited before choice.
        manager.ChooseIndianapolisLaneChange(direction);
        Assert.IsFalse(waiting.keepWaiting);
        Assert.IsFalse(choice.MoveNext());
        while (traversal.MoveNext()) Drain((IEnumerator)traversal.Current);

        CollectionAssert.AreEqual(new[] { track.GetNodePosition(0, 1),
            track.GetNodePosition(1, selected), track.GetNodePosition(2, selected) }, animator.Targets);
        Assert.AreEqual(selected, lanes[0]);
        Assert.AreEqual(3, lanes[1]);
        Assert.AreEqual(3, manager.Player.position); // Outer movement owns final position settlement.
        Assert.AreEqual(2, manager.Player.gear);
        Assert.IsFalse(panel.activeSelf);
        Assert.IsFalse(input.WaitingForLaneChange);
    }

    [TestCase("ai")]
    [TestCase("ordinary-track")]
    [TestCase("final-lap")]
    [TestCase("missing-panel")]
    public void TraversalWithoutChoiceRetainsLaneAndCrossingSettlement(string reason)
    {
        TargetRecorder animator = ConfigureTraversal(out GameObject car);
        var session = (RaceSession)typeof(MVPGameManager).GetField("session", Private).GetValue(manager);
        PlayerState participant = reason == "ai" ? session.Players[1] : manager.Player;
        participant.position = 3;
        if (reason == "ordinary-track") crossingConfig.trackId = "silverstone_afternoon_tea";
        if (reason == "final-lap") participant.lap = 2;
        Drain(Traverse(participant, car, 6));
        CollectionAssert.AreEqual(new[] { track.GetNodePosition(0, 1),
            track.GetNodePosition(1, 1), track.GetNodePosition(2, 1) }, animator.Targets);
        Assert.AreEqual(reason == "final-lap" ? 3 : 1, participant.lap);
        Assert.AreEqual(reason == "final-lap", participant.hasFinished);
        Assert.AreEqual(3, participant.position);
        Assert.IsFalse(input.WaitingForLaneChange);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void MovementNotCrossingTheLineDoesNotOfferChoice(int target)
    {
        TargetRecorder animator = ConfigureTraversal(out GameObject car);
        manager.Player.position = 0;
        Drain(Traverse(manager.Player, car, target));
        Assert.AreEqual(target, animator.Targets.Count);
        for (int i = 1; i <= target; i++)
            Assert.AreEqual(track.GetNodePosition(i, 1), animator.Targets[i - 1]);
        Assert.AreEqual(0, manager.Player.lap);
        Assert.IsFalse(input.WaitingForLaneChange);
    }

    [TestCase(TeamId.CN)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.JP)]
    public void RepeatedNonZeroCrossingsWaitForEachChoiceBeforeVisitingRemainingNodes(TeamId team)
    {
        TargetRecorder animator = ConfigureTraversal(out GameObject car, 2);
        GameObject panel = CreateTraversalPanel();
        manager.Player.teamId = team;
        manager.Player.position = 1;
        IEnumerator traversal = Traverse(manager.Player, car, 11);
        int lane = 1;
        int crossings = 0;
        for (int absolute = 2; absolute <= 11; absolute++)
        {
            Assert.IsTrue(traversal.MoveNext());
            Drain((IEnumerator)traversal.Current);
            Assert.AreEqual(absolute - 1, animator.Targets.Count);
            Assert.AreEqual(track.GetNodePosition(absolute % 4, lane), animator.Targets[absolute - 2]);
            if (absolute % 4 == 2)
            {
                crossings++;
                Assert.IsTrue(traversal.MoveNext()); // Settle crossing only after node animation.
                Assert.AreEqual(crossings, manager.Player.lap);
                if (crossings < 3)
                {
                    var choice = (IEnumerator)traversal.Current;
                    Assert.IsTrue(choice.MoveNext());
                    var waiting = (WaitWhile)choice.Current;
                    Assert.IsTrue(waiting.keepWaiting);
                    Assert.IsTrue(panel.activeSelf);
                    Assert.AreEqual(absolute - 1, animator.Targets.Count);
                    Assert.IsFalse(manager.Player.hasFinished);
                    manager.ChooseIndianapolisLaneChange(crossings == 1 ? 1 : -1);
                    lane = crossings == 1 ? 0 : 1;
                    Assert.AreEqual(track.GetNodePosition(2, lane), car.transform.position);
                    Assert.IsFalse(waiting.keepWaiting);
                    Assert.IsFalse(choice.MoveNext());
                    Assert.IsFalse(panel.activeSelf);
                    Assert.IsTrue(traversal.MoveNext()); // Per-node delay follows the resolved choice.
                }
                else
                {
                    Assert.IsTrue(manager.Player.hasFinished);
                    Assert.IsFalse(panel.activeSelf);
                    Assert.IsFalse(input.WaitingForLaneChange);
                }
            }
            else Assert.IsTrue(traversal.MoveNext()); // Per-node delay without a crossing.
            Drain((IEnumerator)traversal.Current);
        }
        Assert.IsFalse(traversal.MoveNext());
        Assert.AreEqual(3, crossings);
        Assert.AreEqual(1, manager.Player.finishOrder);
        Assert.AreEqual(1, manager.Player.position); // Still owned by the outer movement coroutine.
        Assert.AreEqual(2, manager.Player.gear);
        Assert.AreEqual(1, lanes[0]);
        Assert.AreEqual(3, lanes[1]);
        var session = (RaceSession)typeof(MVPGameManager).GetField("session", Private).GetValue(manager);
        Assert.AreEqual(0, session.Players[1].lap);
        Assert.IsFalse(session.Players[1].hasFinished);
        Assert.IsFalse(input.WaitingForLaneChange);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void MissingPanelAcrossTwoLapsUsesConfiguredStartFinishNode(int startFinish)
    {
        TargetRecorder animator = ConfigureTraversal(out GameObject car, startFinish);
        int start = (startFinish + 3) % 4;
        manager.Player.position = start;
        Drain(Traverse(manager.Player, car, start + 8));
        Assert.AreEqual(8, animator.Targets.Count);
        for (int i = 1; i <= 8; i++)
            Assert.AreEqual(track.GetNodePosition((start + i) % 4, 1), animator.Targets[i - 1]);
        Assert.AreEqual(2, manager.Player.lap);
        Assert.IsFalse(manager.Player.hasFinished);
        Assert.AreEqual(start, manager.Player.position);
        Assert.IsFalse(input.WaitingForLaneChange);
    }

    [TestCase(0, 1)]
    [TestCase(3, -1)]
    public void RejectedEdgeAtNonZeroLineKeepsMovementWaitingUntilKeepChoice(int lane, int direction)
    {
        TargetRecorder animator = ConfigureTraversal(out GameObject car, 2);
        GameObject panel = CreateTraversalPanel();
        manager.Player.position = 1;
        lanes[0] = lane;
        IEnumerator traversal = Traverse(manager.Player, car, 4, lane);
        Assert.IsTrue(traversal.MoveNext());
        Drain((IEnumerator)traversal.Current);
        Assert.IsTrue(traversal.MoveNext());
        var choice = (IEnumerator)traversal.Current;
        Assert.IsTrue(choice.MoveNext());
        var waiting = (WaitWhile)choice.Current;
        manager.ChooseIndianapolisLaneChange(direction);
        Assert.IsTrue(waiting.keepWaiting);
        Assert.IsTrue(panel.activeSelf);
        Assert.AreEqual(1, animator.Targets.Count);
        Assert.AreEqual(track.GetNodePosition(2, lane), car.transform.position);
        manager.ChooseIndianapolisLaneChange(0);
        Assert.IsFalse(waiting.keepWaiting);
        Assert.IsFalse(choice.MoveNext());
        while (traversal.MoveNext()) Drain((IEnumerator)traversal.Current);
        CollectionAssert.AreEqual(new[] { track.GetNodePosition(2, lane),
            track.GetNodePosition(3, lane), track.GetNodePosition(0, lane) }, animator.Targets);
        Assert.AreEqual(1, manager.Player.lap);
        Assert.IsFalse(panel.activeSelf);
        Assert.IsFalse(input.WaitingForLaneChange);
    }

    [TestCase(3)]
    [TestCase(2)]
    public void EmptyTraversalDoesNotSettleCrossingsOrConsumeExistingGate(int target)
    {
        TargetRecorder animator = ConfigureTraversal(out GameObject car);
        input.BeginGearSelection(2);
        Drain(Traverse(manager.Player, car, target));
        Assert.IsEmpty(animator.Targets);
        Assert.AreEqual(0, manager.Player.lap);
        Assert.IsFalse(manager.Player.hasFinished);
        Assert.IsTrue(input.WaitingForGear);
        Assert.IsFalse(input.WaitingForLaneChange);
    }

    [TestCase("null-player")]
    [TestCase("missing-track")]
    [TestCase("empty-nodes")]
    public void OuterMovementGuardsExitWithoutTouchingRaceOrPresentation(string reason)
    {
        TargetRecorder animator = ConfigureTraversal(out GameObject car);
        GameObject panel = CreateTraversalPanel();
        input.BeginGearSelection(2);
        PlayerState participant = manager.Player;
        if (reason == "missing-track") manager.trackManager = null;
        if (reason == "empty-nodes") typeof(TrackManager).GetField("nodes", Private)
            .SetValue(track, new List<TrackNode>());
        Vector3 originalCarPosition = car.transform.position;
        Assert.IsFalse(OuterMovement(reason == "null-player" ? null : participant, 0, 9).MoveNext());
        Assert.AreEqual(3, participant.position);
        Assert.AreEqual(0, participant.lap);
        Assert.IsFalse(participant.hasFinished);
        Assert.AreEqual(2, participant.gear);
        Assert.AreEqual(originalCarPosition, car.transform.position);
        Assert.IsEmpty(animator.Targets);
        CollectionAssert.AreEqual(new[] { 1, 3 }, lanes);
        Assert.IsTrue(input.WaitingForGear);
        Assert.IsFalse(input.WaitingForLaneChange);
        Assert.IsFalse(panel.activeSelf);
    }

    [TestCase("negative-index", -3, 3)]
    [TestCase("negative-index", 0, 3)]
    [TestCase("negative-index", 1, 0)]
    [TestCase("negative-index", 9, 0)]
    [TestCase("past-list", 5, 0)]
    [TestCase("empty-list", 5, 0)]
    [TestCase("null-car", 5, 0)]
    [TestCase("destroyed-car", 5, 0)]
    public void MissingCarFallbackOnlyWrapsPositionWithoutCrossingSettlement(
        string reason, int movement, int expected)
    {
        TargetRecorder animator = ConfigureTraversal(out GameObject car);
        GameObject panel = CreateTraversalPanel();
        var cars = (List<GameObject>)typeof(MVPGameManager).GetField("carInstances", Private).GetValue(manager);
        int index = 0;
        if (reason == "negative-index") index = -1;
        if (reason == "past-list") index = cars.Count;
        if (reason == "empty-list") cars.Clear(); // Only the temporary fixture's list.
        if (reason == "null-car") cars[0] = null;
        if (reason == "destroyed-car") Object.DestroyImmediate(car);
        manager.Player.lap = 2;
        manager.Player.totalMovementThisTurn = 99; // Explicit movement argument is authoritative.
        manager.Player.pitStopRequested = true;
        input.BeginGearSelection(2);
        manager.config = null; // Fallback must not require camera/presentation configuration.
        Vector3 originalCarPosition = car != null ? car.transform.position : Vector3.zero;
        var originalDeck = manager.Player.deck;

        Assert.IsFalse(OuterMovement(manager.Player, index, movement).MoveNext());

        Assert.AreEqual(expected, manager.Player.position);
        Assert.AreEqual(2, manager.Player.lap); // Existing fallback does not traverse nodes.
        Assert.IsFalse(manager.Player.hasFinished);
        Assert.AreEqual(0, manager.Player.finishOrder);
        Assert.AreEqual(2, manager.Player.gear);
        Assert.AreEqual(99, manager.Player.totalMovementThisTurn);
        Assert.IsTrue(manager.Player.pitStopRequested);
        Assert.AreSame(originalDeck, manager.Player.deck);
        if (car != null) Assert.AreEqual(originalCarPosition, car.transform.position);
        Assert.IsEmpty(animator.Targets);
        CollectionAssert.AreEqual(new[] { 1, 3 }, lanes);
        Assert.IsTrue(input.WaitingForGear);
        Assert.IsFalse(input.WaitingForLaneChange);
        Assert.IsFalse(panel.activeSelf);
    }

    [TestCase(TeamId.CN, false)]
    [TestCase(TeamId.CN, true)]
    [TestCase(TeamId.US, false)]
    [TestCase(TeamId.US, true)]
    [TestCase(TeamId.DE, false)]
    [TestCase(TeamId.DE, true)]
    [TestCase(TeamId.IT, false)]
    [TestCase(TeamId.IT, true)]
    [TestCase(TeamId.UK, false)]
    [TestCase(TeamId.UK, true)]
    [TestCase(TeamId.JP, false)]
    [TestCase(TeamId.JP, true)]
    public void FallbackSettlesOnlyTheSuppliedParticipantAcrossTeams(TeamId team, bool ai)
    {
        TargetRecorder animator = ConfigureTraversal(out GameObject car);
        var session = (RaceSession)typeof(MVPGameManager).GetField("session", Private).GetValue(manager);
        PlayerState participant = session.Players[ai ? 1 : 0];
        PlayerState other = session.Players[ai ? 0 : 1];
        participant.teamId = team;
        participant.position = 2;
        participant.lap = 1;
        int otherPosition = other.position;
        participant.totalMovementThisTurn = 99;
        Assert.IsFalse(OuterMovement(participant, -1, 7).MoveNext());
        Assert.AreEqual(1, participant.position);
        Assert.AreEqual(1, participant.lap);
        Assert.AreEqual(99, participant.totalMovementThisTurn);
        Assert.AreEqual(2, participant.gear);
        Assert.IsFalse(participant.hasFinished);
        Assert.AreEqual(otherPosition, other.position);
        Assert.AreEqual(0, other.lap);
        Assert.IsFalse(other.hasFinished);
        Assert.IsEmpty(animator.Targets);
        CollectionAssert.AreEqual(new[] { 1, 3 }, lanes);
    }

    private IEnumerator OuterMovement(PlayerState participant, int carIndex, int movement) =>
        (IEnumerator)typeof(MVPGameManager).GetMethod("AnimateMovementByAmount", Private)
            .Invoke(manager, new object[] { participant, carIndex, movement, true });

    private GameObject CreateTraversalPanel()
    {
        var panel = new GameObject("Traversal lane panel", typeof(RectTransform));
        panel.transform.SetParent(host.transform, false);
        panel.SetActive(false);
        typeof(MVPGameManager).GetField("laneChangePanel", Private).SetValue(manager, panel);
        AddButton(panel, "laneInButton");
        AddButton(panel, "laneOutButton");
        AddButton(panel, "laneKeepButton");
        return panel;
    }

    private IEnumerator Traverse(PlayerState participant, GameObject car, int target, int lane = 1) =>
        (IEnumerator)typeof(MVPGameManager).GetMethod("TraverseMovementNodes", Private)
            .Invoke(manager, new object[] { participant, car, target, lane });

    private TargetRecorder ConfigureTraversal(out GameObject car, int startFinish = 0)
    {
        ConfigureCrossings();
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 4; i++) nodes.Add(new TrackNode(i, 99, "test node"));
        nodes[startFinish].isStartFinish = true;
        typeof(TrackManager).GetField("nodes", Private).SetValue(track, nodes);
        typeof(TrackManager).GetField("worldPathCoordinates", Private).SetValue(track,
            new[] { Vector2.zero, Vector2.right * 10, Vector2.one * 10, Vector2.up * 10 });
        typeof(TrackManager).GetField("laneOffsets", Private).SetValue(track, new[] { -3f, -1f, 1f, 3f });
        car = new GameObject("Traversal car");
        car.transform.SetParent(host.transform, false);
        ((List<GameObject>)typeof(MVPGameManager).GetField("carInstances", Private).GetValue(manager)).Add(car);
        var animator = new TargetRecorder();
        typeof(MVPGameManager).GetField("carMovementAnimator", Private).SetValue(manager, animator);
        typeof(MVPGameManager).GetField("nodeWaitDuration", Private).SetValue(manager, 0f);
        manager.Player.position = 3;
        return animator;
    }

    private static void Drain(IEnumerator routine)
    {
        int steps = 0;
        while (routine.MoveNext())
        {
            Assert.Less(++steps, 100, "Unexpected unbounded presentation wait");
            if (routine.Current is IEnumerator nested) Drain(nested);
        }
    }

    private sealed class TargetRecorder : ICarMovementAnimator
    {
        public readonly List<Vector3> Targets = new List<Vector3>();
        public IEnumerator MoveToNode(GameObject car, Vector3 targetPosition)
        {
            Targets.Add(targetPosition);
            car.transform.position = targetPosition;
            yield break;
        }
    }

    private bool RegisterCrossing(PlayerState participant) =>
        (bool)typeof(MVPGameManager).GetMethod("RegisterStartFinishCrossing", Private)
            .Invoke(manager, new object[] { participant });
}
