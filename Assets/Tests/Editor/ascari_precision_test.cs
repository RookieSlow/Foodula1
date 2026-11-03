using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>D05 selected-original commit, owner-input lifetime, first-apex tail and forecast isolation.</summary>
public class AscariPrecisionTests
{
    private RaceSession race;
    private PlayerState player;
    private GameConfigSO config;
    private List<CardData> selected;
    private List<TrackNode> nodes;
    private CardData two, four;
    private const string L3 = "it-l3-cavallino-rampante";

    [SetUp]
    public void SetUp()
    {
        race = new RaceSession { TeamVehicleBonusesEnabled = false };
        player = new PlayerState("owned Ascari", false, 0, 2) { teamId = TeamId.IT,
            driverId = "it_alberto_ascari", techState = new TechTreeState(TeamId.IT, 50000) };
        player.driverSkill.Initialize(player.DriverProfile, 3, true);
        two = new CardData(CardType.Speed, 2); four = new CardData(CardType.Speed, 4);
        player.deck.InitializeExactOrder(new[] { two, four, new CardData(CardType.Speed, 1) }, new HeatPool(6));
        player.deck.DrawToHand(3); selected = new List<CardData> { two, four };
        race.Players.Add(player); race.BeginTurn(player, 1);
        Assert.That(race.BeginDriverSkillInput(player), Is.True);
        nodes = new List<TrackNode>(); for (int i = 0; i < 20; i++) nodes.Add(new TrackNode(i, 99));
        nodes[3] = new TrackNode(3, 5, "first", 9, false, true);
        nodes[6] = new TrackNode(6, 3, "tail", 2, false, true);
        config = ScriptableObject.CreateInstance<GameConfigSO>(); config.totalLaps = 3;
        config.gearOneCooldown = 0; config.gearTwoCooldown = 0; config.enableTechTree = true;
    }
    [TearDown] public void TearDown() => Object.DestroyImmediate(config);

    private AscariPrecisionActivationPlan Prepare(CardData target = null, ItalyPrecisionTuneActivationPlan tech = null)
    {
        Assert.That(race.TryPrepareAscariPrecision(player, selected, target ?? two, 1, 7, true,
            nodes, 3, out var plan, out string reason, tech), Is.True, reason);
        return plan;
    }
    private SpeedCardCommitResult Commit(AscariPrecisionActivationPlan plan, ItalyPrecisionTuneActivationPlan tech = null,
        int turn = 1, int request = 7, IReadOnlyList<TrackNode> path = null)
        => CardPlayRules.CommitSpeedCards(player, selected, 4, race, 20, tech, turn, 9, true,
            plan, request, path ?? nodes, 3);

    private void EnableTech()
    {
        Assert.That(TechTreeRules.UnlockNode(player.techState, L3, race.TechDb), Is.True);
        TechTreeRules.SelectActiveNodes(player.techState, new[] { L3 }, race.TechDb);
        Assert.That(race.BeginItalyPrecisionTuneInput(player, 1), Is.True);
    }
    private ItalyPrecisionTuneActivationPlan PrepareTech(CardData target, CardData driverTarget = null)
    {
        Assert.That(race.TryPrepareItalyPrecisionTune(player, selected, target, 1, 9, true, true,
            out var tech, out string reason, driverTarget), Is.True, reason);
        return tech;
    }

