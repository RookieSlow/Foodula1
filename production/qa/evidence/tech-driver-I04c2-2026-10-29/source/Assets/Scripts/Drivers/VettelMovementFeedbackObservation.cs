using System;

/// <summary>Frozen native movement facts; only a successful actual settlement may turn these into D04 feedback.</summary>
public sealed class VettelMovementFeedbackObservation
{
    private readonly RaceSession session;
    private readonly PlayerState owner;
    private readonly DriverSkillRuntimeState skill;
    private readonly CardDeck deck;
    private readonly HeatPool pool;
    private readonly int input, initialization, movement;
    private readonly string driverId;
    private readonly bool cruise, cornerExit;
    public int SourceLap { get; }
    public int SourcePosition { get; }
    public int ActualCellsBefore { get; }

    private VettelMovementFeedbackObservation(RaceSession race, PlayerState player, bool cruise, bool cornerExit)
    {
        session = race; owner = player; skill = player.driverSkill; deck = player.deck; pool = deck.heatPool;
        input = skill.Feedback.InputsStarted; initialization = skill.InitializationVersion; driverId = player.driverId;
        SourceLap = player.lap; SourcePosition = player.position; movement = player.totalMovementThisTurn;
        ActualCellsBefore = player.turnMovement.ActualCells;
        this.cruise = cruise; this.cornerExit = cornerExit;
    }

    /// <summary>Pure capture: chassis pace, arbitrary technology and preview calculations never grant feedback.</summary>
    public static VettelMovementFeedbackObservation Capture(RaceSession race, PlayerState player, bool crossedCorner, bool germanCornerExit)
    {
        if (player?.driverSkill?.Feedback == null || !player.driverSkill.Feedback.IsInputOpen || player.teamId != TeamId.DE ||
            player.isAI || player.deck?.heatPool == null || race == null || !race.Players.Contains(player)) return null;
        bool cruise = HasCruiseSource(race, player, crossedCorner);
        return new VettelMovementFeedbackObservation(race, player, cruise, germanCornerExit);
    }

    /// <summary>Shared native predicate for actual capture and isolated forecast, without granting a source.</summary>
    public static bool HasCruiseSource(RaceSession race, PlayerState player, bool crossedCorner)
    {
        if (race == null || player == null || !race.TeamVehicleBonusesEnabled || crossedCorner) return false;
        foreach (CardData card in player.playedSpeedCardsThisTurn)
            if (card != null && TeamVehicleRules.GetStraightCardBonus(player.teamId,
                CardPlayRules.GetCommittedSpeedCardValue(card)) > 0) return true;
        return false;
    }

    /// <summary>Requires the same owner/input/deck and complete real displacement; source lap precedes any finish crossing.</summary>
    public bool ApplyIfSettled(RaceSession race, PlayerState player, int actualDistance, bool completedCorner)
    {
        if (!IsSettled(race, player, actualDistance)) return false;
        bool recorded = cruise && skill.Feedback.RecordSource(race, player, VettelNativeSource.BasicCruise, 1, SourceLap);
        if (cornerExit && completedCorner)
            recorded = skill.Feedback.RecordSource(race, player, VettelNativeSource.GermanCornerExit, 1, SourceLap) || recorded;
        return recorded;
    }

    /// <summary>One driver-only reward from the already settled native source; it never emits another technology event.</summary>
    public int ConsumeRhythmBonusIfSettled(RaceSession race, PlayerState player, int actualDistance, bool completedCorner)
    {
        if (!IsSettled(race, player, actualDistance)) return 0;
        if (cruise && skill.Rhythm.LockedSource == VettelNativeSource.BasicCruise)
            return skill.Rhythm.ConsumeSettledSource(race, player, VettelNativeSource.BasicCruise, 1);
        if (cornerExit && completedCorner && skill.Rhythm.LockedSource == VettelNativeSource.GermanCornerExit)
            return skill.Rhythm.ConsumeSettledSource(race, player, VettelNativeSource.GermanCornerExit, 1);
        return skill.Rhythm.LockedSource == VettelNativeSource.PaidBeer && (player.techState?.germanBurst.FuelMovement ?? 0) > 0
            ? skill.Rhythm.ConsumeSettledSource(race, player, VettelNativeSource.PaidBeer, 2) : 0;
    }

    private bool IsSettled(RaceSession race, PlayerState player, int actualDistance) =>
        ReferenceEquals(session, race) && ReferenceEquals(owner, player) && ReferenceEquals(skill, player?.driverSkill) &&
            player != null && ReferenceEquals(deck, player.deck) && ReferenceEquals(pool, player.deck?.heatPool) && driverId == player.driverId &&
            initialization == skill.InitializationVersion && input == skill.Feedback.InputsStarted &&
            skill.Feedback.IsInputOpen && !RaceTurnRules.IsTerminal(player) && !RaceTurnRules.ShouldSkip(player) &&
            movement > 0 && actualDistance >= movement;
}
