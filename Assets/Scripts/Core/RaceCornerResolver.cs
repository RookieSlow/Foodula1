using System;
using System.Collections.Generic;

/// <summary>
/// Resolves crossed apexes in their supplied order. Track lookup, heat payment,
/// and audio remain with the race coordinator so this flow can be tested alone.
/// </summary>
public static class RaceCornerResolver
{
    public static (bool completed, string log) Resolve(
        PlayerState player, int laneIndex, IEnumerable<int> corners,
        Func<int, int> effectiveLimit, Func<int, string> cornerName,
        Func<int, int> heatCost, Func<int, string, bool> payHeat,
        Action<bool> playCornerSound)
    {
        int totalSpeed = player.cornerTotalThisTurn;
        string log = "";

        // Extra movement from Wurstplatte does not exempt the original speed.
        foreach (int cornerId in corners)
        {
            if (player.driverSkill != null && player.driverSkill.TryConsumeCornerIgnore())
            {
                log += $"{player.name} 使用 {player.DriverProfile.ActiveName} 无视 {cornerName(cornerId)} 限速。\n";
                continue;
            }

            int limit = effectiveLimit(cornerId);
            if (totalSpeed > limit)
            {
                int overspeed = totalSpeed - limit;
                int heat = heatCost(overspeed);
                string name = cornerName(cornerId);
                if (heat > 0 && !payHeat(heat, $"overspeed at {name} ({totalSpeed}>{limit})"))
                    return (false, log);

                playCornerSound(true);
                log += heat > 0
                    ? $"{player.name} 在 {name} 超速 (lane {laneIndex + 1}, 限速 {limit}) 超 {overspeed}！+{heat} 热量。\n"
                    : $"{player.name} 使用 {player.DriverProfile.ActiveName} 零热量通过 {name}。\n";
            }
            else
            {
                playCornerSound(false);
                log += $"{player.name} 安全通过 {cornerName(cornerId)} (lane {laneIndex + 1}, {totalSpeed}<={limit})。\n";
            }
        }

        if (string.IsNullOrEmpty(log))
            log = $"{player.name} 直道 - 无弯道。\n";
        return (true, log);
    }
}