    [TestCase(3, 1, 2)] [TestCase(5, 1, 3)] [TestCase(7, 2, 3)]
    public void TargetCommitSpendsOneOriginalUseAndEarnsOnlyOneSafeFirstApexTail(int level, int uses, int reward)
    {
        player.driverSkill.Initialize(player.DriverProfile, level, true);
        race.BeginDriverSkillInput(player);
        var free = Prepare(); free.Cancel();
        Assert.That(Commit(free), Is.EqualTo(SpeedCardCommitResult.AscariPrecisionExpired));
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
        Assert.That(Commit(Prepare()), Is.EqualTo(SpeedCardCommitResult.Success));
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
        Assert.That(player.driverSkill.Precision.CommittedTarget, Is.SameAs(two));
        Assert.That(two.value, Is.EqualTo(2)); Assert.That(four.value, Is.EqualTo(4));
        player.cornerTotalThisTurn = RaceMovementRules.ComputeCornerSpeed(player, null, false);
        Assert.That(player.cornerTotalThisTurn, Is.EqualTo(5));
        Assert.That(DriverSkillRules.GetCornerLimitBonus(player.driverSkill), Is.Zero);
        Assert.That(DriverSkillRules.ReduceCornerHeat(player.driverSkill, 2), Is.EqualTo(2));
        Assert.That(player.driverSkill.Precision.ConsumeSettledReward(race, player, nodes, 0, 0, 3, 5,
            id => id == 9 ? 5 : 3), Is.EqualTo(reward));
        Assert.That(player.driverSkill.Precision.ConsumeSettledReward(race, player, nodes, 0, 0, 3, 5, _ => 99), Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(6));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void OrdinaryUniqueOriginalTargetIsRequiredWithoutSpendingAUse(int kind)
    {
        CardData target = two;
        if (kind == 0) target = player.deck.Hand[2];
        if (kind == 1) { target = CardData.CreateTempHeat(); player.deck.AddCardsToHand(new[] { target }); selected.Add(target); }
        if (kind == 2) target = new CardData(CardType.Speed, 2);
        if (kind == 3) selected.Add(two);
        if (kind == 4) two.isTemp = true;
        Assert.That(race.TryPrepareAscariPrecision(player, selected, target, 1, 7, true, nodes, 3,
            out _, out _), Is.False);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(1));
        Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void StaleIntentRejectsTheWholeCardGroupBeforeMutation(int change)
    {
        var plan = Prepare(); int turn = 1, request = 7;
        IReadOnlyList<TrackNode> path = nodes;
        if (change == 0) turn++;
        if (change == 1) request++;
        if (change == 2) player.gear++;
        if (change == 3) player.position++;
        if (change == 4) player.lap++;
        if (change == 5) two.value++;
        if (change == 6) player.driverSkill.InterruptDrivingInput();
        if (change == 7) { player.driverSkill.Initialize(player.DriverProfile, 3, true); race.BeginDriverSkillInput(player); }
        if (change == 8) selected.Reverse();
        if (change == 9) path = new List<TrackNode>(nodes);
        if (change == 10) plan.Cancel();
        Assert.That(Commit(plan, null, turn, request, path), Is.EqualTo(SpeedCardCommitResult.AscariPrecisionExpired));
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(1));
        Assert.That(player.deck.HandCount, Is.EqualTo(3));
        Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
    }

