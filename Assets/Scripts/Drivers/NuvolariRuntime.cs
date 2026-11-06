using System;
using System.Collections.Generic;

/// <summary>
/// D06 UnyieldingLegend (locked 2026-10-22 candidate branch).
///
/// The passive is a single, owner-input opportunity: when the input opened with
/// at most the configured low engine reserve, the first successful ordinary
/// corner payment can return only old permanent heat that was already in hand.
/// No payment is waived; the ordinary payment path settles first and the return
/// happens afterwards.  The runtime keeps exact card instances and race/session
/// identity so previews or stale callbacks cannot mint heat.
/// </summary>
public sealed class NuvolariRuntime
{
    private RaceSession session;
    private PlayerState owner;
    private DriverSkillRuntimeState skill;
    private CardDeck deck;
    private HeatPool pool;
    private string driverId;
    private int initialization;
    private int engineAtInput;
    private int inputLap;
    private bool startedThisGlobalTurn;
    private bool forcedSpinThisInput;
    private bool presentationCopy;
    private NuvolariRuntime presentationSource;
    private List<CardData> oldHandHeat = new List<CardData>();
    private HashSet<int> usedLaps = new HashSet<int>();

    public int InputsStarted { get; private set; }
    public bool IsInputOpen { get; private set; }
    public int EngineAtInput => engineAtInput;
    public int InputLap => inputLap;
    public int LastRestored { get; private set; }
    public bool HasPrepaidSpin { get; private set; }

    public bool HasTriggeredInLap(int lap) => usedLaps.Contains(lap);

    /// <summary>Global turn closes the prior owner-input window; no global cadence is added.</summary>
    public void BeginGlobalTurn()
    {
        CloseInput();
        startedThisGlobalTurn = false;
        forcedSpinThisInput = false;
        LastRestored = 0;
    }

    /// <summary>Starts one real human Italian owner-input sample before gear confirmation.</summary>
    public bool BeginInput(RaceSession race, PlayerState player)
    {
        if (!Eligible(race, player))
        {
            Interrupt();
            return false;
        }
        if (startedThisGlobalTurn) return false;

        CloseInput();
        if (session != null && !BindingMatches(race, player)) ClearTransient();
        Bind(race, player);
        startedThisGlobalTurn = true;
        IsInputOpen = true;
        InputsStarted++;
        LastRestored = 0;
        forcedSpinThisInput = false;
        engineAtInput = Math.Max(0, deck.heatPool.remaining);
        inputLap = player.lap;
        oldHandHeat.Clear();
        foreach (CardData card in deck.Hand)
        {
            if (deck.IsUniquePermanentHeatInHandOrDiscard(card) && !oldHandHeat.Contains(card))
                oldHandHeat.Add(card);
        }
        return true;
    }

    /// <summary>
    /// Resolves the first successful ordinary-corner receipt in this input.
    /// Partial payment never reaches this method, so the normal spin path remains
    /// authoritative and no ordinary penalty is silently forgiven.
    /// </summary>
    public int ResolveAfterOrdinaryCorner(RaceSession race, PlayerState player, int actualDrawn)
    {
        LastRestored = 0;
        if (actualDrawn <= 0 || forcedSpinThisInput || !IsInputOpen || !Eligible(race, player) ||
            !BindingMatches(race, player) || skill.IsActive || player.lap != inputLap ||
            usedLaps.Contains(player.lap) || engineAtInput > DriverSkillRules.GetNuvolariPassiveEntryEngineMax(skill.PassiveTier))
            return 0;

        // The qualifying corner is consumed even when an old card was removed by
        // another exact transaction between input start and this receipt.
        usedLaps.Add(player.lap);
        var eligible = new List<CardData>();
        int limit = DriverSkillRules.GetNuvolariPassiveOldHandReturn(skill.PassiveTier);
        foreach (CardData card in oldHandHeat)
        {
            if (eligible.Count >= limit) break;
            if (deck.ContainsInHand(card) && deck.IsUniquePermanentHeatInHandOrDiscard(card))
                eligible.Add(card);
        }

        LastRestored = deck.ReturnHeatCardsToPool(eligible);
        return LastRestored;
    }

