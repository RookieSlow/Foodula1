using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class RaceStateLogFormatterTests
{
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void TeamSetupRetainsAuthoredOrderAndDoesNotApplyCheckpoints(TeamId team)
    {
        var scenario = TutorialScenarioDefinition.CreateTeamSpecialty(team);
        TutorialCardSpec[] cards = scenario.exactDrawOrder.ToArray();
        var checkpoints = scenario.playerCheckpoints.ToArray();
        var cues = scenario.opponentScript.ToArray();
        foreach (TutorialRunPhase phase in new[] { TutorialRunPhase.Guided, TutorialRunPhase.Practice })
        {
            var expected = new List<string>
            {
                $"[TUTORIAL_SETUP] event=runtime_ready scenario={scenario.id} phase={phase}",
                $"[TUTORIAL_SETUP] event=exact_deck_loaded count={cards.Length} opening={scenario.openingHandSize}",
                $"[TUTORIAL_SETUP] event=weather_script_ready start={scenario.guidedStartWeatherId} practice={scenario.practiceWeatherId}",
                $"[TUTORIAL_SETUP] event=player_checkpoints_ready count={checkpoints.Length} exact_zones=true"
            };
            if (scenario.tutorialPitLane != null)
                expected.Add($"[TUTORIAL_SETUP] event=virtual_pit_ready entry={scenario.tutorialPitLane.entryCell} " +
                    $"exit={scenario.tutorialPitLane.exitCell} official_track_mutated=false");
            if (cues.Length > 0 && cues[0] != null)
                expected.Add($"[TUTORIAL_SETUP] event=opponent_script_ready step={cues[0].step} " +
                    $"leader={cues[0].leaderCell} player={cues[0].playerCell} distance={cues[0].expectedSlipstreamDistance}");
            Assert.That(RaceStateLogFormatter.BuildTutorialSetup(scenario, phase), Is.EqualTo(expected));
        }
        Assert.That(scenario.exactDrawOrder, Is.EqualTo(cards));
        Assert.That(scenario.playerCheckpoints, Is.EqualTo(checkpoints));
        Assert.That(scenario.opponentScript, Is.EqualTo(cues));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void OptionalPitAndOnlyFirstOpponentKeepExistingEventContract(bool pit, bool firstCue)
    {
        var original = TutorialScenarioDefinition.CreateLeMansUk();
        var cues = new List<TutorialOpponentCue> { firstCue ? original.opponentScript[0] : null };
        // Later cues are not setup evidence; even a valid one must not replace a null first cue.
        cues.Add(new TutorialOpponentCue(TutorialStepId.Slipstream, 999, 997, 2));
        var scenario = (TutorialScenarioDefinition)typeof(TutorialScenarioDefinition)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single()
            .Invoke(new object[] { original.exactDrawOrder, original.steps, original.weatherScript, cues,
                original.playerCheckpoints, pit ? original.tutorialPitLane : null, original.id,
                original.trackId, original.playerTeam, original.teamVehicleBonusesEnabled, original.opponentTeam });
        string[] output = RaceStateLogFormatter.BuildTutorialSetup(scenario, TutorialRunPhase.Practice).ToArray();
        Assert.That(output.Length, Is.EqualTo(4 + (pit ? 1 : 0) + (firstCue ? 1 : 0)));
        Assert.That(output.Any(line => line.Contains("event=virtual_pit_ready")), Is.EqualTo(pit));
        Assert.That(output.Any(line => line.Contains("event=opponent_script_ready")), Is.EqualTo(firstCue));
        Assert.That(string.Join("\n", output), Does.Not.Contain("leader=999"));
        cues.Clear();
        Assert.That(RaceStateLogFormatter.BuildTutorialSetup(scenario, TutorialRunPhase.Practice).Count(),
            Is.EqualTo(4 + (pit ? 1 : 0)));
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void CareerSetupUsesImmutableLaunchRatherThanCurrentSeason(TeamId team)
    {
        var season = new CareerSeasonState();
        TeamId[] field = new[] { team }.Concat(FreeRaceRosterRules.AvailableTeams.Where(t => t != team).Take(3)).ToArray();
        Assert.That(CareerModeRules.TryStartSeason(season, team, field), Is.True);
        Assert.That(CareerRaceLaunchRequest.TryCreate(season, "formatter-" + team, out var launch), Is.True);
        string expected = $"[CAREER_SETUP] result_id=formatter-{team} race=1/8 " +
            $"track={launch.TrackId} team={team} competitors=4 tech_snapshot=true";
        TeamId[] participants = launch.Competitors.ToArray();
        Assert.That(RaceStateLogFormatter.BuildCareerSetup(launch), Is.EqualTo(expected));
        // A later change to the source season does not change the captured event.
        var mutableParticipants = (List<TeamId>)typeof(CareerSeasonState)
            .GetField("competitors", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(season);
        mutableParticipants.Clear();
        Assert.That(RaceStateLogFormatter.BuildCareerSetup(launch), Is.EqualTo(expected));
        Assert.That(launch.Competitors, Is.EqualTo(participants));
    }

    [Test]
    public void TwelveDriverRosterRetainsPrimaryIdAndOrderedRealShortNames()
    {
        FreeRaceRosterEntry[] roster = DriverCatalog.All.Reverse()
            .Select(driver => new FreeRaceRosterEntry(driver.Team, driver.Id)).ToArray();
        string expected = "[FREE_RACE_SETUP] field=12 player=" + roster[0].TeamId + ":" + roster[0].DriverId +
            " roster=[" + string.Join(",", DriverCatalog.All.Reverse().Select(driver => driver.Team + ":" + driver.ShortName)) + "]";
        FreeRaceRosterEntry[] before = roster.ToArray();
        Assert.That(RaceStateLogFormatter.BuildFreeRaceSetup(roster), Is.EqualTo(expected));
        Assert.That(roster, Is.EqualTo(before));
    }

    [Test]
    public void UnknownAndEmptyDriverIdsRemainDiagnosticFallbacks()
    {
        var roster = new[] { new FreeRaceRosterEntry(TeamId.CN, "missing-driver"), new FreeRaceRosterEntry(TeamId.US, null) };
        Assert.That(RaceStateLogFormatter.BuildFreeRaceSetup(roster), Is.EqualTo(
            "[FREE_RACE_SETUP] field=2 player=CN:missing-driver roster=[CN:missing-driver,US:]"));
    }

    [Test]
    public void EmptyRosterStillRejectsInvalidSetupInsteadOfInventingAPlayer()
    {
        Assert.Throws<IndexOutOfRangeException>(() => RaceStateLogFormatter.BuildFreeRaceSetup(Array.Empty<FreeRaceRosterEntry>()));
    }

    [TestCase(false, false, false, "PLAYER", "False", "False")]
    [TestCase(true, true, false, "AI", "True", "False")]
    [TestCase(true, false, true, "AI", "False", "True")]
    public void SnapshotPreservesFieldsAndDoesNotMutateDeck(
        bool ai, bool blown, bool finished, string role, string blownText, string finishedText)
    {
        var player = new PlayerState("车手", ai, 7, 2)
        {
            lap = 1, position = 7, isBlown = blown, hasFinished = finished
        };
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 3) }, new HeatPool(6));
        player.deck.DrawToHand(1);
        player.deck.DrawHeatFromPoolToHand(2);
        Assert.AreEqual($"[STATE] 车手 role={role} lap=1 position=7 gear=2 engine_heat=4 hand_speed=1 " +
            $"blown={blownText} finished={finishedText}", RaceStateLogFormatter.BuildPlayerSnapshot(player));
        Assert.AreEqual(4, player.deck.heatPool.remaining);
        Assert.AreEqual(3, player.deck.HandCount);
        Assert.AreEqual(7, player.position);
    }

    [Test]
    public void EmptyPlayedAreaPreservesEmptyListAndOptionalSlotFields()
    {
        var player = new PlayerState("Player", false, 0, 2)
        {
            teamId = TeamId.US, extraCardSlotsThisTurn = 1
        };
        Assert.AreEqual("[CARDS] Player source=PLAYER count=0 values=[] gear_limit=3 base_limit=2 " +
            "extra_slots=1 hotpot_attack=False hotpot_card_value=0",
            RaceStateLogFormatter.BuildPlayedCards(player, "PLAYER"));
    }

    [TestCase(1, 3)]
    [TestCase(2, 4)]
    public void ChinaCardsPreserveCommitOrderAndAttackMetadata(int consecutive, int baseCount)
    {
        var player = new PlayerState("CN", false, 0, 2)
        {
            teamId = TeamId.CN, chinaConsecutiveGearCount = consecutive,
            extraCardSlotsThisTurn = 1, hotpotAttackAppliedThisTurn = true,
            hotpotAttackCardValueThisTurn = 3
        };
        var first = new CardData(CardType.Speed, 3);
        player.playedSpeedCardsThisTurn.Add(first);
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 1));
        Assert.AreEqual($"[CARDS] CN source=AI count=2 values=[3,1] gear_limit={baseCount + 1} " +
            $"base_limit={baseCount} extra_slots=1 hotpot_attack=True hotpot_card_value=3",
            RaceStateLogFormatter.BuildPlayedCards(player, "AI"));
        Assert.AreSame(first, player.playedSpeedCardsThisTurn[0]);
        Assert.AreEqual(2, player.playedSpeedCardsThisTurn.Count);
        Assert.IsTrue(player.hotpotAttackAppliedThisTurn);
        Assert.AreEqual(1, player.extraCardSlotsThisTurn);
    }

    [Test]
    public void NullSourceRetainsOriginalEmptySourceValue()
    {
        var player = new PlayerState("Player", false, 0, 1) { teamId = TeamId.UK };
        StringAssert.Contains("source= count=0 values=[]", RaceStateLogFormatter.BuildPlayedCards(player, null));
    }
}
