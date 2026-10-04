using System.Collections.Generic;
using System.Text;

/// <summary>
/// Read-only race HUD text. Ranking and track wrapping remain delegated to
/// their gameplay rules; this class only maps the current snapshot to labels.
/// </summary>
public static class RaceHudTextRules
{
    public static string FormatLap(PlayerState player, int totalLaps) =>
        $"圈数: {player.lap}/{totalLaps}";

    public static string FormatPosition(PlayerState player,
        IReadOnlyList<PlayerState> allPlayers, int totalNodes)
    {
        string cell = TrackPresentationRules.FormatCellPosition(player.position, totalNodes);
        if (allPlayers == null || allPlayers.Count == 0)
            return cell;

        int rank = RaceRanking.GetCurrentRank(player, new List<PlayerState>(allPlayers));
        return $"{cell} | 排名: {rank}/{allPlayers.Count}";
    }

    public static string FormatAiStatus(PlayerState ai, int totalNodes)
    {
        if (ai.isBlown)
            return "<color=red>AI: 爆缸!</color>";
        if (ai.hasFinished)
            return "<color=green>AI: 完赛!</color>";
        return $"AI: {TeamGearRules.GetDisplayName(ai.teamId, ai.gear)} | 引擎:{ai.deck.heatPool.remaining} | 圈{ai.lap} | 格{TrackPresentationRules.WrapNodeIndex(ai.position, totalNodes) + 1}";
    }

    public static string FormatStandings(IReadOnlyList<PlayerState> all,
        PlayerState self, int totalNodes)
    {
        var rankings = RaceRanking.GetRankings(new List<PlayerState>(all));
        var text = new StringBuilder();
        foreach (var entry in rankings)
        {
            string mark = entry.player == self ? " ←你" : "";
            string cell = totalNodes > 0
                ? (TrackPresentationRules.WrapNodeIndex(entry.player.position, totalNodes) + 1).ToString()
                : "?";
            text.AppendLine($"{entry.rank}. {entry.player.name} 圈{entry.player.lap} 格{cell}{mark}");
        }
        return text.ToString();
    }
}
