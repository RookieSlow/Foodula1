using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Native fourth-race completion feeds the real summer draft/service and fifth-race
/// launch request. The earlier three races are service-generated setup, not native
/// race evidence. Headless lane UI and real persistent storage remain out of scope.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator RealCareerFourthRaceCompletesSummerDraftAndUnlocksFifthLaunchOnce()
    {
        foreach (TeamId team in FreeRaceRosterRules.AvailableTeams)
            yield return CompleteAuthoredRace(team, 0, false, true,
                CareerCompletionFault.None, 3);
        yield return CompleteAuthoredRace(TeamId.CN, 0, true, true,
            CareerCompletionFault.None, 3);
    }

    private sealed partial class CareerCompletionContext
    {
        internal void AssertSummerBreakAndResume(MVPGameManager manager, string logPath, string closedTrace)
        {
            if (launch.RaceIndex != 3) return;
            Assert.That(launch.TrackId, Is.EqualTo("indianapolis_burger"));
            Assert.That(manager.trackManager.TotalNodes, Is.EqualTo(42));
            Assert.That(manager.Session.IsRaceOver(), Is.True);
            Assert.That(manager.hudUI.gameOverText.text, Does.Contain("已进入夏休"));
            Assert.That(closedTrace, Does.Contain("race=4/8").And.Contain("phase=SummerBreak"));
            var service = new CareerModeService(repository);
            CareerSeasonState summer = service.CurrentState;
            Assert.That(CareerModeRules.CanAdjustTechTree(summer), Is.True);
            Assert.That(CareerModeRules.CanStartNextRace(summer), Is.False);
            Assert.That(summer.SummerBreakUsed, Is.False);
            Assert.That(CareerRaceLaunchRequest.TryCreate(summer, "premature-fifth", out _), Is.False);
            string summerToken = store.Json;
            int savedWrites = store.SuccessfulWrites;
            int savedAttempts = store.WriteAttempts;
            var standings = CareerModeRules.GetStandings(summer)
                .Select(s => Tuple.Create(s.TeamId, s.Points)).ToArray();
            var terminal = manager.Session.Players.Select(p => Tuple.Create(p.position, p.lap,
                p.finishOrder, p.isBlown, p.hasFinished, p.techState.rpBalance, p.driverXp)).ToArray();
            var resultEntry = typeof(MVPGameManager).GetMethod("ShowGameOver", PrivateInstance);
            resultEntry.Invoke(manager, null); // Actual result already arrived from GameLoop.
            Assert.That(store.Json, Is.EqualTo(summerToken), "Repeated fourth result cannot double-score.");
            Assert.That(store.SuccessfulWrites, Is.EqualTo(savedWrites));
            Assert.That(store.WriteAttempts, Is.EqualTo(savedAttempts));
            Assert.That(RepositoryRequests, Is.EqualTo(2));

            TechTreeDatabase database = TechTreeDatabaseFactory.CreateDefault();
            Assert.That(CareerTechTreeDraft.TryCreate(summer.ActiveTechSnapshot, database,
                out CareerTechTreeDraft cancelled), Is.True);
            TechNodeDef node = cancelled.GetNodes(TechTreeTier.L1).First(n => cancelled.CanUnlock(n.id));
            Assert.That(cancelled.TryToggleOrUnlock(node.id), Is.True);
            Assert.That(cancelled.RpBalance, Is.EqualTo(25000 - node.rpCost));
            // Discard this edited draft and reopen: cancellation has no repository call.
            Assert.That(store.Json, Is.EqualTo(summerToken));
            Assert.That(summer.ActiveTechSnapshot.UnlockedNodeIds, Is.Empty);
            Assert.That(CareerTechTreeDraft.TryCreate(summer.ActiveTechSnapshot, database,
                out CareerTechTreeDraft draft), Is.True);
            Assert.That(draft.RpBalance, Is.EqualTo(25000));
            Assert.That(draft.IsUnlocked(node.id), Is.False);
            Assert.That(draft.TryToggleOrUnlock(node.id), Is.True);
            Assert.That(draft.TryBuildSnapshot(out CareerTechSnapshot updated), Is.True);
            Assert.That(updated.ActiveNodeIds, Is.EqualTo(new[] { node.id }));
            Assert.That(updated.UnlockedNodeIds, Is.EqualTo(new[] { node.id }));
            Assert.That(launch.TechSnapshot.RpBalance, Is.EqualTo(25000));
            Assert.That(launch.TechSnapshot.ActiveNodeIds, Is.Empty, "The running race's snapshot is immutable.");
            Assert.That(service.TryConfirmSummerBreak(CareerTechSnapshot.CreateEmpty(field[1])), Is.False);
            Assert.That(store.WriteAttempts, Is.EqualTo(savedAttempts), "Wrong-team confirmation fails before save.");

            // The fourth-race write evidence was already checked. Subsequent writes
            // belong to the summer application service, not result settlement.
            store.BeforeResultWrite = null;
            store.FailWrites = true;
            Assert.That(service.TryConfirmSummerBreak(updated), Is.False);
            Assert.That(service.CurrentState, Is.SameAs(summer), "Failed candidate cannot replace current authority.");
            Assert.That(store.Json, Is.EqualTo(summerToken));
            Assert.That(store.SuccessfulWrites, Is.EqualTo(savedWrites));
            Assert.That(store.WriteAttempts, Is.EqualTo(savedAttempts + 1));
            Assert.That(CareerModeRules.CanAdjustTechTree(service.CurrentState), Is.True);
            Assert.That(CareerRaceLaunchRequest.TryCreate(repository.Load().State, "failed-fifth", out _), Is.False);

            store.FailWrites = false;
            Assert.That(service.TryConfirmSummerBreak(updated), Is.True);
            CareerLoadResult loaded = repository.Load();
            Assert.That(loaded.Status, Is.EqualTo(CareerLoadStatus.Loaded));
            CareerSeasonState resumed = loaded.State;
            Assert.That(resumed.Phase, Is.EqualTo(CareerPhase.Racing));
            Assert.That(resumed.NextTrackIndex, Is.EqualTo(4));
            Assert.That(resumed.RaceResults.Count, Is.EqualTo(4));
            Assert.That(resumed.SummerBreakUsed, Is.True);
            Assert.That(resumed.LockedTeam, Is.EqualTo(field[0]));
            Assert.That(resumed.Competitors, Is.EqualTo(field));
            Assert.That(CareerModeRules.CanAdjustTechTree(resumed), Is.False);
            Assert.That(CareerModeRules.CanStartNextRace(resumed), Is.True);
            Assert.That(resumed.InitialTechSnapshot.RpBalance, Is.EqualTo(25000));
            Assert.That(resumed.InitialTechSnapshot.UnlockedNodeIds, Is.Empty);
            Assert.That(resumed.ActiveTechSnapshot.RpBalance, Is.EqualTo(25000 - node.rpCost));
            Assert.That(resumed.SummerBreakTechSnapshot.ActiveNodeIds, Is.EqualTo(updated.ActiveNodeIds));
            Assert.That(resumed.ActiveTechSnapshot.UnlockedNodeIds, Is.EqualTo(updated.UnlockedNodeIds));
            Assert.That(CareerModeRules.GetStandings(resumed)
                .Select(s => Tuple.Create(s.TeamId, s.Points)), Is.EqualTo(standings));
            Assert.That(store.SuccessfulWrites, Is.EqualTo(savedWrites + 1));
            Assert.That(store.WriteAttempts, Is.EqualTo(savedAttempts + 2));
            string resumedToken = store.Json;
            Assert.That(service.TryConfirmSummerBreak(updated), Is.False);
            Assert.That(store.Json, Is.EqualTo(resumedToken));
            Assert.That(store.WriteAttempts, Is.EqualTo(savedAttempts + 2), "Duplicate confirmation must not save.");
            Assert.That(CareerRaceLaunchState.Request(resumed, "native-fifth-" + Guid.NewGuid().ToString("N")), Is.True);
            CareerRaceLaunchRequest next = CareerRaceLaunchState.Current;
            Assert.That(next.RaceIndex, Is.EqualTo(4));
            Assert.That(next.TrackId, Is.EqualTo("shanghai_dim_sum"));
            Assert.That(next.PlayerTeam, Is.EqualTo(field[0]));
            Assert.That(next.Competitors, Is.EqualTo(field));
            Assert.That(next.TechSnapshot.RpBalance, Is.EqualTo(25000 - node.rpCost));
            Assert.That(next.TechSnapshot.ActiveNodeIds, Is.EqualTo(updated.ActiveNodeIds));
            Assert.That(next.Matches(resumed), Is.True);
            Assert.That(launch.Matches(resumed), Is.False);
            resultEntry.Invoke(manager, null); // A late fourth callback cannot roll back fifth-race authority.
            Assert.That(store.Json, Is.EqualTo(resumedToken));
            Assert.That(store.WriteAttempts, Is.EqualTo(savedAttempts + 2));
            Assert.That(RepositoryRequests, Is.EqualTo(2));
            Assert.That(manager.Session.Players.Select(p => Tuple.Create(p.position, p.lap,
                p.finishOrder, p.isBlown, p.hasFinished, p.techState.rpBalance, p.driverXp)), Is.EqualTo(terminal));
            Assert.That(File.ReadAllText(logPath), Is.EqualTo(closedTrace), "Summer changes cannot rewrite the closed race log.");
            TestContext.WriteLine("Native fourth-race checkpoint: " + field[0] + "; summer draft=" + node.id
                + "; cancel=no-save; failed confirmation=no-advance; retry=one-save; fifth=shanghai_dim_sum; late result=no-write.");
        }
    }
}
