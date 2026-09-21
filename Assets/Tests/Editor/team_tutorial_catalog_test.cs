using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class TeamTutorialCatalogTests
{
    [Test]
    public void CatalogContainsFoundationAndExactlySixTeamCourses()
    {
        IReadOnlyList<TeamTutorialCourseDefinition> courses = TeamTutorialCatalog.All;

        Assert.That(courses.Count, Is.EqualTo(7));
        Assert.That(courses[0].Kind, Is.EqualTo(TutorialCourseKind.Foundation));
        Assert.That(courses[0].IsPlayable, Is.True);
        CollectionAssert.AreEquivalent(
            new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP },
            courses.Where(course => course.HasTeam).Select(course => course.Team));
    }

    [Test]
    public void CourseIdsAndTeamEntriesAreUniqueAndComplete()
    {
        IReadOnlyList<TeamTutorialCourseDefinition> courses = TeamTutorialCatalog.All;

        Assert.That(courses.Select(course => course.Id).Distinct().Count(), Is.EqualTo(courses.Count));
        foreach (TeamTutorialCourseDefinition course in courses)
        {
            Assert.That(course.DisplayName, Is.Not.Empty, course.Id);
            Assert.That(course.Summary, Is.Not.Empty, course.Id);
            Assert.That(course.RecommendedTrackId, Is.Not.Empty, course.Id);
            Assert.That(course.Lessons.Count, Is.GreaterThanOrEqualTo(3), course.Id);
            foreach (TeamTutorialLessonDefinition lesson in course.Lessons)
            {
                Assert.That(lesson.Title, Is.Not.Empty, course.Id);
                Assert.That(lesson.Mechanic, Is.Not.Empty, lesson.Title);
                Assert.That(lesson.PlayerAction, Is.Not.Empty, lesson.Title);
                Assert.That(lesson.SuccessSignal, Is.Not.Empty, lesson.Title);
            }
        }

        foreach (TeamId team in new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP })
            Assert.That(TeamTutorialCatalog.GetForTeam(team), Is.Not.Null, team.ToString());
    }

    [Test]
    public void ChinaAndJapanCoursesDescribeCurrentRuntimeMechanicsOnly()
    {
        TeamTutorialCourseDefinition china = TeamTutorialCatalog.GetForTeam(TeamId.CN);
        string chinaText = string.Join(" ", china.Lessons.Select(lesson => lesson.Mechanic));
        StringAssert.Contains("首次 Go", chinaText);
        StringAssert.Contains("Recover", chinaText);
        StringAssert.Contains("不计入弯道限速", chinaText);
        StringAssert.DoesNotContain("电池衰减", chinaText);

        TeamTutorialCourseDefinition japan = TeamTutorialCatalog.GetForTeam(TeamId.JP);
        string japanText = string.Join(" ", japan.Lessons.Select(lesson => lesson.Title + " " + lesson.Mechanic));
        StringAssert.Contains("鱼雷天妇罗", japanText);
        StringAssert.Contains("关东慢煮", japanText);
        StringAssert.DoesNotContain("随机秘方牌池", japanText);
    }

    [TestCase(TeamId.CN, "shanghai_dim_sum", 7)]
    [TestCase(TeamId.US, "indianapolis_burger", 8)]
    public void SpecialtyScenarioUsesRealTeamRulesWithoutProgression(
        TeamId team, string track, int heatCapacity)
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(team);
        Assert.That(TeamTutorialCatalog.GetForTeam(team).IsPlayable, Is.True);
        Assert.That(scenario.playerTeam, Is.EqualTo(team));
        Assert.That(scenario.trackId, Is.EqualTo(track));
        Assert.That(scenario.engineHeatCapacity, Is.EqualTo(heatCapacity));
        Assert.That(scenario.teamVehicleBonusesEnabled, Is.True);
        Assert.That(scenario.techTreeEnabled, Is.False);
        Assert.That(scenario.driverSkillsEnabled, Is.False);
        Assert.That(scenario.normalRewardsEnabled, Is.False);
        Assert.That(scenario.normalProgressionWritesEnabled, Is.False);
        Assert.That(scenario.exactDrawOrder.Count, Is.EqualTo(16));
        Assert.That(scenario.steps.Last().id, Is.EqualTo(TutorialStepId.Review));
        Assert.That(scenario.steps.Select(step => step.id).Distinct().Count(),
            Is.EqualTo(scenario.steps.Count));
        Assert.That(scenario.playerCheckpoints.Count, Is.EqualTo(5));
    }

    [Test]
    public void ChinaCheckpointsTeachThreeThenFourAndRecoverWithoutRandomDraws()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.CN);
        TutorialPlayerCheckpoint first = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.ChinaFirstGo);
        TutorialPlayerCheckpoint second = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.ChinaConsecutiveGo);
        TutorialPlayerCheckpoint recover = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.ChinaRecover);
        Assert.That(ChinaGearShiftRules.Resolve(first.gear, first.chinaConsecutiveGearCount, 2).SpeedCardCount,
            Is.EqualTo(3));
        ChinaGearShiftRules.Result secondGo = ChinaGearShiftRules.Resolve(
            second.gear, second.chinaConsecutiveGearCount, 2);
        Assert.That(secondGo.SpeedCardCount, Is.EqualTo(4));
        Assert.That(secondGo.AdditionalHeat, Is.EqualTo(1));
        Assert.That(recover.heatInHand, Is.EqualTo(3));
        Assert.That(ChinaGearShiftRules.Resolve(recover.gear, recover.chinaConsecutiveGearCount, 1).Cooldown,
            Is.EqualTo(3));
        Assert.That(scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.ChinaHotpot)
            .exactDrawOrder[0].trickId, Is.EqualTo("cn-hotpot-base"));
    }

    [Test]
    public void UsLandmarkCardsHaveScriptedPriorLandmarkAndTailwindLeader()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.US);
        foreach (TutorialStepId step in new[] { TutorialStepId.UsFries, TutorialStepId.UsCola })
            Assert.That(scenario.playerCheckpoints.Single(c => c.step == step).crossedLandmarkLastTurn,
                Is.True);
        Assert.That(scenario.opponentScript.Single().step, Is.EqualTo(TutorialStepId.UsSlipstream));
        Assert.That(scenario.opponentScript.Single().expectedSlipstreamDistance, Is.EqualTo(2));
        TutorialOpponentCue cue = scenario.opponentScript.Single();
        Assert.That(RaceSession.ForwardDistance(cue.playerCell, cue.leaderCell, 42), Is.EqualTo(4),
            "The cue leaves room for the prescribed speed-1 card plus US straight +1.");
        Assert.That(RaceSession.ForwardDistance(cue.playerCell + 2, cue.leaderCell, 42), Is.EqualTo(2),
            "After speed 1 and the US straight bonus, the player is exactly in tailwind range.");
        Assert.That(TutorialOpponentCueRules.ShouldResolveSlipstreamForFollower(cue, true), Is.True);
        Assert.That(TutorialOpponentCueRules.ShouldResolveSlipstreamForFollower(cue, false), Is.False);
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.JP));
    }

    [Test]
    public void UsGuideNamesTheExactCardsForEveryPlayableStep()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.US);

        TutorialStepDefinition straight = scenario.steps.Single(step => step.id == TutorialStepId.UsStraight);
        StringAssert.Contains("1 张速度 1", straight.actionPrompt);
        StringAssert.Contains("确认出牌", straight.actionPrompt);

        TutorialStepDefinition corner = scenario.steps.Single(step => step.id == TutorialStepId.UsCorner);
        StringAssert.Contains("速度 3 + 速度 2", corner.actionPrompt);
        StringAssert.Contains("不要选速度 1", corner.actionPrompt);

        TutorialStepDefinition slipstream = scenario.steps.Single(step => step.id == TutorialStepId.UsSlipstream);
        StringAssert.Contains("1 张速度 1", slipstream.actionPrompt);
        StringAssert.Contains("不要选速度 3", slipstream.actionPrompt);

        TutorialStepDefinition fries = scenario.steps.Single(step => step.id == TutorialStepId.UsFries);
        StringAssert.Contains("薯条特技牌", fries.actionPrompt);
        StringAssert.Contains("不需要速度牌", fries.actionPrompt);
    }

    [Test]
    public void UsCornerCheckpointMakesThreePlusTwoCrossIndianapolisApex()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.US);
        TutorialPlayerCheckpoint checkpoint = scenario.playerCheckpoints.Single(
            candidate => candidate.step == TutorialStepId.UsCorner);
        TrackConfig track = TrackDataLoader.LoadConfig(scenario.trackId);
        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(track);

        Assert.That(checkpoint.playerCell, Is.EqualTo(2));
        Assert.That(TrackRules.GetUniqueApexCornersCrossed(
            nodes, checkpoint.playerCell, checkpoint.playerCell + 5).Count, Is.EqualTo(1));
        Assert.That(nodes[7].isApex, Is.True,
            "The authored 3+2 lesson must cross Indianapolis's first apex before resolving extra heat.");
    }

    [Test]
    public void UsTutorialRejectsAmbiguousSpeedCardsBeforeTheyCauseEngineFailure()
    {
        string reason;
        Assert.That(TutorialSpecialtyCardRules.ValidateUsSpeedSelection(
            TutorialStepId.UsStraight,
            null,
            new[] { new CardData(CardType.Speed, 3) },
            out reason), Is.False);
        StringAssert.Contains("速度 1", reason);

        Assert.That(TutorialSpecialtyCardRules.ValidateUsSpeedSelection(
            TutorialStepId.UsCorner,
            null,
            new[] { new CardData(CardType.Speed, 3), new CardData(CardType.Speed, 2) },
            out reason), Is.True, reason);
        Assert.That(TutorialSpecialtyCardRules.ValidateUsSpeedSelection(
            TutorialStepId.UsCorner,
            null,
            new[] { new CardData(CardType.Speed, 1), new CardData(CardType.Speed, 2) },
            out reason), Is.False);
        StringAssert.Contains("速度 3", reason);

        Assert.That(TutorialSpecialtyCardRules.ValidateUsSpeedPhaseCompletion(
            TutorialStepId.UsCorner,
            new[] { new CardData(CardType.Speed, 3), new CardData(CardType.Speed, 2) },
            out reason), Is.True, reason);
        Assert.That(TutorialSpecialtyCardRules.ValidateUsSpeedPhaseCompletion(
            TutorialStepId.UsSlipstream,
            new[] { new CardData(CardType.Speed, 3) },
            out reason), Is.False);
    }

    [TestCase(TeamId.CN, TutorialStepId.ChinaConsecutiveGo)]
    [TestCase(TeamId.US, TutorialStepId.UsFries)]
    public void SpecialtyCheckpointRestoresExactZonesAndTeamSpecificCondition(
        TeamId team, TutorialStepId step)
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(team);
        TutorialPlayerCheckpoint checkpoint = scenario.playerCheckpoints.Single(c => c.step == step);
        var player = new PlayerState("tutorial", false, 0, 1) { teamId = team };

        TutorialCheckpointApplyResult result =
            TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, checkpoint, player);

        Assert.That(result.success, Is.True, result.failureReason);
        Assert.That(player.position, Is.EqualTo(checkpoint.playerCell));
        Assert.That(player.deck.UsesExactOrder, Is.True);
        Assert.That(player.deck.heatPool.remaining + player.deck.CountPermanentHeatOutsideEngine(),
            Is.EqualTo(scenario.engineHeatCapacity));
        Assert.That(player.chinaConsecutiveGearCount, Is.EqualTo(checkpoint.chinaConsecutiveGearCount));
        Assert.That(player.trickState.crossedLandmarkLastTurn,
            Is.EqualTo(checkpoint.crossedLandmarkLastTurn));
    }

    [TestCase(TeamId.CN)]
    [TestCase(TeamId.US)]
    public void SpecialtyGuideAdvancesOnlyAfterEachAuthoredAction(TeamId team)
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(team);
        var guide = new TutorialRuntimeDirector(scenario);
        for (int i = 0; i < scenario.steps.Count; i++)
        {
            TutorialStepDefinition step = scenario.steps[i];
            Assert.That(guide.CurrentStep.id, Is.EqualTo(step.id));
            Assert.That(guide.TryPerform(step.requiredAction, out string failure),
                Is.True, failure);
            Assert.That(guide.TryNext(out failure), Is.True, failure);
        }
        Assert.That(guide.Phase, Is.EqualTo(TutorialRunPhase.Practice));
        Assert.That(guide.StepCount, Is.EqualTo(scenario.steps.Count));
    }
}
