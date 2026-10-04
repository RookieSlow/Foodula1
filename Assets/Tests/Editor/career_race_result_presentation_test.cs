using System.Collections.Generic;
using NUnit.Framework;

public sealed class CareerRaceResultPresentationTests
{
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.JP)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.DE)]
    public void EightRaceSeason_ShowsPlayerPointsPhaseAndOtherChampion_WithoutMutation(TeamId team)
    {
        TeamId[] field = CareerMenuPresentation.BuildCompetitorField(team);
        var season = new CareerSeasonState();
        Assert.That(CareerModeRules.TryStartSeason(season, team, field), Is.True);
        for (int race = 0; race < CareerModeRules.RaceCount; race++)
        {
            if (season.Phase == CareerPhase.SummerBreak)
                Assert.That(CareerModeRules.ConfirmSummerBreakTechTree(season), Is.True);
            var entries = new List<CareerCompetitorResult>
            {
                new CareerCompetitorResult(field[1], 1),
                new CareerCompetitorResult(team, 2),
                new CareerCompetitorResult(field[2], 3),
                new CareerCompetitorResult(field[3], 4)
            };
            Assert.That(CareerModeRules.TryRecordRace(season, new CareerRaceResult(
                "presentation-" + race, CareerModeRules.TrackSchedule[race], entries)), Is.True);
            string before = Serialize(season);
            string expected = $"\n\n生涯赛果已保存：总分 {(race + 1) * 6}，总排名第 2 名。";
            if (race == 3)
                expected += "\n已进入夏休，返回主菜单调整一次生涯科技树。";
            if (race == 7)
                expected += $"\n八站生涯已完成。总冠军：{field[1]}（80 分）。";
            Assert.That(CareerRaceResultPresentation.BuildSavedSummary(season), Is.EqualTo(expected));
            Assert.That(CareerRaceResultPresentation.BuildSavedSummary(season), Is.EqualTo(expected));
            Assert.That(Serialize(season), Is.EqualTo(before));
        }
    }

    [Test]
    public void AllDnf_TiedZeroScoresUseOriginalFieldOrder()
    {
        var season = new CareerSeasonState();
        var field = new[] { TeamId.US, TeamId.CN, TeamId.JP, TeamId.UK };
        Assert.That(CareerModeRules.TryStartSeason(season, TeamId.CN, field), Is.True);
        for (int race = 0; race < CareerModeRules.RaceCount; race++)
        {
            if (season.Phase == CareerPhase.SummerBreak)
                Assert.That(CareerModeRules.ConfirmSummerBreakTechTree(season), Is.True);
            var entries = new List<CareerCompetitorResult>();
            foreach (TeamId team in field)
                entries.Add(new CareerCompetitorResult(team, 0, true));
            Assert.That(CareerModeRules.TryRecordRace(season, new CareerRaceResult(
                "dnf-" + race, CareerModeRules.TrackSchedule[race], entries)), Is.True);
        }
        string before = Serialize(season);
        Assert.That(CareerRaceResultPresentation.BuildSavedSummary(season), Is.EqualTo(
            "\n\n生涯赛果已保存：总分 0，总排名第 2 名。\n八站生涯已完成。总冠军：US（0 分）。"));
        Assert.That(Serialize(season), Is.EqualTo(before));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("无法保存生涯赛果")]
    [TestCase("<b>storage</b>\nfailed")]
    public void Rejected_PreservesExistingCopyAndReason(string reason)
    {
        Assert.That(CareerRaceResultPresentation.BuildRejectedSummary(reason), Is.EqualTo(
            "\n\n<color=red>" + reason + "</color>。返回主菜单后可重新开始当前站。"));
    }

    [Test]
    public void AlreadySaved_PreservesIdempotentResultCopy()
    {
        Assert.That(CareerRaceResultPresentation.AlreadySaved, Is.EqualTo(
            "\n\n生涯赛果已保存。返回主菜单可查看更新后的积分榜。"));
    }

    [Test]
    public void EmptyState_PreservesZeroFallbackWithoutAddingPhaseCopy()
    {
        Assert.That(CareerRaceResultPresentation.BuildSavedSummary(new CareerSeasonState()),
            Is.EqualTo("\n\n生涯赛果已保存：总分 0，总排名第 0 名。"));
    }

    private static string Serialize(CareerSeasonState state)
    {
        Assert.That(CareerSaveCodec.TryToData(state, out CareerSaveData data), Is.True);
        return new JsonUtilityCareerSerializer().Serialize(data);
    }
}
