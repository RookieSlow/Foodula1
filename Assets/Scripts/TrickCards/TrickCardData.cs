using System.Collections.Generic;

// ═══════════════════════════════════════════════════════════════════════════════
// TrickCardData.cs — Team trick card data models for Foodula1
// Pure C# data layer, no Unity dependencies. Follows ADR-002.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Trick card category.</summary>
public enum TrickCardType
{
    Attack,
    Defense
}

/// <summary>All trick card effect types (12 unique cards across 6 countries).</summary>
public enum TrickEffectType
{
    // UK
    Scone,              // A: Pay 1 heat from engine → +2 move
    EnglishBreakfastTea,// D: Cool 1 heat from hand

    // DE
    Sauerkraut,         // A: Crossed corner → +2 move; else +1
    Schwarzbrot,        // D: Next heat payment this turn -1 (min 1)

    // IT
    Parmigiano,         // A: Slipstream +2 this turn (total +4)
    Chianti,            // D: Discard 1 speed card → cool 1 heat

    // US
    Fries,              // A: Crossed landmark last turn → 1 temp heat (usable, destroyed end of turn)
    Cola,               // D: Crossed landmark last turn → draw 1 card

    // CN
    HotpotBase,         // A: Go mode → next normal speed card becomes ATTACK (+1; full card excluded from corner speed)
    IceJelly,           // D: Recover mode → block slipstream for trailing cars

    // JP
    TorpedoTempura,     // A: Overtaking → +1 speed; being overtaken → opponent +1
    KantoOden           // D: Skip turn, accumulate gear card count to next turn
}

/// <summary>Definition of a single trick card.</summary>
[System.Serializable]
public class TrickCardDef
{
    /// <summary>Unique ID, e.g. "uk-scone".</summary>
    public string id;

    /// <summary>Display name (Chinese).</summary>
    public string name;

    /// <summary>Display name (English).</summary>
    public string nameEn;

    /// <summary>Which team this card belongs to.</summary>
    public TeamId teamId;

    /// <summary>Attack or Defense.</summary>
    public TrickCardType cardType;

    /// <summary>Effect type.</summary>
    public TrickEffectType effectType;

    /// <summary>Flavor description.</summary>
    public string description;

    /// <summary>Compact glyph-free icon label used by the UI.</summary>
    public string icon;

    public bool IsAttack => cardType == TrickCardType.Attack;
    public bool IsDefense => cardType == TrickCardType.Defense;

    public TrickCardDef() { }

    public TrickCardDef(
        string id, string name, string nameEn, TeamId teamId,
        TrickCardType cardType, TrickEffectType effectType,
        string description, string icon)
    {
        this.id = id;
        this.name = name;
        this.nameEn = nameEn;
        this.teamId = teamId;
        this.cardType = cardType;
        this.effectType = effectType;
        this.description = description;
        this.icon = icon;
    }
}

/// <summary>
/// Per-team runtime state for trick card tracking.
/// Tracks per-turn limits, active modifiers, and conditional flags.
/// </summary>
[System.Serializable]
public class TrickCardState
{
    /// <summary>Whether a trick card was played this turn (max 1 per turn).</summary>
    public bool trickPlayedThisTurn;

    /// <summary>ID of the trick card played this turn.</summary>
    public string trickPlayedThisTurnId;

    // ── DE: Schwarzbrot ──
    /// <summary>Next heat payment this turn is reduced by 1.</summary>
    public bool schwarzbrotActive;
    /// <summary>How many heat reductions remain (typically 1).</summary>
    public int schwarzbrotRemaining;

    // ── DE: Sauerkraut ──
    /// <summary>Sauerkraut was played this turn — check corner crossing at resolution.</summary>
    public bool sauerkrautPlayed;

    // ── IT: Parmigiano ──
    /// <summary>Slipstream bonus +2 active this turn.</summary>
    public bool parmigianoActive;

    // ── JP: Torpedo Tempura ──
    /// <summary>Overtake bonus active this turn.</summary>
    public bool torpedoTempuraActive;

    // ── CN: Ice Jelly ──
    /// <summary>Block slipstream for trailing cars this turn.</summary>
    public bool iceJellyActive;

    // ── CN: Hotpot Base ──
    /// <summary>The next normally played speed card will become an ATTACK card this turn.</summary>
    public bool hotpotBaseActive;

