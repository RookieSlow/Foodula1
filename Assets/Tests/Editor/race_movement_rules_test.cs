using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class RaceMovementRulesTests
{
    [Test]
    public void CornerSpeedUsesCommittedCardsIncludingBbqHeat()
    {
        var player = new PlayerState("UK", false, 0, 1);
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 3));
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Heat, 0));

        Assert.That(RaceMovementRules.ComputeCornerSpeed(player, null, false), Is.EqualTo(5));
    }

    [Test]
    public void HotpotAttackCardIsExcludedFromCornerButNotOtherCommittedSpeed()
    {
        var player = new PlayerState("CN", false, 0, 1)
        {
            hotpotAttackAppliedThisTurn = true,
            hotpotAttackCardValueThisTurn = 4
        };
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 4));
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 2));

        Assert.That(RaceMovementRules.ComputeCornerSpeed(player, null, false), Is.EqualTo(2));
    }

    [Test]
    public void CornerSpeedCannotBecomeNegativeAfterHotpotExclusion()
    {
        var player = new PlayerState("CN", false, 0, 1)
        {
            hotpotAttackAppliedThisTurn = true,
            hotpotAttackCardValueThisTurn = 4
        };

        Assert.That(RaceMovementRules.ComputeCornerSpeed(player, null, false), Is.Zero);
    }

    [Test]
    public void TorpedoCueOnlyClearsTheTeachingLeadersCornerSpeed()
    {
        var player = new PlayerState("JP", true, 0, 1);
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 3));
        var cue = new TutorialOpponentCue(TutorialStepId.JpTorpedo, 0, 0, 0);

        Assert.That(RaceMovementRules.ComputeCornerSpeed(player, cue, true), Is.Zero);
        Assert.That(RaceMovementRules.ComputeCornerSpeed(player, cue, false), Is.EqualTo(3));
    }

    [TestCase(3, 2, 5)]
    [TestCase(3, -7, 0)]
    public void NormalBaseMovementClampsAfterAddingBonus(int cornerSpeed, int bonus, int expected)
    {
        var result = RaceMovementRules.ResolveBaseMovement(cornerSpeed, bonus, null, true);

        Assert.That(result.movement, Is.EqualTo(expected));
        Assert.That(result.cornerSpeed, Is.EqualTo(cornerSpeed));
    }

    [Test]
    public void ScriptedSlipstreamLeaderStopsAfterBaseBonusAndLosesCornerSpeed()
    {
        var cue = new TutorialOpponentCue(TutorialStepId.Slipstream, 0, 0, 1);

        var leader = RaceMovementRules.ResolveBaseMovement(3, 2, cue, true);
        var follower = RaceMovementRules.ResolveBaseMovement(3, 2, cue, false);

        Assert.That(leader, Is.EqualTo((0, 0)));
        Assert.That(follower, Is.EqualTo((5, 3)));
    }

    [Test]
    public void OtherTutorialLeaderKeepsMovementButLosesCornerSpeed()
    {
        var cue = new TutorialOpponentCue(TutorialStepId.Review, 0, 0, 0);

        Assert.That(RaceMovementRules.ResolveBaseMovement(3, 2, cue, true),
            Is.EqualTo((5, 0)));
    }

    [TestCase(3, 3, 3, 4)]
    [TestCase(3, 3, 4, 2)]
    [TestCase(3, 2, 4, 0)]
    public void NigiriAwardsTwoForEachExactCorner(
        int cornerSpeed, int firstLimit, int secondLimit, int expected)
    {
        int[] limits = { firstLimit, secondLimit };
        Assert.That(RaceMovementRules.ComputeNigiriBonus(
            cornerSpeed, new[] { 0, 1 }, id => limits[id]), Is.EqualTo(expected));
    }

    [Test]
    public void NigiriWithoutCrossedCornersNeverQueriesLimit()
    {
        Assert.That(RaceMovementRules.ComputeNigiriBonus(
            3, new int[0], _ => throw new System.Exception("No corner limit expected")), Is.Zero);
    }

    [Test]
    public void TorpedoUsesBaseMovementAndCountsEachEligibleOpponent()
    {
        var player = new PlayerState("JP", false, 0, 1)
        {
            cornerTotalThisTurn = 5,
            totalMovementThisTurn = 0
        };
        var first = new PlayerState("First", true, 2, 1);
        var second = new PlayerState("Second", true, 3, 1);
        var state = new TrickCardState { torpedoTempuraActive = true };

        Assert.That(RaceMovementRules.ComputeTorpedoBonus(
            state, player, new[] { player, first, second }, 12,
            candidate => candidate.skipNextTurn), Is.EqualTo(2));
    }

    [Test]
    public void TorpedoInactiveOrSkippedOpponentGivesNoBonus()
    {
        var player = new PlayerState("JP", false, 0, 1) { cornerTotalThisTurn = 4 };
        var opponent = new PlayerState("Opponent", true, 2, 1) { skipNextTurn = true };
        var order = new[] { player, opponent };
        var state = new TrickCardState();

        Assert.That(RaceMovementRules.ComputeTorpedoBonus(
            state, player, order, 12, RaceTurnRules.ShouldSkip), Is.Zero);
        state.torpedoTempuraActive = true;
        Assert.That(RaceMovementRules.ComputeTorpedoBonus(
            state, player, order, 12, RaceTurnRules.ShouldSkip), Is.Zero);
    }

    [Test]
    public void CountsAnOpponentPassedDuringMovement()
    {
        var player = new PlayerState("Player", false, 0, 1)
        {
            cornerTotalThisTurn = 3
        };
        var opponent = new PlayerState("Opponent", true, 2, 1);
        var turnOrder = new List<PlayerState> { player, opponent };

        int overtakes = RaceMovementRules.CountOvertakes(
            player, turnOrder, 10, false, candidate => candidate.skipNextTurn);

        Assert.That(overtakes, Is.EqualTo(1));
    }

    [Test]
    public void SkippedOpponentDoesNotCountAsOvertake()
    {
        var player = new PlayerState("Player", false, 0, 1)
        {
            totalMovementThisTurn = 3
        };
        var opponent = new PlayerState("Opponent", true, 2, 1)
        {
            totalMovementThisTurn = 0,
            skipNextTurn = true
        };

        int overtakes = RaceMovementRules.CountOvertakes(
            player,
            new List<PlayerState> { player, opponent },
            10,
            true,
            candidate => candidate.skipNextTurn);

        Assert.That(overtakes, Is.EqualTo(0));
    }

    [Test]
    public void InvalidTrackSizeProducesNoOvertakes()
    {
        var player = new PlayerState("Player", false, 0, 1);

        Assert.That(RaceMovementRules.CountOvertakes(
            player,
            new List<PlayerState> { player },
            0,
            true,
            null), Is.EqualTo(0));
        Assert.That(RaceMovementRules.IsAhead(1, 0, 0), Is.False);
    }
}

