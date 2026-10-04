using System.Collections.Generic;

/// <summary>Read-only race-result copy; settlement and persistence stay with the caller.</summary>
public static class CareerRaceResultPresentation
{
    public const string AlreadySaved = "\n\n生涯赛果已保存。返回主菜单可查看更新后的积分榜。";

    public static string BuildSavedSummary(CareerSeasonState state)
    {
        List<CareerStanding> standings = CareerModeRules.GetStandings(state);
        CareerStanding player = standings.Find(entry => entry.TeamId == state.LockedTeam);
        string summary = $"\n\n生涯赛果已保存：总分 {player?.Points ?? 0}，" +
                         $"总排名第 {player?.Rank ?? 0} 名。";
        if (state.Phase == CareerPhase.SummerBreak)
            summary += "\n已进入夏休，返回主菜单调整一次生涯科技树。";
        else if (state.Phase == CareerPhase.Completed)
        {
            CareerStanding champion = standings.Count > 0 ? standings[0] : null;
            summary += $"\n八站生涯已完成。总冠军：" +
                       $"{(champion == null ? "—" : champion.TeamId.ToString())}" +
                       $"（{champion?.Points ?? 0} 分）。";
        }
        return summary;
    }

    public static string BuildRejectedSummary(string failureReason)
    {
        return $"\n\n<color=red>{failureReason}</color>。返回主菜单后可重新开始当前站。";
    }
}
