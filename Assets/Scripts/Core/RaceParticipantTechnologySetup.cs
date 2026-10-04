using System;

/// <summary>
/// Selects one participant's race technology and heat capacity before deck
/// construction. Profile I/O and invalid-career feedback are caller supplied.
/// </summary>
public static class RaceParticipantTechnologySetup
{
    public static int Prepare(
        PlayerState player,
        TeamId team,
        TutorialScenarioDefinition tutorial,
        CareerTechSnapshot careerSnapshot,
        bool enableTechTree,
        int configuredHeatPool,
        BrothType demoBroth,
        RaceSession session,
        string loadedTrackCountry,
        Func<TeamId, TechTreeState> loadHumanProfile,
        Action reportInvalidCareerSnapshot)
    {
        player.teamId = team;
        player.usesChinaGearSystem = team == TeamId.CN;
        int poolSize = tutorial != null
            ? tutorial.engineHeatCapacity
            : TeamVehicleRules.GetBaseHeatPoolSize(team, configuredHeatPool);

        if (tutorial != null)
        {
            player.techState = null;
        }
        else if (careerSnapshot != null)
        {
            if (!CareerTechSnapshotMapper.TryCreateRuntimeState(
                    careerSnapshot, session.TechDb, out TechTreeState careerTechState))
            {
                reportInvalidCareerSnapshot();
                player.techState = null;
            }
            else
            {
                player.techState = careerTechState;
                session.PrepareTechnologyForRace(player, loadedTrackCountry);
                poolSize = session.EffectiveHeatPoolSize(player, poolSize);
            }
        }
        else if (enableTechTree)
        {
            player.techState = player.isAI
                ? session.CreateDemoTechState(team)
                : loadHumanProfile(team);
            session.PrepareTechnologyForRace(player, loadedTrackCountry);
            poolSize = session.EffectiveHeatPoolSize(player, poolSize);

            // Demo selection remains after track binding and capacity setup.
            if (demoBroth != BrothType.None && session.GetModifiers(player).hasBrothSelection)
                TechTreeRules.SelectBroth(player.techState, demoBroth);
        }
        else
        {
            player.techState = null;
        }

        return poolSize;
    }
}
