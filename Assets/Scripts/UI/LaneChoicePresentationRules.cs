/// <summary>Read-only direction copy; lane indices increase toward the outside.</summary>
public static class LaneChoicePresentationRules
{
    public static string GetChoiceLabel(int direction) =>
        direction > 0 ? "向内一格" : direction < 0 ? "向外一格" : "保持车道";

    public static string GetResolvedLog(int oldLane, int selectedLane)
    {
        string choice = selectedLane == oldLane
            ? "保持当前车道"
            : GetChoiceLabel(oldLane.CompareTo(selectedLane));
        return $"印地起点换道：{choice}（第 {selectedLane + 1} 道）";
    }
}