    [Test]
    public void TechnologyAndDriverRejectSameOriginalAndAtomicallyCommitDifferentTargets()
    {
        EnableTech(); var same = PrepareTech(two);
        Assert.That(race.TryPrepareAscariPrecision(player, selected, two, 1, 7, true, nodes, 3,
            out _, out _, same), Is.False);
        same.Cancel(); var driver = Prepare();
        Assert.That(race.TryPrepareItalyPrecisionTune(player, selected, two, 1, 9, true, true,
            out _, out _, driver.Target), Is.False);
        var tech = PrepareTech(four, driver.Target);
        Assert.That(Commit(driver, tech), Is.EqualTo(SpeedCardCommitResult.Success));
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.True);
        Assert.That(player.driverSkill.UsesRemaining, Is.Zero);
        Assert.That(RaceMovementRules.ComputeCornerSpeed(player, null, false), Is.EqualTo(4));
        Assert.That(two.value, Is.EqualTo(2)); Assert.That(four.value, Is.EqualTo(4));
        Assert.That(player.deck.HandCount, Is.EqualTo(1));
        Assert.That(player.techState.rpBalance, Is.EqualTo(38000));
    }

    [Test]
    public void AddingTechnologyAfterDriverRechecksReducedRouteAndCommitsNeitherWhenApexBecomesUnreachable()
    {
        EnableTech(); nodes[3].isApex = false; nodes[5] = new TrackNode(5, 99, "first", 9, false, true);
        var driver = Prepare(); var tech = PrepareTech(four, two);
        Assert.That(Commit(driver, tech), Is.EqualTo(SpeedCardCommitResult.AscariPrecisionExpired));
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.False);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(1));
        Assert.That(player.deck.HandCount, Is.EqualTo(3));
        Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void FirstApexMustBeActuallyReachedSafeAndNonTerminalWithoutReplacementByLaterSafeApex(int failure)
    {
        Assert.That(Commit(Prepare()), Is.EqualTo(SpeedCardCommitResult.Success));
        player.cornerTotalThisTurn = 5;
        if (failure == 1) player.hasFinished = true;
        if (failure == 2) player.skipNextTurn = true;
        int actual = failure == 3 ? 2 : 5;
        Assert.That(player.driverSkill.Precision.ConsumeSettledReward(race, player, nodes, 0, 0, 3, actual,
            id => id == 9 ? 4 : 99), Is.Zero);
        Assert.That(player.driverSkill.UsesRemaining, Is.Zero);
        if (failure == 0)
            Assert.That(player.driverSkill.Precision.ConsumeSettledReward(race, player, nodes, 0, 0, 3, 8, _ => 99), Is.Zero);
    }

    [Test]
    public void OrderedFirstApexIsNotLowestIdAndFinalFinishCannotPreborrowTail()
    {
        Assert.That(AscariPrecisionRules.TryGetFirstBaseApex(nodes, 0, 8, 3, out int first, out int distance), Is.True);
        Assert.That(first, Is.EqualTo(9)); Assert.That(distance, Is.EqualTo(3));
        nodes[2].isStartFinish = true;
        Assert.That(AscariPrecisionRules.TryGetFirstBaseApex(nodes, 0, 8, 1, out _, out _), Is.False);
        nodes[2].isStartFinish = false; nodes[3].isApex = false;
        Assert.That(race.TryPrepareAscariPrecision(player, selected, two, 1, 7, true, nodes, 3, out _, out _), Is.False,
            "Base5 cannot borrow the reward to reach its first apex at6.");
    }

    [Test]
    public void ForecastProjectsCopiedTargetAndTailAndPaysNewApexOnlyInCopiedDeck()
    {
        var plan = Prepare(); int uses = player.driverSkill.UsesRemaining;
        RaceTurnPreview preview = RaceTurnPreviewRules.Evaluate(player, race, config, selected, false, 2,
            (a, b) => TrackRules.GetUniqueApexCornersCrossed(nodes, a, b).Count > 0, null, 20,
            (a, b) => TrackRules.GetUniqueApexCornersCrossed(nodes, a, b), id => id == 9 ? 5 : 3,
            nodes, null, plan);
        Assert.That(preview.Movement.TotalCells, Is.EqualTo(7)); Assert.That(preview.CornerHeat, Is.EqualTo(2));
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
        Assert.That(player.driverSkill.Precision.CommittedTarget, Is.Null);
        Assert.That(player.driverSkill.Precision.RewardConsumed, Is.False);
        Assert.That(player.deck.HandCount, Is.EqualTo(3)); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(6));
        Assert.That(two.value, Is.EqualTo(2)); Assert.That(four.value, Is.EqualTo(4));
    }

    [TestCase(false)] [TestCase(true)]
    public void HotpotOrdinaryCardBodyCanReachSafeApexButCannotBorrowItsExtraOne(bool apexBeyondBody)
    {
        selected = new List<CardData> { two }; player.gear = 1;
        player.trickState.hotpotBaseActive = true;
        nodes[3].isApex = false; nodes[6].isApex = false;
        nodes[apexBeyondBody ? 2 : 1] = new TrackNode(apexBeyondBody ? 2 : 1, 0, "attack body", 9, false, true);
        bool prepared = race.TryPrepareAscariPrecision(player, selected, two, 1, 7, true, nodes, 3,
            out var plan, out string reason);
        Assert.That(prepared, Is.EqualTo(!apexBeyondBody), reason);
        if (apexBeyondBody)
        {
            Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(1));
            return;
        }
        var forecast = RaceTurnPreviewRules.Evaluate(player, race, config, selected, false, 1,
            (a, b) => TrackRules.GetUniqueApexCornersCrossed(nodes, a, b).Count > 0, null, 20,
            (a, b) => TrackRules.GetUniqueApexCornersCrossed(nodes, a, b), _ => 0, nodes, null, plan);
        Assert.That(forecast.Movement.TotalCells, Is.EqualTo(4), "Ordinary2-1 plus Hotpot1, then earned tail2.");
        Assert.That(forecast.CornerHeat, Is.Zero);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(1));
        Assert.That(Commit(plan), Is.EqualTo(SpeedCardCommitResult.Success));
        player.cornerTotalThisTurn = RaceMovementRules.ComputeCornerSpeed(player, null, false);
        Assert.That(player.cornerTotalThisTurn, Is.Zero);
        Assert.That(AscariPrecisionRules.GetCommittedBaseSpeed(player), Is.EqualTo(1));
        Assert.That(player.cornerTotalThisTurn + RaceMovementRules.ComputeCardAndDriverMovementBonus(player, true), Is.EqualTo(2));
        Assert.That(player.driverSkill.Precision.ConsumeSettledReward(race, player, nodes, 0, 0, 3, 1, _ => 0), Is.EqualTo(2));
        Assert.That(two.value, Is.EqualTo(2));
    }

    [Test]
    public void CleanupAndRaceReinitializationClearTargetsWithoutReturningCommittedUseUntilNewRace()
    {
        Assert.That(Commit(Prepare()), Is.EqualTo(SpeedCardCommitResult.Success));
        RaceTurnCleanup.Execute(player, null, null, null);
        Assert.That(player.driverSkill.UsesRemaining, Is.Zero);
        Assert.That(player.driverSkill.Precision.CommittedTarget, Is.Null);
        Assert.That(player.driverSkill.Precision.RewardConsumed, Is.False);
        Assert.That(player.driverSkill.Precision.InputOpen, Is.False);
        player.driverSkill.Initialize(player.DriverProfile, 7, true);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(2));
        Assert.That(player.driverSkill.Precision.InputsStarted, Is.Zero);
        Assert.That(player.driverSkill.Precision.CommittedTarget, Is.Null);
    }

    [TestCase(3)] [TestCase(5)] [TestCase(7)]
    public void OnlyQualifiedOwnerInputAdvancesAndLegacyUntargetedActivationCannotBypass(int level)
    {
        player.driverSkill.Initialize(player.DriverProfile, level, true);
        Assert.That(player.driverSkill.TryActivate(player.DriverProfile,
            new DriverSkillActivationContext(true, 2, 3, 6, 6, 0), out _), Is.False);
        Assert.That(race.BeginDriverSkillInput(player), Is.True);
        Assert.That(race.BeginDriverSkillInput(player), Is.False);
        Assert.That(player.driverSkill.Precision.InputsStarted, Is.EqualTo(1));
        player.skipNextTurn = true; race.BeginTurn(player, 2);
        Assert.That(race.BeginDriverSkillInput(player), Is.False);
        Assert.That(player.driverSkill.Precision.InputsStarted, Is.EqualTo(1));
        Assert.That(player.driverSkill.Precision.CommittedTarget, Is.Null);
        player.skipNextTurn = false; race.BeginTurn(player, 3); race.BeginDriverSkillInput(player);
        Assert.That(player.driverSkill.Precision.InputsStarted, Is.EqualTo(2));
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(level == 7 ? 2 : 1));
    }

    [TestCase(false, false)] [TestCase(true, true)]
    public void DisabledOrAiCannotOpenAscariTargetScopeOrAdvanceItsInputClock(bool ai, bool enabled)
    {
        player.isAI = ai;
        player.driverSkill.Initialize(player.DriverProfile, 3, enabled);
        race.BeginDriverSkillInput(player);
        Assert.That(player.driverSkill.Precision.InputOpen, Is.False);
        Assert.That(player.driverSkill.Precision.InputsStarted, Is.Zero);
        Assert.That(player.driverSkill.Precision.CommittedTarget, Is.Null);
        Assert.That(AscariPrecisionActivationPlan.CanPrepare(race, player, true, out _), Is.False);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(1));
        Assert.That(player.deck.HandCount, Is.EqualTo(3));
    }
}
