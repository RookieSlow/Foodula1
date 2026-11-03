using System;

/// <summary>Per-participant input scope and race opportunity; never persisted in the research profile.</summary>
public sealed class ItalyPrecisionTuneRuntime
{
    private RaceSession session;
    private PlayerState owner;
    private CardDeck deck;
    private TechTreeState state;
    private bool presentationOnly;

    public bool UsedThisRace { get; private set; }
    public bool InputOpen { get; private set; }
    public int InputId { get; private set; }
    public int TurnId { get; private set; } = -1;
    public int Version { get; private set; }
    public CardData CommittedTarget { get; private set; }

    internal bool BeginInput(RaceSession race, PlayerState player, int globalTurn)
    {
        if (presentationOnly) return false;
        if (InputOpen && Matches(race, player) && TurnId == globalTurn) return false;
        CloseInput();
        session = race; owner = player; deck = player.deck; state = player.techState;
        TurnId = globalTurn; InputId++; InputOpen = true; Version++;
        return true;
    }

    internal bool Matches(RaceSession race, PlayerState player)
        => ReferenceEquals(session, race) && ReferenceEquals(owner, player) &&
            ReferenceEquals(deck, player?.deck) && ReferenceEquals(state, player?.techState) &&
            ReferenceEquals(player?.techState?.precisionTune, this);

    internal bool IsCommittedFor(PlayerState player)
        => UsedThisRace && CommittedTarget != null && ReferenceEquals(owner, player) &&
            ReferenceEquals(deck, player?.deck) && ReferenceEquals(state, player?.techState) &&
            player.playedSpeedCardsThisTurn.Contains(CommittedTarget);

    internal void CommitValidated(CardData target)
    {
        CommittedTarget = target; UsedThisRace = true; Version++;
    }

    /// <summary>Release current input/target references without returning a spent race use.</summary>
    public void CloseInput()
    {
        InputOpen = false; CommittedTarget = null;
        session = null; owner = null; deck = null; state = null; Version++;
    }

    public void ResetForRace()
    {
        CloseInput(); UsedThisRace = false; InputId = 0; TurnId = -1;
    }

    public ItalyPrecisionTuneRuntime CreatePresentationCopy()
    {
        var copy = (ItalyPrecisionTuneRuntime)MemberwiseClone();
        copy.presentationOnly = true;
        return copy;
    }

    internal void RebindPresentationCopy(RaceSession race, PlayerState original, PlayerState copy)
    {
        if (!presentationOnly || !ReferenceEquals(session, race) || !ReferenceEquals(owner, original)) return;
        owner = copy; deck = copy.deck; state = copy.techState;
    }
}
