using NUnit.Framework;

public class TutorialRaceResultPresentationTests
{
    [TestCase(true, "勒芒教程练习完成！")]
    [TestCase(false, "本次练习未完成；可以使用教程面板重新开始。")]
    public void SummaryPreservesExactHeadingRankingAndNoRewardNotice(
        bool completed, string heading)
    {
        const string ranking = "第一名：测试车手";
        Assert.AreEqual(
            heading + "\n\n" + ranking +
            "\n\n教程模式：不发放 RP、车手 XP、解锁或正常赛事进度。",
            TutorialRaceResultPresentation.BuildSummary(ranking, completed));
    }

    [TestCase(true, true, RaceLogTermination.Completed)]
    [TestCase(true, false, RaceLogTermination.TutorialIncomplete)]
    [TestCase(false, true, RaceLogTermination.Completed)]
    [TestCase(false, false, RaceLogTermination.Completed)]
    public void TerminationSeparatesIncompleteTutorialFromOtherModes(
        bool isTutorial, bool completed, RaceLogTermination expected)
    {
        Assert.AreEqual(expected,
            TutorialRaceResultPresentation.GetTermination(isTutorial, completed));
    }
}