public sealed class InstantTechnologyMovementPlanTests
{
    [TestCase(0, 0, 0, 0, 0)]
    [TestCase(0, 0, 1, 1, 0)]
    [TestCase(0, 3, 1, 0, 1)]
    [TestCase(2, 1, 1, 2, 1)]
    [TestCase(2, 2, 1, 3, 0)]
    [TestCase(2, 2, 4, 2, 1)]
    [TestCase(2, 3, 8, 3, 2)]
    [TestCase(2, 0, 10, 2, 3)]
    [TestCase(0, 2, -3, -1, 0)]
    [TestCase(0, -2, 2, 0, 1)]
    [TestCase(2, -3, 1, -2, 1)]
    [TestCase(2, 8, 2, 2, 1)]
    public void PlanKeepsRawRouteAndSignedRemainder(
        int startFinish, int start, int movement, int final, int crossings)
    {
        List<TrackNode> nodes = Enumerable.Range(0, 4)
            .Select(i => new TrackNode(i, 0, isStartFinish: i == startFinish)).ToList();

        RaceMovementRules.InstantMovementPlan plan =
            RaceMovementRules.PlanInstantTechnologyMovement(nodes, start, movement);

        Assert.That(plan.StartPosition, Is.EqualTo(start));
        Assert.That(plan.RawEnd, Is.EqualTo(start + movement));
        Assert.That(plan.FinalPosition, Is.EqualTo(final));
        Assert.That(plan.FinishCrossings, Is.EqualTo(crossings));
    }

