using System;

/// <summary>
/// D04 PerfectRhythm, locked Word paragraphs 243/247/251/254-258 (ADR-002).
/// Fixed source, one +2 per eligible owner input; current input only at tier 1, current plus next at tiers 2/3.
/// Payment and race-use commits belong to VettelRhythmActivationPlan; generic active time is untouched.
/// </summary>
public sealed class VettelRhythmRuntime
{
    private RaceSession session;
    private PlayerState owner;
    private DriverSkillRuntimeState skill;
    private CardDeck deck;
    private HeatPool pool;
    private string driverId;
    private int initialization, activationInput, expiresInput;
    private bool startedThisGlobalTurn, consumed, paidBeerSeen, presentationCopy;
    private VettelRhythmRuntime presentationSource;

    public bool IsInputOpen { get; private set; }
    public int InputsStarted { get; private set; }
    public int Version { get; private set; }
    public bool ManualPath { get; private set; }
    public bool HasWindow { get; private set; }
    public VettelNativeSource LockedSource { get; private set; }
    public int BonusThisInput { get; private set; }
    public bool ActivatedThisInput => IsInputOpen && activationInput == InputsStarted;

    /// <summary>Global rounds close input collection without spending another owner's opportunity.</summary>
    public void BeginGlobalTurn() { CloseInput(); startedThisGlobalTurn = false; }

    /// <summary>Advances only once for the eligible owner's real input; interruption never refunds paid costs.</summary>
    public bool BeginInput(RaceSession race, PlayerState player)
    {
        if (presentationCopy && !ReferenceEquals(owner?.driverSkill?.Rhythm, this)) return false;
        if (!Eligible(race, player)) { Interrupt(); return false; }
        if (startedThisGlobalTurn) return false;
        CloseInput();
        if (session != null && !BindingMatches(race, player)) ClearWindow();
        Bind(race, player); InputsStarted++; IsInputOpen = true; startedThisGlobalTurn = true; Version++;
        if (HasWindow && InputsStarted > expiresInput) ClearWindow();
        return true;
    }

    /// <summary>Only the real paid-beer adapter marks an event; the active skill's own fee never marks beer.</summary>
    public bool MarkPaidBeer(RaceSession race, PlayerState player)
    {
        if (!HasInputScope(race, player) || !HasWindow || LockedSource != VettelNativeSource.PaidBeer) return false;
        paidBeerSeen = true; return true;
    }

    /// <summary>Read-only native preview; repeated queries do not consume an event or pay resources.</summary>
    public int PreviewBonus(RaceSession race, PlayerState player, bool cruise, bool exit)
    {
        if (!HasInputScope(race, player) || !HasWindow || consumed) return 0;
        return LockedSource == VettelNativeSource.BasicCruise && cruise ||
            LockedSource == VettelNativeSource.GermanCornerExit && exit ||
            LockedSource == VettelNativeSource.PaidBeer && paidBeerSeen ? 2 : 0;
    }

    /// <summary>Settled positive native effects grant one driver-only tail, never another native source callback.</summary>
    public int ConsumeSettledSource(RaceSession race, PlayerState player, VettelNativeSource source, int positiveAmount)
    {
        if (positiveAmount <= 0 || source != LockedSource || !HasInputScope(race, player) || !HasWindow || consumed ||
            source == VettelNativeSource.PaidBeer && !paidBeerSeen) return 0;
        consumed = true; BonusThisInput = 2; Version++; return 2;
    }

    /// <summary>Unused current opportunities expire; only a tier 2/3 next-input window survives normal cleanup.</summary>
    public void CloseInput()
    {
        if (HasWindow && InputsStarted >= expiresInput) ClearWindow();
        IsInputOpen = false; consumed = false; paidBeerSeen = false; BonusThisInput = 0; Version++;
    }

    /// <summary>Pit, skip, terminal or exit loses all remaining opportunities, retaining the paid migration marker.</summary>
    public void Interrupt()
    {
        ClearWindow(); IsInputOpen = false; startedThisGlobalTurn = true;
        session = null; owner = null; skill = null; deck = null; pool = null; driverId = null; Version++;
    }

    /// <summary>All mutable state is scalar; source identities are rebound before forecast consumption.</summary>
    public VettelRhythmRuntime CreatePresentationCopy()
    {
        var copy = (VettelRhythmRuntime)MemberwiseClone();
        copy.presentationCopy = true; copy.presentationSource = this; return copy;
    }

    /// <summary>Rejects shared real owner/deck/pool identity and binds an isolated forecast to copied resources.</summary>
    public bool RebindPresentationCopy(RaceSession race, PlayerState original, PlayerState copy)
    {
        if (!presentationCopy || presentationSource == null || !BindingMatches(race, original) ||
            !ReferenceEquals(original.driverSkill.Rhythm, presentationSource) || copy?.driverSkill == null ||
            !ReferenceEquals(copy.driverSkill.Rhythm, this) || copy.deck?.heatPool == null ||
            ReferenceEquals(copy.deck, original.deck) || ReferenceEquals(copy.deck.heatPool, original.deck.heatPool) ||
            copy.driverId != original.driverId || copy.teamId != original.teamId ||
            copy.driverSkill.InitializationVersion != original.driverSkill.InitializationVersion) return false;
        Bind(race, copy); return true;
    }

    internal bool HasInputScope(RaceSession race, PlayerState player) => IsInputOpen && Eligible(race, player) && BindingMatches(race, player);
    internal void AdvancePlanVersion() => Version++;
    internal void CommitActivation(VettelNativeSource source)
    {
        ManualPath = true; HasWindow = true; LockedSource = source; activationInput = InputsStarted;
        expiresInput = InputsStarted + (skill.Tier >= 2 ? 1 : 0);
        consumed = false; paidBeerSeen = false; BonusThisInput = 0; Version++;
    }

    private bool Eligible(RaceSession race, PlayerState player) => race?.Players != null && player?.driverSkill != null &&
        player.deck?.heatPool != null && (race.Players.Contains(player) || presentationCopy && ReferenceEquals(owner, player)) &&
        ReferenceEquals(player.driverSkill.Rhythm, this) && !player.isAI && player.teamId == TeamId.DE && player.driverSkill.Enabled &&
        player.driverSkill.Skill == DriverActiveSkillId.PerfectRhythm &&
        DriverSkillRules.GetSkill(player.driverId) == DriverActiveSkillId.PerfectRhythm &&
        player.driverSkill.Tier > 0 && RaceTurnRules.GetStartAction(player) == RaceTurnStartAction.SelectGear;
    private bool BindingMatches(RaceSession race, PlayerState player) => player?.driverSkill != null &&
        ReferenceEquals(session, race) && ReferenceEquals(owner, player) && ReferenceEquals(skill, player.driverSkill) &&
        ReferenceEquals(deck, player.deck) && ReferenceEquals(pool, player.deck?.heatPool) && driverId == player.driverId &&
        initialization == player.driverSkill.InitializationVersion;
    private void Bind(RaceSession race, PlayerState player)
    { session = race; owner = player; skill = player.driverSkill; deck = player.deck; pool = deck.heatPool; driverId = player.driverId; initialization = skill.InitializationVersion; }
    private void ClearWindow()
    { HasWindow = false; LockedSource = default; expiresInput = 0; consumed = false; paidBeerSeen = false; BonusThisInput = 0; }
}
