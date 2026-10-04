/// <summary>Read-only tutorial result copy and log closure classification.</summary>
public static class TutorialRaceResultPresentation
{
    public static string BuildSummary(string ranking, bool practiceCompleted)
    {
        string heading = practiceCompleted
            ? "勒芒教程练习完成！\n\n"
            : "本次练习未完成；可以使用教程面板重新开始。\n\n";
        return heading + ranking + "\n\n教程模式：不发放 RP、车手 XP、解锁或正常赛事进度。";
    }

    public static RaceLogTermination GetTermination(bool isTutorial, bool practiceCompleted)
    {
        return isTutorial && !practiceCompleted
            ? RaceLogTermination.TutorialIncomplete
            : RaceLogTermination.Completed;
    }
}
