/// <summary>
/// Team-aware gear facade.  The race coordinator only talks to this class;
/// standard HEAT shifting and China's electric drivetrain remain independent
/// pure modules behind the same contract.
/// </summary>
public static class TeamGearRules
{
    public readonly struct Resolution
    {
        public Resolution(int targetGear, int heatCost, int consecutiveCount,
            int cardCount, int additionalHeat, int cooldown, bool isChina)
        {
            TargetGear = targetGear;
            HeatCost = heatCost;
            ConsecutiveCount = consecutiveCount;
            CardCount = cardCount;
            AdditionalHeat = additionalHeat;
            Cooldown = cooldown;
            IsChina = isChina;
        }

        public int TargetGear { get; }
        public int HeatCost { get; }
        public int ConsecutiveCount { get; }
        public int CardCount { get; }
        public int AdditionalHeat { get; }
        public int Cooldown { get; }
        public bool IsChina { get; }
    }

    public static bool IsChina(TeamId teamId) => teamId == TeamId.CN;

    public static Resolution Resolve(TeamId teamId, int currentGear,
        int currentConsecutiveCount, int requestedGear, int minimumGear,
        int maximumGear, int twoGearShiftHeatCost, int gearOneCooldown,
        int gearTwoCooldown)
    {
        if (IsChina(teamId))
        {
            ChinaGearShiftRules.Result china = ChinaGearShiftRules.Resolve(
                currentGear, currentConsecutiveCount, requestedGear);
            return new Resolution(china.TargetGear, 0, china.ConsecutiveCount,
                china.SpeedCardCount, china.AdditionalHeat, china.Cooldown, true);
        }

        GearShiftResult standard = RaceRules.ResolveGearShift(
            currentGear, requestedGear, minimumGear, maximumGear,
            twoGearShiftHeatCost);
        return new Resolution(standard.TargetGear, standard.HeatCost, 0,
            standard.TargetGear, 0,
            RaceRules.GetCooldown(standard.TargetGear, gearOneCooldown, gearTwoCooldown),
            false);
    }

    public static int GetSpeedCardCount(TeamId teamId, int gear,
        int consecutiveCount, int extraSlots)
    {
        int baseCount = IsChina(teamId)
            ? ChinaGearShiftRules.GetSpeedCardCount(gear, consecutiveCount)
            : gear;
        return baseCount + extraSlots;
    }

    public static int GetCooldown(TeamId teamId, int gear, int consecutiveCount,
        int gearOneCooldown, int gearTwoCooldown)
    {
        if (IsChina(teamId))
            return ChinaGearShiftRules.GetCooldown(gear, consecutiveCount);
        return RaceRules.GetCooldown(gear, gearOneCooldown, gearTwoCooldown);
    }

    public static string GetDisplayName(TeamId teamId, int gear)
    {
        if (!IsChina(teamId)) return $"G{gear}";
        return ChinaGearShiftRules.IsGo(gear) ? "Go" : "Recover";
    }

    public static string GetDisplayNameWithCards(TeamId teamId, int gear,
        int consecutiveCount, int extraSlots = 0)
    {
        int cards = GetSpeedCardCount(teamId, gear, consecutiveCount, extraSlots);
        return IsChina(teamId)
            ? $"{GetDisplayName(teamId, gear)} ({cards}张)"
            : $"G{gear} ({cards}张)";
    }
}
