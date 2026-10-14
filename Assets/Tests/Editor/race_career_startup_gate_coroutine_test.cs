using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Native Unity Start rejects a formerly valid launch after authoritative progress
/// reaches summer break or season completion. Earlier classifications are seeded
/// through the production service; this is not an eight-race/visual/storage acceptance run.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator RealCareerStartRejectsSummerBreakAndCompletedAuthorityBeforeAssembly()
    {
        foreach (TeamId team in FreeRaceRosterRules.AvailableTeams)
        foreach (CareerPhase phase in new[] { CareerPhase.SummerBreak, CareerPhase.Completed })
            yield return RejectCareerStartAfterAuthorityAdvance(team, phase);
    }

    private IEnumerator RejectCareerStartAfterAuthorityAdvance(TeamId team, CareerPhase phase)
    {
        UnityEngine.Random.State savedRandom = UnityEngine.Random.state;
        using (var scope = new RaceStartupTestScope())
        try
        {
            CreateStartupRace(0, out List<string> reads);
            ConfigureHeadlessResultControls();
            TeamId[] field = new[] { team }.Concat(FreeRaceRosterRules.AvailableTeams
                .Where(t => t != team).Take(3)).ToArray();
            var store = new NativeCareerStore();
            TechTreeDatabase database = TechTreeDatabaseFactory.CreateDefault();
            var repository = new CareerRepository(store, new JsonUtilityCareerSerializer(),
                state => CareerTechSnapshotMapper.IsSeasonValidForDatabase(state, database));
            var service = new CareerModeService(repository);
            CareerTechSnapshot snapshot = CareerTechSnapshot.CreateEmpty(team);
            Assert.That(service.TryCreateNew(team, field, snapshot, false), Is.True);
            int completedRaces = phase == CareerPhase.SummerBreak ? 4 : CareerModeRules.RaceCount;
            while (service.CurrentState.NextTrackIndex < completedRaces - 1)
            {
                if (service.CurrentState.Phase == CareerPhase.SummerBreak)
                    Assert.That(service.TryConfirmSummerBreak(snapshot), Is.True);
                Assert.That(service.TryRecordRace(SeedCareerGateResult(service.CurrentState, field)), Is.True);
            }

            // Queue a legal request, load precisely its official track, then let
            // another authoritative result invalidate it before Unity invokes Start.
            Assert.That(CareerRaceLaunchState.Request(service.CurrentState,
                "native-career-gate-" + Guid.NewGuid().ToString("N")), Is.True);
            CareerRaceLaunchRequest launch = CareerRaceLaunchState.Current;
            Assert.That(launch.Matches(repository.Load().State), Is.True);
            TrackConfig track = TrackDataLoader.LoadConfig(launch.TrackId);
            Assert.That(track, Is.Not.Null);
            typeof(TrackManager).GetProperty("LoadedTrackConfig").SetValue(manager.trackManager, track);
            Set(manager.trackManager, "nodes", TrackDataLoader.ConfigToNodes(track));
            Assert.That(manager.trackManager.TrackId, Is.EqualTo(launch.TrackId));
            Assert.That(service.TryRecordRace(SeedCareerGateResult(service.CurrentState, field)), Is.True);
            CareerLoadResult authority = repository.Load();
            Assert.That(authority.Status, Is.EqualTo(CareerLoadStatus.Loaded));
            Assert.That(authority.State.Phase, Is.EqualTo(phase));
            Assert.That(authority.State.NextTrackIndex, Is.EqualTo(completedRaces));
            Assert.That(CareerModeRules.CanStartNextRace(authority.State), Is.False);
            Assert.That(launch.Matches(authority.State), Is.False);
            string token = store.Json;
            int savedWrites = store.SuccessfulWrites;
            int savedAttempts = store.WriteAttempts;
            var standings = CareerModeRules.GetStandings(authority.State)
                .Select(s => Tuple.Create(s.TeamId, s.Points)).ToArray();

            // Configured free race remains present but may not become a fallback.
            config.playerTeam = field[1];
            config.playerDriverId = DriverCatalog.GetDefaultForTeam(field[1]).Id;
            FreeRaceRosterState.InitializeThunderstorm(DriverCatalog.GetDefaultForTeam(field[1]));
            string[] freeDrivers = FreeRaceRosterState.SelectedTeams
                .Select(FreeRaceRosterState.GetDriverId).ToArray();
            int repositoryReads = 0;
            Set(manager, "createRaceCareerRepository", (Func<CareerRepository>)(() =>
            {
                repositoryReads++;
                reads.Add("CAREER");
                Assert.That(manager.Session, Is.Null, "Authority must be checked before participant creation.");
                return repository;
            }));
            const string reason = "启动请求与当前生涯存档不一致";
            LogAssert.Expect(LogType.Error,
                "[CAREER] 生涯比赛无法启动：" + reason + "\n进度未改变，请返回主菜单重试。");
            manager.enabled = true; // Actual Unity Start, not reflection InitializeGame.
            yield return WaitFor(() => !string.IsNullOrEmpty(Get<string>(manager,
                "careerInitializationFailure")), "career blocked Start " + team + "/" + phase);
            for (int frame = 0; frame < 4; frame++) yield return null;

            Assert.That(Get<string>(manager, "careerInitializationFailure"), Is.EqualTo(reason));
            Assert.That(manager.IsCareerMode, Is.True);
            Assert.That(manager.IsTutorialMode, Is.False);
            Assert.That(CareerRaceLaunchState.Current, Is.SameAs(launch));
            Assert.That(CareerRaceLaunchState.IsActive, Is.True);
            Assert.That(CareerRaceLaunchState.IsRequested, Is.False);
            Assert.That(manager.Session, Is.Null);
            Assert.That(Get<Dictionary<PlayerState, AIController>>(manager, "aiControllers"), Is.Empty);
            Assert.That(host.GetComponents<AIController>(), Is.Empty);
            Assert.That(Get<List<GameObject>>(manager, "carInstances"), Is.Empty);
            Assert.That(manager.LastRaceLogPath, Is.Null, "Rejected startup must not open a race log.");
            Assert.That(Turn, Is.Zero, "No GameLoop should run after the rejected Start.");
            Assert.That(manager.TutorialInputPhase, Is.EqualTo("none"));
            Assert.That(reads, Is.EqualTo(new[] { "DISPLAY", "CAREER" }),
                "No normal technology profile, driver XP or second authority read after rejection.");
            Assert.That(repositoryReads, Is.EqualTo(1));
            Assert.That(manager.hudUI.gameOverPanel.activeSelf, Is.True);
            Assert.That(manager.hudUI.gameOverText.text, Does.Contain(reason).And.Contain("进度未改变")
                .And.Not.Contain("赛果已保存").And.Not.Contain("RP 奖励").And.Not.Contain("车手 XP"));
            Assert.That(store.Json, Is.EqualTo(token));
            Assert.That(store.SuccessfulWrites, Is.EqualTo(savedWrites));
            Assert.That(store.WriteAttempts, Is.EqualTo(savedAttempts));
            CareerLoadResult after = repository.Load();
            Assert.That(after.Status, Is.EqualTo(CareerLoadStatus.Loaded));
            Assert.That(after.State.Phase, Is.EqualTo(phase));
            Assert.That(after.State.NextTrackIndex, Is.EqualTo(completedRaces));
            Assert.That(after.State.RaceResults.Count, Is.EqualTo(completedRaces));
            Assert.That(CareerModeRules.GetStandings(after.State)
                .Select(s => Tuple.Create(s.TeamId, s.Points)), Is.EqualTo(standings));
            Assert.That(FreeRaceRosterState.IsThunderstorm, Is.True);
            Assert.That(FreeRaceRosterState.SelectedTeams.Select(FreeRaceRosterState.GetDriverId),
                Is.EqualTo(freeDrivers));
            TestContext.WriteLine("Native Start blocked before assembly: " + team + " / " + phase
                + " / " + launch.TrackId + "; authority and free-race selections preserved.");
        }
        finally
        {
            DestroyRace();
            UnityEngine.Random.state = savedRandom;
        }
        yield return null;
    }

    private static CareerRaceResult SeedCareerGateResult(CareerSeasonState state, TeamId[] field)
        => new CareerRaceResult("gate-seed-" + state.NextTrackIndex,
            CareerModeRules.GetNextTrackId(state), field.Select((team, index) =>
                new CareerCompetitorResult(team, index + 1)).ToArray());
}
