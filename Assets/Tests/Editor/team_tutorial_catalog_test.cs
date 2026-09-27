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
    public void SpecialtyCoursesCanLaunchOnlyWhenTheirMenusCoverEveryScenarioStep()
    {
        foreach (TeamId team in new[]
        {
            TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP
        })
        {
            TeamTutorialCourseDefinition course = TeamTutorialCatalog.GetForTeam(team);
            TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(team);
            TutorialStepId[] expected = scenario.steps
                .Where(step => step.id != TutorialStepId.ObjectiveAndInterface)
                .Select(step => step.id)
                .ToArray();
            TutorialStepId[] mapped = course.Lessons
                .SelectMany(lesson => lesson.ScenarioStepIds)
                .ToArray();

            Assert.That(TeamTutorialCatalog.IsScenarioAligned(course), Is.True, team.ToString());
            Assert.That(course.IsPlayable, Is.True, team.ToString());
            CollectionAssert.AreEquivalent(expected, mapped, team.ToString());
            Assert.That(mapped.Distinct().Count(), Is.EqualTo(mapped.Length),
                $"{team} scenario steps must not be owned by multiple menu lessons");
        }
    }

    [Test]
    public void SpecialtyCourseWithAnUnmappedScenarioStepIsNotPlayable()
    {
        var incomplete = new TeamTutorialCourseDefinition(
            "tutorial-team-us-incomplete-test", TutorialCourseKind.TeamSpecialty,
            TeamId.US, true, "US test", "test", "test", "indianapolis_burger", true,
            new TeamTutorialLessonDefinition(
                "直道爆发", "直道 +1", "打速度 1", "前进 2 格", TutorialStepId.UsStraight));
        var duplicated = new TeamTutorialCourseDefinition(
            "tutorial-team-us-duplicate-test", TutorialCourseKind.TeamSpecialty,
            TeamId.US, true, "US test", "test", "test", "indianapolis_burger", true,
            new TeamTutorialLessonDefinition(
                "直道爆发 A", "直道 +1", "打速度 1", "前进 2 格", TutorialStepId.UsStraight),
            new TeamTutorialLessonDefinition(
                "直道爆发 B", "直道 +1", "打速度 1", "前进 2 格", TutorialStepId.UsStraight));
        var foreignStep = new TeamTutorialCourseDefinition(
            "tutorial-team-us-foreign-test", TutorialCourseKind.TeamSpecialty,
            TeamId.US, true, "US test", "test", "test", "indianapolis_burger", true,
            new TeamTutorialLessonDefinition(
                "意大利弯道", "操控 +2", "观察弯道", "看见限速修正", TutorialStepId.ItCorner));

        Assert.That(TeamTutorialCatalog.IsScenarioAligned(incomplete), Is.False);
        Assert.That(incomplete.IsPlayable, Is.False,
            "an incomplete menu must not launch a scripted course with inaccessible lessons");
        Assert.That(TeamTutorialCatalog.IsScenarioAligned(duplicated), Is.False,
            "one scripted step cannot be represented by multiple lesson rows");
        Assert.That(TeamTutorialCatalog.IsScenarioAligned(foreignStep), Is.False,
            "a course cannot map to another team's scenario step");
        Assert.That(TeamTutorialCatalog.IsScenarioAligned(
            TeamTutorialCatalog.All[0]), Is.False,
            "the foundation course is not a team-specialty scenario");
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
        StringAssert.DoesNotContain("被超车时对方 +1", japanText);
    }

    [Test]
    public void GermanyMenuListsOnlyItsScriptedLessons()
    {
        TeamTutorialCourseDefinition germany = TeamTutorialCatalog.GetForTeam(TeamId.DE);

        CollectionAssert.AreEqual(new[]
        {
            "直道保底", "酸菜发酵", "黑面包垫底", "完整练习"
        }, germany.Lessons.Select(lesson => lesson.Title));
        StringAssert.DoesNotContain("高耐久巡航",
            string.Join(" ", germany.Lessons.Select(lesson => lesson.Title)));
    }

    [Test]
    public void UnitedStatesMenuMirrorsExactScriptedActions()
    {
        TeamTutorialCourseDefinition unitedStates = TeamTutorialCatalog.GetForTeam(TeamId.US);

        CollectionAssert.AreEqual(new[]
        {
            "直道爆发", "弯道代价", "强化尾流", "薯条", "可乐", "完整练习"
        }, unitedStates.Lessons.Select(lesson => lesson.Title));
        Assert.That(unitedStates.Lessons[0].PlayerAction, Does.Contain("G1").And.Contain("速度 1"));
        Assert.That(unitedStates.Lessons[1].PlayerAction, Does.Contain("G2").And.Contain("速度 3 + 速度 2"));
        Assert.That(unitedStates.Lessons[2].SuccessSignal, Does.Contain("后车"));
        Assert.That(unitedStates.Lessons[3].PlayerAction, Is.EqualTo("只打薯条特技牌"));
        Assert.That(unitedStates.Lessons[4].PlayerAction, Is.EqualTo("只打可乐特技牌"));
    }

    [Test]
    public void ChinaMenuMatchesGoRecoverAndTrickStepsWithoutPitClaims()
    {
        TeamTutorialCourseDefinition china = TeamTutorialCatalog.GetForTeam(TeamId.CN);
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.CN);

        CollectionAssert.AreEqual(new[]
        {
            "Go 节奏", "Recover 回收", "火锅底料", "冰糕防守", "完整练习"
        }, china.Lessons.Select(lesson => lesson.Title));
        Assert.That(string.Join(" ", china.Lessons.Select(lesson => lesson.Mechanic)),
            Does.Not.Contain("维修区"));
        CollectionAssert.AreEqual(new[]
        {
            TutorialStepId.ObjectiveAndInterface, TutorialStepId.ChinaFirstGo,
            TutorialStepId.ChinaConsecutiveGo, TutorialStepId.ChinaRecover,
            TutorialStepId.ChinaHotpot, TutorialStepId.ChinaIceJelly, TutorialStepId.Review
        }, scenario.steps.Select(step => step.id));
        Assert.That(china.Lessons[2].PlayerAction, Does.Contain("火锅底料").And.Contain("1 张速度牌"));
        Assert.That(china.Lessons[3].PlayerAction, Does.Contain("Recover").And.Contain("冰糕"));
    }

    [Test]
    public void ItalyMenuNamesEachScriptedMechanicAndPractice()
    {
        TeamTutorialCourseDefinition italy = TeamTutorialCatalog.GetForTeam(TeamId.IT);
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.IT);

        CollectionAssert.AreEqual(new[]
        {
            "操控优势", "出弯加速", "帕尔马干酪", "基安蒂红酒", "完整练习"
        }, italy.Lessons.Select(lesson => lesson.Title));
        CollectionAssert.AreEqual(new[]
        {
            TutorialStepId.ObjectiveAndInterface, TutorialStepId.ItCorner,
            TutorialStepId.ItCornerExit, TutorialStepId.ItParmigiano,
            TutorialStepId.ItChianti, TutorialStepId.Review
        }, scenario.steps.Select(step => step.id));
        Assert.That(italy.Lessons[0].PlayerAction, Does.Contain("G1").And.Contain("速度 1"));
        Assert.That(italy.Lessons[1].SuccessSignal, Does.Contain("2 格"));
        Assert.That(italy.Lessons[2].SuccessSignal, Does.Contain("后车").And.Contain("+4"));
        Assert.That(italy.Lessons[3].PlayerAction, Does.Contain("基安蒂红酒"));
    }

    [TestCase(TeamId.UK, "silverstone_afternoon_tea", 7)]
    [TestCase(TeamId.JP, "suzuka_sushi", 6)]
    [TestCase(TeamId.CN, "shanghai_dim_sum", 7)]
    [TestCase(TeamId.US, "indianapolis_burger", 8)]
    [TestCase(TeamId.DE, "nurburgring_bier", 8)]
    [TestCase(TeamId.IT, "monza_pasta", 6)]
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
        Assert.That(scenario.playerCheckpoints.Count,
            Is.EqualTo(team == TeamId.UK ? 2 : team == TeamId.JP ? 3 :
                team == TeamId.DE ? 3 : team == TeamId.IT ? 4 : 5));
    }

    [Test]
    public void JapanCourseScriptsKantoCarryAndStationaryOvertakeTarget()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.JP);
        Assert.That(scenario.steps.Select(s => s.id), Is.EqualTo(new[]
        {
            TutorialStepId.ObjectiveAndInterface, TutorialStepId.JpKantoSkip,
            TutorialStepId.JpKantoRelease, TutorialStepId.JpTorpedo, TutorialStepId.Review
        }));
        var player = new PlayerState("player", false, 0, 1) { teamId = TeamId.JP };
        TutorialPlayerCheckpoint skip = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.JpKantoSkip);
        Assert.That(TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, skip, player).success, Is.True);
        Assert.That(player.gear, Is.EqualTo(2));
        Assert.That(player.deck.Hand.First().trickId, Is.EqualTo("jp-kanto-oden"));
        Assert.That(player.deck.CountHeatInHand(), Is.EqualTo(1));
        Assert.That(TutorialSpecialtyCardRules.JpKantoResolved(
            true, 2, 2, 1, 0, 5, 6), Is.True);
        Assert.That(TutorialSpecialtyCardRules.JpKantoResolved(
            true, 2, 2, 1, 1, 5, 6), Is.False);

        TutorialPlayerCheckpoint release = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.JpKantoRelease);
        Assert.That(TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, release, player).success, Is.True);
        Assert.That(player.trickState.kantoOdenActive, Is.True);
        Assert.That(player.trickState.kantoOdenAccumulatedCards, Is.EqualTo(2));
        var session = new RaceSession();
        session.BeginTurn(player);
        Assert.That(player.extraCardSlotsThisTurn, Is.EqualTo(2));
        Assert.That(player.trickState.kantoOdenActive, Is.False);
        Assert.That(TeamGearRules.GetSpeedCardCount(TeamId.JP, 1, 0,
            player.extraCardSlotsThisTurn), Is.EqualTo(3));
        Assert.That(TutorialSpecialtyCardRules.ValidateJpSpeedCompletion(
            TutorialStepId.JpKantoRelease, false, 2,
            new[] { new CardData(CardType.Speed, 1), new CardData(CardType.Speed, 2),
                new CardData(CardType.Speed, 2) }, out _), Is.True);
        Assert.That(TutorialSpecialtyCardRules.ValidateJpSpeedCompletion(
            TutorialStepId.JpKantoRelease, false, 1, null, out _), Is.False);

        TutorialPlayerCheckpoint torpedo = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.JpTorpedo);
        Assert.That(TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, torpedo, player).success, Is.True);
        Assert.That(player.trickState.kantoOdenActive, Is.False);
        Assert.That(player.deck.Hand.First().trickId, Is.EqualTo("jp-torpedo-tempura"));
        TutorialOpponentCue cue = scenario.opponentScript.Single();
        Assert.That(cue.step, Is.EqualTo(TutorialStepId.JpTorpedo));
        Assert.That(cue.leaderCell - cue.playerCell, Is.EqualTo(2));
        Assert.That(TutorialOpponentCueRules.ResolveLeaderMovement(cue, true, 3), Is.Zero);
        Assert.That(TutorialOpponentCueRules.ShouldResolveSlipstreamForFollower(cue, false), Is.False);
        player.position = cue.playerCell;
        player.cornerTotalThisTurn = 3;
        var leader = new PlayerState("leader", true, cue.leaderCell, 1);
        Assert.That(RaceMovementRules.CountOvertakes(player, new[] { player, leader },
            60, false, null), Is.EqualTo(1));
        Assert.That(TutorialSpecialtyCardRules.ValidateJpSpeedSelection(
            TutorialStepId.JpTorpedo, false, null,
            new[] { new CardData(CardType.Speed, 3) }, out _), Is.False);
        Assert.That(TutorialSpecialtyCardRules.ValidateJpSpeedCompletion(
            TutorialStepId.JpTorpedo, true, 0,
            new[] { new CardData(CardType.Speed, 3) }, out _), Is.True);
        Assert.That(TutorialSpecialtyCardRules.ValidateJpTrickSelection(
            TutorialStepId.JpTorpedo, "jp-kanto-oden", out _), Is.False);
        Assert.That(TutorialSpecialtyCardRules.ValidateJpTrickSelection(
            TutorialStepId.JpTorpedo, "jp-torpedo-tempura", out _), Is.True);
        Assert.That(TutorialSpecialtyFocusRules.SelectionTarget(TutorialStepId.JpTorpedo, false),
            Is.EqualTo(TutorialFocusTarget.TeamTrickCard));
        Assert.That(TutorialSpecialtyFocusRules.SelectionTarget(TutorialStepId.JpTorpedo, true),
            Is.EqualTo(TutorialFocusTarget.Hand));
        Assert.That(TutorialSpecialtyFocusRules.CanHighlightEndCards(
            TutorialStepId.JpTorpedo, false, true, deEffectReady: false), Is.False);
        Assert.That(TutorialSpecialtyFocusRules.CanHighlightEndCards(
            TutorialStepId.JpTorpedo, false, true, deEffectReady: true), Is.True);
    }

    [Test]
    public void UnitedKingdomCourseUsesExactTrickAndHeatCheckpoints()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.UK);
        Assert.That(scenario.id, Is.EqualTo("tutorial_team_uk_v1"));
        Assert.That(scenario.steps.Select(s => s.id), Is.EqualTo(new[]
        {
            TutorialStepId.ObjectiveAndInterface, TutorialStepId.UkSpecialtyScone,
            TutorialStepId.UkSpecialtyTea, TutorialStepId.Review
        }));
        var player = new PlayerState("tutorial", false, 0, 1) { teamId = TeamId.UK };
        TutorialPlayerCheckpoint scone = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.UkSpecialtyScone);
        Assert.That(TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, scone, player).success, Is.True);
        Assert.That(player.deck.Hand.First().trickId, Is.EqualTo("uk-scone"));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(7));
        TutorialPlayerCheckpoint tea = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.UkSpecialtyTea);
        Assert.That(TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, tea, player).success, Is.True);
        Assert.That(player.deck.Hand.First().trickId, Is.EqualTo("uk-english-breakfast-tea"));
        Assert.That(player.deck.CountHeatInHand(), Is.EqualTo(1));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(6));
        Assert.That(TutorialSpecialtyCardRules.ValidateUkTrickSelection(
            TutorialStepId.UkSpecialtyTea, "uk-scone", out string reason), Is.False);
        StringAssert.Contains("红茶", reason);
        Assert.That(TutorialSpecialtyCardRules.ValidateUkTrickSelection(
            TutorialStepId.UkSpecialtyTea, "uk-english-breakfast-tea", out _), Is.True);
        Assert.That(TutorialSpecialtyCardRules.ValidateTrickLessonSpeedSelection(
            TutorialStepId.UkSpecialtyScone, false,
            new[] { new CardData(CardType.Speed, 1) }, out _), Is.False);
        Assert.That(TutorialSpecialtyCardRules.UkSconeResolved(6, 5, 0, 2), Is.True);
        Assert.That(TutorialSpecialtyCardRules.UkSconeResolved(6, 6, 0, 2), Is.False);
        Assert.That(TutorialSpecialtyCardRules.UkTeaResolved(1, 0, 5, 6), Is.True);
        Assert.That(TutorialSpecialtyCardRules.UkTeaResolved(1, 0, 5, 5), Is.False);
        Assert.That(TutorialSpecialtyFocusRules.SelectionTarget(TutorialStepId.UkSpecialtyScone, false),
            Is.EqualTo(TutorialFocusTarget.UkSconeCard));
        Assert.That(TutorialSpecialtyFocusRules.SelectionTarget(TutorialStepId.UkSpecialtyTea, false),
            Is.EqualTo(TutorialFocusTarget.UkTeaCard));
    }

    [Test]
    public void ItalyCourseScriptsRealCornerFollowerAndHeatRecovery()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.IT);
        TutorialPlayerCheckpoint corner = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.ItCorner);
        TrackConfig track = TrackDataLoader.LoadConfig(scenario.trackId);
        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(track);
        Assert.That(TrackRules.GetUniqueApexCornersCrossed(nodes,
            corner.playerCell, corner.playerCell + 1).Count, Is.EqualTo(1));
        Assert.That(TeamVehicleRules.GetHandling(TeamId.IT), Is.EqualTo(2));

        TutorialPlayerCheckpoint exit = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.ItCornerExit);
        Assert.That(exit.italyCornerExitBoostReady, Is.True);
        Assert.That(exit.beginAtCardSelection, Is.True);
        Assert.That(exit.exactDrawOrder[0].value, Is.EqualTo(1));
        Assert.That(TrackRules.GetUniqueApexCornersCrossed(nodes,
            exit.playerCell, exit.playerCell + 2).Count, Is.Zero);
        Assert.That(scenario.steps.Single(s => s.id == TutorialStepId.ItCornerExit).requiredAction,
            Is.EqualTo(TutorialAction.ResolveItCornerExit));

        TutorialPlayerCheckpoint tailwind = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.ItParmigiano);
        Assert.That(tailwind.exactDrawOrder[0].trickId, Is.EqualTo("it-parmigiano"));
        TutorialOpponentCue cue = scenario.opponentScript.Single();
        Assert.That(cue.step, Is.EqualTo(TutorialStepId.ItParmigiano));
        Assert.That(RaceSession.ForwardDistance(cue.playerCell + 1, cue.leaderCell,
            track.gameCellCount), Is.EqualTo(1));
        Assert.That(TutorialOpponentCueRules.ShouldResolveSlipstreamForFollower(cue, true), Is.True);
        Assert.That(TutorialOpponentCueRules.ShouldResolveSlipstreamForFollower(cue, false), Is.False);
        Assert.That(TutorialOpponentCueRules.ResolveLeaderMovement(cue, true, 4), Is.Zero);

        TutorialPlayerCheckpoint wine = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.ItChianti);
        Assert.That(wine.heatInHand, Is.EqualTo(1));
        Assert.That(wine.exactDrawOrder[0].trickId, Is.EqualTo("it-chianti"));
        Assert.That(wine.beginAtCardSelection, Is.True);
    }

    [Test]
    public void ItalyCardGateRequiresNamedTrickThenSingleSpeedOneForTailwind()
    {
        var one = new[] { new CardData(CardType.Speed, 1) };
        var two = new[] { new CardData(CardType.Speed, 2) };
        Assert.That(TutorialSpecialtyCardRules.ValidateItCornerSpeedSelection(
            TutorialStepId.ItCorner, null, two, out _), Is.False);
        Assert.That(TutorialSpecialtyCardRules.ValidateItCornerSpeedCompletion(
            TutorialStepId.ItCorner, one, out _), Is.True);
        Assert.That(TutorialSpecialtyCardRules.ValidateItCornerSpeedSelection(
            TutorialStepId.ItCornerExit, null, two, out _), Is.False);
        Assert.That(TutorialSpecialtyCardRules.ValidateItCornerSpeedCompletion(
            TutorialStepId.ItCornerExit, one, out _), Is.True);
        Assert.That(TutorialSpecialtyCardRules.ValidateItCornerSpeedCompletion(
            TutorialStepId.ItCornerExit, null, out _), Is.False);
        Assert.That(TutorialSpecialtyCardRules.ValidateItSlipstreamSpeedSelection(
            TutorialStepId.ItParmigiano, false, null, one, out _), Is.False);
        Assert.That(TutorialSpecialtyCardRules.ValidateItSlipstreamSpeedSelection(
            TutorialStepId.ItParmigiano, true, null, one, out _), Is.True);
        Assert.That(TutorialSpecialtyCardRules.ValidateItSlipstreamSpeedSelection(
            TutorialStepId.ItParmigiano, true, null, two, out _), Is.False);
        Assert.That(TutorialSpecialtyCardRules.ValidateItSlipstreamSpeedCompletion(
            TutorialStepId.ItParmigiano, true, one, out _), Is.True);
        Assert.That(TutorialSpecialtyCardRules.ValidateItSlipstreamSpeedCompletion(
            TutorialStepId.ItParmigiano, true, null, out _), Is.False);
        Assert.That(TutorialSpecialtyCardRules.ValidateTrickLessonSpeedSelection(
            TutorialStepId.ItChianti, false, one, out _), Is.False);
        Assert.That(TutorialSpecialtyCardRules.ValidateTrickLessonCompletion(
            TutorialStepId.ItChianti, false, out _), Is.False);
        Assert.That(TutorialSpecialtyFocusRules.SelectionTarget(TutorialStepId.ItParmigiano, true),
            Is.EqualTo(TutorialFocusTarget.Hand));
        Assert.That(TutorialSpecialtyFocusRules.CanHighlightEndCards(
            TutorialStepId.ItParmigiano, false, true, deEffectReady: true), Is.True);
    }

    [Test]
    public void GermanyCourseFixesStraightHandAndBothTrickCheckpoints()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.DE);
        TutorialPlayerCheckpoint straight = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.DeStraight);
        Assert.That(straight.playerCell, Is.EqualTo(28));
        Assert.That(straight.exactDrawOrder[0].value, Is.EqualTo(1));
        Assert.That(TeamVehicleRules.GetStraightMovementBonus(TeamId.DE) +
            TeamVehicleRules.GetStraightCardBonus(TeamId.DE, 1) + 1, Is.EqualTo(3));
        Assert.That(scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.DeSauerkraut)
            .exactDrawOrder[0].trickId, Is.EqualTo("de-sauerkraut"));
        TutorialPlayerCheckpoint bread = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.DeSchwarzbrot);
        Assert.That(bread.exactDrawOrder[0].trickId, Is.EqualTo("de-schwarzbrot"));
        Assert.That(bread.gear, Is.EqualTo(3));
        Assert.That(RaceRules.GetMissingSpeedCardCount(3, 1), Is.EqualTo(2));
        TrackConfig track = TrackDataLoader.LoadConfig(scenario.trackId);
        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(track);
        TutorialPlayerCheckpoint cabbage = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.DeSauerkraut);
        Assert.That(TrackRules.GetUniqueApexCornersCrossed(nodes,
            cabbage.playerCell, cabbage.playerCell + 1).Count, Is.EqualTo(1));
        Assert.That(TrackRules.GetUniqueApexCornersCrossed(nodes,
            straight.playerCell, straight.playerCell + 1).Count, Is.Zero);
        CollectionAssert.AreEqual(
            scenario.CreateExactDeck().Select(c => c.ToString()).ToArray(),
            TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.DE)
                .CreateExactDeck().Select(c => c.ToString()).ToArray());
    }

    [Test]
    public void GermanyStraightSelectionAndTrickActionsCannotAdvanceEarly()
    {
        var one = new List<CardData> { new CardData(CardType.Speed, 1) };
        var two = new List<CardData> { new CardData(CardType.Speed, 2) };
        Assert.That(TutorialSpecialtyCardRules.ValidateDeStraightSpeedSelection(
            TutorialStepId.DeStraight, null, one, out _), Is.True);
        Assert.That(TutorialSpecialtyCardRules.ValidateDeStraightSpeedSelection(
            TutorialStepId.DeStraight, null, two, out _), Is.False);
        Assert.That(TutorialSpecialtyCardRules.ValidateDeStraightSpeedCompletion(
            TutorialStepId.DeStraight, one, out _), Is.True);
        Assert.That(TutorialSpecialtyCardRules.ValidateDeStraightSpeedCompletion(
            TutorialStepId.DeStraight, null, out _), Is.False);
        foreach (TutorialStepId step in new[] { TutorialStepId.DeSauerkraut, TutorialStepId.DeSchwarzbrot })
        {
            Assert.That(TutorialSpecialtyCardRules.ValidateDeEffectSpeedSelection(
                step, false, null, one, out _), Is.False);
            Assert.That(TutorialSpecialtyCardRules.ValidateDeEffectSpeedSelection(
                step, true, null, one, out _), Is.True);
            Assert.That(TutorialSpecialtyCardRules.ValidateDeEffectSpeedSelection(
                step, true, null, two, out _), Is.False);
            Assert.That(TutorialSpecialtyCardRules.ValidateDeEffectSpeedCompletion(
                step, true, null, out _), Is.False);
            Assert.That(TutorialSpecialtyCardRules.ValidateDeEffectSpeedCompletion(
                step, true, one, out _), Is.True);
            Assert.That(TutorialSpecialtyFocusRules.RequiresLessonCard(step), Is.True);
            Assert.That(TutorialSpecialtyFocusRules.SelectionTarget(step, true),
                Is.EqualTo(TutorialFocusTarget.Hand));
            Assert.That(TutorialSpecialtyFocusRules.CanHighlightEndCards(
                step, false, true, deEffectReady: true), Is.True);
        }
        var cabbageState = new TrickCardState { sauerkrautPlayed = true };
        Assert.That(TrickCardRules.GetSauerkrautBonus(cabbageState, true), Is.EqualTo(2));
        Assert.That(TrickCardRules.GetSauerkrautBonus(cabbageState, false), Is.EqualTo(1));
        var breadState = new TrickCardState { schwarzbrotActive = true, schwarzbrotRemaining = 1 };
        Assert.That(TrickCardRules.ApplySchwarzbrot(breadState, 2), Is.EqualTo(1));
        Assert.That(breadState.schwarzbrotActive, Is.False);
        Assert.That(TrickCardRules.ApplySchwarzbrot(breadState, 2), Is.EqualTo(2));
    }

    [Test]
    public void GermanyGuideWaitsForResolvedEffectRatherThanTrickPlay()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.DE);
        var guide = new TutorialRuntimeDirector(scenario);
        Assert.That(guide.TryPerform(TutorialAction.AcknowledgeObjective, out _), Is.True);
        Assert.That(guide.TryNext(out _), Is.True);
        Assert.That(guide.TryPerform(TutorialAction.ResolveDeStraight, out _), Is.True);
        Assert.That(guide.TryNext(out _), Is.True);
        Assert.That(guide.CurrentStep.id, Is.EqualTo(TutorialStepId.DeSauerkraut));
        Assert.That(guide.TryPerform(TutorialAction.ResolveDeSchwarzbrot, out _), Is.False);
        Assert.That(guide.IsActiveStepComplete, Is.False);
        Assert.That(guide.TryPerform(TutorialAction.ResolveDeSauerkraut, out _), Is.True);
        Assert.That(guide.TryNext(out _), Is.True);
        Assert.That(guide.CurrentStep.id, Is.EqualTo(TutorialStepId.DeSchwarzbrot));
        Assert.That(guide.IsActiveStepComplete, Is.False);
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
        Assert.That(TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.JP).trackId,
            Is.EqualTo("suzuka_sushi"));
    }

    [Test]
    public void TeachingLeadersPairWithPreparedPlayerCheckpointBeforeGuide()
    {
        TutorialScenarioDefinition foundation = TutorialScenarioDefinition.CreateLeMansUk();
        TutorialOpponentCue foundationLeader = foundation.opponentScript.Single();
        TutorialPlayerCheckpoint foundationCheckpoint = foundation.playerCheckpoints
            .Single(c => c.step == foundationLeader.step);
        Assert.That(TutorialOpponentCueRules.AppliesWithPlayerCheckpoint(
            foundationLeader, foundationCheckpoint), Is.True);
        Assert.That(foundationCheckpoint.playerCell,
            Is.EqualTo(foundationLeader.playerCell));

        foreach (TeamId team in new[] { TeamId.US, TeamId.IT, TeamId.JP })
        {
            TutorialScenarioDefinition scenario =
                TutorialScenarioDefinition.CreateTeamSpecialty(team);
            foreach (TutorialOpponentCue leader in scenario.opponentScript)
            {
                TutorialPlayerCheckpoint checkpoint = scenario.playerCheckpoints
                    .Single(c => c.step == leader.step);
                Assert.That(TutorialOpponentCueRules.AppliesWithPlayerCheckpoint(
                    leader, checkpoint), Is.True, team.ToString());
                Assert.That(checkpoint.playerCell, Is.EqualTo(leader.playerCell),
                    $"{team} guide must not display a different player cell");
            }
        }

        TutorialOpponentCue jpLeader = TutorialScenarioDefinition
            .CreateTeamSpecialty(TeamId.JP).opponentScript.Single();
        TutorialPlayerCheckpoint otherStep = TutorialScenarioDefinition
            .CreateTeamSpecialty(TeamId.JP).playerCheckpoints
            .Single(c => c.step == TutorialStepId.JpKantoSkip);
        Assert.That(TutorialOpponentCueRules.AppliesWithPlayerCheckpoint(
            jpLeader, otherStep), Is.False);
        Assert.That(TutorialOpponentCueRules.AppliesWithPlayerCheckpoint(null, otherStep),
            Is.False);
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

    [Test]
    public void HotpotLessonRequiresTrickThenOneCommittedSpeedCard()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.CN);
        TutorialStepDefinition hotpot = scenario.steps.Single(step => step.id == TutorialStepId.ChinaHotpot);
        TutorialStepDefinition jelly = scenario.steps.Single(step => step.id == TutorialStepId.ChinaIceJelly);
        StringAssert.Contains("单独选中 1 张速度牌", hotpot.actionPrompt);
        StringAssert.Contains("冰糕特技牌", jelly.actionPrompt);
        var oneSpeed = new[] { new CardData(CardType.Speed, 1) };
        var twoSpeeds = new[]
        {
            new CardData(CardType.Speed, 1), new CardData(CardType.Speed, 2)
        };
        Assert.That(TutorialSpecialtyCardRules.ValidateTrickLessonSpeedSelection(
            TutorialStepId.ChinaHotpot, false, oneSpeed, out string reason), Is.False);
        StringAssert.Contains("火锅底料", reason);
        Assert.That(TutorialSpecialtyCardRules.ValidateTrickLessonSpeedSelection(
            TutorialStepId.ChinaHotpot, true, twoSpeeds, out reason), Is.False);
        StringAssert.Contains("1 张速度牌", reason);
        Assert.That(TutorialSpecialtyCardRules.ValidateTrickLessonSpeedSelection(
            TutorialStepId.ChinaHotpot, true, oneSpeed, out reason), Is.True, reason);
        Assert.That(TutorialSpecialtyCardRules.ValidateTrickLessonCompletion(
            TutorialStepId.ChinaHotpot, false, out reason), Is.False);
        StringAssert.Contains("ATTACK", reason);
        Assert.That(TutorialSpecialtyCardRules.ValidateTrickLessonCompletion(
            TutorialStepId.ChinaHotpot, true, out reason), Is.True, reason);
    }

    [TestCase(TutorialStepId.ChinaIceJelly, "冰糕")]
    [TestCase(TutorialStepId.UsFries, "薯条")]
    [TestCase(TutorialStepId.UsCola, "可乐")]
    [TestCase(TutorialStepId.ItChianti, "基安蒂红酒")]
    public void SpecialtyTrickLessonCannotEndBeforeItsCardIsPlayed(
        TutorialStepId step, string cardName)
    {
        Assert.That(TutorialSpecialtyCardRules.ValidateTrickLessonSpeedSelection(
            step, false, new[] { new CardData(CardType.Speed, 1) },
            out string reason), Is.False);
        StringAssert.Contains(cardName, reason);
        Assert.That(TutorialSpecialtyCardRules.ValidateTrickLessonCompletion(
            step, false, out reason), Is.False);
        StringAssert.Contains(cardName, reason);
        Assert.That(TutorialSpecialtyCardRules.ValidateTrickLessonCompletion(
            step, true, out reason), Is.True, reason);
    }

    [TestCase(TutorialStepId.ChinaIceJelly, "冰糕")]
    [TestCase(TutorialStepId.UsFries, "薯条")]
    [TestCase(TutorialStepId.UsCola, "可乐")]
    [TestCase(TutorialStepId.DeSauerkraut, "酸菜发酵")]
    [TestCase(TutorialStepId.DeSchwarzbrot, "黑面包垫底")]
    [TestCase(TutorialStepId.ItParmigiano, "帕尔马干酪")]
    [TestCase(TutorialStepId.ItChianti, "基安蒂红酒")]
    public void SpecialtyFocusNamesAndTargetsTheRequiredTrickCard(
        TutorialStepId step, string cardName)
    {
        Assert.That(TutorialSpecialtyFocusRules.RequiresLessonCard(step), Is.True);
        Assert.That(TutorialSpecialtyFocusRules.SelectionTarget(step, false),
            Is.EqualTo(TutorialFocusTarget.TeamTrickCard));
        StringAssert.Contains(cardName,
            TutorialSpecialtyFocusRules.SelectionPrompt(step, false));
        Assert.That(TutorialSpecialtyFocusRules.CanHighlightEndCards(
            step, stepComplete: false, canEndCards: true), Is.False,
            "the spotlight must not suggest End Cards while the required trick is unplayed");
        Assert.That(TutorialFocusOperationRules.Resolve(
            "cards", 0, canEndCards: false),
            Is.EqualTo(TutorialFocusOperation.SelectCards));
        Assert.That(TutorialSpecialtyFocusRules.CanHighlightEndCards(
            step, stepComplete: true, canEndCards: true), Is.True);
    }

    [Test]
    public void HotpotFocusMovesFromTrickToOneSpeedAfterAttackIsArmed()
    {
        Assert.That(TutorialSpecialtyFocusRules.SelectionTarget(
            TutorialStepId.ChinaHotpot, false),
            Is.EqualTo(TutorialFocusTarget.TeamTrickCard));
        Assert.That(TutorialSpecialtyFocusRules.SelectionTarget(
            TutorialStepId.ChinaHotpot, true),
            Is.EqualTo(TutorialFocusTarget.Hand));
        StringAssert.Contains("火锅底料", TutorialSpecialtyFocusRules.SelectionPrompt(
            TutorialStepId.ChinaHotpot, false));
        StringAssert.Contains("1 张速度牌", TutorialSpecialtyFocusRules.SelectionPrompt(
            TutorialStepId.ChinaHotpot, true));
        Assert.That(TutorialSpecialtyFocusRules.RequiresLessonCard(TutorialStepId.UsCorner),
            Is.False, "non-trick lessons retain their established speed-card focus");
    }

    [TestCase(TeamId.CN, TutorialStepId.ChinaConsecutiveGo)]
    [TestCase(TeamId.US, TutorialStepId.UsFries)]
    [TestCase(TeamId.DE, TutorialStepId.DeSauerkraut)]
    [TestCase(TeamId.IT, TutorialStepId.ItChianti)]
    [TestCase(TeamId.UK, TutorialStepId.UkSpecialtyTea)]
    [TestCase(TeamId.JP, TutorialStepId.JpKantoRelease)]
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
        Assert.That(player.italyCornerExitBoostReady,
            Is.EqualTo(checkpoint.italyCornerExitBoostReady));
        Assert.That(player.trickState.kantoOdenAccumulatedCards,
            Is.EqualTo(checkpoint.kantoCarryCards));
    }

    [Test]
    public void ItalyExitCheckpointArmsOnlyItsLessonAndClearsOnNextCheckpoint()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.IT);
        var player = new PlayerState("tutorial", false, 0, 1) { teamId = TeamId.IT };
        TutorialPlayerCheckpoint exit = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.ItCornerExit);
        TutorialPlayerCheckpoint next = scenario.playerCheckpoints.Single(c => c.step == TutorialStepId.ItParmigiano);
        Assert.That(TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, exit, player).success, Is.True);
        Assert.That(player.italyCornerExitBoostReady, Is.True);
        player.ClearTurnState();
        Assert.That(player.italyCornerExitBoostReady, Is.True, "bonus survives per-turn cleanup");
        player.italyCornerExitBonusAppliedThisTurn = true;
        Assert.That(TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, next, player).success, Is.True);
        Assert.That(player.italyCornerExitBoostReady, Is.False);
        Assert.That(player.italyCornerExitBonusAppliedThisTurn, Is.False);
    }

    [TestCase(TeamId.CN)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.JP)]
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
