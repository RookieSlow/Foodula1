using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// I05a; locked technology draft paragraphs 291-295. Actual participant technology
/// preparation and deck construction, with caller-owned profiles/snapshots only.
/// No PlayerPrefs, scene startup or complete-mode acceptance.
/// </summary>
public class ItalyL2RuntimeTests
{
    private const string Lasagna = "it-l2-lasagna";
    private const string Pizza = "it-l1-pizza-sottile";
    private RaceSession race;
    private GameConfigSO config;
    private UnityEngine.Random.State previousRandom;

    [SetUp]
    public void SetUp()
    {
        previousRandom = UnityEngine.Random.state;
        race = new RaceSession(new SystemRandomSource(19));
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.handSize = 7;
        config.heatPoolPerPlayer = 6;
        config.enableTechTree = true;
        config.enableTrickCards = false;
        config.speedCardDistribution = new[] { 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4 };
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Random.state = previousRandom;
        Object.DestroyImmediate(config);
    }

    [TestCase(false, false)] [TestCase(false, true)]
    [TestCase(true, false)] [TestCase(true, true)]
    public void OrdinaryAssemblyAddsOneHandCardWithoutEngineOrSpinBonus(bool active, bool pizza)
    {
        TechTreeState profile = LegalItalianProfile(active, pizza);
        string before = TechTreeProfileCodec.Encode(profile);
        int loads = 0;
        PlayerState player = CreatePlayer(TeamId.IT);
        int capacity = Prepare(player, null, null, true, _ => { loads++; return profile; });
        race.InitializeRaceDeck(player, config, capacity);

        Assert.That(loads, Is.EqualTo(1));
        Assert.That(player.techState, Is.SameAs(profile));
        Assert.That(player.deck.HandCount, Is.EqualTo(active ? 8 : 7), "Pizza never doubles the L2 hand effect.");
        Assert.That(capacity, Is.EqualTo(6));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(6));
        Assert.That(race.EffectiveSpinMax(player), Is.EqualTo(3));
        Assert.That(RaceSpinRules.Evaluate(2, WeatherType.Sunny, race.EffectiveSpinMax(player)).Eliminated, Is.True);
        Assert.That(player.deck.CountPermanentHeatOutsideEngine(), Is.Zero);
        Assert.That(player.deck.HandCount + player.deck.DrawPileCount, Is.EqualTo(12));
        Assert.That(CardPlayRules.GetSpeedCardRequirement(player).RequiredCardCount, Is.EqualTo(4), "Hand depth does not expand G4 slots.");
        Assert.That(TechTreeProfileCodec.Encode(profile), Is.EqualTo(before));
    }

    [Test]
    public void IndependentCommonDurabilitySurvivesHandOnlyLasagna()
    {
        TechTreeState profile = LegalItalianProfile(true, false);
        TechTreeRules.SelectActiveNodes(profile, new[] { Lasagna, "common-l1-expanded-tank" }, race.TechDb);
        PlayerState player = CreatePlayer(TeamId.IT);
        int capacity = Prepare(player, null, null, true, _ => profile);
        race.InitializeRaceDeck(player, config, capacity);
        Assert.That(player.deck.HandCount, Is.EqualTo(8));
        Assert.That(capacity, Is.EqualTo(7), "The independent researched common bonus remains; L2 itself adds no engine heat.");
        Assert.That(race.GetModifiers(player).engineCapacityBonus, Is.Zero);
        Assert.That(race.GetModifiers(player).durabilityBonus, Is.EqualTo(1));
        Assert.That(race.EffectiveSpinMax(player), Is.EqualTo(3));
    }

    [TestCase(false)] [TestCase(true)]
    public void BritishBorrowingDeduplicatesLegacyExplicitNodeAndNeverAddsForeignEngine(bool legacyExplicit)
    {
        var profile = new TechTreeState(TeamId.UK, 100000);
        Assert.That(TechTreeRules.UnlockNode(profile, "uk-l3-sun-never-sets", race.TechDb), Is.True);
        TechTreeRules.SelectActiveNodes(profile, new[] { "uk-l3-sun-never-sets" }, race.TechDb);
        if (legacyExplicit)
        {
            // Preserve an old explicit saved selection; this does not purchase foreign research.
            profile.unlockedNodeIds.Add(Lasagna);
            profile.activeNodeIds.Add(Lasagna);
        }
        int balance = profile.rpBalance;
        var unlocked = new HashSet<string>(profile.unlockedNodeIds);
        var active = new HashSet<string>(profile.activeNodeIds);
        PlayerState player = CreatePlayer(TeamId.UK);
        int capacity = Prepare(player, null, null, true, _ => profile);
        race.InitializeRaceDeck(player, config, capacity);

        Assert.That(player.deck.HandCount, Is.EqualTo(8));
        Assert.That(capacity, Is.EqualTo(TeamVehicleRules.GetBaseHeatPoolSize(TeamId.UK, 6)));
        Assert.That(race.GetModifiers(player).engineCapacityBonus, Is.Zero);
        Assert.That(race.GetModifiers(player).spinCounterMaxBonus, Is.Zero);
        Assert.That(race.EffectiveSpinMax(player), Is.EqualTo(3));
        Assert.That(profile.sunNeverSetsTarget, Is.EqualTo(TeamId.IT));
        CollectionAssert.AreEquivalent(unlocked, profile.unlockedNodeIds);
        CollectionAssert.AreEquivalent(active, profile.activeNodeIds);
        Assert.That(profile.rpBalance, Is.EqualTo(balance));
    }

    [TestCase(false)] [TestCase(true)]
    public void TutorialUsesAuthoredDeckAndNeverReadsExpandedNormalProfile(bool ai)
    {
        TechTreeState normal = LegalItalianProfile(true, true);
        string before = TechTreeProfileCodec.Encode(normal);
        PlayerState player = CreatePlayer(TeamId.IT, ai);
        player.techState = normal;
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.IT);
        int capacity = Prepare(player, scenario, null, true, _ => throw new InvalidOperationException("Normal profile must not be read by tutorial."));
        race.InitializeRaceDeck(player, config, capacity, scenario);

        Assert.That(player.techState, Is.Null);
        Assert.That(player.deck.HandCount, Is.EqualTo(scenario.openingHandSize));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(scenario.engineHeatCapacity));
        Assert.That(race.EffectiveSpinMax(player), Is.EqualTo(3));
        Assert.That(TechTreeProfileCodec.Encode(normal), Is.EqualTo(before));
    }

    [Test]
    public void DisabledTechnologyClearsRaceBindingWithoutReadingOrEditingNormalProfile()
    {
        TechTreeState normal = LegalItalianProfile(true, false);
        string before = TechTreeProfileCodec.Encode(normal);
        PlayerState player = CreatePlayer(TeamId.IT);
        player.techState = normal;
        int capacity = Prepare(player, null, null, false, _ => throw new InvalidOperationException("Disabled technology must not load a profile."));
        race.InitializeRaceDeck(player, config, capacity);
        Assert.That(player.techState, Is.Null);
        Assert.That(player.deck.HandCount, Is.EqualTo(7));
        Assert.That(capacity, Is.EqualTo(6));
        Assert.That(race.EffectiveSpinMax(player), Is.EqualTo(3));
        Assert.That(TechTreeProfileCodec.Encode(normal), Is.EqualTo(before));
    }

    [TestCase(false)] [TestCase(true)]
    public void CareerSnapshotOwnsHandDepthAndReassemblyWithoutNormalProfileAccess(bool active)
    {
        TechTreeState careerSource = LegalItalianProfile(active, true);
        Assert.That(CareerTechSnapshotMapper.TryCapture(careerSource, out CareerTechSnapshot snapshot), Is.True);
        int balance = snapshot.RpBalance;
        var frozenActive = snapshot.ActiveNodeIds.ToArray();
        careerSource.activeNodeIds.Clear();
        careerSource.rpBalance = 0;

        PlayerState first = CreatePlayer(TeamId.IT);
        int firstCapacity = Prepare(first, null, snapshot, true, _ => throw new InvalidOperationException("Career must not read normal RP/technology."));
        race.InitializeRaceDeck(first, config, firstCapacity);
        Assert.That(first.deck.HandCount, Is.EqualTo(active ? 8 : 7));
        Assert.That(firstCapacity, Is.EqualTo(6));
        Assert.That(race.EffectiveSpinMax(first), Is.EqualTo(3));
        first.techState.activeNodeIds.Clear();
        first.deck.DrawHeatFromPoolToHand(1);

        PlayerState second = CreatePlayer(TeamId.IT);
        int secondCapacity = Prepare(second, null, snapshot, true, _ => throw new InvalidOperationException("Career replay must not read a normal profile."));
        race.InitializeRaceDeck(second, config, secondCapacity);
        Assert.That(second.deck.HandCount, Is.EqualTo(active ? 8 : 7));
        Assert.That(second.deck.heatPool.remaining, Is.EqualTo(6));
        Assert.That(second.techState, Is.Not.SameAs(first.techState));
        Assert.That(second.deck.heatPool, Is.Not.SameAs(first.deck.heatPool));
        Assert.That(snapshot.RpBalance, Is.EqualTo(balance));
        CollectionAssert.AreEquivalent(frozenActive, snapshot.ActiveNodeIds);
        CollectionAssert.AreEquivalent(frozenActive, second.techState.activeNodeIds);
    }

    private TechTreeState LegalItalianProfile(bool active, bool pizza)
    {
        var profile = new TechTreeState(TeamId.IT, 100000);
        foreach (TechNodeDef node in race.TechDb.GetCommonInTier(TechTreeTier.L1))
            Assert.That(TechTreeRules.UnlockNode(profile, node.id, race.TechDb), Is.True, node.id);
        foreach (TechNodeDef node in race.TechDb.GetCommonInTier(TechTreeTier.L2).Take(3))
            Assert.That(TechTreeRules.UnlockNode(profile, node.id, race.TechDb), Is.True, node.id);
        Assert.That(TechTreeRules.UnlockNode(profile, Lasagna, race.TechDb), Is.True);
        if (pizza) Assert.That(TechTreeRules.UnlockNode(profile, Pizza, race.TechDb), Is.True);
        var selected = new List<string>();
        if (active) selected.Add(Lasagna);
        if (pizza) selected.Add(Pizza);
        TechTreeRules.SelectActiveNodes(profile, selected, race.TechDb);
        return profile;
    }

    private static PlayerState CreatePlayer(TeamId team, bool ai = false)
        => new PlayerState("owned I05a participant", ai, 0, 4) { teamId = team };

    private int Prepare(PlayerState player, TutorialScenarioDefinition tutorial, CareerTechSnapshot career,
        bool enabled, Func<TeamId, TechTreeState> load)
        => RaceParticipantTechnologySetup.Prepare(player, player.teamId, tutorial, career, enabled,
            6, BrothType.None, race, "IT", load,
            () => Assert.Fail("The owned career snapshot must be valid."));
}
