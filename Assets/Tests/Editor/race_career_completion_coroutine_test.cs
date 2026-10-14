using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Genuine career Start -> native authored-track completion -> JSON repository.
/// Reuses the ordinary full-race driver; only the launch and result expectations
/// differ. Controlled decks/headless presentation are not visual or real-save acceptance.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator RealCareerStartupRunsAuthoredTrackToFinishAndIsolatedSettlement()
    {
        foreach (TeamId team in FreeRaceRosterRules.AvailableTeams)
            yield return CompleteAuthoredRace(team, 0, false, true);
        yield return CompleteAuthoredRace(TeamId.UK, 0, true, true);
    }

    private CareerCompletionContext PrepareCareerCompletion(TeamId team, List<string> reads,
        CareerCompletionFault fault = CareerCompletionFault.None, int raceIndex = 0)
    {
        Assert.That(raceIndex, Is.EqualTo(0).Or.EqualTo(3).Or.EqualTo(4).Or.EqualTo(7),
            "This fixture covers first race, summer checkpoint, fifth race or finale only.");
        if (raceIndex != 0) Assert.That(fault, Is.EqualTo(CareerCompletionFault.None));
        TeamId[] field = new[] { team }.Concat(FreeRaceRosterRules.AvailableTeams
            .Where(t => t != team).Take(3)).ToArray();
        var store = new NativeCareerStore();
        TechTreeDatabase database = TechTreeDatabaseFactory.CreateDefault();
        var repository = new CareerRepository(store, new JsonUtilityCareerSerializer(),
            state => CareerTechSnapshotMapper.IsSeasonValidForDatabase(state, database));
        Assert.That(CareerTechSnapshotMapper.TryCapture(new TechTreeState(team, 25000),
            out CareerTechSnapshot snapshot), Is.True);
        var service = new CareerModeService(repository);
        Assert.That(service.TryCreateNew(team, field, snapshot, false), Is.True);
        // Prior races are valid service-generated setup, not claimed native completion.
        for (int race = 0; race < raceIndex; race++)
        {
            CareerRaceResult history = raceIndex == 7
                ? SeedFinaleHistory(service.CurrentState, field)
                : SeedCareerGateResult(service.CurrentState, field);
            Assert.That(service.TryRecordRace(history), Is.True);
            if (race == 3 && raceIndex >= 4) PrepareFifthCareerSnapshot(service, database);
        }
        Assert.That(CareerRaceLaunchState.Request(service.CurrentState,
            "native-career-" + Guid.NewGuid().ToString("N")), Is.True);
        CareerRaceLaunchRequest launch = CareerRaceLaunchState.Current;
        if (raceIndex != 0)
        {
            TrackConfig loaded = TrackDataLoader.LoadConfig(launch.TrackId);
            Assert.That(loaded, Is.Not.Null);
            typeof(TrackManager).GetProperty("LoadedTrackConfig").SetValue(manager.trackManager, loaded);
            Set(manager.trackManager, "nodes", TrackDataLoader.ConfigToNodes(loaded));
        }
        Assert.That(launch.TrackId, Is.EqualTo(manager.trackManager.LoadedTrackConfig.trackId));
        Assert.That(RaceModeLaunchResolver.ResolveTrackId("fallback_42"), Is.EqualTo(launch.TrackId));

        // Deliberately conflicting free-race/config selections must not override
        // the locked career field, default team driver or captured technology.
        config.playerTeam = field[1];
        config.playerDriverId = DriverCatalog.GetDefaultForTeam(field[1]).Id;
        config.aiTeams = new[] { team };
        config.aiOpponentCount = 1;
        FreeRaceRosterState.InitializeThunderstorm(DriverCatalog.GetDefaultForTeam(field[1]));
        Assert.That(FreeRaceRosterState.IsConfigured, Is.True);

        var context = new CareerCompletionContext(store, repository, launch, field);
        context.ConfigureFault(fault);
        Set(manager, "createRaceCareerRepository", (Func<CareerRepository>)(() =>
        {
            context.RepositoryRequests++;
            if (context.RepositoryRequests == 1)
            {
                Assert.That(manager.Session, Is.Null, "Validate the authoritative save before assembly.");
                reads.Add("CAREER");
            }
            else
            {
                context.AssertResultRepositoryEntry(manager);
            }
            return repository;
        }));
        // Record then assert outside Repository.Save's catch boundary: a failed
        // persistence test must not swallow a fixture AssertionException as its fault.
        store.BeforeResultWrite = () => context.ObserveResultWrite(manager);
        return context;
    }

    private sealed partial class CareerCompletionContext
    {
        private readonly NativeCareerStore store;
        private readonly CareerRepository repository;
        private readonly CareerRaceLaunchRequest launch;
        private readonly TeamId[] field;
        private readonly int writesBeforeRace;
        private readonly int attemptsBeforeRace;
        private readonly Dictionary<TeamId, int> pointsBeforeRace;
        internal int RepositoryRequests;

        internal CareerCompletionContext(NativeCareerStore store, CareerRepository repository,
            CareerRaceLaunchRequest launch, TeamId[] field)
        {
            this.store = store;
            this.repository = repository;
            this.launch = launch;
            this.field = field;
            writesBeforeRace = store.SuccessfulWrites;
            attemptsBeforeRace = store.WriteAttempts;
            pointsBeforeRace = CareerModeRules.GetStandings(repository.Load().State)
                .ToDictionary(s => s.TeamId, s => s.Points);
        }

        internal void AssertStartup(MVPGameManager manager, List<string> reads)
        {
            Assert.That(manager.IsCareerMode, Is.True);
            Assert.That(manager.IsTutorialMode, Is.False);
            Assert.That(CareerRaceLaunchState.IsActive, Is.True);
            Assert.That(CareerRaceLaunchState.IsRequested, Is.False);
            Assert.That(CareerRaceLaunchState.Current, Is.SameAs(launch));
            Assert.That(manager.Session.Players.Select(p => p.teamId), Is.EqualTo(field));
            Assert.That(manager.Session.Human.driverId, Is.EqualTo(DriverCatalog.GetDefaultForTeam(field[0]).Id));
            Assert.That(reads, Is.EqualTo(new[] { "DISPLAY", "CAREER", "XP:" + manager.Session.Human.driverId }),
                "Career must not read the free-race technology profile.");
            Assert.That(manager.Session.Human.techState.rpBalance, Is.EqualTo(launch.TechSnapshot.RpBalance));
            Assert.That(manager.Session.Human.techState.unlockedNodeIds, Is.EquivalentTo(launch.TechSnapshot.UnlockedNodeIds));
            Assert.That(manager.Session.Human.techState.activeNodeIds, Is.EquivalentTo(launch.TechSnapshot.ActiveNodeIds));
            Assert.That(manager.Session.Human.techState.sunNeverSetsTarget, Is.EqualTo(launch.TechSnapshot.SunNeverSetsTarget));
            AssertFifthStartup(manager);
            AssertFinaleStartup(manager);
            AssertNotSettled();
        }

        internal void AssertNotSettled()
        {
            Assert.That(store.SuccessfulWrites, Is.EqualTo(writesBeforeRace), "Only setup classifications have been saved.");
            Assert.That(store.WriteAttempts, Is.EqualTo(attemptsBeforeRace), "No attempted result save before completion.");
            Assert.That(RepositoryRequests, Is.EqualTo(1), "No premature result repository access.");
        }

        internal void AssertSettlement(MVPGameManager manager, bool retiredHuman)
        {
            AssertSettledOnce();
            if (HasInjectedFault) AssertRejected(manager);
            else AssertSavedState(manager, retiredHuman);
        }

        private void AssertSavedState(MVPGameManager manager, bool retiredHuman)
        {
            CareerLoadResult loaded = repository.Load();
            Assert.That(loaded.Status, Is.EqualTo(CareerLoadStatus.Loaded), "Production JSON must round-trip.");
            CareerSeasonState state = loaded.State;
            Assert.That(state.LockedTeam, Is.EqualTo(field[0]));
            Assert.That(state.Competitors, Is.EqualTo(field));
            Assert.That(state.NextTrackIndex, Is.EqualTo(launch.RaceIndex + 1));
            Assert.That(state.Phase, Is.EqualTo(launch.RaceIndex == 7 ? CareerPhase.Completed :
                launch.RaceIndex == 3 ? CareerPhase.SummerBreak : CareerPhase.Racing));
            Assert.That(state.RaceResults.Count, Is.EqualTo(launch.RaceIndex + 1));
            CareerRaceResult result = state.RaceResults[launch.RaceIndex];
            Assert.That(result.ResultId, Is.EqualTo(launch.ResultId));
            Assert.That(result.TrackId, Is.EqualTo(launch.TrackId));
            Assert.That(state.ActiveTechSnapshot.RpBalance, Is.EqualTo(launch.TechSnapshot.RpBalance));
            Assert.That(state.ActiveTechSnapshot.UnlockedNodeIds, Is.EqualTo(launch.TechSnapshot.UnlockedNodeIds));
            Assert.That(state.ActiveTechSnapshot.ActiveNodeIds, Is.EqualTo(launch.TechSnapshot.ActiveNodeIds));
            Assert.That(state.InitialTechSnapshot.RpBalance, Is.EqualTo(25000));
            Assert.That(state.InitialTechSnapshot.UnlockedNodeIds, Is.Empty);
            Assert.That(state.SummerBreakUsed, Is.EqualTo(launch.RaceIndex >= 4));
            List<CareerStanding> standings = CareerModeRules.GetStandings(state);
            foreach (PlayerState player in manager.Session.Players)
            {
                CareerCompetitorResult entry = result.Standings.Single(s => s.TeamId == player.teamId);
                Assert.That(entry.DidNotFinish, Is.EqualTo(player.isBlown));
                Assert.That(entry.FinishPosition, Is.EqualTo(player.isBlown ? 0 : player.finishOrder));
                int expectedPoints = player.isBlown ? 0 : new[] { 10, 6, 4, 2 }[player.finishOrder - 1];
                Assert.That(standings.Single(s => s.TeamId == player.teamId).Points,
                    Is.EqualTo(pointsBeforeRace[player.teamId] + expectedPoints));
            }
            if (retiredHuman)
                Assert.That(standings.Single(s => s.TeamId == field[0]).Points, Is.EqualTo(pointsBeforeRace[field[0]]));
            Assert.That(manager.hudUI.gameOverText.text,
                Does.Contain("生涯赛果已保存：总分").And.Not.Contain("RP 奖励").And.Not.Contain("车手 XP"));
            Assert.That(CareerModeRules.GetNextTrackId(state),
                Is.EqualTo(launch.RaceIndex == 7 ? string.Empty : launch.RaceIndex == 4 ? "suzuka_sushi" :
                    launch.RaceIndex == 3 ? "shanghai_dim_sum" : "nurburgring_bier"));
        }

        internal void AssertLog(string contents, RaceLogAnalysisResult analysis)
        {
            Assert.That(analysis.Mode, Is.EqualTo(RaceLogMode.Career));
            Assert.That(contents, Does.Contain("[CAREER_SETUP] result_id=" + launch.ResultId + " race=" + (launch.RaceIndex + 1) + "/8")
                .And.Not.Contain("[FREE_RACE_SETUP]").And.Not.Contain("[TUTORIAL_SETUP]"));
            string marker = "[CAREER_RESULT] status=" + (HasInjectedFault ? "rejected" : "saved") +
                " result_id=" + launch.ResultId;
            Assert.That(contents.Split(new[] { marker }, StringSplitOptions.None).Length - 1, Is.EqualTo(1));
            Assert.That(contents.Split(new[] { "[CAREER_RESULT]" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1));
            if (HasInjectedFault)
                Assert.That(contents, Does.Contain("reason=" + RejectionReason).And.Not.Contain("status=saved")
                    .And.Not.Contain("status=already_saved"));
            Assert.That(contents.IndexOf(marker, StringComparison.Ordinal),
                Is.LessThan(contents.IndexOf("[RACE_TERMINATION]", StringComparison.Ordinal)));
        }

        internal void AssertSettledOnce()
        {
            Assert.That(store.SuccessfulWrites, Is.EqualTo(writesBeforeRace + (HasInjectedFault ? 0 : 1)),
                "Only accepted results advance the stored season.");
            Assert.That(RepositoryRequests, Is.EqualTo(2), "No repeated result access after completion.");
            AssertFirstWriteEvidence();
        }
    }

    private sealed class NativeCareerStore : ICareerKeyValueStore
    {
        private string json;
        internal int SuccessfulWrites;
        internal int WriteAttempts;
        internal bool FailWrites;
        internal bool ThrowOnWrite;
        internal string Json => json;
        internal Action BeforeResultWrite;
        internal void ReplaceExternalValue(string value) => json = value;
        public bool HasKey(string key) { AssertCareerKey(key); return json != null; }
        public string GetString(string key) { AssertCareerKey(key); return json ?? string.Empty; }
        public bool TrySetAndSave(string key, string value)
        {
            AssertCareerKey(key);
            WriteAttempts++;
            BeforeResultWrite?.Invoke();
            if (ThrowOnWrite) throw new InvalidOperationException("Isolated career persistence failure.");
            if (FailWrites) return false;
            json = value;
            SuccessfulWrites++;
            return true;
        }
        public bool TryDeleteAndSave(string key)
        {
            Assert.Fail("Native completion must not delete progress.");
            return false;
        }
        private static void AssertCareerKey(string key) => Assert.That(key, Is.EqualTo(CareerRepository.SaveKey));
    }
}