    [TestCase("silverstone_afternoon_tea")]
    [TestCase("nurburgring_bier")]
    [TestCase("monza_pasta")]
    [TestCase("indianapolis_burger")]
    [TestCase("shanghai_dim_sum")]
    [TestCase("suzuka_sushi")]
    [TestCase("nurburgring_24h_endurance")]
    [TestCase("le_mans_old_mulsanne")]
    public void AuthoredTracksMatchDisplacedCalculationWithoutMutatingNodes(string trackId)
    {
        TrackConfig config = TrackDataLoader.LoadConfig(trackId);
        Assert.That(config, Is.Not.Null);
        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(config);
        TrackNode[] references = nodes.ToArray();
        var before = nodes.Select(node => (node.nodeIndex, node.speedLimit, node.nodeName,
            node.cornerId, node.isStartFinish, node.isApex, node.isPitEntry, node.isPitExit)).ToArray();
        int line = TrackRules.FindStartFinishNodeIndex(nodes);
        var routes = new[]
        {
            (start: line, movement: 0),
            (start: line, movement: 1),
            (start: line - 1, movement: 1),
            (start: line, movement: nodes.Count),
            (start: nodes.Count - 1, movement: 3 * nodes.Count + 2),
            (start: 0, movement: -1)
        };

        foreach (var route in routes)
        {
            // Reference is the calculation displaced from the coordinator, not another plan call.
            int rawEnd = route.start + route.movement;
            int crossings = TrackRules.CountStartFinishCrossings(nodes, route.start, rawEnd);
            int position = rawEnd % nodes.Count;

            var plan = RaceMovementRules.PlanInstantTechnologyMovement(nodes, route.start, route.movement);

            Assert.That((plan.StartPosition, plan.RawEnd, plan.FinalPosition, plan.FinishCrossings),
                Is.EqualTo((route.start, rawEnd, position, crossings)), trackId);
        }
        Assert.That(nodes, Is.EqualTo(references));
        Assert.That(nodes.Select(node => (node.nodeIndex, node.speedLimit, node.nodeName,
            node.cornerId, node.isStartFinish, node.isApex, node.isPitEntry, node.isPitExit)), Is.EqualTo(before));
    }

    [TestCase(false, 0)]
    [TestCase(true, 5)]
    public void PlanUsesAuthoredMarkersWithoutInventingOrCollapsingLines(bool marked, int crossings)
    {
        var nodes = Enumerable.Range(0, 4)
            .Select(i => new TrackNode(i, 0, isStartFinish: marked && i % 2 == 0)).ToList();

        Assert.That(RaceMovementRules.PlanInstantTechnologyMovement(nodes, 3, 9).FinishCrossings,
            Is.EqualTo(crossings));
    }

    [TestCase(int.MaxValue, 1)]
    [TestCase(int.MaxValue - 1, 5)]
    public void OverflowKeepsExistingIntegerAdditionAndDoesNotInventForwardCrossings(int start, int movement)
    {
        var nodes = new[] { new TrackNode(0, 0, isStartFinish: true),
            new TrackNode(1, 0), new TrackNode(2, 0), new TrackNode(3, 0) };
        int rawEnd = unchecked(start + movement);

        var plan = RaceMovementRules.PlanInstantTechnologyMovement(nodes, start, movement);

        Assert.That(plan.RawEnd, Is.EqualTo(rawEnd));
        Assert.That(plan.FinalPosition, Is.EqualTo(rawEnd % nodes.Length));
        Assert.That(plan.FinishCrossings, Is.Zero);
    }

    [Test]
    public void EmptyTrackPreservesDivideByZeroInsteadOfReturningAPlan()
    {
        Assert.Throws<System.DivideByZeroException>(() =>
            RaceMovementRules.PlanInstantTechnologyMovement(new TrackNode[0], 3, 1));
    }

    [Test]
    public void NullTrackPreservesMissingTrackFailure()
    {
        Assert.Throws<System.NullReferenceException>(() =>
            RaceMovementRules.PlanInstantTechnologyMovement(null, 3, 1));
    }

    [Test]
    public void MalformedCrossedNodeFailsDuringCountingBeforeWrapping()
    {
        Assert.Throws<System.NullReferenceException>(() =>
            RaceMovementRules.PlanInstantTechnologyMovement(new[] { new TrackNode(0, 0), null }, 0, 1));
    }
}

