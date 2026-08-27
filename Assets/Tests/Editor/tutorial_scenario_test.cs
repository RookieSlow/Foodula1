using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class TutorialScenarioTests
{
    [SetUp]
    public void SetUp()
    {
        TutorialLaunchState.Clear();
        TrackSelectionState.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        TutorialLaunchState.Clear();
        TrackSelectionState.Reset();
    }

    [Test]
    public void LeMansScenarioIsIsolatedUkWithNoProgressionBenefits()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();

        Assert.That(scenario.id, Is.EqualTo("tutorial_le_mans_uk_v1"));
        Assert.That(scenario.trackId, Is.EqualTo("le_mans_old_mulsanne"));
        Assert.That(scenario.playerTeam, Is.EqualTo(TeamId.UK));
        Assert.That(scenario.techTreeEnabled, Is.False);
        Assert.That(scenario.driverSkillsEnabled, Is.False);
        Assert.That(scenario.normalRewardsEnabled, Is.False);
        Assert.That(scenario.normalProgressionWritesEnabled, Is.False);
        Assert.That(scenario.opponentScript.Single().expectedSlipstreamDistance, Is.EqualTo(2));
        Assert.That(scenario.opponentCount, Is.EqualTo(1));
        Assert.That(scenario.opponentTeam, Is.EqualTo(TeamId.JP));
        Assert.That(scenario.guidedStartWeatherId, Is.EqualTo("sunny"));
        Assert.That(scenario.practiceWeatherId, Is.EqualTo("cloudy"));
    }

    [Test]
    public void TutorialLaunchOverridesTrackWithoutMutatingQuickRaceSelection()
    {
        const string quickRaceTrack = "monza_pasta";
        Assert.That(TrackSelectionState.TrySelect(quickRaceTrack), Is.True);

        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        TutorialLaunchState.Request(scenario);

        Assert.That(TutorialLaunchState.IsRequested, Is.True);
        Assert.That(TutorialLaunchState.ResolveTrackId("silverstone_afternoon_tea"),
            Is.EqualTo(TutorialScenarioDefinition.TrackId));
        Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo(quickRaceTrack));

        Assert.That(TutorialLaunchState.ActivateRequested(), Is.SameAs(scenario));
        Assert.That(TutorialLaunchState.IsActive, Is.True);
        Assert.That(TutorialLaunchState.ResolveTrackId("silverstone_afternoon_tea"),
            Is.EqualTo(TutorialScenarioDefinition.TrackId));

        TutorialLaunchState.Clear();
        Assert.That(TutorialLaunchState.ResolveTrackId("silverstone_afternoon_tea"),
            Is.EqualTo(quickRaceTrack));
    }

    [Test]
    public void TeachingOpponentDeckIsExactSpeedOnlyAndRepeatable()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        List<CardData> first = scenario.CreateOpponentDeck();
        List<CardData> second = scenario.CreateOpponentDeck();

        Assert.That(first.Select(CardLabel), Is.EqualTo(second.Select(CardLabel)));
        Assert.That(first.Count, Is.EqualTo(12));
        Assert.That(first.All(card => card.type == CardType.Speed), Is.True);

        var deck = new CardDeck();
        deck.InitializeExactOrder(first, new HeatPool(scenario.engineHeatCapacity));
        Assert.That(deck.DrawToHand(scenario.openingHandSize), Is.True);
        Assert.That(deck.Hand.Select(card => card.value), Is.EqualTo(new[] { 1, 1, 2, 2, 3, 1, 2 }));
    }

    [Test]
    public void TutorialSessionCanDisableUkVehicleHandlingWithoutChangingNormalDefault()
    {
        var uk = new PlayerState("UK", false, 0, 1) { teamId = TeamId.UK };
        var normalSession = new RaceSession(new SystemRandomSource(1));
        normalSession.Weather = WeatherType.Sunny;
        Assert.That(normalSession.EffectiveCornerLimit(uk, 4), Is.EqualTo(5));

        var tutorialSession = new RaceSession(new SystemRandomSource(1))
        {
            TeamVehicleBonusesEnabled = false,
            Weather = WeatherType.Sunny
        };
        Assert.That(tutorialSession.EffectiveCornerLimit(uk, 4), Is.EqualTo(4));
        Assert.That(tutorialSession.EffectiveHeatPoolSize(uk, 6), Is.EqualTo(6));
    }

    [Test]
    public void ExactDeckMatchesAuthoredOpeningAndFutureDrawsWithoutSeed()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var deck = new CardDeck();
        deck.InitializeExactOrder(scenario.CreateExactDeck(), new HeatPool(scenario.engineHeatCapacity));

        Assert.That(deck.UsesExactOrder, Is.True);
        Assert.That(deck.DrawToHand(scenario.openingHandSize), Is.True);
        Assert.That(deck.Hand.Select(CardLabel), Is.EqualTo(new[]
        {
            "1", "2", "2", "3", "4", "1", "T[uk-scone]"
        }));

        var played = new List<CardData> { deck.Hand[0], deck.Hand[1] };
        deck.RemoveFromHand(played);
        deck.DiscardSpeedCards(played);
        Assert.That(deck.DrawToHand(scenario.openingHandSize), Is.True);
        Assert.That(deck.Hand.Skip(5).Select(CardLabel), Is.EqualTo(new[]
        {
            "3", "T[uk-english-breakfast-tea]"
        }));
    }

    [Test]
    public void ExactDeckRecyclePreservesDiscardChronologyAndIgnoresShuffleCalls()
    {
        var deck = new CardDeck();
        deck.InitializeExactOrder(new[]
        {
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 3)
        }, new HeatPool(6));

        deck.ShuffleDrawPile();
        Assert.That(deck.DrawToHand(3), Is.True);
        var firstCycle = new List<CardData>(deck.Hand);
        deck.RemoveFromHand(firstCycle);
        deck.DiscardSpeedCards(firstCycle);

        Assert.That(deck.DrawToHand(3), Is.True);
        Assert.That(deck.Hand.Select(card => card.value), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void GuidedStepsRejectOutOfOrderActionsAndEnterPracticeInAuthoredOrder()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var machine = new TutorialStateMachine(scenario);

        Assert.That(machine.TryPerform(TutorialAction.PayHeat, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("expected_AcknowledgeObjective"));
        Assert.That(machine.CurrentStep.id, Is.EqualTo(TutorialStepId.ObjectiveAndInterface));

        foreach (TutorialStepDefinition step in scenario.steps)
        {
            Assert.That(machine.CurrentStep.id, Is.EqualTo(step.id));
            Assert.That(machine.TryPerform(step.requiredAction, out reason), Is.True, reason);
        }

        Assert.That(machine.Phase, Is.EqualTo(TutorialRunPhase.Practice));
        Assert.That(machine.CompletedStepCount, Is.EqualTo(scenario.steps.Count));
        Assert.That(machine.Events.Any(entry => entry.ToString().Contains("event=practice_started")), Is.True);
    }

    [Test]
    public void SkipRestartCompleteAndExitHaveExplicitRecoverableStates()
    {
        var machine = new TutorialStateMachine(TutorialScenarioDefinition.CreateLeMansUk());

        machine.SkipGuidedSection();
        Assert.That(machine.Phase, Is.EqualTo(TutorialRunPhase.Practice));

        machine.RestartPracticeLap();
        Assert.That(machine.Phase, Is.EqualTo(TutorialRunPhase.Practice));
        Assert.That(machine.TryPerform(TutorialAction.CompletePracticeLap, out _), Is.True);
        Assert.That(machine.Phase, Is.EqualTo(TutorialRunPhase.Completed));

        machine.RestartGuidedSection();
        Assert.That(machine.Phase, Is.EqualTo(TutorialRunPhase.Guided));
        Assert.That(machine.CurrentStep.id, Is.EqualTo(TutorialStepId.ObjectiveAndInterface));

        machine.ExitTutorial();
        Assert.That(machine.Phase, Is.EqualTo(TutorialRunPhase.Exited));
    }

    [Test]
    public void ScenarioContainsEveryRequiredGuidedTopicOnce()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        TutorialStepId[] ids = scenario.steps.Select(step => step.id).ToArray();

        Assert.That(ids, Is.EqualTo((TutorialStepId[])System.Enum.GetValues(typeof(TutorialStepId))));
        Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        Assert.That(scenario.weatherScript.Any(cue => cue.step == TutorialStepId.Weather), Is.True);
        Assert.That(scenario.opponentScript.Any(cue => cue.step == TutorialStepId.Slipstream), Is.True);
        Assert.That(scenario.playerCheckpoints.Select(cue => cue.step).Distinct().Count(),
            Is.EqualTo(scenario.playerCheckpoints.Count));
        Assert.That(scenario.tutorialPitLane, Is.Not.Null);
    }

    [Test]
    public void GuidedStepsContainAuthoredPresentationAndOnlyKnowledgeStepsAdvanceManually()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();

        Assert.That(scenario.steps.All(step => !string.IsNullOrWhiteSpace(step.title)), Is.True);
        Assert.That(scenario.steps.All(step => !string.IsNullOrWhiteSpace(step.instruction)), Is.True);
        Assert.That(
            scenario.steps.Where(step => step.allowManualAdvance).Select(step => step.id),
            Is.EqualTo(new[]
            {
                TutorialStepId.ObjectiveAndInterface,
                TutorialStepId.DeckHandDiscardAndRecycle,
                TutorialStepId.Weather,
                TutorialStepId.Review
            }));
    }

    [Test]
    public void RuntimeDirectorEmitsStateEventsExactlyOnceAndRejectsWrongActionWithoutCue()
    {
        var director = new TutorialRuntimeDirector(TutorialScenarioDefinition.CreateLeMansUk());

        Assert.That(director.DrainNewEvents().Select(entry => entry.eventId),
            Is.EqualTo(new[] { "guided_started" }));
        Assert.That(director.BlocksRaceInput, Is.True);
        Assert.That(director.DrainNewEvents(), Is.Empty);
        Assert.That(director.TakePendingCue(), Is.Null);

        Assert.That(director.TryPerform(TutorialAction.PayHeat, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("expected_AcknowledgeObjective"));
        Assert.That(director.TakePendingCue(), Is.Null);
        Assert.That(director.DrainNewEvents().Single().eventId, Is.EqualTo("action_rejected"));

        Assert.That(director.TryPerform(TutorialAction.AcknowledgeObjective, out reason), Is.True);
        Assert.That(director.BlocksRaceInput, Is.False);
    }

    [Test]
    public void RuntimeDirectorProducesWeatherAndOpponentCuesOnlyWhenTheirStepsStart()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var director = new TutorialRuntimeDirector(scenario);

        for (int i = 0; i < scenario.steps.Count; i++)
        {
            TutorialStepDefinition step = scenario.steps[i];
            Assert.That(director.CurrentStep.id, Is.EqualTo(step.id));
            Assert.That(director.TryPerform(step.requiredAction, out string reason), Is.True, reason);

            TutorialCheckpointCue cue = director.TakePendingCue();
            TutorialStepId? nextStep = i + 1 < scenario.steps.Count
                ? scenario.steps[i + 1].id
                : (TutorialStepId?)null;
            TutorialPlayerCheckpoint expectedPlayer = nextStep.HasValue
                ? scenario.playerCheckpoints.SingleOrDefault(item => item.step == nextStep.Value)
                : null;

            if (nextStep == TutorialStepId.Weather)
            {
                Assert.That(cue, Is.Not.Null);
                Assert.That(cue.Weather.weatherId, Is.EqualTo("rain"));
                Assert.That(cue.Opponent, Is.Null);
            }
            else if (nextStep == TutorialStepId.Slipstream)
            {
                Assert.That(cue, Is.Not.Null);
                Assert.That(cue.Weather, Is.Null);
                Assert.That(cue.Opponent.leaderCell, Is.EqualTo(42));
                Assert.That(cue.Opponent.playerCell, Is.EqualTo(40));
                Assert.That(cue.Opponent.expectedSlipstreamDistance, Is.EqualTo(2));
            }
            else if (nextStep == TutorialStepId.Review)
            {
                Assert.That(cue, Is.Not.Null);
                Assert.That(cue.Weather.weatherId, Is.EqualTo("cloudy"));
                Assert.That(cue.Opponent, Is.Null);
            }
            else if (expectedPlayer == null)
            {
                Assert.That(cue, Is.Null);
            }

            if (expectedPlayer != null)
            {
                Assert.That(cue, Is.Not.Null);
                Assert.That(cue.Player, Is.SameAs(expectedPlayer));
            }
        }

        Assert.That(director.Phase, Is.EqualTo(TutorialRunPhase.Practice));
    }

    [Test]
    public void PracticeBootStartsAtOneLapBoundaryWithoutReplayingGuidedCues()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var director = new TutorialRuntimeDirector(scenario, startInPractice: true);

        Assert.That(director.Phase, Is.EqualTo(TutorialRunPhase.Practice));
        Assert.That(director.CurrentStep, Is.Null);
        Assert.That(director.CompletedStepCount, Is.EqualTo(scenario.steps.Count));
        Assert.That(director.BlocksRaceInput, Is.False);
        Assert.That(director.TakePendingCue(), Is.Null);
        Assert.That(director.DrainNewEvents().Select(entry => entry.eventId),
            Is.EqualTo(new[] { "practice_started" }));
        Assert.That(
            TutorialPracticeRules.GetRequiredLapCount(director.Phase, normalLapCount: 3),
            Is.EqualTo(1));
    }

    [Test]
    public void PracticeCompletionRestartReplayAndExitRemainRecoverable()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var director = new TutorialRuntimeDirector(scenario, startInPractice: true);
        director.DrainNewEvents();

        Assert.That(director.CompletePracticeLap(out string reason), Is.True, reason);
        Assert.That(director.Phase, Is.EqualTo(TutorialRunPhase.Completed));
        Assert.That(TutorialPracticeRules.ShouldEndImmediately(director.Phase), Is.True);
        Assert.That(director.DrainNewEvents().Single().eventId, Is.EqualTo("practice_completed"));

        director.RestartPracticeLap();
        Assert.That(director.Phase, Is.EqualTo(TutorialRunPhase.Practice));
        Assert.That(director.DrainNewEvents().Single().eventId, Is.EqualTo("practice_restarted"));

        director.RestartGuidedSection();
        Assert.That(director.Phase, Is.EqualTo(TutorialRunPhase.Guided));
        Assert.That(director.CurrentStep.id, Is.EqualTo(TutorialStepId.ObjectiveAndInterface));
        Assert.That(director.TakePendingCue(), Is.Null);

        director.ExitTutorial();
        Assert.That(director.Phase, Is.EqualTo(TutorialRunPhase.Exited));
        Assert.That(TutorialPracticeRules.ShouldEndImmediately(director.Phase), Is.True);
    }

    [Test]
    public void PracticeResetRecreatesSameExactOpeningWhileNormalLapRulesStayUnchanged()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var firstDeck = new CardDeck();
        var restartedDeck = new CardDeck();
        firstDeck.InitializeExactOrder(
            scenario.CreateExactDeck(),
            new HeatPool(scenario.engineHeatCapacity));
        restartedDeck.InitializeExactOrder(
            scenario.CreateExactDeck(),
            new HeatPool(scenario.engineHeatCapacity));

        Assert.That(firstDeck.DrawToHand(scenario.openingHandSize), Is.True);
        Assert.That(restartedDeck.DrawToHand(scenario.openingHandSize), Is.True);
        Assert.That(firstDeck.Hand.Select(CardLabel),
            Is.EqualTo(restartedDeck.Hand.Select(CardLabel)));
        Assert.That(firstDeck.UsesExactOrder, Is.True);
        Assert.That(restartedDeck.UsesExactOrder, Is.True);
        Assert.That(TutorialPracticeRules.GetRequiredLapCount(null, normalLapCount: 3),
            Is.EqualTo(3));
        Assert.That(TutorialPracticeRules.GetRequiredLapCount(TutorialRunPhase.Guided, 3),
            Is.EqualTo(3));
    }

    [Test]
    public void TutorialSlipstreamCueKeepsNormalRearOnlyBenefitRule()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        TutorialOpponentCue cue = scenario.opponentScript.Single(item => item.step == TutorialStepId.Slipstream);
        var player = new PlayerState("player", false, cue.playerCell, 1) { teamId = TeamId.UK };
        var leader = new PlayerState("leader", true, cue.leaderCell, 1) { teamId = TeamId.JP };
        var session = new RaceSession(new SystemRandomSource(1))
        {
            TeamVehicleBonusesEnabled = false,
            SlipstreamRangeOverride = cue.expectedSlipstreamDistance,
            Weather = WeatherType.Sunny
        };
        session.Players.Add(player);
        session.Players.Add(leader);

        var settled = new Dictionary<PlayerState, int>
        {
            [player] = 0,
            [leader] = 0
        };
        SlipstreamChainResult playerChain = session.ComputeSlipstreamChain(
            player, session.Players, 60, settled, 2, session.Players);
        SlipstreamChainResult leaderChain = session.ComputeSlipstreamChain(
            leader, session.Players, 60, settled, 2, session.Players);

        Assert.That(RaceSession.ForwardDistance(player.position, leader.position, 60),
            Is.EqualTo(cue.expectedSlipstreamDistance));
        Assert.That(playerChain.Triggered, Is.True);
        Assert.That(playerChain.TotalBonus, Is.EqualTo(2));
        Assert.That(leaderChain.Triggered, Is.False);
    }

    [Test]
    public void PlayerCheckpointsRebuildExactZonesAndPreserveHeatConservation()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();

        foreach (TutorialPlayerCheckpoint checkpoint in scenario.playerCheckpoints)
        {
            var player = new PlayerState("player", false, 99, 4)
            {
                teamId = TeamId.UK,
                spinCounter = 2,
                skipNextTurn = true,
                pitStopRequested = true,
                pitStopScheduled = true
            };

            TutorialCheckpointApplyResult result = TutorialCheckpointRules.ApplyPlayerCheckpoint(
                scenario, checkpoint, player);

            Assert.That(result.success, Is.True, $"Checkpoint {checkpoint.step}: {result.failureReason}");
            Assert.That(player.position, Is.EqualTo(checkpoint.playerCell), checkpoint.step.ToString());
            Assert.That(player.gear, Is.EqualTo(checkpoint.gear), checkpoint.step.ToString());
            Assert.That(player.deck.HandCount,
                Is.EqualTo(checkpoint.normalHandSize + checkpoint.heatInHand));
            Assert.That(player.deck.CountHeatInHand(), Is.EqualTo(checkpoint.heatInHand));
            Assert.That(player.deck.CountHeatInDiscardPile(), Is.EqualTo(checkpoint.heatInDiscard));
            Assert.That(player.deck.heatPool.remaining + player.deck.CountPermanentHeatOutsideEngine(),
                Is.EqualTo(scenario.engineHeatCapacity));
            Assert.That(player.deck.UsesExactOrder, Is.True);
            Assert.That(player.spinCounter, Is.Zero);
            Assert.That(player.skipNextTurn, Is.False);
            Assert.That(player.pitStopRequested, Is.False);
            Assert.That(player.pitStopScheduled, Is.False);
        }
    }

    [Test]
    public void MissingCardAndSpinCheckpointsGuaranteeTheirFailureConditions()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        TutorialPlayerCheckpoint missing = scenario.playerCheckpoints.Single(
            item => item.step == TutorialStepId.MissingCardPenalty);
        TutorialPlayerCheckpoint spin = scenario.playerCheckpoints.Single(
            item => item.step == TutorialStepId.CornerLimitAndSpin);
        var player = new PlayerState("player", false, 0, 1) { teamId = TeamId.UK };

        Assert.That(TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, missing, player).success, Is.True);
        Assert.That(player.deck.CountSpeedInHand(), Is.EqualTo(1));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(1));
        Assert.That(RaceRules.GetMissingSpeedCardCount(2, player.deck.CountSpeedInHand()), Is.EqualTo(1));

        Assert.That(TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, spin, player).success, Is.True);
        Assert.That(player.position, Is.EqualTo(8));
        Assert.That(player.deck.heatPool.remaining, Is.Zero);
        Assert.That(player.deck.Hand.Take(2).Sum(card => card.value), Is.EqualTo(5));
        Assert.That(player.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(6));
    }

    [Test]
    public void TutorialVirtualPitReusesNormalPitRulesWithoutMutatingOfficialNodes()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var officialNodes = Enumerable.Range(0, 142)
            .Select(index => new TrackNode(index, 0, "Official"))
            .ToList();
        IReadOnlyList<TrackNode> tutorialNodes = TutorialCheckpointRules.CreateVirtualPitRuleNodes(
            officialNodes.Count, scenario.tutorialPitLane);

        Assert.That(PitLaneRules.HasPitLane(officialNodes), Is.False);
        Assert.That(PitLaneRules.HasPitLane(tutorialNodes), Is.True);
        Assert.That(PitLaneRules.GetDistanceToPitEntry(130, tutorialNodes), Is.EqualTo(2));
        Assert.That(PitLaneRules.CrossedPitEntry(130, 132, tutorialNodes), Is.True);
        Assert.That(officialNodes.Any(node => node.isPitEntry || node.isPitExit), Is.False);

        var player = new PlayerState("player", false, 132, 1) { teamId = TeamId.UK };
        PitStopResult result = PitLaneRules.EnterPit(player, tutorialNodes, exitMoveBonus: 1);

        Assert.That(result.success, Is.True);
        Assert.That(result.pitExitPosition, Is.EqualTo(scenario.tutorialPitLane.exitCell));
        Assert.That(result.exitPosition, Is.EqualTo(scenario.tutorialPitLane.exitCell + 1));
    }

    [Test]
    public void SconeAndTeaCheckpointsGuaranteeActualUkCardsAndRequiredHeat()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var player = new PlayerState("player", false, 0, 1) { teamId = TeamId.UK };

        TutorialPlayerCheckpoint scone = scenario.playerCheckpoints.Single(
            item => item.step == TutorialStepId.UkScone);
        TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, scone, player);
        Assert.That(player.deck.Hand.Any(card => card.trickId == "uk-scone"), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.GreaterThanOrEqualTo(1));

        TutorialPlayerCheckpoint tea = scenario.playerCheckpoints.Single(
            item => item.step == TutorialStepId.UkEnglishBreakfastTea);
        TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, tea, player);
        Assert.That(player.deck.Hand.Any(card => card.trickId == "uk-english-breakfast-tea"), Is.True);
        Assert.That(player.deck.CountHeatInHand(), Is.EqualTo(1));
    }

    private static string CardLabel(CardData card)
    {
        return card.ToString();
    }
}
