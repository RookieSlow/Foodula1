using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ItalyPrecisionTuneTests
{
    private RaceSession race;
    private PlayerState player;
    private GameConfigSO config;
    private CardData three, two;
    private List<CardData> selected;
    private const string L3 = "it-l3-cavallino-rampante";

    [SetUp]
    public void SetUp()
    {
        race = new RaceSession(); race.TeamVehicleBonusesEnabled = false;
        player = CreateParticipant(TeamId.IT);
        three = player.deck.Hand[0]; two = player.deck.Hand[1];
        selected = new List<CardData> { three, two };
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.enableTechTree = true; config.gearOneCooldown = 0; config.gearTwoCooldown = 0;
    }

    [TearDown] public void TearDown() => Object.DestroyImmediate(config);

    private PlayerState CreateParticipant(TeamId team)
    {
        var p = new PlayerState("owned", false, 0, 2) { teamId = team, techState = new TechTreeState(team, 50000) };
        if (team == TeamId.UK)
        {
            Assert.That(TechTreeRules.UnlockNode(p.techState, "uk-l3-sun-never-sets", race.TechDb), Is.True);
            TechTreeRules.SelectActiveNodes(p.techState, new[] { "uk-l3-sun-never-sets" }, race.TechDb);
            p.techState.sunNeverSetsTarget = TeamId.IT;
        }
        else
        {
            Assert.That(TechTreeRules.UnlockNode(p.techState, L3, race.TechDb), Is.True);
            TechTreeRules.SelectActiveNodes(p.techState, new[] { L3 }, race.TechDb);
        }
        p.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 3), new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 1), CardData.CreateTrick("cn-hotpot") }, new HeatPool(6));
        p.deck.DrawToHand(3);
        // Temporary heat is granted to the hand, never inserted into the ordinary draw pile.
        // Keep [3,2,1,tempHeat,trick] so the invalid-target cases retain their exact indices.
        p.deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
        Assert.That(p.deck.DrawToHand(5), Is.True);
        race.Players.Add(p);
        race.BeginTurn(p, 1);
        Assert.That(race.BeginItalyPrecisionTuneInput(p, 1), Is.True);
        return p;
    }

    private ItalyPrecisionTuneActivationPlan Prepare(CardData target = null)
    {
        Assert.That(race.TryPrepareItalyPrecisionTune(player, selected, target ?? three, 1, 7, true, true,
            out var plan, out string reason), Is.True, reason);
        return plan;
    }

    private SpeedCardCommitResult Commit(ItalyPrecisionTuneActivationPlan plan, int turn = 1, int request = 7)
        => CardPlayRules.CommitSpeedCards(player, selected, 4, race, 40, plan, turn, request);

    [Test]
    public void CancelIsFreeAndFormalAtomicCommitConsumesExactlyOneUseWithoutRPOrCardMutation()
    {
        int balance = player.techState.rpBalance, heat = player.deck.heatPool.remaining;
        var cancelled = Prepare(); cancelled.Cancel();
        Assert.That(Commit(cancelled), Is.EqualTo(SpeedCardCommitResult.PrecisionTuneExpired));
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.False);
        Assert.That(player.deck.HandCount, Is.EqualTo(5));
        var plan = Prepare(); Assert.That(Commit(plan), Is.EqualTo(SpeedCardCommitResult.Success));
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.True);
        Assert.That(player.techState.precisionTune.CommittedTarget, Is.SameAs(three));
        Assert.That(three.value, Is.EqualTo(3)); Assert.That(two.value, Is.EqualTo(2));
        Assert.That(RaceMovementRules.ComputeCornerSpeed(player, null, false), Is.EqualTo(4));
        Assert.That(player.techState.rpBalance, Is.EqualTo(balance));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(heat));
        Assert.That(Commit(plan), Is.EqualTo(SpeedCardCommitResult.CardNotInHand));
        Assert.That(player.playedSpeedCardsThisTurn, Is.EqualTo(selected));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void RejectsNonOrdinaryNonSelectedDuplicateAndSameDriverTarget(int invalid)
    {
        CardData target = three, driver = null;
        if (invalid == 0) target = player.deck.Hand[2];
        if (invalid == 1) { target = player.deck.Hand[3]; selected.Add(target); }
        if (invalid == 2) { target = player.deck.Hand[4]; selected.Add(target); }
        if (invalid == 3) three.isTemp = true;
        if (invalid == 4) selected.Add(three);
        if (invalid == 5) driver = three;
        Assert.That(race.TryPrepareItalyPrecisionTune(player, selected, target, 1, 7, true, true,
            out _, out _, driver), Is.False);
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.False);
        Assert.That(player.deck.HandCount, Is.EqualTo(5));
    }

    [Test]
    public void DifferentDriverSlowdownTargetRemainsEligibleAndTargetTwoStopsAtOne()
    {
        Assert.That(race.TryPrepareItalyPrecisionTune(player, selected, two, 1, 7, true, true,
            out var plan, out _, three), Is.True);
        Assert.That(Commit(plan), Is.EqualTo(SpeedCardCommitResult.Success));
        Assert.That(RaceMovementRules.ComputeCornerSpeed(player, null, false), Is.EqualTo(4));
        Assert.That(two.value, Is.EqualTo(2));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void StalePlanDoesNotPartiallyMoveCardsOrSpendOpportunity(int change)
    {
        var plan = Prepare();
        switch (change)
        {
            case 0: player.gear++; break;
            case 1: player.position++; break;
            case 2: player.lap++; break;
            case 3: selected.Reverse(); break;
            case 4: three.value++; break;
            case 5: player.techState.activeNodeIds.Clear(); break;
            case 6: player.techState.precisionTune.CloseInput(); break;
            case 7: player.techState.precisionTune = new ItalyPrecisionTuneRuntime(); break;
            case 8: player.hasFinished = true; break;
            case 9: player.skipNextTurn = true; break;
            case 10: player.pitStopScheduled = true; break;
        }
        Assert.That(Commit(plan), Is.EqualTo(SpeedCardCommitResult.PrecisionTuneExpired));
        Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
        Assert.That(player.deck.HandCount, Is.EqualTo(5));
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.False);
    }

    [TestCase(false)] [TestCase(true)]
    public void TurnAndRequestVersionsAreRequired(bool changedTurn)
    {
        var plan = Prepare();
        Assert.That(Commit(plan, changedTurn ? 2 : 1, changedTurn ? 7 : 8), Is.EqualTo(SpeedCardCommitResult.PrecisionTuneExpired));
        Assert.That(player.deck.HandCount, Is.EqualTo(5));
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.False);
    }

    [Test]
    public void WrongSessionOrParticipantCannotUsePlanAndIndependentSameTeamRetainsItsUse()
    {
        var plan = Prepare(); var other = CreateParticipant(TeamId.IT);
        Assert.That(plan.Validate(new RaceSession(), player, selected, 1, 7, true, true, out _), Is.False);
        Assert.That(plan.Validate(race, other, selected, 1, 7, true, true, out _), Is.False);
        Assert.That(Commit(plan), Is.EqualTo(SpeedCardCommitResult.Success));
        Assert.That(other.techState.precisionTune.UsedThisRace, Is.False);
        Assert.That(race.TryPrepareItalyPrecisionTune(other, new[] { other.deck.Hand[0] }, other.deck.Hand[0],
            1, 0, true, true, out _, out _), Is.True);
    }

    [TestCase(false)] [TestCase(true)]
    public void TutorialOrClosedHumanInputCannotPrepare(bool enabled)
    {
        Assert.That(race.TryPrepareItalyPrecisionTune(player, selected, three, 1, 7, !enabled, enabled,
            out _, out _), Is.False);
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.False);
    }

    [Test]
    public void BritishBorrowingAndLegacyExplicitIdDoNotShareOrDoubleTheOpportunity()
    {
        var uk = CreateParticipant(TeamId.UK);
        uk.techState.activeNodeIds.Add(L3);
        var target = uk.deck.Hand[0];
        var cards = new[] { target };
        int rp = uk.techState.rpBalance;
        Assert.That(race.TryPrepareItalyPrecisionTune(uk, cards, target, 1, 0, true, true, out var plan, out _), Is.True);
        Assert.That(CardPlayRules.CommitSpeedCards(uk, cards, 4, race, 40, plan, 1), Is.EqualTo(SpeedCardCommitResult.Success));
        Assert.That(ItalyPrecisionTuneRules.GetCommittedReduction(uk), Is.EqualTo(1));
        Assert.That(uk.techState.rpBalance, Is.EqualTo(rp));
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.False);
        Assert.That(race.TryPrepareItalyPrecisionTune(uk, new[] { uk.deck.Hand[0] }, uk.deck.Hand[0], 1, 0, true, true,
            out _, out _), Is.False);
    }

    [TestCase(false)] [TestCase(true)]
    public void PreviewIsIndependentBeforeAndAfterCommitAndNeverMutatesSharedCard(bool committed)
    {
        var plan = Prepare();
        if (committed) Assert.That(Commit(plan), Is.EqualTo(SpeedCardCommitResult.Success));
        int count = player.deck.HandCount, balance = player.techState.rpBalance;
        bool used = player.techState.precisionTune.UsedThisRace;
        int version = player.techState.precisionTune.Version;
        var preview = RaceTurnPreviewRules.Evaluate(player, race, config,
            committed ? null : selected, false, player.gear, totalCells: 40,
            precisionTunePlan: committed ? null : plan);
        Assert.That(preview.CardSpeedTotal, Is.EqualTo(4));
        Assert.That(preview.Movement.TotalCells, Is.EqualTo(4));
        Assert.That(player.deck.HandCount, Is.EqualTo(count));
        Assert.That(player.techState.rpBalance, Is.EqualTo(balance));
        Assert.That(player.techState.precisionTune.Version, Is.EqualTo(version));
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.EqualTo(used));
        Assert.That(three.value, Is.EqualTo(3));
        var copied = player.techState.CreatePresentationCopy();
        copied.precisionTune.CloseInput(); copied.activeNodeIds.Clear();
        Assert.That(player.techState.activeNodeIds, Does.Contain(L3));
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.EqualTo(used));
        if (committed) Assert.That(player.techState.precisionTune.CommittedTarget, Is.SameAs(three));
        else Assert.That(player.techState.precisionTune.CommittedTarget, Is.Null);
    }

    [TestCase(false)] [TestCase(true)]
    public void HotpotTargetPreservesSourceExclusionButAlwaysLosesOneMovement(bool loneAttack)
    {
        if (loneAttack) selected = new List<CardData> { three };
        // Use the existing authored trick effect rather than changing the target card.
        player.trickState.hotpotBaseActive = true;
        var plan = Prepare();
        var preview = RaceTurnPreviewRules.Evaluate(player, race, config, selected, false, player.gear,
            totalCells: 40, precisionTunePlan: plan);
        Assert.That(Commit(plan), Is.EqualTo(SpeedCardCommitResult.Success));
        int corner = RaceMovementRules.ComputeCornerSpeed(player, null, false);
        int movement = corner + RaceMovementRules.ComputeCardAndDriverMovementBonus(player, false);
        Assert.That(corner, Is.EqualTo(loneAttack ? 0 : 1), "The existing zero floor remains for lone ATTACK.");
        Assert.That(movement, Is.EqualTo(loneAttack ? 3 : 5));
        Assert.That(preview.Movement.TotalCells, Is.EqualTo(movement));
        Assert.That(player.hotpotAttackCardValueThisTurn, Is.EqualTo(3));
        Assert.That(three.value, Is.EqualTo(3));
    }

    [Test]
    public void CleanupEndsContributionWithoutRefundAndNewRaceResetRestoresOpportunity()
    {
        Assert.That(Commit(Prepare()), Is.EqualTo(SpeedCardCommitResult.Success));
        RaceTurnCleanup.Execute(player, null, null, null);
        Assert.That(player.techState.precisionTune.CommittedTarget, Is.Null);
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.True);
        race.BeginTurn(player, 2); race.BeginItalyPrecisionTuneInput(player, 2);
        Assert.That(ItalyPrecisionTuneActivationPlan.CanPrepare(race, player, 2, true, true, out _), Is.False);
        TechTreeRules.ResetPerRaceState(player.techState);
        race.BeginItalyPrecisionTuneInput(player, 3);
        Assert.That(ItalyPrecisionTuneActivationPlan.CanPrepare(race, player, 3, true, true, out _), Is.True);
    }

    [Test]
    public void NormalCardCapacityFailurePrecedesAnyTuneConsumption()
    {
        var plan = Prepare();
        Assert.That(CardPlayRules.CommitSpeedCards(player, selected, 1, race, 40, plan, 1, 7),
            Is.EqualTo(SpeedCardCommitResult.SpeedLimitReached));
        Assert.That(player.deck.HandCount, Is.EqualTo(5));
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.False);
    }

    [Test]
    public void ReplacedDeckCannotReceiveTheOldOwnersPlan()
    {
        var plan = Prepare();
        player.deck = new CardDeck();
        // Reuse the same exact references in the replacement deck to prove identity,
        // rather than card membership alone, invalidates the captured boundary.
        player.deck.InitializeExactOrder(selected, new HeatPool(6));
        player.deck.DrawToHand(2);
        Assert.That(Commit(plan), Is.EqualTo(SpeedCardCommitResult.PrecisionTuneExpired));
        Assert.That(player.deck.HandCount, Is.EqualTo(2));
        Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
        Assert.That(player.techState.precisionTune.UsedThisRace, Is.False);
    }
}