    public int ConsumeLastRestored()
    {
        int restored = LastRestored;
        LastRestored = 0;
        return restored;
    }

    /// <summary>Marks a forced spin so a later callback in the same input cannot refund heat.</summary>
    public void MarkForcedSpin() => forcedSpinThisInput = true;

    /// <summary>Records D06's active pre-commit without mutating the live spin counter early.</summary>
    public void CommitPrepaidSpin() => HasPrepaidSpin = true;

    public void ConsumePrepaidSpin() => HasPrepaidSpin = false;

    public void CloseInput()
    {
        IsInputOpen = false;
        forcedSpinThisInput = false;
        engineAtInput = 0;
        inputLap = 0;
        oldHandHeat.Clear();
    }

    /// <summary>Skip, pit, terminal and exit close the binding but do not restore race uses.</summary>
    public void Interrupt()
    {
        CloseInput();
        startedThisGlobalTurn = true;
        session = null;
        owner = null;
        skill = null;
        deck = null;
        pool = null;
        driverId = null;
    }

    public NuvolariRuntime CreatePresentationCopy()
    {
        var copy = (NuvolariRuntime)MemberwiseClone();
        copy.oldHandHeat = new List<CardData>(oldHandHeat);
        copy.usedLaps = new HashSet<int>(usedLaps);
        copy.presentationCopy = true;
        copy.presentationSource = this;
        return copy;
    }

    public bool RebindPresentationCopy(RaceSession race, PlayerState original, PlayerState copy)
    {
        if (!presentationCopy || presentationSource == null || !BindingMatches(race, original) ||
            !ReferenceEquals(original.driverSkill?.Nuvolari, presentationSource) || copy?.driverSkill == null ||
            !ReferenceEquals(copy.driverSkill.Nuvolari, this) || copy.deck?.heatPool == null ||
            ReferenceEquals(copy.deck, original.deck) || ReferenceEquals(copy.deck.heatPool, original.deck.heatPool) ||
            copy.driverId != original.driverId || copy.teamId != original.teamId ||
            copy.driverSkill.InitializationVersion != original.driverSkill.InitializationVersion)
            return false;
        Bind(race, copy);
        return true;
    }

    private bool Eligible(RaceSession race, PlayerState player) =>
        race?.Players != null && player?.driverSkill != null && player.deck?.heatPool != null &&
        (race.Players.Contains(player) || presentationCopy && ReferenceEquals(owner, player)) &&
        ReferenceEquals(player.driverSkill.Nuvolari, this) && !player.isAI && player.teamId == TeamId.IT &&
        player.driverSkill.Enabled && player.driverSkill.PassiveSkill == DriverPassiveSkillId.UnyieldingLegend &&
        DriverSkillRules.GetPassiveSkill(player.driverId) == DriverPassiveSkillId.UnyieldingLegend &&
        player.driverSkill.PassiveTier > 0 && RaceTurnRules.GetStartAction(player) == RaceTurnStartAction.SelectGear;

    private bool BindingMatches(RaceSession race, PlayerState player) =>
        player?.driverSkill != null && ReferenceEquals(session, race) && ReferenceEquals(owner, player) &&
        ReferenceEquals(skill, player.driverSkill) && ReferenceEquals(deck, player.deck) &&
        ReferenceEquals(pool, player.deck?.heatPool) && driverId == player.driverId &&
        initialization == player.driverSkill.InitializationVersion;

    private void Bind(RaceSession race, PlayerState player)
    {
        session = race;
        owner = player;
        skill = player.driverSkill;
        deck = player.deck;
        pool = deck.heatPool;
        driverId = player.driverId;
        initialization = skill.InitializationVersion;
    }

    private void ClearTransient()
    {
        CloseInput();
        session = null;
        owner = null;
        skill = null;
        deck = null;
        pool = null;
        driverId = null;
    }
}