    // ── JP: Kanto Oden ──
    /// <summary>Skip turn + accumulate mode active.</summary>
    public bool kantoOdenActive;
    /// <summary>Extra cards to add to next turn's gear count.</summary>
    public int kantoOdenAccumulatedCards;

    // ── US: Landmark tracking ──
    /// <summary>Player crossed a landmark last turn (set by game loop).</summary>
    public bool crossedLandmarkLastTurn;

    // ── US: Fries temp heat ──
    /// <summary>Temporary heat card available from Fries (usable this turn, destroyed end of turn).</summary>
    public bool tempHeatAvailable;

    /// <summary>Reset per-turn state. Call at start of each turn.</summary>
    public void ResetPerTurn()
    {
        trickPlayedThisTurn = false;
        trickPlayedThisTurnId = null;
        schwarzbrotActive = false;
        schwarzbrotRemaining = 0;
        sauerkrautPlayed = false;
        parmigianoActive = false;
        torpedoTempuraActive = false;
        iceJellyActive = false;
        hotpotBaseActive = false;
        // Kanto Oden persists across the skipped turn — cleared externally
        // tempHeatAvailable persists until end of turn — cleared by game loop
    }

    /// <summary>Reset per-race state. Call at start of race.</summary>
    public void ResetPerRace()
    {
        ResetPerTurn();
        kantoOdenActive = false;
        kantoOdenAccumulatedCards = 0;
        crossedLandmarkLastTurn = false;
        tempHeatAvailable = false;
    }
}

/// <summary>
/// Trick card database — holds all 12 trick card definitions.
/// Create via TrickCardDatabaseFactory.CreateDefault().
/// </summary>
[System.Serializable]
public class TrickCardDatabase
{
    /// <summary>All trick card definitions keyed by ID.</summary>
    public Dictionary<string, TrickCardDef> cards = new Dictionary<string, TrickCardDef>();

    /// <summary>Card IDs grouped by team.</summary>
    public Dictionary<TeamId, List<string>> cardsByTeam = new Dictionary<TeamId, List<string>>()
    {
        { TeamId.UK, new List<string>() },
        { TeamId.DE, new List<string>() },
        { TeamId.IT, new List<string>() },
        { TeamId.US, new List<string>() },
        { TeamId.CN, new List<string>() },
        { TeamId.JP, new List<string>() }
    };

    public void Add(TrickCardDef card)
    {
        cards[card.id] = card;
        cardsByTeam[card.teamId].Add(card.id);
    }

    public TrickCardDef Get(string id)
    {
        cards.TryGetValue(id, out var card);
        return card;
    }

    /// <summary>Get all trick cards for a team (attack + defense).</summary>
    public List<TrickCardDef> GetForTeam(TeamId teamId)
    {
        var result = new List<TrickCardDef>();
        if (cardsByTeam.TryGetValue(teamId, out var ids))
        {
            foreach (var id in ids)
                result.Add(cards[id]);
        }
        return result;
    }

    /// <summary>Get attack card ID for a team.</summary>
    public string GetAttackId(TeamId teamId)
    {
        if (cardsByTeam.TryGetValue(teamId, out var ids))
        {
            foreach (var id in ids)
                if (cards[id].IsAttack) return id;
        }
        return null;
    }

    /// <summary>Get defense card ID for a team.</summary>
    public string GetDefenseId(TeamId teamId)
    {
        if (cardsByTeam.TryGetValue(teamId, out var ids))
        {
            foreach (var id in ids)
                if (cards[id].IsDefense) return id;
        }
        return null;
    }
}

/// <summary>Result of playing a trick card with an immediate effect.</summary>
public struct TrickPlayResult
{
    public bool success;
    public string message;

    // ── Numeric effect outputs ──
    /// <summary>Extra movement granted.</summary>
    public int extraMovement;
    /// <summary>Heat to pay from engine (Scone).</summary>
    public int heatToPay;
    /// <summary>Heat to cool (Tea, Chianti).</summary>
    public int heatToCool;
    /// <summary>Cards to draw (Cola).</summary>
    public int cardsToDraw;
    /// <summary>Slipstream bonus to add this turn (Parmigiano).</summary>
    public int slipstreamBonus;
    /// <summary>Speed card to discard for effect (Chianti).</summary>
    public bool requiresSpeedDiscard;

    public static TrickPlayResult Ok(string msg) => new TrickPlayResult { success = true, message = msg };
    public static TrickPlayResult Fail(string msg) => new TrickPlayResult { success = false, message = msg };
}
