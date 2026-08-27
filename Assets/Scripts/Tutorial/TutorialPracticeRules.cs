/// <summary>
/// Pure boundary rules for the post-guide tutorial lap. Keeping the one-lap
/// override here prevents tutorial completion checks from changing normal
/// race configuration or Le Mans track data.
/// </summary>
public static class TutorialPracticeRules
{
    public static int GetRequiredLapCount(TutorialRunPhase? phase, int normalLapCount)
    {
        return phase == TutorialRunPhase.Practice ? 1 : normalLapCount;
    }

    public static bool IsGuidedLap(TutorialRunPhase? phase)
    {
        return phase == TutorialRunPhase.Guided;
    }

    public static bool ShouldEndImmediately(TutorialRunPhase? phase)
    {
        return phase == TutorialRunPhase.Completed || phase == TutorialRunPhase.Exited;
    }
}
