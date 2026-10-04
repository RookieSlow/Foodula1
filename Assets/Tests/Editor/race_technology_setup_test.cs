using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Real coordinator setup on inactive objects. Cached human profiles are injected
/// in memory and restored, so these tests never save or delete PlayerPrefs data.
/// This is not scene startup, GameLoop scheduling or Play Mode acceptance.
/// </summary>
public class RaceTechnologySetupTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private MVPGameManager manager;
    private GameConfigSO config;
    private RaceSession session;
    private Random.State previousRandom;
    private Dictionary<TeamId, TechTreeState> cache;
    private readonly Dictionary<TeamId, TechTreeState> previousProfiles = new Dictionary<TeamId, TechTreeState>();

    [SetUp]
    public void SetUp()
    {
        previousRandom = Random.state;
        host = new GameObject("Technology setup regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        manager.trackManager = host.AddComponent<TrackManager>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.enableTechTree = true;
        config.enableTrickCards = false;
        config.ensurePlayerAttackTrickInOpeningHand = false;
        config.jpDemoBroth = BrothType.None;
        config.handSize = 7;
        config.heatPoolPerPlayer = 6;
        config.speedCardDistribution = new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
        manager.config = config;
        session = new RaceSession(new SystemRandomSource(1));
        SetField("session", session);
        cache = (Dictionary<TeamId, TechTreeState>)typeof(TechTreeProfileStore)
            .GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        previousProfiles.Clear();
        SetCountry("IT");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var pair in previousProfiles)
        {
            if (pair.Value == null) cache.Remove(pair.Key);
            else cache[pair.Key] = pair.Value;
        }
        Random.state = previousRandom;
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(config);
    }

    private void SetField(string name, object value) =>
        typeof(MVPGameManager).GetField(name, Private).SetValue(manager, value);

    private void SetCountry(string country) =>
        typeof(TrackManager).GetProperty("LoadedTrackConfig").SetValue(manager.trackManager,
            country == null ? null : new TrackConfig { country = country });

    private void Setup(PlayerState player, TeamId team, CareerTechSnapshot snapshot = null) =>
        typeof(MVPGameManager).GetMethod("SetupPlayerForRace", Private)
            .Invoke(manager, new object[] { player, team, snapshot });

    private PlayerState CreateParticipant(RaceParticipantPlan plan, int start = 17) =>
        (PlayerState)typeof(MVPGameManager).GetMethod("CreateParticipantForRace", Private)
            .Invoke(manager, new object[] { plan, start });

    private static List<RaceParticipantPlan> Plans(
        DriverProfile driver, int xp, TutorialScenarioDefinition tutorial = null,
        CareerRaceLaunchRequest career = null, FreeRaceRosterEntry[] roster = null) =>
        RaceParticipantPlanBuilder.Build(tutorial, career, roster, driver,
            new[] { driver.Team }, 1, _ => xp);

    private AIController BindController(PlayerState opponent, int index) =>
        (AIController)typeof(MVPGameManager).GetMethod("BindAiControllerForRace", Private)
            .Invoke(manager, new object[] { opponent, index });

    private static IRandomSource ControllerRandom(AIController controller) =>
        (IRandomSource)typeof(AIController).GetField("randomSource", Private).GetValue(controller);

    private void AssertControllerBinding(AIController controller, PlayerState opponent)
    {
        var bindings = (Dictionary<PlayerState, AIController>)typeof(MVPGameManager)
            .GetField("aiControllers", Private).GetValue(manager);
        Assert.AreSame(controller, bindings[opponent]);
        Assert.AreSame(host, controller.gameObject);
        Assert.AreSame(opponent, typeof(AIController).GetField("ai", Private).GetValue(controller));
        Assert.AreSame(manager, typeof(AIController).GetField("game", Private).GetValue(controller));
        Assert.AreSame(config, typeof(AIController).GetField("config", Private).GetValue(controller));
        Assert.AreSame(manager.Track, typeof(AIController).GetField("track", Private).GetValue(controller));
    }

    [TestCaseSource(nameof(SelectedDrivers))]
    public void NormalOpponentBindingSelectsOwnCardsAndPreservesHuman(string driverId)
    {
        Assert.IsTrue(DriverCatalog.TryGet(driverId, out var driver));
        config.enableTechTree = false;
        config.aiCardVariationChance = 1;
        var plans = Plans(driver, 500);
        var human = CreateParticipant(plans[0]);
        var opponent = CreateParticipant(plans[1]);
        var humanCards = new List<CardData>(human.deck.Hand);
        var ownCards = new List<CardData> { new CardData(CardType.Speed, 4),
            new CardData(CardType.Speed, 3), new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 1) };
        opponent.deck.InitializeExactOrder(ownCards, new HeatPool(6));
        opponent.deck.DrawToHand(ownCards.Count);
        opponent.gear = 2;
        float beforeBinding;
        Random.InitState(120);
        beforeBinding = Random.value;
        Random.InitState(120);
        var controller = BindController(opponent, 0);
        Assert.AreEqual(beforeBinding, Random.value, "Binding must not consume random values.");
        AssertControllerBinding(controller, opponent);
        Assert.IsInstanceOf<UnityRandomSource>(ControllerRandom(controller));
        Random.InitState(711);
        var expected = AIPlanner.ChooseSpeedCards(opponent.deck,
            manager.GetMaxSpeedCardsThisTurn(opponent), opponent.HeatRatio, false,
            config.aiHeatWarningThreshold, config.aiCautiousHeatThreshold,
            config.aiCardVariationChance, new UnityRandomSource());
        float expectedNext = Random.value;
        Random.InitState(711);
        controller.SelectCards();
        CollectionAssert.AreEqual(expected, opponent.playedSpeedCardsThisTurn);
        Assert.AreEqual(expectedNext, Random.value);
        foreach (var card in opponent.playedSpeedCardsThisTurn)
        {
            CollectionAssert.Contains(ownCards, card);
            CollectionAssert.DoesNotContain(opponent.deck.Hand, card);
        }
        CollectionAssert.AreEqual(humanCards, human.deck.Hand);
        Assert.IsEmpty(human.playedSpeedCardsThisTurn);
        Assert.IsTrue(human.driverSkill.Enabled);
        Assert.IsFalse(opponent.driverSkill.Enabled);
        Assert.AreEqual(2, session.Players.Count);
        Assert.AreEqual(1, host.GetComponents<AIController>().Length);
    }

    [TestCase(TeamId.CN)] [TestCase(TeamId.US)] [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)] [TestCase(TeamId.IT)] [TestCase(TeamId.JP)]
    public void TutorialControllerSelectionReplaysWithoutGlobalRandomConsumption(TeamId team)
    {
        var scenario = TutorialScenarioDefinition.CreateTeamSpecialty(team);
        SetField("tutorialScenario", scenario);
        config.aiCardVariationChance = 1;
        var plans = Plans(DriverCatalog.All[0], 0, scenario);
        var first = CreateParticipant(plans[1]);
        var second = CreateParticipant(plans[1]);
        var other = CreateParticipant(plans[1]);
        var firstController = BindController(first, 0);
        var secondController = BindController(second, 0);
        var otherController = BindController(other, 1);
        foreach (var controller in new[] { firstController, secondController, otherController })
            Assert.IsInstanceOf<SystemRandomSource>(ControllerRandom(controller));
        Assert.AreNotSame(ControllerRandom(firstController), ControllerRandom(secondController));
        Assert.AreNotSame(ControllerRandom(firstController), ControllerRandom(otherController));
        AssertControllerBinding(firstController, first);
        AssertControllerBinding(secondController, second);
        CollectionAssert.AreEqual(Cards(first.deck.Hand), Cards(second.deck.Hand));
        var expectedSource = new SystemRandomSource(TutorialScenarioDefinition.RuntimeSeed + 1);
        var expectedCards = AIPlanner.ChooseSpeedCards(first.deck,
            manager.GetMaxSpeedCardsThisTurn(first), first.HeatRatio, false,
            config.aiHeatWarningThreshold, config.aiCautiousHeatThreshold,
            config.aiCardVariationChance, expectedSource);
        Random.InitState(812);
        float expectedGlobal = Random.value;
        Random.InitState(812);
        firstController.SelectCards();
        secondController.SelectCards();
        Assert.AreEqual(expectedGlobal, Random.value);
        CollectionAssert.AreEqual(Cards(first.playedSpeedCardsThisTurn),
            Cards(second.playedSpeedCardsThisTurn));
        CollectionAssert.AreEqual(expectedCards, first.playedSpeedCardsThisTurn);
        for (int i = 0; i < 8; i++)
            Assert.AreEqual(expectedSource.NextDouble(), ControllerRandom(firstController).NextDouble());
        var otherSeed = new SystemRandomSource(TutorialScenarioDefinition.RuntimeSeed + 2);
        // The untouched second-index stream has the original per-opponent seed.
        for (int i = 0; i < 8; i++)
            Assert.AreEqual(otherSeed.NextDouble(), ControllerRandom(otherController).NextDouble());
        Assert.IsFalse(first.driverSkill.Enabled);
        Assert.IsFalse(second.driverSkill.Enabled);
        Assert.IsNull(first.techState);
        Assert.AreEqual(3, session.Players.Count);
        Assert.AreEqual(3, host.GetComponents<AIController>().Length);
    }

    [Test]
    public void TwelveCarBindingsKeepHumanOutAndEveryOpponentDistinct()
    {
        config.enableTechTree = false;
        var roster = new List<FreeRaceRosterEntry>();
        foreach (var driver in DriverCatalog.All)
            roster.Add(new FreeRaceRosterEntry(driver.Team, driver.Id));
        var plans = Plans(DriverCatalog.All[0], 500, roster: roster.ToArray());
        var human = CreateParticipant(plans[0]);
        var controllers = new HashSet<AIController>();
        for (int i = 1; i < plans.Count; i++)
        {
            var opponent = CreateParticipant(plans[i]);
            var controller = BindController(opponent, i - 1);
            Assert.IsTrue(controllers.Add(controller));
            AssertControllerBinding(controller, opponent);
            Assert.IsInstanceOf<UnityRandomSource>(ControllerRandom(controller));
            Assert.AreSame(opponent, session.Players[i]);
            Assert.IsFalse(opponent.driverSkill.Enabled);
        }
        var bindings = (Dictionary<PlayerState, AIController>)typeof(MVPGameManager)
            .GetField("aiControllers", Private).GetValue(manager);
        Assert.IsFalse(bindings.ContainsKey(human));
        Assert.IsTrue(human.driverSkill.Enabled);
        Assert.AreEqual(12, session.Players.Count);
        Assert.AreEqual(11, bindings.Count);
        Assert.AreEqual(11, host.GetComponents<AIController>().Length);
    }

    public static IEnumerable<TestCaseData> SelectedDrivers
    {
        get
        {
            foreach (var driver in DriverCatalog.All)
                yield return new TestCaseData(driver.Id);
        }
    }

    private static List<string> Cards(IReadOnlyList<CardData> cards) =>
        new List<CardData>(cards).ConvertAll(c => c.type + ":" + c.value + ":" + c.trickId);

    private static void AssertIdentityAndSkill(PlayerState player, RaceParticipantPlan plan,
        int start, int gear, bool skillEnabled)
    {
        Assert.AreEqual(plan.Name, player.name);
        Assert.AreEqual(!plan.IsHuman, player.isAI);
        Assert.AreEqual(plan.Team, player.teamId);
        Assert.AreEqual(plan.Team == TeamId.CN, player.usesChinaGearSystem);
        Assert.AreEqual(plan.Driver.Id, player.driverId);
        Assert.AreSame(plan.Driver, player.DriverProfile);
        Assert.AreEqual(plan.InitialXp, player.driverXp);
        Assert.AreEqual(start, player.position);
        Assert.AreEqual(gear, player.gear);
        Assert.AreEqual(DriverSkillRules.GetSkill(plan.Driver.Id), player.driverSkill.Skill);
        Assert.AreEqual(DriverSkillRules.GetPassiveSkill(plan.Driver.Id), player.driverSkill.PassiveSkill);
        Assert.AreEqual(DriverProgression.GetActiveTier(player.DriverLevel), player.driverSkill.Tier);
        Assert.AreEqual(DriverProgression.GetPassiveTier(player.DriverLevel), player.driverSkill.PassiveTier);
        Assert.AreEqual(DriverProgression.GetActiveUsesPerRace(player.DriverLevel, plan.Team),
            player.driverSkill.UsesRemaining);
        Assert.AreEqual(skillEnabled, player.driverSkill.Enabled);
        Assert.IsFalse(player.driverSkill.IsActive);
        Assert.AreEqual(0, player.driverSkill.ActiveTurnsRemaining);
        Assert.AreEqual(0, player.driverSkill.PassiveMovementBonusThisTurn);
        Assert.IsFalse(player.hasFinished);
        Assert.IsFalse(player.isBlown);
    }

    [TestCaseSource(nameof(SelectedDrivers))]
    public void PlannedDriverSetupMatchesOldSequenceAndRandomConsumption(string driverId)
    {
        Assert.IsTrue(DriverCatalog.TryGet(driverId, out var driver));
        config.enableTechTree = false;
        config.enableTrickCards = true;
        config.minGear = 2;
        var roster = new[] { new FreeRaceRosterEntry(driver.Team, driver.Id),
            new FreeRaceRosterEntry(driver.Team, driver.Id) };
        var plans = Plans(driver, 4000, roster: roster);
        foreach (var plan in plans)
        {
            Random.InitState(2718);
            var old = new PlayerState(plan.Name, !plan.IsHuman, 17, config.minGear)
                { driverId = plan.Driver.Id, driverXp = plan.InitialXp };
            Setup(old, plan.Team, plan.CareerTechSnapshot);
            old.driverSkill.Initialize(plan.Driver, old.DriverLevel, plan.IsHuman);
            float oldNextRandom = Random.value;

            Random.InitState(2718);
            PlayerState actual = CreateParticipant(plan);
            AssertIdentityAndSkill(actual, plan, 17, 2, plan.IsHuman);
            Assert.AreEqual(plan.IsHuman ? 7 : 1, actual.DriverLevel);
            CollectionAssert.AreEqual(Cards(old.deck.Hand), Cards(actual.deck.Hand));
            CollectionAssert.AreEqual(Cards(old.deck.DrawPile), Cards(actual.deck.DrawPile));
            Assert.AreEqual(old.deck.heatPool.remaining, actual.deck.heatPool.remaining);
            Assert.AreEqual(oldNextRandom, Random.value);
            Assert.IsNull(actual.techState);
            Assert.AreSame(actual, session.Players[session.Players.Count - 1]);
        }
        Assert.AreEqual(2, session.Players.Count);
        Assert.AreNotSame(session.Players[0].driverSkill, session.Players[1].driverSkill);
        Assert.AreNotSame(session.Players[0].deck.heatPool, session.Players[1].deck.heatPool);
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void TutorialPlansRegisterBothRolesWithDisabledSkillsAndReplayFreshState(TeamId team)
    {
        var profile = Profile(team);
        DirtyUsage(profile);
        InjectCachedProfile(profile);
        var scenario = TutorialScenarioDefinition.CreateTeamSpecialty(team);
        SetField("tutorialScenario", scenario);
        int xpLoads = 0;
        var plans = RaceParticipantPlanBuilder.Build(scenario, null, null,
            DriverCatalog.GetDefaultForTeam(TeamId.DE), null, 0,
            _ => { xpLoads++; throw new System.InvalidOperationException("Tutorial must not load XP"); });
        var previous = new List<PlayerState>();
        Random.State beforeSetup = Random.state;
        float expectedRandom = Random.value;
        Random.state = beforeSetup;
        for (int replay = 0; replay < 2; replay++)
        {
            session.Players.Clear();
            for (int i = 0; i < plans.Count; i++)
            {
                PlayerState player = CreateParticipant(plans[i], 23);
                AssertIdentityAndSkill(player, plans[i], 23, config.minGear, false);
                Assert.AreEqual(1, player.DriverLevel);
                Assert.IsNull(player.techState);
                var exact = plans[i].IsHuman ? scenario.CreateExactDeck() : scenario.CreateOpponentDeck();
                CollectionAssert.AreEqual(Cards(exact.GetRange(0, scenario.openingHandSize)), Cards(player.deck.Hand));
                Assert.AreEqual(scenario.engineHeatCapacity, player.deck.heatPool.remaining);
                Assert.AreSame(player, session.Players[i]);
                if (replay == 0) previous.Add(player);
                else
                {
                    Assert.AreNotSame(previous[i], player);
                    Assert.AreNotSame(previous[i].driverSkill, player.driverSkill);
                    Assert.AreNotSame(previous[i].deck.heatPool, player.deck.heatPool);
                    CollectionAssert.DoesNotContain(player.deck.Hand, previous[i].deck.Hand[0]);
                }
            }
        }
        Assert.AreEqual(expectedRandom, Random.value);
        Assert.AreEqual(0, xpLoads);
        Assert.IsTrue(profile.fishAndChipsUsed);
        Assert.IsEmpty(host.GetComponents<AIController>(), "Scene components remain outside this adapter");
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void CareerPlanInjectsSnapshotBeforeHandAndRegistersIsolatedDriverState(TeamId team)
    {
        var cached = Profile(team);
        DirtyUsage(cached);
        InjectCachedProfile(cached);
        var source = Profile(team);
        Assert.IsTrue(CareerTechSnapshotMapper.TryCapture(source, out var snapshot));
        var season = new CareerSeasonState();
        var field = new List<TeamId> { team };
        foreach (var other in new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP })
            if (other != team && field.Count < 4) field.Add(other);
        Assert.IsTrue(CareerModeRules.TryStartSeason(season, team, field, snapshot));
        Assert.IsTrue(CareerRaceLaunchRequest.TryCreate(season, "participant-setup", out var launch));
        var plans = Plans(DriverCatalog.GetDefaultForTeam(TeamId.US), 500, career: launch);
        config.enableTechTree = false; // Snapshot still takes precedence for the human.
        PlayerState first = CreateParticipant(plans[0]);
        AssertIdentityAndSkill(first, plans[0], 17, config.minGear, true);
        AssertSetup(first, team);
        Assert.AreEqual(4, first.DriverLevel);
        Assert.AreNotSame(cached, first.techState);
        Assert.AreNotSame(source, first.techState);
        first.techState.activeNodeIds.Clear();
        first.deck.DrawHeatFromPoolToHand(1);
        PlayerState second = CreateParticipant(plans[0]);
        AssertIdentityAndSkill(second, plans[0], 17, config.minGear, true);
        AssertSetup(second, team);
        CollectionAssert.AreEquivalent(source.activeNodeIds, second.techState.activeNodeIds);
        Assert.AreNotSame(first.driverSkill, second.driverSkill);
        Assert.AreNotSame(first.techState, second.techState);
        for (int i = 1; i < plans.Count; i++)
        {
            PlayerState ai = CreateParticipant(plans[i]);
            AssertIdentityAndSkill(ai, plans[i], 17, config.minGear, false);
            Assert.IsNull(ai.techState);
            Assert.AreEqual(0, ai.driverXp);
            Assert.AreSame(ai, session.Players[i + 1]);
        }
        Assert.IsTrue(cached.fishAndChipsUsed);
        CollectionAssert.AreEquivalent(source.activeNodeIds, snapshot.ActiveNodeIds);
    }

    [Test]
    public void TwelveCarPlanPreservesNamesOrderAndDistinctRuntimeInstances()
    {
        config.enableTechTree = false;
        var roster = new List<FreeRaceRosterEntry>();
        foreach (var driver in DriverCatalog.All)
            roster.Add(new FreeRaceRosterEntry(driver.Team, driver.Id));
        var plans = Plans(DriverCatalog.GetDefaultForTeam(TeamId.JP), 1000, roster: roster.ToArray());
        foreach (var plan in plans) CreateParticipant(plan, 19);
        Assert.AreEqual(12, session.Players.Count);
        for (int i = 0; i < plans.Count; i++)
        {
            AssertIdentityAndSkill(session.Players[i], plans[i], 19, config.minGear, i == 0);
            if (i == 0) continue;
            Assert.AreNotSame(session.Players[i - 1].deck, session.Players[i].deck);
            Assert.AreNotSame(session.Players[i - 1].driverSkill, session.Players[i].driverSkill);
            Assert.AreNotSame(session.Players[i - 1].trickState, session.Players[i].trickState);
        }
        Assert.IsEmpty(host.GetComponents<AIController>());
    }

    [Test]
    public void ParticipantSetupExceptionDoesNotRegisterPartialParticipantOrTouchExistingField()
    {
        config.enableTechTree = false;
        var plan = Plans(DriverCatalog.GetDefaultForTeam(TeamId.US), 250)[0];
        PlayerState existing = CreateParticipant(plan);
        var pool = existing.deck.heatPool;
        manager.config = null;
        var exception = Assert.Throws<TargetInvocationException>(() => CreateParticipant(plan));
        Assert.IsInstanceOf<System.NullReferenceException>(exception.InnerException);
        Assert.AreEqual(1, session.Players.Count);
        Assert.AreSame(existing, session.Players[0]);
        Assert.AreSame(pool, existing.deck.heatPool);
        Assert.AreEqual(250, existing.driverXp);
    }

    private TechTreeState Profile(TeamId team)
    {
        var profile = new TechTreeState(team, 12345);
        foreach (var tier in new[] { TechTreeTier.L1, TechTreeTier.L2, TechTreeTier.L3 })
            foreach (var node in session.TechDb.GetUniqueInTier(team, tier))
            {
                profile.unlockedNodeIds.Add(node.id);
                profile.activeNodeIds.Add(node.id);
            }
        return profile;
    }

    private void InjectCachedProfile(TechTreeState profile)
    {
        if (!previousProfiles.ContainsKey(profile.teamId))
        {
            cache.TryGetValue(profile.teamId, out var original);
            previousProfiles.Add(profile.teamId, original);
        }
        cache[profile.teamId] = profile;
    }

    private static void DirtyUsage(TechTreeState profile)
    {
        profile.heatReductionUsedThisLap = true;
        profile.fishAndChipsUsed = true;
        profile.schwarzbierFuelLastLap = 5;
        profile.grillSpezialUsed = true;
        profile.grillSpezialHeatPaidThisTurn = 2;
        profile.brothSelection = BrothType.Miso;
        profile.bankuruwaseActive = true;
        profile.bankuruwaseTurnsLeft = 3;
        profile.landmark1PassCount = profile.landmark2PassCount = profile.totalRepairs = 4;
        profile.landmark1UltUsed = profile.landmark2UltUsed = true;
        profile.dimSumPlayedTrick = profile.dimSumPlayedSpeed = profile.dimSumPaidHeat = true;
        profile.sunNeverSetsTarget = TeamId.CN;
    }

    private static void AssertUsageReset(TechTreeState profile, BrothType broth = BrothType.None)
    {
        Assert.IsFalse(profile.heatReductionUsedThisLap);
        Assert.IsFalse(profile.fishAndChipsUsed);
        Assert.AreEqual(-1, profile.schwarzbierFuelLastLap);
        Assert.IsFalse(profile.grillSpezialUsed);
        Assert.AreEqual(0, profile.grillSpezialHeatPaidThisTurn);
        Assert.AreEqual(broth, profile.brothSelection);
        Assert.IsFalse(profile.bankuruwaseActive);
        Assert.AreEqual(0, profile.bankuruwaseTurnsLeft);
        Assert.AreEqual(0, profile.landmark1PassCount);
        Assert.AreEqual(0, profile.landmark2PassCount);
        Assert.AreEqual(0, profile.totalRepairs);
        Assert.IsFalse(profile.landmark1UltUsed);
        Assert.IsFalse(profile.landmark2UltUsed);
        Assert.IsFalse(profile.dimSumPlayedTrick);
        Assert.IsFalse(profile.dimSumPlayedSpeed);
        Assert.IsFalse(profile.dimSumPaidHeat);
    }

    private void AssertSetup(PlayerState player, TeamId team)
    {
        Assert.AreEqual(team, player.teamId);
        Assert.AreEqual(team == TeamId.CN, player.usesChinaGearSystem);
        int basePool = TeamVehicleRules.GetBaseHeatPoolSize(team, config.heatPoolPerPlayer);
        Assert.AreEqual(session.EffectiveHeatPoolSize(player, basePool), player.deck.heatPool.remaining);
        Assert.AreEqual(session.EffectiveHandSize(player, config.handSize), player.deck.Hand.Count);
        Assert.AreEqual(0, player.deck.CountPermanentHeatOutsideEngine());
        Assert.IsFalse(player.trickState.schwarzbrotActive);
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void HumanReinitializationResetsUsageRetainsPermanentProfileAndReplacesDeck(TeamId team)
    {
        var profile = Profile(team);
        InjectCachedProfile(profile);
        string permanentBefore = TechTreeProfileCodec.Encode(profile);
        var player = new PlayerState("human", false, 0, 1);
        Setup(player, team);
        Assert.AreSame(profile, player.techState);
        AssertSetup(player, team);
        var previousPool = player.deck.heatPool;
        var previousCard = player.deck.Hand[0];
        var previousTricks = player.trickState;
        player.deck.DrawHeatFromPoolToHand(2);
        player.trickState.schwarzbrotActive = true;
        DirtyUsage(profile);

        Setup(player, team);
        Assert.AreSame(profile, player.techState);
        AssertUsageReset(profile);
        AssertSetup(player, team);
        Assert.AreEqual(team == TeamId.UK ? (TeamId?)TeamId.IT : null, profile.sunNeverSetsTarget);
        Assert.AreEqual(permanentBefore, TechTreeProfileCodec.Encode(profile));
        Assert.AreNotSame(previousPool, player.deck.heatPool);
        Assert.AreNotSame(previousTricks, player.trickState);
        CollectionAssert.DoesNotContain(player.deck.Hand, previousCard);
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void AiInitializationOwnsFreshDemoStateNotTheHumanCache(TeamId team)
    {
        var humanProfile = Profile(team);
        DirtyUsage(humanProfile);
        InjectCachedProfile(humanProfile);
        string permanentBefore = TechTreeProfileCodec.Encode(humanProfile);
        var player = new PlayerState("AI", true, 0, 1);
        Setup(player, team);
        var first = player.techState;
        Assert.AreNotSame(humanProfile, first);
        DirtyUsage(first);
        Setup(player, team);
        Assert.AreNotSame(first, player.techState);
        AssertUsageReset(player.techState);
        AssertSetup(player, team);
        Assert.IsNull(player.techState.sunNeverSetsTarget, "Demo owns L1, not UK L3");
        Assert.AreSame(humanProfile, cache[team]);
        Assert.IsTrue(humanProfile.fishAndChipsUsed);
        Assert.AreEqual(permanentBefore, TechTreeProfileCodec.Encode(humanProfile));
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void CareerSnapshotCreatesIndependentRuntimeOnEveryInitialization(TeamId team)
    {
        var source = Profile(team);
        if (team == TeamId.UK) source.sunNeverSetsTarget = TeamId.CN;
        Assert.IsTrue(CareerTechSnapshotMapper.TryCapture(source, out var snapshot));
        string permanentBefore = TechTreeProfileCodec.Encode(source);
        config.enableTechTree = false; // The supplied career snapshot retains precedence.
        var player = new PlayerState("career", false, 0, 1);
        Setup(player, team, snapshot);
        var first = player.techState;
        Assert.AreNotSame(source, first);
        DirtyUsage(first);
        first.rpBalance = 1;
        first.activeNodeIds.Clear();
        Setup(player, team, snapshot);
        Assert.AreNotSame(first, player.techState);
        Assert.AreEqual(12345, player.techState.rpBalance);
        CollectionAssert.AreEquivalent(source.activeNodeIds, player.techState.activeNodeIds);
        AssertUsageReset(player.techState);
        AssertSetup(player, team);
        Assert.AreEqual(team == TeamId.UK ? (TeamId?)TeamId.IT : null, player.techState.sunNeverSetsTarget);
        Assert.AreEqual(team == TeamId.UK ? (TeamId?)TeamId.CN : null, snapshot.SunNeverSetsTarget);
        Assert.AreEqual(permanentBefore, TechTreeProfileCodec.Encode(source));
    }

    [TestCase("IT", 8, 8)] [TestCase("US", 7, 7)]
    [TestCase("GB", 7, 7)] [TestCase("FR", 7, 7)] [TestCase(null, 7, 7)]
    public void UkInitializationUsesCurrentTrackBeforeOpeningCapacityAndHand(
        string country, int expectedPool, int expectedHand)
    {
        InjectCachedProfile(Profile(TeamId.UK));
        var player = new PlayerState("UK", false, 0, 1);
        Setup(player, TeamId.UK);
        SetCountry(country);
        Setup(player, TeamId.UK);
        Assert.AreEqual(expectedPool, player.deck.heatPool.remaining);
        Assert.AreEqual(expectedHand, player.deck.Hand.Count);
        Assert.AreEqual(TechTreeRules.ResolveSunNeverSetsTarget(country), player.techState.sunNeverSetsTarget);
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void DisabledTechnologyDropsOldRuntimeWithoutTouchingCachedProfile(TeamId team)
    {
        var profile = Profile(team);
        DirtyUsage(profile);
        InjectCachedProfile(profile);
        config.enableTechTree = false;
        var player = new PlayerState("disabled", false, 0, 1) { techState = profile };
        Setup(player, team);
        Assert.IsNull(player.techState);
        AssertSetup(player, team);
        Assert.IsTrue(profile.fishAndChipsUsed);
        Assert.AreEqual(TeamId.CN, profile.sunNeverSetsTarget);
    }

    [TestCase(TeamId.CN)] [TestCase(TeamId.US)]
    public void TutorialSetupIgnoresCacheAndCareerSnapshotAndKeepsExactOpening(TeamId team)
    {
        var profile = Profile(team);
        DirtyUsage(profile);
        InjectCachedProfile(profile);
        Assert.IsTrue(CareerTechSnapshotMapper.TryCapture(Profile(team), out var snapshot));
        var scenario = TutorialScenarioDefinition.CreateTeamSpecialty(team);
        SetField("tutorialScenario", scenario);
        var player = new PlayerState("tutorial", false, 0, 1) { techState = profile };
        Setup(player, team, snapshot);
        var firstOrder = new List<CardData>(player.deck.Hand).ConvertAll(card => card.type + ":" + card.value + ":" + card.trickId);
        Setup(player, team, snapshot);
        Assert.IsNull(player.techState);
        Assert.AreEqual(scenario.engineHeatCapacity, player.deck.heatPool.remaining);
        Assert.AreEqual(scenario.openingHandSize, player.deck.Hand.Count);
        CollectionAssert.AreEqual(firstOrder, new List<CardData>(player.deck.Hand).ConvertAll(card => card.type + ":" + card.value + ":" + card.trickId));
        Assert.IsTrue(profile.fishAndChipsUsed);
    }

    [Test]
    public void InvalidCareerSnapshotFailsClosedWithoutFallingBackToNormalCache()
    {
        var cached = Profile(TeamId.UK);
        InjectCachedProfile(cached);
        Assert.IsTrue(CareerTechSnapshot.TryCreate(TeamId.UK, 50,
            new[] { "unknown-node" }, new[] { "unknown-node" }, null, out var snapshot));
        var player = new PlayerState("invalid", false, 0, 1) { techState = cached };
        LogAssert.Expect(LogType.Error, "[CAREER] Invalid technology snapshot; career race has no active technology.");
        Setup(player, TeamId.UK, snapshot);
        Assert.IsNull(player.techState);
        AssertSetup(player, TeamId.UK);
        Assert.AreEqual("科技快照无法映射到当前科技数据库",
            typeof(MVPGameManager).GetField("careerInitializationFailure", Private).GetValue(manager));
        Assert.AreSame(cached, cache[TeamId.UK]);
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void OpeningAttackAssistRetainsTeamGateAndCardConservation(TeamId team)
    {
        config.enableTechTree = false;
        config.enableTrickCards = true;
        config.ensurePlayerAttackTrickInOpeningHand = true;
        config.handSize = 1;
        var player = new PlayerState("opening", false, 0, 1);
        Setup(player, team);
        Assert.AreEqual(1, player.deck.HandCount);
        Assert.AreEqual(16, player.deck.HandCount + player.deck.DrawPileCount);
        Assert.AreEqual(0, player.deck.DiscardPileCount);
        Assert.AreEqual(0, player.deck.CountPermanentHeatOutsideEngine());
        if (team == TeamId.CN)
            Assert.AreEqual(session.TrickDb.GetAttackId(team), player.deck.Hand[0].trickId);
        foreach (var card in player.deck.Hand)
            if (card.IsTrick) Assert.IsNotNull(session.TrickDb.Get(card.trickId));
    }

    [Test]
    public void UnavailableHumanChinaAssistWarnsWithoutSynthesizingACard()
    {
        config.enableTechTree = false;
        config.ensurePlayerAttackTrickInOpeningHand = true;
        LogAssert.Expect(LogType.Warning,
            "[MVPGameManager] 无法保证中国队 ATTACK 牌 cn-hotpot-base 进入开局手牌。");
        var player = new PlayerState("no tricks", false, 0, 1);
        Setup(player, TeamId.CN);
        Assert.AreEqual(12, player.deck.HandCount + player.deck.DrawPileCount);
        foreach (var card in player.deck.Hand) Assert.IsFalse(card.IsTrick);
    }

    [Test]
    public void ChinaAiAndTutorialDoNotAttemptUnavailableOpeningAssist()
    {
        config.enableTechTree = false;
        config.ensurePlayerAttackTrickInOpeningHand = true;
        var ai = new PlayerState("ai", true, 0, 1);
        Setup(ai, TeamId.CN);
        foreach (var card in ai.deck.Hand) Assert.IsFalse(card.IsTrick);
        var scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.CN);
        SetField("tutorialScenario", scenario);
        var player = new PlayerState("tutorial", false, 0, 1);
        Setup(player, TeamId.CN);
        CollectionAssert.AreEqual(
            scenario.CreateExactDeck().GetRange(0, scenario.openingHandSize).ConvertAll(c => c.type + ":" + c.value + ":" + c.trickId),
            new List<CardData>(player.deck.Hand).ConvertAll(c => c.type + ":" + c.value + ":" + c.trickId));
        LogAssert.NoUnexpectedReceived();
    }

    [TestCase(TeamId.JP)] [TestCase(TeamId.UK)]
    public void NormalBrothDemoSelectionRunsAfterResetAndVirtualTrackBinding(TeamId team)
    {
        var profile = Profile(team);
        DirtyUsage(profile);
        InjectCachedProfile(profile);
        config.jpDemoBroth = BrothType.Shio;
        SetCountry("JP");
        var player = new PlayerState("broth", false, 0, 1);
        Setup(player, team);
        AssertUsageReset(profile, BrothType.Shio);
        AssertSetup(player, team);
        Assert.IsTrue(session.GetModifiers(player).hasBrothSelection);
        Assert.AreEqual(team == TeamId.UK ? (TeamId?)TeamId.JP : null, profile.sunNeverSetsTarget);
    }
}

/// <summary>Pure setup boundary; the caller supplies storage and feedback.</summary>
public class RaceParticipantTechnologySetupTests
{
    private static TechTreeState UnexpectedProfile(TeamId team) =>
        throw new System.InvalidOperationException("Profile storage must not be read");

    private static void UnexpectedFailure() =>
        throw new System.InvalidOperationException("No career failure expected");

    [TestCase(TeamId.CN)]
    [TestCase(TeamId.US)]
    public void TutorialTakesPrecedenceOverTechnologyAndUsesExactHeatCapacity(TeamId team)
    {
        var tutorial = TutorialScenarioDefinition.CreateTeamSpecialty(team);
        var player = new PlayerState("tutorial", false, 0, 1)
        {
            techState = new TechTreeState(team, 10)
        };
        var session = new RaceSession(new SystemRandomSource(1));

        int pool = RaceParticipantTechnologySetup.Prepare(player, team, tutorial,
            null, true, 99, BrothType.Shio, session, "JP",
            UnexpectedProfile, UnexpectedFailure);

        Assert.AreEqual(tutorial.engineHeatCapacity, pool);
        Assert.AreEqual(team, player.teamId);
        Assert.AreEqual(team == TeamId.CN, player.usesChinaGearSystem);
        Assert.IsNull(player.techState);
    }

    [Test]
    public void DisabledNormalTechnologyDoesNotLoadProfileOrChangeBaseCapacity()
    {
        var player = new PlayerState("normal", false, 0, 1)
        {
            techState = new TechTreeState(TeamId.UK, 10)
        };
        var session = new RaceSession(new SystemRandomSource(1));

        int pool = RaceParticipantTechnologySetup.Prepare(player, TeamId.CN, null,
            null, false, 6, BrothType.None, session, "CN",
            UnexpectedProfile, UnexpectedFailure);

        Assert.AreEqual(TeamVehicleRules.GetBaseHeatPoolSize(TeamId.CN, 6), pool);
        Assert.AreEqual(TeamId.CN, player.teamId);
        Assert.IsTrue(player.usesChinaGearSystem);
        Assert.IsNull(player.techState);
    }

    [Test]
    public void HumanProfileIsLoadedOnceAndTransientUsageIsReset()
    {
        var profile = new TechTreeState(TeamId.UK, 10)
        {
            fishAndChipsUsed = true
        };
        var player = new PlayerState("human", false, 0, 1);
        var session = new RaceSession(new SystemRandomSource(1));
        int loads = 0;

        int pool = RaceParticipantTechnologySetup.Prepare(player, TeamId.UK, null,
            null, true, 6, BrothType.None, session, "IT",
            team => { Assert.AreEqual(TeamId.UK, team); loads++; return profile; },
            UnexpectedFailure);

        Assert.AreEqual(1, loads);
        Assert.AreSame(profile, player.techState);
        Assert.IsFalse(profile.fishAndChipsUsed);
        Assert.AreEqual(session.EffectiveHeatPoolSize(player,
            TeamVehicleRules.GetBaseHeatPoolSize(TeamId.UK, 6)), pool);
    }

    [Test]
    public void InvalidCareerSnapshotFailsClosedWithoutLoadingHumanProfile()
    {
        Assert.IsTrue(CareerTechSnapshot.TryCreate(TeamId.UK, 50,
            new[] { "unknown-node" }, new[] { "unknown-node" }, null,
            out var snapshot));
        var player = new PlayerState("career", false, 0, 1)
        {
            techState = new TechTreeState(TeamId.UK, 10)
        };
        var session = new RaceSession(new SystemRandomSource(1));
        int failures = 0;

        int pool = RaceParticipantTechnologySetup.Prepare(player, TeamId.UK, null,
            snapshot, true, 6, BrothType.None, session, "IT",
            UnexpectedProfile, () => failures++);

        Assert.AreEqual(1, failures);
        Assert.IsNull(player.techState);
        Assert.AreEqual(TeamVehicleRules.GetBaseHeatPoolSize(TeamId.UK, 6), pool);
    }

    [Test]
    public void ValidCareerSnapshotOverridesDisabledNormalTechnologyWithoutReadingProfile()
    {
        var session = new RaceSession(new SystemRandomSource(1));
        var source = new TechTreeState(TeamId.UK, 37) { fishAndChipsUsed = true };
        foreach (var node in session.TechDb.GetUniqueInTier(TeamId.UK, TechTreeTier.L1))
        {
            source.unlockedNodeIds.Add(node.id);
            source.activeNodeIds.Add(node.id);
            break;
        }
        Assert.IsTrue(CareerTechSnapshotMapper.TryCapture(source, out var snapshot));
        var player = new PlayerState("career", false, 0, 1);

        int pool = RaceParticipantTechnologySetup.Prepare(player, TeamId.UK, null,
            snapshot, false, 6, BrothType.None, session, "IT",
            UnexpectedProfile, UnexpectedFailure);

        Assert.IsNotNull(player.techState);
        Assert.AreNotSame(source, player.techState);
        Assert.AreEqual(37, player.techState.rpBalance);
        CollectionAssert.AreEquivalent(source.activeNodeIds, player.techState.activeNodeIds);
        Assert.IsFalse(player.techState.fishAndChipsUsed);
        Assert.IsTrue(source.fishAndChipsUsed);
        Assert.AreEqual(session.EffectiveHeatPoolSize(player,
            TeamVehicleRules.GetBaseHeatPoolSize(TeamId.UK, 6)), pool);
    }

    [Test]
    public void AiUsesNewDemoProfileWithoutReadingHumanStorage()
    {
        var session = new RaceSession(new SystemRandomSource(1));
        var prior = new TechTreeState(TeamId.CN, 0);
        var player = new PlayerState("opponent", true, 0, 1) { techState = prior };

        int pool = RaceParticipantTechnologySetup.Prepare(player, TeamId.CN, null,
            null, true, 6, BrothType.None, session, "CN",
            UnexpectedProfile, UnexpectedFailure);

        Assert.AreEqual(TeamId.CN, player.teamId);
        Assert.IsTrue(player.usesChinaGearSystem);
        Assert.IsNotNull(player.techState);
        Assert.AreNotSame(prior, player.techState);
        Assert.AreEqual(TeamId.CN, player.techState.teamId);
        Assert.IsNotEmpty(player.techState.activeNodeIds);
        Assert.AreEqual(session.EffectiveHeatPoolSize(player,
            TeamVehicleRules.GetBaseHeatPoolSize(TeamId.CN, 6)), pool);
    }

    [Test]
    public void JapaneseDemoBrothIsSelectedAfterRaceUsageReset()
    {
        var session = new RaceSession(new SystemRandomSource(1));
        var profile = new TechTreeState(TeamId.JP, 0)
        {
            brothSelection = BrothType.Miso,
            fishAndChipsUsed = true
        };
        foreach (var tier in new[] { TechTreeTier.L1, TechTreeTier.L2, TechTreeTier.L3 })
            foreach (var node in session.TechDb.GetUniqueInTier(TeamId.JP, tier))
            {
                profile.unlockedNodeIds.Add(node.id);
                profile.activeNodeIds.Add(node.id);
            }
        var player = new PlayerState("jp", false, 0, 1);

        RaceParticipantTechnologySetup.Prepare(player, TeamId.JP, null,
            null, true, 6, BrothType.Shio, session, "JP",
            team => { Assert.AreEqual(TeamId.JP, team); return profile; },
            UnexpectedFailure);

        Assert.AreSame(profile, player.techState);
        Assert.IsFalse(profile.fishAndChipsUsed);
        Assert.AreEqual(BrothType.Shio, profile.brothSelection);
    }
}
