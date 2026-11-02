using System;
using System.Collections.Generic;

/// <summary>Explicit D03 active transitions: idle -> preparation -> execution one -> execution two -> idle.</summary>
public enum SchumacherStrategyStage { Idle, PreparationCommitted, ExecutionAvailable, ExecutionCommitted }

/// <summary>
/// Locked D03 draft paragraphs 195/199/203/207. Owner-input race data only (ADR-002).
/// Discard/payment authority belongs to the snapshot plan; this clock never spends resources.
/// </summary>
public sealed class SchumacherStrategyRuntime
{
    private RaceSession session;
    private PlayerState owner;
    private DriverSkillRuntimeState skill;
    private CardDeck deck;
    private HeatPool pool;
    private string driverId;
    private int initialization, nextInput, executionsCommitted;
    private bool startedThisGlobalTurn, waitingForNextInput, blocksPreparationThisInput, presentationCopy;
    private SchumacherStrategyRuntime presentationSource;

    public int Version { get; private set; }
    public int InputsStarted { get; private set; }
    public bool IsInputOpen { get; private set; }
    public bool ManualPath { get; private set; }
    public SchumacherStrategyStage Stage { get; private set; }
    public int ExecutionNumber { get; private set; }
    public int MovementBonusThisInput { get; private set; }
    public bool IsExecutionAvailable => IsInputOpen && Stage == SchumacherStrategyStage.ExecutionAvailable;
    public bool CommittedThisInput => Stage == SchumacherStrategyStage.PreparationCommitted || Stage == SchumacherStrategyStage.ExecutionCommitted;
    public bool HasSequence => waitingForNextInput || Stage != SchumacherStrategyStage.Idle || blocksPreparationThisInput;
    public bool IsPreparationAvailable => IsInputOpen && !HasSequence;

    /// <summary>Global time only closes the preceding input; it never advances the owner clock.</summary>
    public void BeginGlobalTurn() { CloseInput(); startedThisGlobalTurn = false; }

    /// <summary>Opens each actual eligible owner input at most once; other cars cannot advance this clock.</summary>
    public bool BeginInput(RaceSession race, PlayerState player)
    {
        if (!Eligible(race, player)) { Interrupt(); return false; }
        if (startedThisGlobalTurn) return false;
        bool continues = waitingForNextInput && BindingMatches(race, player) && nextInput == InputsStarted + 1;
        if (session != null && !BindingMatches(race, player)) ClearSequence();
        Bind(race, player);
        startedThisGlobalTurn = true; InputsStarted++; IsInputOpen = true; Version++;
        waitingForNextInput = false; MovementBonusThisInput = 0;
        blocksPreparationThisInput = continues;
        Stage = continues ? SchumacherStrategyStage.ExecutionAvailable : SchumacherStrategyStage.Idle;
        ExecutionNumber = continues ? executionsCommitted + 1 : 0;
        if (!continues) executionsCommitted = 0;
        return true;
    }

    /// <summary>A committed preparation locks low gears; a paid execution locks high gears.</summary>
    public bool IsGearAllowed(int gear) => Stage == SchumacherStrategyStage.PreparationCommitted
        ? gear >= 1 && gear <= 2 : Stage != SchumacherStrategyStage.ExecutionCommitted || gear >= 3 && gear <= 4;

    /// <summary>Declining an unpaid execution at gear confirmation loses its remaining sequence, never costs.</summary>
    public bool SealGear(int gear)
    {
        if (!IsGearAllowed(gear)) { Interrupt(); return false; }
        if (IsExecutionAvailable) DeclineExecution(Version);
        return true;
    }

    /// <summary>Missing/non-ordinary formal speed cards, interruption or wrong gear forfeits movement and future execution.</summary>
    public bool ValidateMovementInput(RaceSession race, PlayerState player, int requiredCardCount)
    {
        if (!CommittedThisInput) return true;
        int ordinary = 0;
        bool unique = true;
        var seen = new HashSet<CardData>();
        if (player != null)
            foreach (CardData card in player.playedSpeedCardsThisTurn)
            {
                if (!seen.Add(card)) unique = false;
                if (IsOrdinarySpeed(card)) ordinary++;
            }
        if (!IsInputOpen || !Eligible(race, player) || !BindingMatches(race, player) ||
            !IsGearAllowed(player.gear) || !unique || requiredCardCount < 0 || ordinary < requiredCardCount)
        { Interrupt(); return false; }
        return true;
    }

    /// <summary>Only successful committed stages may carry a next execution across normal cleanup.</summary>
    public void CloseInput()
    {
        if (IsExecutionAvailable) ClearSequence();
        IsInputOpen = false; Stage = SchumacherStrategyStage.Idle;
        ExecutionNumber = 0; MovementBonusThisInput = 0; blocksPreparationThisInput = false; Version++;
    }

