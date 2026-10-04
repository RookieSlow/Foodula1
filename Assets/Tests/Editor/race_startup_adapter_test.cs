using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Actual InitializeGame on inactive objects; no Start, frame scheduling or player I/O.</summary>
public sealed partial class RaceResultSettlementAdapterTests
{
    [Test]
    public void StartupDependenciesRetainTheirOriginalProductionDefaults()
    {
        Assert.That(GetField<Action>("prepareRaceDisplay").Method,
            Is.EqualTo(typeof(GameSettingsRuntime).GetMethod("EnsureLoadedAndApplyDisplay")));
        Assert.That(GetField<Func<string, int>>("loadRaceDriverXp").Method,
            Is.EqualTo(typeof(DriverProgressStore).GetMethod("Load")));
        Assert.That(GetField<Func<TeamId, TechTreeDatabase, TechTreeState>>("loadRaceTechProfile").Method,
            Is.EqualTo(typeof(TechTreeProfileStore).GetMethod("GetOrCreate")));
        Assert.That(GetField<Func<CareerRepository>>("createRaceCareerRepository").Method,
            Is.EqualTo(typeof(CareerRuntimeRepository).GetMethod("CreateDefault")));
    }

    [Test]
    public void StartupScopeRestoresPopulatedRosterDriverAndExactLaunchIdentities()
    {
        using (var original = new RaceStartupTestScope(true))
        {
            var tutorial = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.CN);
            TutorialLaunchState.Request(tutorial);
            CareerRepository repository = CreateSeason(TeamId.US, out _);
            Assert.That(CareerRaceLaunchState.Request(repository.Load().State, "scope-restore"), Is.True);
            CareerRaceLaunchRequest career = CareerRaceLaunchState.ActivateRequested();
            DriverProfile selected = DriverCatalog.GetDefaultForTeam(TeamId.JP);
            Assert.That(DriverSelectionState.TrySelect(selected.Id), Is.True);
            FreeRaceRosterState.InitializeThunderstorm(selected);
            TeamId[] teams = FreeRaceRosterState.SelectedTeams.ToArray();
            string[] drivers = teams.Select(FreeRaceRosterState.GetDriverId).ToArray();
            using (var temporary = new RaceStartupTestScope())
            {
                Assert.That(TutorialLaunchState.Scenario, Is.Null);
                Assert.That(CareerRaceLaunchState.Current, Is.Null);
                FreeRaceRosterState.InitializeDefault(DriverCatalog.GetDefaultForTeam(TeamId.DE), Teams);
            }
            Assert.That(TutorialLaunchState.Scenario, Is.SameAs(tutorial));
            Assert.That(TutorialLaunchState.IsRequested, Is.True);
            Assert.That(TutorialLaunchState.IsActive, Is.False);
            Assert.That(CareerRaceLaunchState.Current, Is.SameAs(career));
            Assert.That(CareerRaceLaunchState.IsActive, Is.True);
            Assert.That(CareerRaceLaunchState.IsRequested, Is.False);
            Assert.That(DriverSelectionState.SelectedDriverId, Is.EqualTo(selected.Id));
            Assert.That(FreeRaceRosterState.IsThunderstorm, Is.True);
            Assert.That(FreeRaceRosterState.SelectedTeams, Is.EqualTo(teams));
            Assert.That(teams.Select(FreeRaceRosterState.GetDriverId), Is.EqualTo(drivers));
            Assert.That(FreeRaceRosterState.TryBuildRoster(out FreeRaceRosterEntry[] roster, out _), Is.True);
            Assert.That(roster[0].DriverId, Is.EqualTo(selected.Id));
        }
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void InitializeGameWithTechnologyDisabledNeverReadsProfiles(TeamId team)
    {
        using (var scope = new RaceStartupTestScope(true))
        {
            config.playerTeam = team;
            config.playerDriverId = DriverCatalog.GetDefaultForTeam(team).Id;
            config.aiOpponentCount = 3;
            config.aiTeams = Teams.Where(t => t != team).Take(3).ToArray();
            config.enableTechTree = false;
            SetField("loadRaceDriverXp", (Func<string, int>)(_ => 137));
            SetField("loadRaceTechProfile", (Func<TeamId, TechTreeDatabase, TechTreeState>)((_, __) =>
                throw new InvalidOperationException("Disabled technology must not load a profile.")));
            SetField("createRaceCareerRepository", (Func<CareerRepository>)(() =>
                throw new InvalidOperationException("Normal race must not load career.")));
            InitializeWithTemporaryLog("silverstone_afternoon_tea");
            AssertStartupField(team, 4, 137);
            Assert.That(session.Players.All(p => p.techState == null), Is.True);
        }
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void InitializeGameAssemblesNormalFieldWithOnlyHumanProfileAndXpReads(TeamId team)
    {
        using (var scope = new RaceStartupTestScope(true))
        {
            var calls = new List<string>();
            config.playerTeam = team;
            config.playerDriverId = DriverCatalog.GetDefaultForTeam(team).Id;
            config.aiTeams = Teams.Where(t => t != team).Take(3).ToArray();
            config.aiOpponentCount = 3;
            SetField("loadRaceDriverXp", (Func<string, int>)(id => { calls.Add("XP:" + id); return 137; }));
            SetField("loadRaceTechProfile", (Func<TeamId, TechTreeDatabase, TechTreeState>)((id, db) =>
            {
                Assert.That(db, Is.SameAs(GetField<RaceSession>("session").TechDb));
                calls.Add("TECH:" + id);
                return cache[id];
            }));
            SetField("createRaceCareerRepository", (Func<CareerRepository>)(() =>
                throw new InvalidOperationException("Ordinary startup must not open career storage.")));
            InitializeWithTemporaryLog("silverstone_afternoon_tea");
            Assert.That(calls, Is.EqualTo(new[] { "XP:" + config.playerDriverId, "TECH:" + team }));
            AssertStartupField(team, 4, 137);
            Assert.That(session.Human.techState, Is.SameAs(cache[team]));
            Assert.That(session.Players.Skip(1).All(p => !ReferenceEquals(p.techState, cache[p.teamId])), Is.True);
        }
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void InitializeGameTutorialWinsOverCareerAndRosterWithoutProgressionReads(TeamId team)
    {
        using (var scope = new RaceStartupTestScope(true))
        {
            var tutorial = TutorialScenarioDefinition.CreateTeamSpecialty(team);
            CareerRepository repository = CreateSeason(TeamId.DE, out _);
            Assert.That(CareerRaceLaunchState.Request(repository.Load().State, "startup-precedence"), Is.True);
            FreeRaceRosterState.InitializeThunderstorm(DriverCatalog.GetDefaultForTeam(TeamId.IT));
            TutorialLaunchState.Request(tutorial);
            RejectStartupReads();
            InitializeWithTemporaryLog(tutorial.trackId);
            AssertStartupField(team, tutorial.opponentCount + 1, 0);
            Assert.That(CareerRaceLaunchState.Current, Is.Null);
            Assert.That(session.Players.All(p => p.techState == null), Is.True);
            Assert.That(GetField<TutorialRuntimeDirector>("tutorialDirector"), Is.Not.Null);
            Assert.That(File.ReadAllText(manager.LastRaceLogPath), Does.Contain("[TUTORIAL_SETUP]").And.Not.Contain("[CAREER_SETUP]").And.Not.Contain("[FREE_RACE_SETUP]"));
        }
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void InitializeGameValidatesCareerWithInjectedRepositoryAndNoOrdinaryTechRead(TeamId team)
    {
        using (var scope = new RaceStartupTestScope(true))
        {
            CareerRepository repository = CreateSeason(team, out MemoryCareerStore store);
            CareerSeasonState state = repository.Load().State;
            string json = store.Json;
            Assert.That(CareerRaceLaunchState.Request(state, "startup-career"), Is.True);
            FreeRaceRosterState.InitializeThunderstorm(DriverCatalog.GetDefaultForTeam(TeamId.IT));
            int repositoryCalls = 0;
            SetField("createRaceCareerRepository", (Func<CareerRepository>)(() => { repositoryCalls++; return repository; }));
            SetField("loadRaceDriverXp", (Func<string, int>)(_ => 137)); // Existing career XP read is retained.
            SetField("loadRaceTechProfile", (Func<TeamId, TechTreeDatabase, TechTreeState>)((_, __) =>
                throw new InvalidOperationException("Career must use its own snapshot.")));
            InitializeWithTemporaryLog(CareerRaceLaunchState.Current.TrackId);
            AssertStartupField(team, 4, 137);
            Assert.That(repositoryCalls, Is.EqualTo(1));
            Assert.That(session.Players.Select(p => p.teamId), Is.EquivalentTo(state.Competitors));
            Assert.That(session.Human.techState, Is.Not.SameAs(cache[team]));
            Assert.That(session.Human.techState.rpBalance, Is.EqualTo(state.ActiveTechSnapshot.RpBalance));
            Assert.That(store.Json, Is.EqualTo(json));
            Assert.That(store.SuccessfulWrites, Is.EqualTo(1), "Initialization must not save/advance career.");
            Assert.That(File.ReadAllText(manager.LastRaceLogPath), Does.Contain("[CAREER_SETUP]").And.Not.Contain("[FREE_RACE_SETUP]"));
        }
    }

    [Test]
    public void InvalidCareerStartupStopsBeforeXpProfileAiAndLogCreation()
    {
        using (var scope = new RaceStartupTestScope(true))
        {
            CareerRepository repository = CreateSeason(TeamId.UK, out _);
            Assert.That(CareerRaceLaunchState.Request(repository.Load().State, "wrong-track"), Is.True);
            SetField("createRaceCareerRepository", (Func<CareerRepository>)(() => repository));
            SetField("loadRaceDriverXp", (Func<string, int>)(_ => throw new InvalidOperationException("No XP after validation failure.")));
            SetField("loadRaceTechProfile", (Func<TeamId, TechTreeDatabase, TechTreeState>)((_, __) => throw new InvalidOperationException("No profile after validation failure.")));
            SetTrack(TrackDataLoader.LoadConfig("monza_pasta"));
            typeof(MVPGameManager).GetMethod("InitializeGame", PrivateInstance).Invoke(manager, null);
            Assert.That(GetField<string>("careerInitializationFailure"), Is.Not.Empty);
            Assert.That(GetField<RaceSession>("session"), Is.Null);
            Assert.That(GetField<Dictionary<PlayerState, AIController>>("aiControllers"), Is.Empty);
            Assert.That(manager.LastRaceLogPath, Is.Null);
        }
    }

    private void RejectStartupReads()
    {
        SetField("loadRaceDriverXp", (Func<string, int>)(_ => throw new InvalidOperationException("Tutorial XP read.")));
        SetField("loadRaceTechProfile", (Func<TeamId, TechTreeDatabase, TechTreeState>)((_, __) => throw new InvalidOperationException("Tutorial tech read.")));
        SetField("createRaceCareerRepository", (Func<CareerRepository>)(() => throw new InvalidOperationException("Tutorial career read.")));
    }

    private void InitializeWithTemporaryLog(string trackId)
    {
        TrackConfig track = TrackDataLoader.LoadConfig(trackId);
        Assert.That(track, Is.Not.Null);
        SetTrack(track);
        typeof(TrackManager).GetField("nodes", PrivateInstance).SetValue(manager.trackManager, TrackDataLoader.ConfigToNodes(track));
        manager.hudUI = null;
        manager.cardHandUI = null;
        var writer = new RaceTestLogWriter(Path.Combine(
            PlaytestTelemetryTestIsolationTests.AllowedRoot, "startup-adapter-" + Guid.NewGuid().ToString("N")));
        SetField("raceLogWriter", writer);
        try { typeof(MVPGameManager).GetMethod("InitializeGame", PrivateInstance).Invoke(manager, null); }
        finally { writer.Dispose(); } // Inactive hosts have no lifecycle-driven OnDestroy; always close the test file.
        session = GetField<RaceSession>("session");
        Assert.That(manager.LastRaceLogPath, Does.StartWith(PlaytestTelemetryTestIsolationTests.AllowedRoot + Path.DirectorySeparatorChar));
    }

    private void AssertStartupField(TeamId team, int count, int xp)
    {
        Assert.That(session.Players.Count, Is.EqualTo(count));
        Assert.That(session.Human.teamId, Is.EqualTo(team));
        Assert.That(session.Human.driverXp, Is.EqualTo(xp));
        Assert.That(session.Players.Skip(1).All(p => p.isAI && p.driverXp == 0), Is.True);
        Assert.That(GetField<Dictionary<PlayerState, AIController>>("aiControllers").Keys,
            Is.EquivalentTo(session.Players.Skip(1)));
        Assert.That(session.Players.All(p => p.deck.Hand.Count > 0 && !p.hasFinished && !p.isBlown), Is.True);
        Assert.That(manager.TutorialInputPhase, Is.EqualTo("gear"));
        Assert.That(GetField<string>("careerInitializationFailure"), Is.Empty);
    }
}
