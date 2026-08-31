using System.Collections.Generic;
using NUnit.Framework;

public sealed class CareerMenuPresentationTests
{
    [Test]
    public void MainMenuLabels_DistinguishQuickRaceAndCareer()
    {
        Assert.That(MainMenuLabels.QuickRace, Is.EqualTo("自由赛事"));
        Assert.That(MainMenuLabels.QuickRaceTrackTitle, Does.Contain("自由赛事"));
        Assert.That(MainMenuLabels.Career, Is.EqualTo("生涯模式"));
    }

    [Test]
    public void CompetitorField_IsStableDistinctAndSupportsEveryPlayerTeam()
    {
        foreach (TeamId team in CareerMenuPresentation.AvailableTeams)
        {
            TeamId[] field = CareerMenuPresentation.BuildCompetitorField(team);
            Assert.That(field.Length, Is.EqualTo(4));
            Assert.That(field[0], Is.EqualTo(team));
            Assert.That(new HashSet<TeamId>(field).Count, Is.EqualTo(4));
        }
    }

    [Test]
    public void MissingAndInvalidSave_ShowCreationAndRecoveryCopy()
    {
        CareerMenuViewModel missing = CareerMenuPresentation.Build(
            new CareerSeasonState(), CareerLoadStatus.Missing, false);
        Assert.That(missing.HasCareer, Is.False);
        Assert.That(missing.RequiresRecovery, Is.False);
        Assert.That(missing.PrimaryAction, Does.Contain("新建"));
        Assert.That(missing.Calendar, Does.Contain("1."));
        Assert.That(missing.Calendar, Does.Contain("8."));

        CareerMenuViewModel invalid = CareerMenuPresentation.Build(
            new CareerSeasonState(), CareerLoadStatus.Invalid, false);
        Assert.That(invalid.RequiresRecovery, Is.True);
        Assert.That(invalid.Status, Does.Contain("明确确认"));
        Assert.That(invalid.PrimaryAction, Does.Contain("覆盖"));
    }

    [Test]
    public void ActiveSeason_ShowsNextTrackCalendarAndComputedStandings()
    {
        CareerSeasonState state = StartSeason();
        Assert.That(CareerModeRules.TryRecordRace(state, BuildResult(state, "race-1")), Is.True);

        CareerMenuViewModel view = CareerMenuPresentation.Build(state, CareerLoadStatus.Loaded, false);
        Assert.That(view.HasCareer, Is.True);
        Assert.That(view.Status, Does.Contain("第 2/8 站"));
        Assert.That(view.Calendar, Does.Contain("✓ 1."));
        Assert.That(view.Calendar, Does.Contain("▶ 2."));
        Assert.That(view.Standings, Does.Contain("10 分"));
        Assert.That(view.CanLaunchRace, Is.False);
        Assert.That(view.PrimaryAction, Does.Contain("下一阶段"));
    }

    [Test]
    public void SummerBreakAndCompletedSeason_ShowTheirDistinctGates()
    {
        CareerSeasonState state = StartSeason();
        for (int race = 0; race < 4; race++)
            Assert.That(CareerModeRules.TryRecordRace(state, BuildResult(state, $"race-{race + 1}")), Is.True);

        CareerMenuViewModel summer = CareerMenuPresentation.Build(state, CareerLoadStatus.Loaded, true);
        Assert.That(summer.CanAdjustTech, Is.True);
        Assert.That(summer.CanLaunchRace, Is.False);
        Assert.That(summer.Status, Does.Contain("夏休"));

        Assert.That(CareerModeRules.ConfirmSummerBreakTechTree(state), Is.True);
        for (int race = 4; race < CareerModeRules.RaceCount; race++)
            Assert.That(CareerModeRules.TryRecordRace(state, BuildResult(state, $"race-{race + 1}")), Is.True);

        CareerMenuViewModel completed = CareerMenuPresentation.Build(state, CareerLoadStatus.Loaded, true);
        Assert.That(completed.Status, Does.Contain("已完成"));
        Assert.That(completed.Status, Does.Contain("总冠军"));
        Assert.That(completed.Status, Does.Contain("中国 CN"));
        Assert.That(completed.PlayerSummary, Does.Contain("最终第 1 名"));
        Assert.That(completed.PrimaryAction, Is.EqualTo("开启新一轮生涯"));
        Assert.That(completed.CanStartNewSeason, Is.True);
        Assert.That(completed.CanLaunchRace, Is.False);
        Assert.That(completed.Calendar, Does.Not.Contain("▶"));
    }

    private static CareerSeasonState StartSeason()
    {
        var state = new CareerSeasonState();
        Assert.That(CareerModeRules.TryStartSeason(
            state, TeamId.CN, CareerMenuPresentation.BuildCompetitorField(TeamId.CN)), Is.True);
        return state;
    }

    private static CareerRaceResult BuildResult(CareerSeasonState state, string id)
    {
        return new CareerRaceResult(id, CareerModeRules.GetNextTrackId(state), new[]
        {
            new CareerCompetitorResult(state.Competitors[0], 1),
            new CareerCompetitorResult(state.Competitors[1], 2),
            new CareerCompetitorResult(state.Competitors[2], 3),
            new CareerCompetitorResult(state.Competitors[3], 4)
        });
    }
}
