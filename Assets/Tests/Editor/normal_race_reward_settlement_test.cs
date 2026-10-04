using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class NormalRaceRewardSettlementTest
{
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void IsolatedModesReturnBeforeValidatingNormalDependencies(bool tutorial, bool career)
        => Assert.That(NormalRaceRewardSettlement.Settle(null, null, tutorial, career, null, null), Is.Empty);

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void AbsentTechStillPersistsExactDriverXp(TeamId team)
    {
        var session = new RaceSession(new SystemRandomSource(24));
        var human = new PlayerState("Human", false, 0, 1)
        {
            teamId = team, hasFinished = true, finishOrder = 1, driverXp = 42
        };
        session.Players.Add(human);
        int xpSaves = 0;
        string report = NormalRaceRewardSettlement.Settle(session, null, false, false,
            state => Assert.Fail("Absent tech cannot be persisted."), (id, xp) =>
            {
                Assert.AreEqual(human.DriverProfile.Id, id);
                Assert.AreEqual(42 + DriverProgression.CalculateRaceXp(1,
                    human.DriverProfile.TalentMultiplier, human.DriverProfile.Team), xp);
                xpSaves++;
            });
        Assert.AreEqual(1, xpSaves);
        StringAssert.Contains("1. Human: +5000 RP\n\n车手 XP:", report);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StorageFailureStopsLaterWorkWithoutRollingBackEarlierMutation(bool failXp)
    {
        var session = new RaceSession(new SystemRandomSource(25));
        var human = new PlayerState("Human", false, 0, 1)
        {
            teamId = TeamId.US, hasFinished = true, finishOrder = 1,
            techState = new TechTreeState(TeamId.US, 77), driverXp = 42
        };
        var ai = new PlayerState("AI", true, 0, 1)
        {
            teamId = TeamId.DE, hasFinished = true, finishOrder = 2,
            techState = new TechTreeState(TeamId.DE, 77), driverXp = 42
        };
        session.Players.Add(human);
        session.Players.Add(ai);
        var failure = new System.InvalidOperationException("recorded storage failure");
        int xpCalls = 0;
        var raised = Assert.Throws<System.InvalidOperationException>(() =>
            NormalRaceRewardSettlement.Settle(session, "US", false, false,
                state => { if (!failXp) throw failure; },
                (id, xp) => { xpCalls++; throw failure; }));
        Assert.AreSame(failure, raised);
        Assert.AreEqual(5077, human.techState.rpBalance);
        Assert.AreEqual(failXp ? 3577 : 77, ai.techState.rpBalance);
        Assert.AreEqual(failXp ? 1 : 0, xpCalls);
        Assert.AreEqual(failXp ? 42 + DriverProgression.CalculateRaceXp(1,
            human.DriverProfile.TalentMultiplier, human.DriverProfile.Team) : 42, human.driverXp);
        Assert.AreEqual(42, ai.driverXp, "AI XP follows human XP and must stop after its save fails.");
    }

    [Test]
    public void test_normal_race_grants_ranked_rewards_and_persists_only_human()
    {
        var session = new RaceSession(new SystemRandomSource(17));
        var human = new PlayerState("Human", false, 8, 1)
        {
            teamId = TeamId.UK, hasFinished = true, finishOrder = 1,
            techState = new TechTreeState(TeamId.UK, 200)
        };
        var ai = new PlayerState("AI", true, 4, 1)
        {
            teamId = TeamId.DE, hasFinished = true, finishOrder = 2,
            techState = new TechTreeState(TeamId.DE, 100)
        };
        session.Players.Add(human);
        session.Players.Add(ai);
        var savedTech = new List<TechTreeState>();
        var savedXp = new List<int>();

        string report = NormalRaceRewardSettlement.Settle(
            session, "UK", false, false,
            state => savedTech.Add(state), (id, xp) => savedXp.Add(xp));

        Assert.That(human.techState.rpBalance, Is.EqualTo(5200));
        Assert.That(ai.techState.rpBalance, Is.EqualTo(3600));
        Assert.That(human.driverXp, Is.EqualTo(
            DriverProgression.CalculateRaceXp(1, human.DriverProfile.TalentMultiplier, TeamId.UK)));
        Assert.That(ai.driverXp, Is.EqualTo(
            DriverProgression.CalculateRaceXp(2, ai.DriverProfile.TalentMultiplier, TeamId.DE)));
        Assert.That(savedTech, Is.EqualTo(new[] { human.techState }));
        Assert.That(savedXp, Is.EqualTo(new[] { human.driverXp }));
        StringAssert.Contains("RP 奖励:\n1. Human: +5000 RP", report);
        StringAssert.Contains("\n\n车手 XP:\n", report);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void test_tutorial_and_career_never_mutate_or_save_normal_rewards(
        bool tutorialActive, bool careerActive)
    {
        var session = new RaceSession(new SystemRandomSource(18));
        var human = new PlayerState("Human", false, 0, 1)
        {
            teamId = TeamId.CN, techState = new TechTreeState(TeamId.CN, 77), driverXp = 42
        };
        session.Players.Add(human);
        int saveCount = 0;

        string report = NormalRaceRewardSettlement.Settle(
            session, "CN", tutorialActive, careerActive,
            state => saveCount++, (id, xp) => saveCount++);

        Assert.That(report, Is.Empty);
        Assert.That(human.techState.rpBalance, Is.EqualTo(77));
        Assert.That(human.driverXp, Is.EqualTo(42));
        Assert.That(saveCount, Is.Zero);
    }

    [Test]
    public void test_cavallino_home_race_and_blown_driver_keep_existing_rules()
    {
        var session = new RaceSession(new SystemRandomSource(19));
        var winner = new PlayerState("Winner", false, 0, 1)
        {
            teamId = TeamId.IT, hasFinished = true, finishOrder = 1,
            techState = new TechTreeState(TeamId.IT)
        };
        winner.techState.activeNodeIds.Add("it-l3-cavallino-rampante");
        var blown = new PlayerState("Blown", true, 0, 1)
        {
            teamId = TeamId.US, isBlown = true, driverXp = 90
        };
        session.Players.Add(winner);
        session.Players.Add(blown);

        string report = NormalRaceRewardSettlement.Settle(
            session, "IT", false, false, state => { }, (id, xp) => { });

        Assert.That(winner.techState.rpBalance, Is.EqualTo(11250));
        Assert.That(blown.driverXp, Is.EqualTo(90));
        StringAssert.Contains("Winner: +11250 RP", report);
        StringAssert.Contains("Blown（", report);
        StringAssert.Contains("+0 XP", report);
    }
}

/// <summary>Inactive coordinator fixture: no scene startup, UI or PlayerPrefs writes.</summary>
public class NormalRaceRewardBoundaryTests
{
    private GameObject host;
    private MVPGameManager manager;
    private RaceSession session;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Reward boundary regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        session = new RaceSession(new SystemRandomSource(23));
        SetField("session", session);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(host);

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void OrdinaryAdapterPersistsOnlyHumanInRpThenXpOrder(TeamId team)
    {
        PlayerState human = AddPlayer(team, false, 1);
        PlayerState ai = AddPlayer(team, true, 2);
        var calls = new List<string>();
        string report = Settle(
            tech =>
            {
                Assert.AreSame(human.techState, tech);
                Assert.AreEqual(5077, tech.rpBalance);
                Assert.AreEqual(42, human.driverXp, "XP must not settle before the RP callback.");
                calls.Add("RP");
            },
            (id, xp) =>
            {
                Assert.AreEqual(human.DriverProfile.Id, id);
                Assert.AreEqual(human.driverXp, xp);
                Assert.AreEqual(3577, ai.techState.rpBalance, "All RP precedes XP persistence.");
                calls.Add("XP");
            });
        Assert.That(calls, Is.EqualTo(new[] { "RP", "XP" }));
        Assert.AreEqual(42 + DriverProgression.CalculateRaceXp(
            1, human.DriverProfile.TalentMultiplier, human.DriverProfile.Team), human.driverXp);
        Assert.AreEqual(42 + DriverProgression.CalculateRaceXp(
            2, ai.DriverProfile.TalentMultiplier, ai.DriverProfile.Team), ai.driverXp);
        StringAssert.Contains("human: +5000 RP", report);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void LiveModeGuardsRejectEvenAbsentSessionAndPersistence(bool tutorial, bool career)
    {
        SetModes(tutorial, career);
        SetField("session", null);
        Assert.That(Settle(null, null), Is.Empty);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void ActualGameOverKeepsNormalBalancesAndXpIsolated(bool tutorial, bool career)
    {
        PlayerState human = AddPlayer(TeamId.UK, false, 1);
        PlayerState ai = AddPlayer(TeamId.DE, true, 2);
        SetModes(tutorial, career);
        // The already-saved career branch avoids the unrelated career repository.
        SetField("careerResultRecorded", true);
        typeof(MVPGameManager).GetMethod("ShowGameOver", PrivateInstance).Invoke(manager, null);
        Assert.AreEqual(77, human.techState.rpBalance);
        Assert.AreEqual(77, ai.techState.rpBalance);
        Assert.AreEqual(42, human.driverXp);
        Assert.AreEqual(42, ai.driverXp);
    }

    [TestCase("IT", 11250)]
    [TestCase("US", 7500)]
    [TestCase(null, 7500)]
    public void LoadedCountryReachesCavallinoRule(string country, int expectedReward)
    {
        PlayerState human = AddPlayer(TeamId.IT, false, 1);
        human.techState.activeNodeIds.Add("it-l3-cavallino-rampante");
        TrackManager track = host.AddComponent<TrackManager>();
        typeof(TrackManager).GetProperty("LoadedTrackConfig").SetValue(
            track, new TrackConfig { country = country });
        manager.trackManager = track;
        Settle(state => { }, (id, xp) => { });
        Assert.AreEqual(77 + expectedReward, human.techState.rpBalance);
    }

    [Test]
    public void MissingTrackAndMissingTechStillGrantXpWithoutTechSave()
    {
        PlayerState human = AddPlayer(TeamId.IT, false, 1);
        human.techState = null;
        int xpSaves = 0;
        string report = Settle(state => Assert.Fail("Absent tech cannot be saved."),
            (id, xp) => xpSaves++);
        Assert.AreEqual(1, xpSaves);
        StringAssert.Contains("human: +5000 RP", report);
        Assert.Greater(human.driverXp, 42);
    }

    [Test]
    public void PersistenceFailurePreservesExistingNonTransactionalOrder()
    {
        PlayerState human = AddPlayer(TeamId.US, false, 1);
        PlayerState ai = AddPlayer(TeamId.DE, true, 2);
        var failure = new System.InvalidOperationException("recorded storage failure");
        TargetInvocationException raised = Assert.Throws<TargetInvocationException>(() =>
            Settle(state => throw failure, (id, xp) => Assert.Fail("XP must not run after failure.")));
        Assert.AreSame(failure, raised.InnerException);
        Assert.AreEqual(5077, human.techState.rpBalance);
        Assert.AreEqual(77, ai.techState.rpBalance);
        Assert.AreEqual(42, human.driverXp);
        Assert.AreEqual(42, ai.driverXp);
    }

    private PlayerState AddPlayer(TeamId team, bool ai, int order)
    {
        var player = new PlayerState(ai ? "ai" : "human", ai, 0, 1)
        {
            teamId = team, hasFinished = true, finishOrder = order,
            techState = new TechTreeState(team, 77), driverXp = 42
        };
        session.Players.Add(player);
        return player;
    }

    [TestCase(TeamId.IT, false, 11250)]
    [TestCase(TeamId.IT, true, 11250)]
    [TestCase(TeamId.UK, false, 7500)]
    [TestCase(TeamId.UK, true, 7500)]
    [TestCase(TeamId.DE, false, 7500)]
    [TestCase(TeamId.DE, true, 7500)]
    [TestCase(TeamId.US, false, 7500)]
    [TestCase(TeamId.US, true, 7500)]
    [TestCase(TeamId.CN, false, 7500)]
    [TestCase(TeamId.CN, true, 7500)]
    [TestCase(TeamId.JP, false, 7500)]
    [TestCase(TeamId.JP, true, 7500)]
    public void LoadedHomeTrackUsesActualDriverNotVehicleTeam(TeamId driverTeam, bool ai, int reward)
    {
        PlayerState player = AddPlayer(TeamId.IT, ai, 1);
        player.driverId = DriverCatalog.GetDefaultForTeam(driverTeam).Id;
        player.techState.activeNodeIds.Add("it-l3-cavallino-rampante");
        TrackManager track = host.AddComponent<TrackManager>();
        typeof(TrackManager).GetProperty("LoadedTrackConfig").SetValue(
            track, new TrackConfig { country = "IT" });
        manager.trackManager = track;
        int techSaves = 0;
        int xpSaves = 0;
        string report = Settle(state => { techSaves++; }, (id, xp) => { xpSaves++; });
        Assert.AreEqual(77 + reward, player.techState.rpBalance);
        Assert.That(report, Does.Contain("+" + reward + " RP"));
        Assert.AreEqual(ai ? 0 : 1, techSaves);
        Assert.AreEqual(ai ? 0 : 1, xpSaves);
    }

    private void SetModes(bool tutorial, bool career)
    {
        if (tutorial) SetField("tutorialScenario", TutorialScenarioDefinition.CreateLeMansUk());
        if (!career) return;
        var state = new CareerSeasonState();
        Assert.IsTrue(CareerModeRules.TryStartSeason(state, TeamId.UK,
            new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US }));
        Assert.IsTrue(CareerRaceLaunchRequest.TryCreate(state, "reward-isolation", out var launch));
        SetField("careerRaceLaunch", launch);
    }

    private void SetField(string name, object value)
        => typeof(MVPGameManager).GetField(name, PrivateInstance).SetValue(manager, value);

    private string Settle(System.Action<TechTreeState> saveTech, System.Action<string, int> saveXp)
        => (string)typeof(MVPGameManager).GetMethod("SettleNormalRaceRewards", PrivateInstance)
            .Invoke(manager, new object[] { saveTech, saveXp });
}

public sealed class CavallinoDriverRewardTests
{
    [TestCase(TeamId.UK, false, 7500)]
    [TestCase(TeamId.UK, true, 7500)]
    [TestCase(TeamId.IT, false, 11250)]
    [TestCase(TeamId.IT, true, 11250)]
    public void BorrowedItalianTechnologyUsesActualDriverAndPreservesUnlocks(TeamId driverTeam, bool ai, int reward)
    {
        var session = new RaceSession(new SystemRandomSource(41));
        var player = new PlayerState("UK winner", ai, 0, 1)
        {
            teamId = TeamId.UK, driverId = DriverCatalog.GetDefaultForTeam(driverTeam).Id,
            hasFinished = true, finishOrder = 1, techState = new TechTreeState(TeamId.UK, 77)
        };
        player.techState.activeNodeIds.Add("uk-l3-sun-never-sets");
        session.Players.Add(player);
        session.PrepareTechnologyForRace(player, "IT");
        Assert.That(TechTreeRules.ShouldApplyCavallino(player.techState, session.TechDb, 1), Is.True);
        int saves = 0;
        NormalRaceRewardSettlement.Settle(session, "IT", false, false,
            state => { saves++; }, (id, xp) => { saves++; });
        Assert.AreEqual(77 + reward, player.techState.rpBalance);
        Assert.AreEqual(ai ? 0 : 2, saves);
        Assert.That(player.techState.unlockedNodeIds, Is.Empty);
        CollectionAssert.AreEqual(new[] { "uk-l3-sun-never-sets" }, player.techState.activeNodeIds);
        Assert.That(player.techState.sunNeverSetsTarget, Is.EqualTo(TeamId.IT));
    }

    private static IEnumerable<TestCaseData> DriverCases()
    {
        foreach (DriverProfile driver in DriverCatalog.All)
        {
            yield return new TestCaseData(driver.Id, "IT", driver.Team == TeamId.IT ? 11250 : 7500);
            yield return new TestCaseData(driver.Id, "US", 7500);
        }
    }

    [TestCaseSource(nameof(DriverCases))]
    public void ChampionRewardUsesAllTwelveCatalogDrivers(string driverId, string country, int reward)
    {
        var session = new RaceSession(new SystemRandomSource(41));
        var player = new PlayerState("Winner", false, 0, 1)
        {
            teamId = TeamId.IT, driverId = driverId, hasFinished = true, finishOrder = 1,
            techState = new TechTreeState(TeamId.IT, 77), driverXp = 42
        };
        player.techState.activeNodeIds.Add("it-l3-cavallino-rampante");
        session.Players.Add(player);
        int saves = 0;
        int expectedXp = 42 + DriverProgression.CalculateRaceXp(
            1, player.DriverProfile.TalentMultiplier, player.DriverProfile.Team);
        NormalRaceRewardSettlement.Settle(session, country, false, false,
            state => { saves++; Assert.AreSame(player.techState, state); },
            (id, xp) => { saves++; Assert.AreEqual(driverId, id); Assert.AreEqual(expectedXp, xp); });
        Assert.AreEqual(77 + reward, player.techState.rpBalance);
        Assert.AreEqual(expectedXp, player.driverXp);
        Assert.AreEqual(2, saves);
        CollectionAssert.AreEqual(new[] { "it-l3-cavallino-rampante" }, player.techState.activeNodeIds);
        Assert.That(player.techState.unlockedNodeIds, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("unknown-driver")]
    public void MissingDriverUsesExistingItalianDefault(string driverId)
    {
        var session = new RaceSession(new SystemRandomSource(41));
        var player = new PlayerState("Winner", false, 0, 1)
        {
            teamId = TeamId.IT, driverId = driverId, hasFinished = true, finishOrder = 1,
            techState = new TechTreeState(TeamId.IT)
        };
        player.techState.activeNodeIds.Add("it-l3-cavallino-rampante");
        session.Players.Add(player);
        NormalRaceRewardSettlement.Settle(session, "it", false, false, state => { }, (id, xp) => { });
        Assert.AreEqual(11250, player.techState.rpBalance);
        Assert.AreEqual(driverId, player.driverId, "Reward display must not rewrite the selection.");
    }

    [TestCase("IT", TeamId.IT, true)]
    [TestCase("it", TeamId.IT, true)]
    [TestCase("US", TeamId.IT, false)]
    [TestCase(null, TeamId.IT, false)]
    [TestCase("", TeamId.IT, false)]
    [TestCase("IT", TeamId.US, false)]
    public void HomeEligibilityRequiresCountryAndDriver(string country, TeamId driver, bool expected)
    {
        Assert.AreEqual(expected, TechTreeRules.IsCavallinoHomeRace(country, driver));
    }
}
