using System;
using System.Collections.Generic;

/// <summary>
/// D03 EngineersTune (locked 2026-10-22 draft paragraphs 194/198/202/206).
/// Only actual ordinary-corner permanent receipts prepare a future owner-input discount.
/// ADR-002: race data only; no Unity, UI or coordinator lifetime ownership.
/// </summary>
public sealed class SchumacherTuningRuntime
{
    private RaceSession session;
    private PlayerState owner;
    private DriverSkillRuntimeState skill;
    private CardDeck deck;
    private HeatPool pool;
    private string driverId;
    private int initialization, receiptFloor, grantedInput, expiresInput;
    private bool startedThisGlobalTurn, presentationCopy;
    private SchumacherTuningRuntime presentationSource;
    private HashSet<int> grantedLaps = new HashSet<int>();
    private HashSet<CardData> seenReceipts = new HashSet<CardData>();
    private HashSet<CardData> oldReceipts = new HashSet<CardData>();

    public int InputsStarted { get; private set; }
    public int PaidThisInput { get; private set; }
    public bool IsInputOpen { get; private set; }
    public bool HasPending { get; private set; }
    public int GrantedInput => grantedInput;
    public int ExpiresInput => expiresInput;
    public bool HasGrantedInLap(int lap) => grantedLaps.Contains(lap);

    /// <summary>Global time closes collection only; future validity advances on BeginInput.</summary>
    public void BeginGlobalTurn()
    {
        CloseInput();
        startedThisGlobalTurn = false;
    }

    /// <summary>Starts at most one real eligible human input in this participant's global turn.</summary>
    public bool BeginInput(RaceSession race, PlayerState player)
    {
        if (!Eligible(race, player)) { Interrupt(); return false; }
        if (startedThisGlobalTurn) return false;
        if (session != null && !BindingIdentityMatches(race, player)) ClearTransient();
        Bind(race, player);
        startedThisGlobalTurn = true;
        InputsStarted++;
        IsInputOpen = true;
        PaidThisInput = 0;
        receiptFloor = player.heatPaidCardsThisTurn.Count;
        oldReceipts.Clear(); seenReceipts.Clear();
        foreach (CardData receipt in player.heatPaidCardsThisTurn) oldReceipts.Add(receipt);
        if (HasPending && InputsStarted > expiresInput) HasPending = false;
        return true;
    }

    /// <summary>
    /// Called only by the typed actual-corner payment adapter after appending actualDrawn receipts.
    /// Repeated callbacks, old receipts, temporary heat and ambiguous instances cannot add credit.
    /// </summary>
    public void RecordOrdinaryCornerPayment(RaceSession race, PlayerState player, int actualDrawn)
    {
        if (actualDrawn <= 0 || !ValidateOpenBinding(race, player)) return;
        var receipts = player.heatPaidCardsThisTurn;
        int first = receipts.Count - actualDrawn;
        if (first < receiptFloor || first < 0) return;
        int unique = 0;
        for (int i = first; i < receipts.Count; i++)
        {
            CardData card = receipts[i];
            if (card == null || !card.IsHeat || card.isTemp || oldReceipts.Contains(card) ||
                !deck.IsUniquePermanentHeatInHandOrDiscard(card) || !seenReceipts.Add(card)) continue;
            unique++;
        }
        PaidThisInput = (int)Math.Min(int.MaxValue, (long)PaidThisInput + unique);
        if (HasPending || grantedLaps.Contains(player.lap) ||
            PaidThisInput < DriverSkillRules.GetSchumacherTuningPaymentThreshold(skill.PassiveTier)) return;
        HasPending = true;
        grantedInput = InputsStarted;
        expiresInput = InputsStarted + DriverSkillRules.GetSchumacherTuningInputLifetime(skill.PassiveTier);
        grantedLaps.Add(player.lap);
    }

