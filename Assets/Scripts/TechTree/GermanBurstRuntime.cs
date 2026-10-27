using System;
using System.Collections.Generic;

/// <summary>Removed payment instances are split into net engine restoration and capacity retirement.</summary>
public readonly struct GermanBurstRefundResult
{
    public GermanBurstRefundResult(HeatCoolingResult removed, int restored)
    { Removed = removed; Restored = restored; }
    public HeatCoolingResult Removed { get; }
    public int Restored { get; }
    public int Retired => Removed.Total - Restored;
}

/// <summary>Per-racer DE declaration, fixed fuel budget and exact post-declaration payment receipts. ADR-002.</summary>
public sealed class GermanBurstRuntime
{
    private RaceSession session;
    private PlayerState owner;
    private CardDeck deck;
    private HeatPool pool;
    private TechTreeState state;
    private bool presentationOnly;
    private List<CardData> receipts = new List<CardData>();
    private HashSet<CardData> excluded = new HashSet<CardData>();

    public int TurnId { get; private set; } = -1;
    public int Version { get; private set; }
    public int OpeningLap { get; private set; }
    public int FuelUsesThisTurn { get; private set; }
    public int FuelMovement => FuelUsesThisTurn * 2;
    public bool DeclarationOpen { get; private set; }
    public bool Declared { get; private set; }
    public bool Locked { get; private set; }
    public bool ManualPathThisTurn { get; private set; }
    public IReadOnlyList<CardData> Receipts => receipts.AsReadOnly();

    /// <summary>A known global turn is idempotent; implicit turns are only for legacy data-side callers.</summary>
    public void BeginTurn(int globalTurn)
    {
        int next = globalTurn >= 0 ? globalTurn : TurnId + 1;
        if (next == TurnId) return;
        CloseWindow();
        TurnId = next;
        FuelUsesThisTurn = 0;
        ManualPathThisTurn = false;
        DeclarationOpen = true;
    }

    internal bool Declare(RaceSession race, PlayerState player, int globalTurn)
    {
        if (presentationOnly || !DeclarationOpen || Declared || TurnId != globalTurn)
            return false;
        session = race; owner = player; deck = player.deck; pool = deck.heatPool; state = player.techState;
        OpeningLap = player.lap; Declared = true; ManualPathThisTurn = true;
        foreach (var card in deck.Hand) excluded.Add(card);
        foreach (var card in deck.DrawPile) excluded.Add(card);
        foreach (var card in deck.DiscardPile) excluded.Add(card);
        foreach (var card in player.heatPaidCardsThisTurn) excluded.Add(card);
        Version++;
        return true;
    }

    internal bool Matches(RaceSession race, PlayerState player)
    {
        if (!Declared || player?.techState?.germanBurst != this || !ReferenceEquals(session, race)) return false;
        if (presentationOnly)
            return !ReferenceEquals(player, owner) && player.deck?.heatPool != null &&
                !ReferenceEquals(player.deck, deck) && !ReferenceEquals(player.deck.heatPool, pool);
        return ReferenceEquals(owner, player) && ReferenceEquals(deck, player.deck) &&
            ReferenceEquals(pool, player.deck.heatPool) && ReferenceEquals(state, player.techState);
    }

    /// <summary>Only newly created, actually paid permanent instances can lock the race opportunity.</summary>
    internal bool RecordPayments(RaceSession race, PlayerState player)
    {
        if (!Matches(race, player)) return false;
        bool added = false;
        foreach (var card in player.heatPaidCardsThisTurn)
        {
            if (card == null || !card.IsHeat || card.isTemp || excluded.Contains(card) || receipts.Contains(card) ||
                !player.deck.IsUniquePermanentHeatInHandOrDiscard(card)) continue;
            receipts.Add(card); added = true;
        }
        if (added) { Locked = true; player.techState.grillSpezialUsed = true; Version++; }
        return added;
    }

    internal void RecordFuel() { FuelUsesThisTurn++; Version++; }
    public void SealDeclaration() { DeclarationOpen = false; Version++; }

    /// <summary>Cancellation is free only before a real payment and only for the observed window version.</summary>
    public bool Cancel(int expectedVersion)
    {
        if (!Declared || Locked || Version != expectedVersion) return false;
        bool canReopen = DeclarationOpen;
        CloseWindow();
        DeclarationOpen = canReopen;
        return true;
    }

    /// <summary>Closes references without refunding an already locked race opportunity or resetting fuel uses.</summary>
    public void CloseWindow()
    {
        Declared = false; Locked = false; DeclarationOpen = false;
        session = null; owner = null; deck = null; pool = null; state = null;
        receipts.Clear(); excluded.Clear(); Version++;
    }

    public void ResetForRace()
    {
        CloseWindow(); TurnId = -1; FuelUsesThisTurn = 0; ManualPathThisTurn = false;
    }

    /// <summary>New mutable containers and virtual-owner guards isolate predicted payments from the live racer.</summary>
    public GermanBurstRuntime CreatePresentationCopy()
    {
        var copy = (GermanBurstRuntime)MemberwiseClone();
        copy.receipts = new List<CardData>(receipts);
        copy.excluded = new HashSet<CardData>(excluded);
        copy.presentationOnly = true;
        return copy;
    }
}
