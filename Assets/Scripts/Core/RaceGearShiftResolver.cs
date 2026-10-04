using System;

/// <summary>
/// Commits an already-resolved shift while keeping actual heat payment and
/// presentation in the race coordinator. The overclock payment must happen
/// after the new gear and China chain have been committed.
/// </summary>
public static class RaceGearShiftResolver
{
    public static void Apply(
        PlayerState player, TeamGearRules.Resolution shift,
        Func<int, string, bool> payHeat,
        Action playShiftSound, Action playFailureSound)
    {
        int previousGear = player.gear;
        int shiftHeatCost = DriverSkillRules.ApplyGearHeatCost(
            player.driverSkill, shift.HeatCost, false);
        if (payHeat(shiftHeatCost, "shift 2 gears"))
        {
            player.gear = shift.TargetGear;
            player.chinaConsecutiveGearCount = shift.IsChina ? shift.ConsecutiveCount : 0;
            if (previousGear != player.gear)
                playShiftSound();

            int additionalHeat = DriverSkillRules.ApplyGearHeatCost(
                player.driverSkill, shift.AdditionalHeat, true);
            if (additionalHeat > 0)
                payHeat(additionalHeat,
                    $"{TeamGearRules.GetDisplayName(player.teamId, player.gear)} overclock");
        }
        else if (!player.isAI)
        {
            playFailureSound();
        }

        // A failed payment may already have forced a recovery gear via spin.
        player.selectedGearThisTurn = player.gear;
    }
}