    /// <summary>Consumes one on the first positive ordinary-corner cost in a later valid owner input.</summary>
    public int ConsumeReduction(RaceSession race, PlayerState player, int positiveCost)
    {
        if (positiveCost <= 0 || !ValidateOpenBinding(race, player) || !HasPending ||
            InputsStarted <= grantedInput || InputsStarted > expiresInput) return 0;
        HasPending = false;
        return Math.Min(positiveCost, DriverSkillRules.GetSchumacherTuningReduction(skill.PassiveTier));
    }

    /// <summary>Normal cleanup closes source collection without removing an unspent future mark.</summary>
    public void CloseInput()
    {
        IsInputOpen = false;
        PaidThisInput = 0;
        receiptFloor = 0;
        oldReceipts.Clear(); seenReceipts.Clear();
    }

    /// <summary>Interruption loses future readiness and race references, retaining already granted lap limits.</summary>
    public void Interrupt()
    {
        ClearTransient();
        startedThisGlobalTurn = true;
    }

    /// <summary>Mutable source/grant sets are independent. RebindPresentationCopy enables forecast use.</summary>
    public SchumacherTuningRuntime CreatePresentationCopy()
    {
        var copy = (SchumacherTuningRuntime)MemberwiseClone();
        copy.grantedLaps = new HashSet<int>(grantedLaps);
        copy.seenReceipts = new HashSet<CardData>(seenReceipts);
        copy.oldReceipts = new HashSet<CardData>(oldReceipts);
        copy.presentationCopy = true;
        copy.presentationSource = this;
        return copy;
    }

    /// <summary>Explicitly binds only a newly isolated copy of this exact owner/runtime to copied resources.</summary>
    public bool RebindPresentationCopy(RaceSession race, PlayerState original, PlayerState copy)
    {
        if (!presentationCopy || presentationSource == null || !BindingIdentityMatches(race, original) ||
            !ReferenceEquals(original.driverSkill.Tuning, presentationSource) ||
            copy?.driverSkill == null || !ReferenceEquals(copy.driverSkill.Tuning, this) ||
            copy.deck?.heatPool == null || ReferenceEquals(copy.deck, original.deck) ||
            ReferenceEquals(copy.deck.heatPool, original.deck.heatPool) || copy.driverId != original.driverId ||
            copy.driverSkill.InitializationVersion != original.driverSkill.InitializationVersion) return false;
        Bind(race, copy);
        return true;
    }

    private bool ValidateOpenBinding(RaceSession race, PlayerState player)
    {
        if (!IsInputOpen) return false;
        if (Eligible(race, player) && BindingIdentityMatches(race, player)) return true;
        Interrupt();
        return false;
    }

    private bool Eligible(RaceSession race, PlayerState player) =>
        race?.Players != null && player?.driverSkill != null && player.deck?.heatPool != null &&
        (race.Players.Contains(player) || presentationCopy && ReferenceEquals(owner, player)) &&
        ReferenceEquals(player.driverSkill.Tuning, this) && !player.isAI && player.driverSkill.Enabled &&
        player.driverSkill.PassiveSkill == DriverPassiveSkillId.EngineersTune &&
        DriverSkillRules.GetPassiveSkill(player.driverId) == DriverPassiveSkillId.EngineersTune &&
        player.driverSkill.PassiveTier > 0 && RaceTurnRules.GetStartAction(player) == RaceTurnStartAction.SelectGear;

    private bool BindingIdentityMatches(RaceSession race, PlayerState player) =>
        player?.driverSkill != null && ReferenceEquals(session, race) && ReferenceEquals(owner, player) &&
        ReferenceEquals(skill, player.driverSkill) && ReferenceEquals(deck, player.deck) &&
        ReferenceEquals(pool, player.deck.heatPool) && driverId == player.driverId &&
        initialization == player.driverSkill.InitializationVersion;

    private void Bind(RaceSession race, PlayerState player)
    {
        session = race; owner = player; skill = player.driverSkill; deck = player.deck; pool = deck.heatPool;
        driverId = player.driverId; initialization = skill.InitializationVersion;
    }

    private void ClearTransient()
    {
        CloseInput();
        HasPending = false;
        grantedInput = 0; expiresInput = 0;
        session = null; owner = null; skill = null; deck = null; pool = null; driverId = null;
    }
}