public sealed class RaceOvertakeResolutionTests
{
    private static PlayerState Racer(string name, int position, int movement)
    {
        return new PlayerState(name, false, position, 1)
        { totalMovementThisTurn = movement };
    }

    private static void ActivateMansell(PlayerState player)
    {
        Assert.That(DriverCatalog.TryGet("uk_nigel_mansell", out DriverProfile profile), Is.True);
        player.driverSkill.Initialize(profile, 5, true);
        var context = new DriverSkillActivationContext(true, 2, 3, 10, 10, 3);
        Assert.That(player.driverSkill.TryActivate(profile, context, out string reason), Is.True, reason);
    }

    [Test]
    public void ActiveMansellGetsBonusBeforePresentationOvertakeCount()
    {
        PlayerState player = Racer("Mansell", 0, 5);
        PlayerState leader = Racer("Leader", 3, 0);
        ActivateMansell(player);
        var counts = new Dictionary<PlayerState, int>();

        RaceOvertakeResolution.Execute(new[] { player, leader },
            new HashSet<PlayerState>(), 20, counts);

        Assert.That(player.totalMovementThisTurn, Is.EqualTo(6));
        Assert.That(counts[player], Is.EqualTo(1));
        Assert.That(counts[leader], Is.Zero);
    }

    [Test]
    public void OrdinaryOvertakeIsCountedWithoutChangingBaseMovement()
    {
        PlayerState player = Racer("Player", 0, 5);
        PlayerState leader = Racer("Leader", 3, 0);
        var counts = new Dictionary<PlayerState, int>();

        RaceOvertakeResolution.Execute(new[] { player, leader },
            new HashSet<PlayerState>(), 20, counts);

        Assert.That(player.totalMovementThisTurn, Is.EqualTo(5));
        Assert.That(counts[player], Is.EqualTo(1));
    }

    [Test]
    public void NoProjectedPassMeansNoMansellBonus()
    {
        PlayerState player = Racer("Mansell", 0, 5);
        PlayerState leader = Racer("Leader", 6, 0);
        ActivateMansell(player);
        var counts = new Dictionary<PlayerState, int>();

        RaceOvertakeResolution.Execute(new[] { player, leader },
            new HashSet<PlayerState>(), 20, counts);

        Assert.That(player.totalMovementThisTurn, Is.EqualTo(5));
        Assert.That(counts[player], Is.Zero);
    }

    [Test]
    public void SkippedParticipantGetsZeroCountAndNoSkillBonus()
    {
        PlayerState player = Racer("Mansell", 0, 5);
        PlayerState leader = Racer("Leader", 3, 0);
        ActivateMansell(player);
        var counts = new Dictionary<PlayerState, int> { [player] = 7 };

        RaceOvertakeResolution.Execute(new[] { player, leader },
            new HashSet<PlayerState> { player }, 20, counts);

        Assert.That(player.totalMovementThisTurn, Is.EqualTo(5));
        Assert.That(counts[player], Is.Zero);
    }

    [Test]
    public void FinishedParticipantGetsZeroCountAndNoSkillBonus()
    {
        PlayerState player = Racer("Mansell", 0, 5);
        PlayerState leader = Racer("Leader", 3, 0);
        player.hasFinished = true;
        ActivateMansell(player);
        var counts = new Dictionary<PlayerState, int>();

        RaceOvertakeResolution.Execute(new[] { player, leader },
            new HashSet<PlayerState>(), 20, counts);

        Assert.That(player.totalMovementThisTurn, Is.EqualTo(5));
        Assert.That(counts[player], Is.Zero);
    }

    [Test]
    public void InvalidTrackSizeDoesNotAwardBonusOrPresentationOvertake()
    {
        PlayerState player = Racer("Mansell", 0, 5);
        PlayerState leader = Racer("Leader", 3, 0);
        ActivateMansell(player);
        var counts = new Dictionary<PlayerState, int>();

        RaceOvertakeResolution.Execute(new[] { player, leader },
            new HashSet<PlayerState>(), 0, counts);

        Assert.That(player.totalMovementThisTurn, Is.EqualTo(5));
        Assert.That(counts[player], Is.Zero);
    }
}
