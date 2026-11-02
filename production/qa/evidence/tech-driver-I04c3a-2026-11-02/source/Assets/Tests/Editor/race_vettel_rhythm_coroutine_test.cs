using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Integration; Assets/Tests/Editor; BLOCKING. Real native GameLoop/API fee, native
/// DE source, separate driver reward movement, new-tail apex cost and ordinary spin.
/// Owned synthetic track/headless UI; not player button, visual or complete-mode acceptance.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator VettelPaidSourceWindowAddsTwoAfterEachOfTwoRealOwnerInputsAndChargesOnlyOnce()
    {
        CreateRace(TeamId.DE);
        try
        {
            session.TeamVehicleBonusesEnabled = true; config.maxGear = 3; config.gearTwoCooldown = 0;
            var human = session.Human; human.driverId = "de_sebastian_vettel"; human.gear = 3;
            human.driverSkill.Initialize(human.DriverProfile, 5, true);
            int uses = human.driverSkill.UsesRemaining;
            manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
            yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "Vettel active first A1");
            manager.OnDriverSkillButtonClicked();
            Assert.That(manager.hudUI.IsReturnToMenuConfirmationVisible, Is.True, "Actual button requires one source/fee choice.");
            var plan = Get<VettelRhythmActivationPlan>(manager, "pendingVettelRhythmPlan");
            Assert.That(plan, Is.Not.Null); manager.hudUI.ConfirmReturnToMenu();
            Assert.That(manager.hudUI.IsReturnToMenuConfirmationVisible, Is.False);
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(5)); Assert.That(human.deck.ContainsInHand(plan.PaidHeat), Is.True);
            Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
            for (int turn = 1; turn <= 2; turn++)
            {
                if (turn == 2) yield return WaitFor(() => Turn == 2 && manager.TutorialInputPhase == "gear", "Vettel active continuation A1");
                Assert.That(human.driverSkill.Rhythm.HasWindow, Is.True, "Owner input " + turn + " must remain within its one paid fixed-source window.");
                Assert.That(human.driverSkill.Rhythm.LockedSource, Is.EqualTo(VettelNativeSource.BasicCruise));
                Assert.That(human.driverSkill.Rhythm.InputsStarted, Is.EqualTo(turn));
                yield return VettelRhythmPlayThreeOrdinarySpeeds();
                yield return WaitFor(() => manager.TutorialInputPhase == "discard", "Vettel active base plus separate driver tail " + turn);
                Assert.That(human.position, Is.EqualTo(turn * 9), "Native DE base7 plus one settled driver tail2, never +2 per speed card.");
                Assert.That(human.totalMovementThisTurn, Is.EqualTo(9));
                Assert.That(human.turnMovement.ActualCells, Is.EqualTo(9));
                Assert.That(human.turnMovement.Entries.Where(e => e.Kind == TurnMovementKind.Driver && e.Label.Contains("人车合拍")).Sum(e => e.ActualCells), Is.EqualTo(2));
                Assert.That(human.driverSkill.Rhythm.BonusThisInput, Is.EqualTo(2));
                Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
                Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6), "First fixed hand fee may return by ordinary DE reaction; continuation never charges another fee.");
                Assert.That(human.spinCounter, Is.Zero); AssertActiveAndHeatConserved();
                manager.OnPlayCardsButtonClicked();
            }
            yield return WaitFor(() => Turn == 3 && manager.TutorialInputPhase == "gear", "Vettel active window expires before third owner input");
            Assert.That(human.driverSkill.Rhythm.HasWindow, Is.False); Assert.That(human.driverSkill.Rhythm.BonusThisInput, Is.Zero);
            Assert.That(human.driverSkill.Rhythm.ManualPath, Is.True); Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
            TestContext.WriteLine("Native Vettel paid source window completed two driven owner inputs and closed before third; one fee/use only.");
        }
        finally { DestroyRace(); }
    }

    [UnityTest]
    public IEnumerator VettelRewardTailActuallyPaysNewApexAndKeepsOrdinaryPartialPaymentSpinRules()
    {
        foreach (bool insufficient in new[] { false, true })
        {
            CreateRace(TeamId.DE);
            try
            {
                session.TeamVehicleBonusesEnabled = true; config.maxGear = 3; config.gearTwoCooldown = 0;
                var human = session.Human; human.driverId = "de_sebastian_vettel"; human.gear = 3;
                human.driverSkill.Initialize(human.DriverProfile, 5, true);
                if (insufficient) human.deck.DrawHeatFromPoolToHand(5);
                int uses = human.driverSkill.UsesRemaining;
                int baseLimit = insufficient ? 0 : 1; // DE handling +1: actual corner limit1 or2 against card corner speed3.
                Get<List<TrackNode>>(manager.trackManager, "nodes")[8] = new TrackNode(8, baseLimit, "Vettel reward-only apex", 1, false, true);
                config.enablePitLane = true;
                var pitNodes = Get<List<TrackNode>>(manager.trackManager, "nodes");
                pitNodes[9].isPitEntry = true; pitNodes[20].isPitExit = true;
                Get<Dictionary<int, int>>(manager.trackManager, "cornerSpeedLimits")[1] = baseLimit;
                Get<Dictionary<int, string>>(manager.trackManager, "cornerNames")[1] = "Vettel reward-only apex";
                manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
                yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "Vettel paid tail input");
                Assert.That(manager.TryPrepareVettelRhythmRequest(out var plan, out string reason), Is.True, reason);
                Assert.That(manager.TryConfirmVettelRhythmRequest(plan, VettelNativeSource.BasicCruise, out reason), Is.True, reason);
                Assert.That(human.deck.heatPool.remaining, Is.EqualTo(insufficient ? 0 : 5));
                Assert.That(human.pitStopRequested, Is.False, "Reservation is made at formal card completion, after the gear-input pit approach gate.");
                yield return VettelRhythmPlayThreeOrdinarySpeeds(true);
                yield return WaitFor(() => human.spinCounter > 0 || manager.TutorialInputPhase == "discard", "Vettel new tail apex settled to spin or discard");
                Assert.That(human.pitStopScheduled, Is.EqualTo(!insufficient),
                    "Pit entry9 belongs only to the successfully settled reward tail; partial corner failure rewinds to native endpoint7 and must not schedule that uncompleted entry.");
                Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
                Assert.That(human.heatPaidCardsThisTurn.Count, Is.EqualTo(2), "Fixed active receipt plus one actual ordinary corner heat receipt, including partial payment before spin.");
                if (!insufficient)
                {
                    Assert.That(human.position, Is.EqualTo(9)); Assert.That(human.spinCounter, Is.Zero);
                    Assert.That(human.deck.heatPool.remaining, Is.EqualTo(5));
                    var cornerFee = human.heatPaidCardsThisTurn.Single(c => !ReferenceEquals(c, plan.PaidHeat));
                    Assert.That(cornerFee.IsHeat && !cornerFee.isTemp && human.deck.ContainsInHand(cornerFee), Is.True,
                        "New apex8 lies beyond native endpoint7; its ordinary fee must be a real permanent hand instance.");
                    Assert.That(human.driverSkill.Rhythm.BonusThisInput, Is.EqualTo(2));
                }
                else
                {
                    Assert.That(human.spinCounter, Is.EqualTo(1)); Assert.That(human.skipNextTurn, Is.True);
                    Assert.That(human.position, Is.EqualTo(7), "Partial tail corner payment rewinds to the real native endpoint.");
                    Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6), "Existing spin recovery returns actual circulating heat; it does not undo the spent race use.");
                    Assert.That(human.turnMovement.RewindCells, Is.EqualTo(2));
                    Assert.That(human.driverSkill.Rhythm.HasWindow, Is.False); Assert.That(human.driverSkill.Rhythm.ManualPath, Is.True);
                }
                AssertActiveAndHeatConserved();
                TestContext.WriteLine("Native Vettel reward-only apex completed; ordinary partial-payment spin=" + insufficient + "; pit scheduled=" + human.pitStopScheduled);
            }
            finally { DestroyRace(); }
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator VettelProductionRewardCoroutineClipsAtNonzeroFinalLineAndChecksEarlierTailApexBeforeFinish()
    {
        foreach (bool earlierApexFails in new[] { false, true })
        {
            CreateRace(TeamId.DE);
            try
            {
                session.TeamVehicleBonusesEnabled = true; config.maxGear = 3; config.totalLaps = 1; config.gearTwoCooldown = 0;
                var human = session.Human; human.driverId = "de_sebastian_vettel"; human.gear = 3;
                human.position = earlierApexFails ? 2 : 3;
                human.driverSkill.Initialize(human.DriverProfile, 5, true);
                if (earlierApexFails) human.deck.DrawHeatFromPoolToHand(5);
                var nodes = Get<List<TrackNode>>(manager.trackManager, "nodes");
                foreach (var node in nodes) node.isStartFinish = false;
                nodes[11].isStartFinish = true;
                if (earlierApexFails)
                {
                    nodes[10] = new TrackNode(10, 0, "Vettel pre-finish tail apex", 1, false, true);
                    Get<Dictionary<int, int>>(manager.trackManager, "cornerSpeedLimits")[1] = 0;
                    Get<Dictionary<int, string>>(manager.trackManager, "cornerNames")[1] = "Vettel pre-finish tail apex";
                }
                session.BeginTurn(human, 1); Assert.That(session.BeginDriverSkillInput(human, nodes.Count, 11), Is.True);
                Set(manager, "raceTurnNumber", 1); Get<RaceInputState>(manager, "inputState").BeginGearSelection(3);
                Assert.That(manager.trackManager.StartFinishNodeIndex, Is.EqualTo(11));
                Assert.That(manager.TryPrepareVettelRhythmRequest(out var plan, out string reason), Is.True, reason);
                Assert.That(manager.TryConfirmVettelRhythmRequest(plan, VettelNativeSource.BasicCruise, out reason), Is.True, reason);
                int uses = human.driverSkill.UsesRemaining;
                Assert.That(CardPlayRules.CommitSpeedCards(human, human.deck.Hand.Where(c => c.IsSpeed).Take(3).ToArray(), 3), Is.EqualTo(SpeedCardCommitResult.Success));
                InvokeVettelRhythmNative("ComputeMovements", session.Players, new HashSet<PlayerState>());
                int start = human.position, lap = human.lap, cornerEnd = start + human.cornerTotalThisTurn;
                bool baseDone = false;
                var baseMovement = (IEnumerator)InvokeVettelRhythmNative("AnimateBaseMovementWithFinishGate", human, -1, null);
                manager.StartCoroutine(CompleteMovement(baseMovement, () => baseDone = true));
                yield return WaitFor(() => baseDone, "Vettel real native base before nonzero final line");
                Assert.That(human.position, Is.EqualTo(earlierApexFails ? 9 : 10));
                Assert.That(human.hasFinished, Is.False);
                InvokeVettelRhythmNative("ReactStep", human);
                bool completedCorner = (bool)InvokeVettelRhythmNative("ResolveBaseMovementCorners", human, start, cornerEnd, lap);
                int reward = (int)InvokeVettelRhythmNative("ResolveVettelFeedbackAfterMovement", human, start, lap, completedCorner);
                Assert.That(reward, Is.EqualTo(2));
                bool rewardDone = false;
                var rewardMovement = (IEnumerator)InvokeVettelRhythmNative("ApplyVettelRhythmReward", human, start, cornerEnd, lap, reward);
                manager.StartCoroutine(CompleteMovement(rewardMovement, () => rewardDone = true));
                yield return WaitFor(() => rewardDone, "Vettel real reward coroutine final-line fee gate");
                if (!earlierApexFails)
                {
                    Assert.That(human.position, Is.EqualTo(11)); Assert.That(human.hasFinished, Is.True);
                    Assert.That(human.finishOrder, Is.EqualTo(1)); Assert.That(session.NextFinishOrder, Is.EqualTo(2));
                    Assert.That(human.turnMovement.Entries.Where(e => e.Kind == TurnMovementKind.Driver && e.Label.Contains("人车合拍")).Sum(e => e.ActualCells), Is.EqualTo(1), "Planned2 stops after one actual reward cell at final line11.");
                    Assert.That(human.turnMovement.ActualCells, Is.EqualTo(8));
                }
                else
                {
                    Assert.That(human.position, Is.EqualTo(9)); Assert.That(human.spinCounter, Is.EqualTo(1));
                    Assert.That(human.hasFinished, Is.False, "Apex10 ordinary fee must settle before final line11 can finish.");
                    Assert.That(human.finishOrder, Is.Zero); Assert.That(session.NextFinishOrder, Is.EqualTo(1));
                    Assert.That(human.driverSkill.Rhythm.HasWindow, Is.False); Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));
                }
                Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(uses));
                TestContext.WriteLine("Real production movement/reward coroutines, nonzero final line11; earlier tail apex failure=" + earlierApexFails + ". This fixture does not run the entire GameLoop.");
            }
            finally { DestroyRace(); }
            yield return null;
        }
    }

    private object InvokeVettelRhythmNative(string name, params object[] arguments)
        => typeof(MVPGameManager).GetMethod(name, PrivateInstance).Invoke(manager, arguments);
    private IEnumerator VettelRhythmPlayThreeOrdinarySpeeds(bool reservePitAfterCardCommit = false)
    {
        manager.OnGearButtonClicked(3); manager.OnConfirmGearClicked();
        yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Vettel active formal card phase");
        var selected = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").Where(c => c.cardData.IsSpeed && !c.cardData.isTemp).Take(3).ToArray();
        Assert.That(selected.Length, Is.EqualTo(3));
        foreach (var card in selected) { Assert.That(card.cardData.value, Is.EqualTo(1)); card.SetSelectedWithoutNotify(true); }
        manager.OnPlayCardsButtonClicked(); Assert.That(session.Human.playedSpeedCardsThisTurn.Count, Is.EqualTo(3));
        if (reservePitAfterCardCommit) session.Human.pitStopRequested = true;
        manager.OnPlayCardsButtonClicked();
    }
}
