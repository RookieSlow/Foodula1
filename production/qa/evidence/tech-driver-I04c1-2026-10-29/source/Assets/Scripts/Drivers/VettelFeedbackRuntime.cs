using System;
using System.Collections.Generic;

/// <summary>Actual owned native effects, recorded after their source settlement rather than prediction.</summary>
public enum VettelNativeSource { BasicCruise = 1, PaidBeer = 2, GermanCornerExit = 3 }

/// <summary>
/// D04 InformationSponge, locked draft paragraphs 242/246/250/255/258 (ADR-002).
/// First real eligible native source per lap prepares one next-owner-input old-hand cooling attempt.
/// Pure race state: no Unity, presentation, source payment or active PerfectRhythm migration.
/// </summary>
public sealed class VettelFeedbackRuntime
{
    private RaceSession session;
    private PlayerState owner;
    private DriverSkillRuntimeState skill;
    private CardDeck deck;
    private HeatPool pool;
    private string driverId;
    private int initialization, grantedInput, readyInput;
    private bool startedThisGlobalTurn, frozen, presentationCopy;
    private VettelFeedbackRuntime presentationSource;
    private List<CardData> oldHandHeat = new List<CardData>();
    private HashSet<int> triggeredLaps = new HashSet<int>();

    public int InputsStarted { get; private set; }
    public bool IsInputOpen { get; private set; }
    public bool HasPending { get; private set; }
    public int GrantedInput => grantedInput;
    public int ReadyInput => readyInput;
    public VettelNativeSource PendingSource { get; private set; }
    public int LastRestored { get; private set; }
    public int LastRetired { get; private set; }
    public bool HasTriggeredInLap(int lap) => triggeredLaps.Contains(lap);

    /// <summary>Closes the prior A2 opportunity without advancing the owner's input counter.</summary>
    public void BeginGlobalTurn() { CloseInput(); startedThisGlobalTurn = false; }

    /// <summary>Only the next real eligible owner input freezes its existing permanent hand heat.</summary>
    public bool BeginInput(RaceSession race, PlayerState player)
    {
        if (!Eligible(race, player)) { Interrupt(); return false; }
        if (startedThisGlobalTurn) return false;
        CloseInput();
        if (session != null && !BindingMatches(race, player)) ClearPending();
        Bind(race, player);
        startedThisGlobalTurn = true; IsInputOpen = true; InputsStarted++;
        LastRestored = 0; LastRetired = 0;
        if (HasPending && InputsStarted > readyInput) ClearPending();
        if (!HasPending || InputsStarted != readyInput) return true;
        frozen = true;
        foreach (CardData card in deck.Hand)
            if (deck.IsUniquePermanentHeatInHandOrDiscard(card) && !oldHandHeat.Contains(card)) oldHandHeat.Add(card);
        return true;
    }

    /// <summary>Typed adapters call only after a positive native effect actually settles for this owner.</summary>
    public bool RecordSource(RaceSession race, PlayerState player, VettelNativeSource source, int positiveAmount, int? sourceLap = null)
    {
        int producedLap = sourceLap ?? player?.lap ?? -1;
        if (producedLap < 0 || player == null || producedLap > player.lap || positiveAmount <= 0 || !IsInputOpen || !Eligible(race, player) || !BindingMatches(race, player) ||
            !SourceAllowed(source, skill.PassiveTier) || !triggeredLaps.Add(producedLap)) return false;
        // The lap's first real source is observed even when an earlier feedback cannot stack or renew.
        if (HasPending) return false;
        HasPending = true; PendingSource = source;
        grantedInput = InputsStarted; readyInput = InputsStarted + 1;
        return true;
    }

    /// <summary>
    /// One A2 attempt removes at most one frozen permanent instance still in hand. Returns actual removed
    /// count; LastRestored/LastRetired distinguish net engine recovery from regional-capacity retirement.
    /// </summary>
    public int ConsumeAfterDraw(RaceSession race, PlayerState player)
    {
        // A fresh presentation clone still retains read-only source identity until explicit rebinding.
        // Reject accidental real-owner calls before any mutable copy state is cleared.
        if (presentationCopy && !ReferenceEquals(owner?.driverSkill?.Feedback, this)) return 0;
        LastRestored = 0; LastRetired = 0;
        if (!HasPending || !frozen || !IsInputOpen || InputsStarted != readyInput) return 0;
        if (!ReferenceEquals(session, race) || !ReferenceEquals(owner, player)) return 0;
        if (!Eligible(race, player) || !BindingMatches(race, player)) { Interrupt(); return 0; }
        CardData chosen = null;
        foreach (CardData card in oldHandHeat)
            if (deck.ContainsInHand(card) && deck.IsUniquePermanentHeatInHandOrDiscard(card)) { chosen = card; break; }
        ClearPending();
        if (chosen == null) return 0;
        LastRestored = deck.ReturnHeatCardsToPool(new[] { chosen });
        LastRetired = 1 - LastRestored;
        return 1;
    }

