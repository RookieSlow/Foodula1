using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Native fifth-race startup and completion consume a confirmed summer snapshot.
/// Earlier classifications and summer confirmation are production-service setup,
/// not evidence of four native races or rendered summer UI. Storage is isolated.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator RealCareerFifthRaceConsumesSummerTechnologyAndCompletesOnce()
    {
        foreach (TeamId team in FreeRaceRosterRules.AvailableTeams)
            yield return CompleteAuthoredRace(team, 0, false, true,
                CareerCompletionFault.None, 4);
        yield return CompleteAuthoredRace(TeamId.CN, 0, true, true,
            CareerCompletionFault.None, 4);
    }

    private static string FifthHeatNode(TeamId team) => team == TeamId.CN
        ? "cn-ev-l1-heat-pump" : "common-l1-heat-coating";

    private static void PrepareFifthCareerSnapshot(CareerModeService service, TechTreeDatabase database)
    {
        CareerSeasonState summer = service.CurrentState;
        Assert.That(summer.Phase, Is.EqualTo(CareerPhase.SummerBreak));
        Assert.That(summer.RaceResults.Count, Is.EqualTo(4));
        Assert.That(CareerModeRules.CanStartNextRace(summer), Is.False);
        Assert.That(CareerTechTreeDraft.TryCreate(summer.ActiveTechSnapshot, database,
            out CareerTechTreeDraft draft), Is.True);
        Assert.That(draft.TryToggleOrUnlock(FifthHeatNode(summer.LockedTeam)), Is.True);
        Assert.That(draft.TryBuildSnapshot(out CareerTechSnapshot updated), Is.True);
        Assert.That(service.TryConfirmSummerBreak(updated), Is.True);
        Assert.That(service.CurrentState.SummerBreakUsed, Is.True);
        Assert.That(service.CurrentState.ActiveTechSnapshot.RpBalance, Is.EqualTo(22500));
        Assert.That(service.CurrentState.InitialTechSnapshot.RpBalance, Is.EqualTo(25000));
        Assert.That(service.CurrentState.InitialTechSnapshot.ActiveNodeIds, Is.Empty);
    }

    private sealed partial class CareerCompletionContext
    {
        internal void AssertFifthStartup(MVPGameManager manager)
        {
            if (launch.RaceIndex != 4) return;
            Assert.That(launch.TrackId, Is.EqualTo("shanghai_dim_sum"));
            Assert.That(manager.trackManager.TotalNodes, Is.EqualTo(62));
            Assert.That(launch.TechSnapshot.RpBalance, Is.EqualTo(22500));
            Assert.That(launch.TechSnapshot.ActiveNodeIds, Is.EqualTo(new[] { FifthHeatNode(field[0]) }));
            Assert.That(manager.Session.GetModifiers(manager.Session.Human).heatReductionPerLap, Is.EqualTo(1),
                "Real startup must prepare the summer technology's race effect, not just copy RP.");
            CareerSeasonState state = repository.Load().State;
            Assert.That(state.SummerBreakUsed, Is.True);
            Assert.That(CareerModeRules.CanAdjustTechTree(state), Is.False);
            Assert.That(launch.Matches(state), Is.True);
            Assert.That(state.InitialTechSnapshot.ActiveNodeIds, Is.Empty);
            Assert.That(state.SummerBreakTechSnapshot.ActiveNodeIds, Is.EqualTo(launch.TechSnapshot.ActiveNodeIds));
        }

        internal void AssertFifthCompletion(MVPGameManager manager, string logPath, string closedTrace)
        {
            if (launch.RaceIndex != 4) return;
            Assert.That(closedTrace, Does.Contain("race=5/8").And.Contain("phase=Racing"));
            CareerSeasonState completed = repository.Load().State;
            Assert.That(completed.NextTrackIndex, Is.EqualTo(5));
            Assert.That(completed.RaceResults.Count, Is.EqualTo(5));
            Assert.That(completed.InitialTechSnapshot.RpBalance, Is.EqualTo(25000));
            Assert.That(completed.InitialTechSnapshot.UnlockedNodeIds, Is.Empty);
            Assert.That(completed.SummerBreakTechSnapshot.RpBalance, Is.EqualTo(22500));
            Assert.That(completed.SummerBreakTechSnapshot.ActiveNodeIds, Is.EqualTo(new[] { FifthHeatNode(field[0]) }));
            Assert.That(manager.Session.Human.techState.activeNodeIds,
                Is.EquivalentTo(completed.ActiveTechSnapshot.ActiveNodeIds));
            Assert.That(manager.Session.GetModifiers(manager.Session.Human).heatReductionPerLap, Is.EqualTo(1));
            var service = new CareerModeService(repository);
            string token = store.Json;
            int attempts = store.WriteAttempts;
            Assert.That(service.TryConfirmSummerBreak(launch.TechSnapshot), Is.False,
                "Finishing race five cannot reopen the summer window.");
            Assert.That(store.Json, Is.EqualTo(token));
            Assert.That(store.WriteAttempts, Is.EqualTo(attempts));
            Assert.That(CareerRaceLaunchState.Request(completed,
                "native-sixth-" + Guid.NewGuid().ToString("N")), Is.True);
            CareerRaceLaunchRequest next = CareerRaceLaunchState.Current;
            Assert.That(next.RaceIndex, Is.EqualTo(5));
            Assert.That(next.TrackId, Is.EqualTo("suzuka_sushi"));
            Assert.That(next.PlayerTeam, Is.EqualTo(field[0]));
            Assert.That(next.Competitors, Is.EqualTo(field));
            Assert.That(next.TechSnapshot.RpBalance, Is.EqualTo(22500));
            Assert.That(next.TechSnapshot.UnlockedNodeIds, Is.EqualTo(launch.TechSnapshot.UnlockedNodeIds));
            Assert.That(next.TechSnapshot.ActiveNodeIds, Is.EqualTo(launch.TechSnapshot.ActiveNodeIds));
            Assert.That(next.Matches(completed), Is.True);
            Assert.That(launch.Matches(completed), Is.False);
            var terminal = manager.Session.Players.Select(p => Tuple.Create(p.position, p.lap,
                p.finishOrder, p.isBlown, p.hasFinished, p.techState.rpBalance, p.driverXp)).ToArray();
            var resultEntry = typeof(MVPGameManager).GetMethod("ShowGameOver", PrivateInstance);
            for (int repeat = 0; repeat < 2; repeat++) resultEntry.Invoke(manager, null);
            Assert.That(store.Json, Is.EqualTo(token));
            Assert.That(store.WriteAttempts, Is.EqualTo(attempts));
            AssertSettledOnce();
            Assert.That(CareerRaceLaunchState.Current, Is.SameAs(next));
            Assert.That(manager.Session.Players.Select(p => Tuple.Create(p.position, p.lap,
                p.finishOrder, p.isBlown, p.hasFinished, p.techState.rpBalance, p.driverXp)), Is.EqualTo(terminal));
            Assert.That(File.ReadAllText(logPath), Is.EqualTo(closedTrace));
            TestContext.WriteLine("Native fifth-race completion: " + field[0] + "; active=" + FifthHeatNode(field[0])
                + "; rp=22500; heatReduction=1; summer=locked; result=one-save; repeats=no-write; sixth=suzuka_sushi.");
        }
    }
}
