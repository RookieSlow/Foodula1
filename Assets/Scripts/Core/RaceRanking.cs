using System.Collections.Generic;

/// <summary>
/// Pure-function race ranking — sorts N players by lap, position, finish order.
/// Replaces the hardcoded 2-player ranking in MVPGameManager.
/// </summary>
public static class RaceRanking
{
    /// <summary>Single ranked entry in the current standings.</summary>
    public struct RankEntry
    {
        public int rank;
        public PlayerState player;

        public RankEntry(int rank, PlayerState player)
        {
            this.rank = rank;
            this.player = player;
        }
    }

    /// <summary>
    /// Sort players by race progression (who's winning right now).
    /// Sort order:
    ///   1. Finished players first (by finishOrder)
    ///   2. More laps completed = ahead
    ///   3. Further along the track (higher position index) = ahead
    ///   4. Blown/DNF players go to the bottom
    /// </summary>
    public static List<PlayerState> SortByPosition(List<PlayerState> players)
    {
        var sorted = new List<PlayerState>(players);
        sorted.Sort(CompareRacePosition);
        return sorted;
    }

    private static int CompareRacePosition(PlayerState a, PlayerState b)
    {
        // Finished players rank above unfinished ones
        if (a.hasFinished && !b.hasFinished) return -1;
        if (!a.hasFinished && b.hasFinished) return 1;

        // Both finished: compare by finish order (earlier finish = ahead)
        if (a.hasFinished && b.hasFinished)
            return a.finishOrder.CompareTo(b.finishOrder);

        // Blown players go to bottom
        if (a.isBlown && !b.isBlown) return 1;
        if (!a.isBlown && b.isBlown) return -1;

        // Both blown: compare laps then position
        int lapCmp = b.lap.CompareTo(a.lap); // More laps = ahead
        if (lapCmp != 0) return lapCmp;

        return b.position.CompareTo(a.position); // Further = ahead
    }

    /// <summary>
    /// Get the current rank (1-indexed) of a specific player.
    /// </summary>
    public static int GetCurrentRank(PlayerState target, List<PlayerState> allPlayers)
    {
        var sorted = SortByPosition(allPlayers);
        for (int i = 0; i < sorted.Count; i++)
        {
            if (sorted[i] == target) return i + 1;
        }
        return allPlayers.Count;
    }

    /// <summary>
    /// Get the full current rankings with positions.
    /// </summary>
    public static List<RankEntry> GetRankings(List<PlayerState> players)
    {
        var sorted = SortByPosition(players);
        var result = new List<RankEntry>();
        for (int i = 0; i < sorted.Count; i++)
            result.Add(new RankEntry(i + 1, sorted[i]));
        return result;
    }

    /// <summary>
    /// Determine turn order for the next turn.
    /// Current rule: trailing players go first (reverse of race position).
    /// This gives catch-up advantage to players behind.
    /// </summary>
    public static List<PlayerState> GetTurnOrder(List<PlayerState> players)
    {
        var sorted = SortByPosition(players);
        // Reverse: last place goes first
        sorted.Reverse();
        return sorted;
    }

    /// <summary>
    /// Assign finish order to a player who just crossed the finish line.
    /// Called each time a player finishes. Increments a global counter.
    /// </summary>
    public static int AssignFinishOrder(PlayerState player, ref int nextFinishOrder)
    {
        player.finishOrder = nextFinishOrder;
        return nextFinishOrder++;
    }

    /// <summary>
    /// Check if the race is over.
    /// Conditions: no non-blown player remains active.
    /// </summary>
    public static bool IsRaceOver(List<PlayerState> players)
    {
        int activeCount = 0;
        foreach (var p in players)
        {
            if (!p.isBlown && !p.hasFinished)
                activeCount++;
        }
        return activeCount <= 0;
    }

    /// <summary>
    /// Count how many players are still actively racing.
    /// </summary>
    public static int CountActive(List<PlayerState> players)
    {
        int count = 0;
        foreach (var p in players)
            if (!p.isBlown && !p.hasFinished)
                count++;
        return count;
    }

    /// <summary>
    /// Get the final race results as a formatted string.
    /// </summary>
    public static string FormatResults(List<PlayerState> players)
    {
        var rankings = GetRankings(players);
        var lines = new List<string> { "=== 比赛结果 ===", "" };

        foreach (var entry in rankings)
        {
            var p = entry.player;
            string status = p.isBlown ? "[爆缸]" :
                           p.hasFinished ? "[完赛]" : "[比赛中]";
            string teamCode = GetTeamCode(p.teamId);
            lines.Add($"{entry.rank}. [{teamCode}] {p.name} - {p.lap}圈 位{p.position} {status}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Get RP rewards for all players based on final ranking.
    /// </summary>
    public static Dictionary<PlayerState, int> CalculateRPRewards(
        List<PlayerState> players,
        Dictionary<TeamId, TechTreeState> techStates = null)
    {
        var rankings = GetRankings(players);
        var rewards = new Dictionary<PlayerState, int>();

        foreach (var entry in rankings)
        {
            int baseRp = TechTreeRules.CalculateRaceRP(entry.rank);
            rewards[entry.player] = baseRp;

            // Apply Cavallino Rampante if applicable
            if (techStates != null &&
                techStates.TryGetValue(entry.player.teamId, out var techState))
            {
                // Full RP calculation with Cavallino requires TechTreeDatabase reference
                // Use TechTreeRules.ApplyCavallinoRampante() when integrating
            }
        }

        return rewards;
    }

    private static string GetTeamCode(TeamId teamId)
    {
        switch (teamId)
        {
            case TeamId.UK: return "UK";
            case TeamId.DE: return "DE";
            case TeamId.IT: return "IT";
            case TeamId.US: return "US";
            case TeamId.CN: return "CN";
            case TeamId.JP: return "JP";
            default: return "NA";
        }
    }
}