    /// <summary>Pit/skip/terminal/exit cleanup loses the plan, retaining the fact that this race uses the paid migration.</summary>
    public void Interrupt()
    {
        ClearSequence(); IsInputOpen = false; startedThisGlobalTurn = true; blocksPreparationThisInput = true;
        session = null; owner = null; skill = null; deck = null; pool = null; driverId = null; Version++;
    }

    /// <summary>Only this exact unpaid window may be declined; stale delegates cannot close a newer plan.</summary>
    public void DeclineExecution(int expectedVersion)
    {
        if (!IsExecutionAvailable || Version != expectedVersion) return;
        ClearSequence(); blocksPreparationThisInput = true; Version++;
    }

    internal void AdvancePlanVersion() => Version++;
    internal bool HasInputScope(RaceSession race, PlayerState player) => IsInputOpen && Eligible(race, player) && BindingMatches(race, player);

    internal void CommitPreparation()
    {
        ManualPath = true; Stage = SchumacherStrategyStage.PreparationCommitted;
        executionsCommitted = 0; ExecutionNumber = 0; MovementBonusThisInput = 0;
        waitingForNextInput = true; nextInput = InputsStarted + 1; blocksPreparationThisInput = true; Version++;
    }

    internal void CommitExecution()
    {
        executionsCommitted = ExecutionNumber; Stage = SchumacherStrategyStage.ExecutionCommitted;
        MovementBonusThisInput = DriverSkillRules.GetSchumacherStrategyMovementBonus(skill.Tier);
        waitingForNextInput = executionsCommitted < DriverSkillRules.GetSchumacherStrategyExecutionCount(skill.Tier);
        nextInput = InputsStarted + 1; blocksPreparationThisInput = true; Version++;
    }

    /// <summary>Only scalar state and immutable identity references are retained; the copy cannot pay real resources.</summary>
    public SchumacherStrategyRuntime CreatePresentationCopy()
    {
        var copy = (SchumacherStrategyRuntime)MemberwiseClone();
        copy.presentationCopy = true; copy.presentationSource = this; return copy;
    }

    /// <summary>Explicit forecast rebinding isolates owner/deck/pool identity without modifying the source.</summary>
    public bool RebindPresentationCopy(RaceSession race, PlayerState original, PlayerState copy)
    {
        if (!presentationCopy || presentationSource == null || !BindingMatches(race, original) ||
            !ReferenceEquals(original.driverSkill.Strategy, presentationSource) || copy?.driverSkill == null ||
            !ReferenceEquals(copy.driverSkill.Strategy, this) || copy.deck?.heatPool == null ||
            ReferenceEquals(copy.deck, original.deck) || ReferenceEquals(copy.deck.heatPool, original.deck.heatPool) ||
            copy.driverId != original.driverId || copy.driverSkill.InitializationVersion != original.driverSkill.InitializationVersion)
            return false;
        Bind(race, copy); return true;
    }

    internal bool Eligible(RaceSession race, PlayerState player) => race?.Players != null && player?.driverSkill != null &&
        player.deck?.heatPool != null && (race.Players.Contains(player) || presentationCopy && ReferenceEquals(owner, player)) &&
        ReferenceEquals(player.driverSkill.Strategy, this) && !player.isAI && player.driverSkill.Enabled &&
        player.driverSkill.Skill == DriverActiveSkillId.PerfectLap && DriverSkillRules.GetSkill(player.driverId) == DriverActiveSkillId.PerfectLap &&
        player.driverSkill.Tier > 0 && RaceTurnRules.GetStartAction(player) == RaceTurnStartAction.SelectGear;

    private bool BindingMatches(RaceSession race, PlayerState player) => player?.driverSkill != null &&
        ReferenceEquals(session, race) && ReferenceEquals(owner, player) && ReferenceEquals(skill, player.driverSkill) &&
        ReferenceEquals(deck, player.deck) && ReferenceEquals(pool, player.deck?.heatPool) && driverId == player.driverId &&
        initialization == player.driverSkill.InitializationVersion;
    private void Bind(RaceSession race, PlayerState player)
    { session = race; owner = player; skill = player.driverSkill; deck = player.deck; pool = deck.heatPool; driverId = player.driverId; initialization = skill.InitializationVersion; }
    private void ClearSequence()
    { waitingForNextInput = false; nextInput = 0; executionsCommitted = 0; Stage = SchumacherStrategyStage.Idle; ExecutionNumber = 0; MovementBonusThisInput = 0; }
    internal static bool IsOrdinarySpeed(CardData card) => card != null && card.IsSpeed && !card.isTemp && string.IsNullOrEmpty(card.trickId);
}
