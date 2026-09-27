using System;
using System.Collections.Generic;

/// <summary>
/// Settles rewards for a normal race only. Tutorial and career results are
/// handled by their own branches in MVPGameManager and must never call this.
/// Persistence is injected so the rule can be verified without PlayerPrefs.
/// </summary>
public static class NormalRaceRewardSettlement
{
    public static string Settle(
        RaceSession session,
        string trackCountry,
        bool tutorialActive,
        bool careerActive,
        Action<TechTreeState> saveTechState,
        Action<string, int> saveDriverXp)
    {
        if (tutorialActive || careerActive) return string.Empty;
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (saveTechState == null) throw new ArgumentNullException(nameof(saveTechState));
        if (saveDriverXp == null) throw new ArgumentNullException(nameof(saveDriverXp));

        List<RaceRanking.RankEntry> rankings = session.GetRankings();
        var rpLines = new List<string> { "RP 奖励:" };
        foreach (RaceRanking.RankEntry entry in rankings)
        {
            PlayerState player = entry.player;
            int rp = TechTreeRules.CalculateRaceRP(entry.rank);
            if (player.techState != null)
            {
                if (TechTreeRules.ShouldApplyCavallino(player.techState, session.TechDb, entry.rank))
                    rp = TechTreeRules.ApplyCavallinoRampante(
                        rp, entry.rank, TechTreeRules.IsCavallinoHomeRace(trackCountry ?? string.Empty));

                player.techState.rpBalance += rp;
                if (!player.isAI)
                    saveTechState(player.techState);
            }

            rpLines.Add($"{entry.rank}. {player.name}: +{rp} RP{(player.techState != null ? $" (余额 {player.techState.rpBalance})" : "")}");
        }

        var xpLines = new List<string> { "车手 XP:" };
        foreach (RaceRanking.RankEntry entry in rankings)
        {
            PlayerState player = entry.player;
            DriverProfile driver = player.DriverProfile;
            int earned = player.isBlown
                ? 0
                : DriverProgression.CalculateRaceXp(entry.rank, driver.TalentMultiplier, driver.Team);
            int previousLevel = player.DriverLevel;
            player.driverXp += earned;
            if (!player.isAI)
                saveDriverXp(driver.Id, player.driverXp);

            xpLines.Add($"{player.name}（{driver.ShortName}）: +{earned} XP → Lv{player.DriverLevel}");
            if (player.DriverLevel > previousLevel)
                xpLines.Add($"  {driver.ShortName} 解锁了新的车手技能层级。");
        }

        return string.Join("\n", rpLines) + "\n\n" + string.Join("\n", xpLines);
    }
}
