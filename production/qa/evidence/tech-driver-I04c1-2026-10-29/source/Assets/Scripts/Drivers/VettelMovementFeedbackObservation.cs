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
        bool cruise = false;
        if (race.TeamVehicleBonusesEnabled && !crossedCorner)
            foreach (CardData card in player.playedSpeedCardsThisTurn)
                if (card != null && TeamVehicleRules.GetStraightCardBonus(player.teamId,
                    CardPlayRules.GetCommittedSpeedCardValue(card)) > 0) { cruise = true; break; }
        return new VettelMovementFeedbackObservation(race, player, cruise, germanCornerExit);
    }

    /// <summary>Requires the same owner/input/deck and complete real displacement; source lap precedes any finish crossing.</summary>
    public bool ApplyIfSettled(RaceSession race, PlayerState player, int actualDistance, bool completedCorner)
    {
        if (!ReferenceEquals(session, race) || !ReferenceEquals(owner, player) || !ReferenceEquals(skill, player?.driverSkill) ||
            !ReferenceEquals(deck, player.deck) || !ReferenceEquals(pool, player.deck?.heatPool) || driverId != player.driverId ||
            initialization != skill.InitializationVersion || input != skill.Feedback.InputsStarted ||
            !skill.Feedback.IsInputOpen || RaceTurnRules.IsTerminal(player) || RaceTurnRules.ShouldSkip(player) ||
            movement <= 0 || actualDistance < movement) return false;
        bool recorded = cruise && skill.Feedback.RecordSource(race, player, VettelNativeSource.BasicCruise, 1, SourceLap);
        if (cornerExit && completedCorner)
            recorded = skill.Feedback.RecordSource(race, player, VettelNativeSource.GermanCornerExit, 1, SourceLap) || recorded;
        return recorded;
    }
}
