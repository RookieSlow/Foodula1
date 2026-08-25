/// <summary>
/// Static team vehicle profiles from the design documents.  Keeping these
/// values in a pure rules module prevents team-specific numbers from leaking
/// into UI or the coroutine-driven race coordinator.
/// </summary>
public readonly struct TeamVehicleProfile
{
    public TeamVehicleProfile(int topSpeed, int acceleration, int handling,
        int cooling, int durability, int slipstream)
    {
        TopSpeed = topSpeed;
        Acceleration = acceleration;
        Handling = handling;
        Cooling = cooling;
        Durability = durability;
        Slipstream = slipstream;
    }

    public int TopSpeed { get; }
    public int Acceleration { get; }
    public int Handling { get; }
    public int Cooling { get; }
    public int Durability { get; }
    public int Slipstream { get; }
}

public static class TeamVehicleRules
{
    public static TeamVehicleProfile GetProfile(TeamId teamId)
    {
        switch (teamId)
        {
            case TeamId.UK: return new TeamVehicleProfile(0, 0, 1, 1, 7, 0);
            case TeamId.DE: return new TeamVehicleProfile(1, 0, 1, 1, 8, 0);
            case TeamId.IT: return new TeamVehicleProfile(0, 1, 2, 0, 6, 0);
            case TeamId.US: return new TeamVehicleProfile(2, 1, -1, 0, 8, 1);
            // China keeps the documented +1 top speed and +2 electric
            // acceleration.  RaceSession applies this package only while Go
            // is active; Recover remains the low-output cooling mode.
            // The benchmark showed the documented -1 corner handling made
            // China lose every track once standard AI began choosing safe
            // cards.  Neutral handling keeps the distinctive Go/Recover heat
            // loop and straight-line cadence without turning every apex into
            // an automatic battery drain.
            case TeamId.CN: return new TeamVehicleProfile(1, 2, 0, 0, 7, 0);
            case TeamId.JP: return new TeamVehicleProfile(0, 0, 1, 0, 6, 0);
            default: return new TeamVehicleProfile(0, 0, 0, 0, 6, 0);
        }
    }

    public static int GetBaseHeatPoolSize(TeamId teamId, int configuredMinimum)
    {
        TeamVehicleProfile profile = GetProfile(teamId);
        return System.Math.Max(configuredMinimum, profile.Durability);
    }

    public static int GetHandling(TeamId teamId) => GetProfile(teamId).Handling;
    public static int GetCooling(TeamId teamId) => GetProfile(teamId).Cooling;
    public static int GetSlipstreamBonus(TeamId teamId) => GetProfile(teamId).Slipstream;

    /// <summary>
    /// Base straight-line movement contribution after bespoke team mechanics.
    /// America's profile values describe its identity, but Straight Roar is
    /// resolved separately as one flat turn bonus and must not be stacked here.
    /// </summary>
    public static int GetStraightMovementBonus(TeamId teamId)
    {
        // Italy's acceleration stat is expressed by its one-shot corner-exit
        // boost, not as a permanent bonus on every straight turn.  America
        // likewise resolves its profile through the flat Straight Roar rule.
        if (teamId == TeamId.IT || teamId == TeamId.US)
            return 0;

        TeamVehicleProfile profile = GetProfile(teamId);
        return profile.TopSpeed + profile.Acceleration;
    }

    /// <summary>
    /// Special straight-line card conversion from the car design sheet.
    /// Germany turns a value-1 card into a value-2 card on straights.
    /// </summary>
    public static int GetStraightCardBonus(TeamId teamId, int cardValue)
    {
        switch (teamId)
        {
            case TeamId.DE:
                return cardValue == 1 ? 1 : 0;
            default:
                return 0;
        }
    }

    /// <summary>
    /// America’s straight-roar identity is tuned as one flat +1 per straight
    /// turn.  This preserves a clear advantage without multiplying the bonus
    /// by every card at high gears.
    /// </summary>
    public static int GetStraightTurnBonus(TeamId teamId)
    {
        return teamId == TeamId.US ? 1 : 0;
    }

    /// <summary>Italy's design-only corner-exit acceleration bonus.</summary>
    public static int GetCornerExitBonus(TeamId teamId)
    {
        return teamId == TeamId.IT ? 1 : 0;
    }

    /// <summary>US pays one extra heat when an overspeed is resolved.</summary>
    public static int GetCornerHeatPenalty(TeamId teamId)
    {
        return teamId == TeamId.US ? 1 : 0;
    }
}