    /// <summary>Normal cleanup expires the ready input, preserving a newly earned future opportunity.</summary>
    public void CloseInput()
    {
        if (HasPending && InputsStarted >= readyInput) ClearPending();
        frozen = false; oldHandHeat.Clear(); IsInputOpen = false;
    }

    /// <summary>Skip, pit, terminal and exit cancel the feedback; already observed lap opportunities stay used.</summary>
    public void Interrupt()
    {
        ClearPending(); IsInputOpen = false; startedThisGlobalTurn = true;
        session = null; owner = null; skill = null; deck = null; pool = null; driverId = null;
    }

    /// <summary>Clones both mutable collections; exact immutable card references match the isolated deck copy.</summary>
    public VettelFeedbackRuntime CreatePresentationCopy()
    {
        var copy = (VettelFeedbackRuntime)MemberwiseClone();
        copy.oldHandHeat = new List<CardData>(oldHandHeat);
        copy.triggeredLaps = new HashSet<int>(triggeredLaps);
        copy.presentationCopy = true; copy.presentationSource = this;
        return copy;
    }

    /// <summary>Forecast mutation requires explicit rebinding to isolated owner, deck and engine instances.</summary>
    public bool RebindPresentationCopy(RaceSession race, PlayerState original, PlayerState copy)
    {
        if (!presentationCopy || presentationSource == null || !BindingMatches(race, original) ||
            !ReferenceEquals(original.driverSkill.Feedback, presentationSource) || copy?.driverSkill == null ||
            !ReferenceEquals(copy.driverSkill.Feedback, this) || copy.deck?.heatPool == null ||
            ReferenceEquals(copy.deck, original.deck) || ReferenceEquals(copy.deck.heatPool, original.deck.heatPool) ||
            copy.driverId != original.driverId || copy.teamId != original.teamId ||
            copy.driverSkill.InitializationVersion != original.driverSkill.InitializationVersion) return false;
        Bind(race, copy); return true;
    }

    private static bool SourceAllowed(VettelNativeSource source, int tier) =>
        source == VettelNativeSource.BasicCruise && tier >= 1 || source == VettelNativeSource.PaidBeer && tier >= 2 ||
        source == VettelNativeSource.GermanCornerExit && tier >= 3;

    private bool Eligible(RaceSession race, PlayerState player) => race?.Players != null && player?.driverSkill != null &&
        player.deck?.heatPool != null && (race.Players.Contains(player) || presentationCopy && ReferenceEquals(owner, player)) &&
        ReferenceEquals(player.driverSkill.Feedback, this) && !player.isAI && player.teamId == TeamId.DE && player.driverSkill.Enabled &&
        player.driverSkill.PassiveSkill == DriverPassiveSkillId.InformationSponge &&
        DriverSkillRules.GetPassiveSkill(player.driverId) == DriverPassiveSkillId.InformationSponge &&
        player.driverSkill.PassiveTier > 0 && RaceTurnRules.GetStartAction(player) == RaceTurnStartAction.SelectGear;

    private bool BindingMatches(RaceSession race, PlayerState player) => player?.driverSkill != null &&
        ReferenceEquals(session, race) && ReferenceEquals(owner, player) && ReferenceEquals(skill, player.driverSkill) &&
        ReferenceEquals(deck, player.deck) && ReferenceEquals(pool, player.deck?.heatPool) && driverId == player.driverId &&
        initialization == player.driverSkill.InitializationVersion;
    private void Bind(RaceSession race, PlayerState player)
    { session = race; owner = player; skill = player.driverSkill; deck = player.deck; pool = deck.heatPool; driverId = player.driverId; initialization = skill.InitializationVersion; }
    private void ClearPending()
    { HasPending = false; PendingSource = default; grantedInput = 0; readyInput = 0; frozen = false; oldHandHeat.Clear(); }
}
