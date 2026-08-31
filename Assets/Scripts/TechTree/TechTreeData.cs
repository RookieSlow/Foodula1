using System.Collections.Generic;

// ═══════════════════════════════════════════════════════════════════════════════
// TechTreeData.cs — Tech Tree data models for Foodula1
// Pure C# data layer, no Unity dependencies. Follows ADR-002 layered architecture.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Six national teams in Foodula1.</summary>
public enum TeamId
{
    UK, DE, IT, US, CN, JP
}

/// <summary>Tech tree tier.</summary>
public enum TechTreeTier
{
    L1, L2, L3
}

/// <summary>Japanese broth selection (JP L2).</summary>
public enum BrothType
{
    None = 0,
    Tonkotsu = 1,  // Straight bonus
    Shoyu = 2,     // Corner bonus
    Miso = 3,      // Slipstream bonus
    Shio = 4       // Cooldown bonus
}

/// <summary>
/// All effect types a tech node can apply.
/// Common numeric effects + per-country unique tech flags.
/// </summary>
public enum TechEffectType
{
    // ── Common Numeric Modifiers ──
    /// <summary>Per-lap heat reduction on corner overspeed (0/1/2).</summary>
    HeatReductionPerLap,
    /// <summary>Speed card sum bonus on straights (0/1/2).</summary>
    SpeedBonusStraight,
    /// <summary>Corner speed limit bonus (0/1/2).</summary>
    CornerLimitBonus,
    /// <summary>Durability / engine heat pool bonus (0/1/2).</summary>
    DurabilityBonus,
    /// <summary>Slipstream range bonus, stacks on base 1 (0=1格, 1=2格).</summary>
    SlipstreamRangeBonus,
    /// <summary>Cells advanced beyond the pit exit after a pit stop.</summary>
    PitExitMoveBonus,

    // ── Stat Modifiers ──
    /// <summary>Engine capacity bonus — increases heat pool max.</summary>
    EngineCapacityBonus,
    /// <summary>Hand size bonus.</summary>
    HandSizeBonus,
    /// <summary>Spin counter max bonus (base 3 → 4 with +1).</summary>
    SpinCounterMaxBonus,

    // ── Category Multipliers ──
    /// <summary>Doubles all "lightweight" category effects (IT L1: PizzaSottile).</summary>
    LightweightDoubler,

    // ── UK Unique ──
    FishAndChips,          // L1: once/race ignore heat + return 1 heat
    FullEnglish,           // L2: hand has heat+speed+trick → +1 temp heat + slipstream +1
    SunNeverSets,          // L3: gain target country's L2+L3

    // ── DE Unique ──
    SchwarzbierFuel,       // L1: pay 1 heat from engine → +2 move
    WurstplatteSuspension, // L2: after corner exit +1 move + skip corner judgment
    GrillSpezial,          // L3: once/race auto-cool all heat paid this turn

    // ── IT Unique ──
    CavallinoRampante,     // L3: RP ×1.5 on win, ×1.5 at home

    // ── US Unique ──
    DriveThru,             // L1: passing landmark → +1 extra move
    SmokedBBQ,             // L2: within landmark range → heat-as-speed-2, no slipstream behind, engine+2
    MotherRoad,            // L3: landmark prosperity/decline/revival system

    // ── CN Unique ──
    YinYangTea,            // L1: Go→pay heat+move / Recover→cool hand heat
    DimSumCombo,           // L2: trick→speed→pay heat sequence → extra YinYang
    SomersaultCloud,       // L3: upgrade ATTACK cards (+2 slipstream, +1 corner limit)

    // ── JP Unique ──
    Nigiri,                // L1: exact speed=limit at corner → +2 extra move
    BrothSelection,        // L2: choose 1 of 4 passives at game start
    Bankuruwase            // L3: last/second-last → 3-turn all-buffs + cool 1/turn
}

/// <summary>An effect instance attached to a tech node.</summary>
[System.Serializable]
public struct TechEffect
{
    public TechEffectType type;
    public float value;

    public TechEffect(TechEffectType type, float value)
    {
        this.type = type;
        this.value = value;
    }
}

/// <summary>
/// Definition of a single tech tree node.
/// Stores ID, metadata, costs, prerequisites, and effects.
/// </summary>
[System.Serializable]
public class TechNodeDef
{
    /// <summary>Unique ID, e.g. "common-l1-heat-coating" or "uk-l1-fish-and-chips".</summary>
    public string id;

    /// <summary>Display name (Chinese).</summary>
    public string name;

    /// <summary>Display name (English, for reference).</summary>
    public string nameEn;

    /// <summary>Tech tier.</summary>
    public TechTreeTier tier;

    /// <summary>null for common techs, set for team-unique techs.</summary>
    public TeamId? teamId;

    /// <summary>Position index within the tier (1-based).</summary>
    public int index;

    /// <summary>Research Point cost to unlock.</summary>
    public int rpCost;

    /// <summary>IDs of prerequisite nodes (empty for no prereqs).</summary>
    public string[] prerequisites;

    /// <summary>ID of the L2 node that upgrades/supersedes this one (L1→L2 chain).</summary>
    public string upgradesTo;

    /// <summary>Effects this node grants when active.</summary>
    public TechEffect[] effects;

    /// <summary>Flavor description.</summary>
    public string description;

    public bool IsCommon => teamId == null;
    public bool IsUnique => teamId != null;

    public TechNodeDef() { }

    public TechNodeDef(
        string id, string name, string nameEn, TechTreeTier tier,
        TeamId? teamId, int index, int rpCost,
        string[] prerequisites, string upgradesTo,
        TechEffect[] effects, string description)
    {
        this.id = id;
        this.name = name;
        this.nameEn = nameEn;
        this.tier = tier;
        this.teamId = teamId;
        this.index = index;
        this.rpCost = rpCost;
        this.prerequisites = prerequisites ?? new string[0];
        this.upgradesTo = upgradesTo ?? "";
        this.effects = effects ?? new TechEffect[0];
        this.description = description ?? "";
    }
}

/// <summary>
/// Computed modifiers from all active tech nodes.
/// Query this struct to get the final effective values for game calculations.
/// </summary>
public struct TechModifiers
{
    // ══ Common numeric modifiers ══
    public int heatReductionPerLap;      // 0, 1, 2 (per-lap corner overspeed heat reduction)
    public int speedBonusStraight;       // 0, 1, 2 (raw; doubled by PizzaSottile if active)
    public int cornerLimitBonus;         // 0, 1, 2
    public int durabilityBonus;          // 0, 1, 2 (raw engine heat pool increase)
    public int slipstreamRangeBonus;     // 0 or 1 (0=1格, 1=2格)

    // ══ Stat modifiers ══
    public int engineCapacityBonus;      // added to base engine capacity
    public int handSizeBonus;            // added to base hand size
    public int spinCounterMaxBonus;      // added to base spin counter max (3)
    public int pitExitMoveBonus;         // cells gained beyond pit exit after pitting

    // ══ Category multipliers ══
    public bool hasPizzaSottile;         // doubles lightweight effects

    // ══ UK flags ══
    public bool hasFishAndChips;
    public bool hasFullEnglish;
    public bool hasSunNeverSets;

    // ══ DE flags ══
    public bool hasSchwarzbierFuel;
    public bool hasWurstplatteSuspension;
    public bool hasGrillSpezial;

    // ══ IT flags ══
    public bool hasCavallinoRampante;

    // ══ US flags ══
    public bool hasDriveThru;
    public bool hasSmokedBBQ;
    public bool hasMotherRoad;

    // ══ CN flags ══
    public bool hasYinYangTea;
    public bool hasDimSumCombo;
    public bool hasSomersaultCloud;

    // ══ JP flags ══
    public bool hasNigiri;
    public bool hasBrothSelection;
    public bool hasBankuruwase;

    // ══ Computed effective values ══

    /// <summary>Effective speed bonus on straights, after PizzaSottile doubler.</summary>
    public int EffectiveSpeedBonusStraight =>
        hasPizzaSottile ? speedBonusStraight * 2 : speedBonusStraight;

    /// <summary>Effective slipstream distance in cells (1 or 2).</summary>
    public int EffectiveSlipstreamRange => 1 + slipstreamRangeBonus;

    /// <summary>Effective spin counter maximum (base 3 + bonus, capped at 4 for IT L2).</summary>
    public int EffectiveSpinCounterMax => 3 + spinCounterMaxBonus;

    /// <summary>Effective engine capacity bonus, includes SmokedBBQ +2.</summary>
    public int EffectiveEngineCapacityBonus =>
        engineCapacityBonus + (hasSmokedBBQ ? 2 : 0);

    /// <summary>Creates a default modifier with all zeros/false.</summary>
    public static TechModifiers Default => new TechModifiers();
}

/// <summary>Result of resolving YinYangTea (CN L1) at end of turn.</summary>
public struct YinYangResult
{
    /// <summary>Whether the effect triggered.</summary>
    public bool triggered;
    /// <summary>"Yin" — Go mode: pay 1 heat from engine to move +1.</summary>
    public bool isYin;
    /// <summary>"Yang" — Recover mode: cool 1 heat from hand to engine.</summary>
    public bool isYang;
    /// <summary>Heat to cool (only for Yang).</summary>
    public int heatToCool;
    /// <summary>Extra movement granted (only for Yin).</summary>
    public int extraMovement;

    public static YinYangResult NoTrigger => new YinYangResult();
    public static YinYangResult Yin => new YinYangResult { triggered = true, isYin = true, extraMovement = 1 };
    public static YinYangResult Yang => new YinYangResult { triggered = true, isYang = true, heatToCool = 1 };
}

/// <summary>Result of resolving a Mother Road landmark pass (US L3).</summary>
public struct MotherRoadResult
{
    /// <summary>Phase after this pass: Prosperity, Decline, or Revival.</summary>
    public MotherRoadPhase phase;
    /// <summary>Free heat to cool (prosperity phase).</summary>
    public int freeCooldown;
    /// <summary>Whether landmark needs repair payment (decline phase).</summary>
    public bool needsRepair;
    /// <summary>Whether revival ultimate is available.</summary>
    public bool ultimateAvailable;
    /// <summary>Heat cards converted to movement (revival ultimate).</summary>
    public int heatConvertedToMove;

    public enum MotherRoadPhase
    {
        None,
        Prosperity,  // Passes 1-2: free cool 2
        Decline,     // Passes 3+: optional pay 1 heat to repair
        Revival      // After enough repairs: once/race convert hand heat → move
    }
}

/// <summary>Passive modifiers from JP L2 Broth Selection.</summary>
public struct BrothModifiers
{
    public int straightBonus;       // Tonkotsu: +1 on straight
    public int cornerLimitBonus;    // Shoyu: +1 corner limit
    public int slipstreamBonus;     // Miso: +1 slipstream
    public int cooldownPerTurn;     // Shio: 1 cool per turn

    public static BrothModifiers FromBrothType(BrothType type)
    {
        switch (type)
        {
            case BrothType.Tonkotsu:
                return new BrothModifiers { straightBonus = 1 };
            case BrothType.Shoyu:
                return new BrothModifiers { cornerLimitBonus = 1 };
            case BrothType.Miso:
                return new BrothModifiers { slipstreamBonus = 1 };
            case BrothType.Shio:
                return new BrothModifiers { cooldownPerTurn = 1 };
            default:
                return new BrothModifiers();
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Runtime State
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Per-team runtime tech tree state — which nodes are unlocked, RP balance,
/// and stateful tracking for once-per-race / per-turn effects.
///
/// NOTE: Runtime-only class. HashSet fields are NOT Unity-serializable —
/// do not expose as [SerializeField] on MonoBehaviour or ScriptableObject.
/// Use TechTreeRules serialization helpers if persistence is needed.
/// </summary>
[System.Serializable]
public class TechTreeState
{
    /// <summary>Which team this state belongs to.</summary>
    public TeamId teamId;

    /// <summary>Current Research Point balance.</summary>
    public int rpBalance;

    /// <summary>Set of permanently unlocked node IDs.</summary>
    public HashSet<string> unlockedNodeIds = new HashSet<string>();

    /// <summary>Set of node IDs selected for the current race (subset of unlocked).</summary>
    public HashSet<string> activeNodeIds = new HashSet<string>();

    // ── Per-Race Once Flags ──
    /// <summary>Whether per-lap heat reduction has been used this lap.</summary>
    public bool heatReductionUsedThisLap;

    /// <summary>UK L1: FishAndChips used this race.</summary>
    public bool fishAndChipsUsed;

    /// <summary>DE L3: GrillSpezial used this race.</summary>
    public bool grillSpezialUsed;

    /// <summary>DE L3: Total heat paid this turn (tracked for GrillSpezial auto-cool).</summary>
    public int grillSpezialHeatPaidThisTurn;

    /// <summary>DE L1: Last lap on which Schwarzbier Fuel was consumed; -1 means unused.</summary>
    public int schwarzbierFuelLastLap = -1;

    // ── JP L2 ──
    /// <summary>JP L2: Which broth was chosen (0=none).</summary>
    public BrothType brothSelection;

    // ── JP L3 ──
    /// <summary>JP L3: Bankuruwase 3-turn rotor mode active.</summary>
    public bool bankuruwaseActive;
    /// <summary>JP L3: Turns remaining in rotor mode (3→0).</summary>
    public int bankuruwaseTurnsLeft;

    // ── US L3: Mother Road Landmark Tracking ──
    /// <summary>Pass counts for landmark 1 (start line).</summary>
    public int landmark1PassCount;
    /// <summary>Pass counts for landmark 2 (midpoint).</summary>
    public int landmark2PassCount;
    /// <summary>Whether landmark 1 ultimate has been used this race.</summary>
    public bool landmark1UltUsed;
    /// <summary>Whether landmark 2 ultimate has been used this race.</summary>
    public bool landmark2UltUsed;
    /// <summary>Cumulative repair count across both landmarks for revival threshold.</summary>
    public int totalRepairs;

    // ── CN L2: DimSumCombo Sequence Tracking ──
    /// <summary>CN L2: trick card played this turn.</summary>
    public bool dimSumPlayedTrick;
    /// <summary>CN L2: speed card played this turn (after trick).</summary>
    public bool dimSumPlayedSpeed;
    /// <summary>CN L2: heat paid this turn (after speed).</summary>
    public bool dimSumPaidHeat;

    // ── UK L3 ──
    /// <summary>UK L3: which country's techs are being copied (null if not set).</summary>
    public TeamId? sunNeverSetsTarget;

    public TechTreeState() { }

    public TechTreeState(TeamId teamId, int initialRp = 0)
    {
        this.teamId = teamId;
        this.rpBalance = initialRp;
        this.unlockedNodeIds = new HashSet<string>();
        this.activeNodeIds = new HashSet<string>();
    }

    /// <summary>Check if a specific tech node is unlocked.</summary>
    public bool IsUnlocked(string nodeId) => unlockedNodeIds.Contains(nodeId);

    /// <summary>Check if a specific tech node is active for this race.</summary>
    public bool IsActive(string nodeId) => activeNodeIds.Contains(nodeId);
}

// ═══════════════════════════════════════════════════════════════════════════════
// Database
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Tech tree node database — holds all node definitions.
/// Passed to TechTreeRules for lookup. Create via TechTreeDatabaseFactory.CreateDefault()
/// or load from JSON.
///
/// NOTE: Runtime-only class. Dictionary fields are NOT Unity-serializable.
/// </summary>
[System.Serializable]
public class TechTreeDatabase
{
    /// <summary>All node definitions keyed by ID.</summary>
    public Dictionary<string, TechNodeDef> nodes = new Dictionary<string, TechNodeDef>();

    /// <summary>All node IDs grouped by tier.</summary>
    public Dictionary<TechTreeTier, List<string>> nodesByTier = new Dictionary<TechTreeTier, List<string>>()
    {
        { TechTreeTier.L1, new List<string>() },
        { TechTreeTier.L2, new List<string>() },
        { TechTreeTier.L3, new List<string>() }
    };

    /// <summary>All node IDs grouped by team (only unique techs).</summary>
    public Dictionary<TeamId, List<string>> nodesByTeam = new Dictionary<TeamId, List<string>>()
    {
        { TeamId.UK, new List<string>() },
        { TeamId.DE, new List<string>() },
        { TeamId.IT, new List<string>() },
        { TeamId.US, new List<string>() },
        { TeamId.CN, new List<string>() },
        { TeamId.JP, new List<string>() }
    };

    /// <summary>Common node IDs (all tiers).</summary>
    public List<string> commonNodeIds = new List<string>();

    /// <summary>CN EV variant node IDs (same effects, different names).</summary>
    public List<string> cnEvNodeIds = new List<string>();

    /// <summary>
    /// Add a node definition to the database and update all indexes.
    /// </summary>
    public void Add(TechNodeDef node)
    {
        nodes[node.id] = node;
        nodesByTier[node.tier].Add(node.id);

        if (node.IsCommon)
        {
            commonNodeIds.Add(node.id);
        }
        else if (node.teamId.HasValue)
        {
            nodesByTeam[node.teamId.Value].Add(node.id);
        }
    }

    /// <summary>Get a node by ID. Returns null if not found.</summary>
    public TechNodeDef Get(string id)
    {
        nodes.TryGetValue(id, out var node);
        return node;
    }

    /// <summary>Get all standard common nodes in a given tier.</summary>
    public List<TechNodeDef> GetCommonInTier(TechTreeTier tier)
    {
        return GetCommonInTier(tier, false);
    }

    /// <summary>
    /// Get the common tier pool for a team presentation. China uses the EV
    /// naming variant (same effects/prices); all other teams use the standard
    /// pool. This keeps catalogue selection out of UI code.
    /// </summary>
    public List<TechNodeDef> GetCommonInTier(TechTreeTier tier, bool useCnEv)
    {
        var result = new List<TechNodeDef>();
        IEnumerable<string> ids = useCnEv ? cnEvNodeIds : commonNodeIds;
        foreach (var id in ids)
        {
            TechNodeDef node;
            if (nodes.TryGetValue(id, out node) && node.tier == tier)
                result.Add(node);
        }
        return result;
    }

    /// <summary>Get all unique nodes for a team in a given tier.</summary>
    public List<TechNodeDef> GetUniqueInTier(TeamId teamId, TechTreeTier tier)
    {
        var result = new List<TechNodeDef>();
        if (nodesByTeam.TryGetValue(teamId, out var ids))
        {
            foreach (var id in ids)
            {
                var node = nodes[id];
                if (node.tier == tier)
                    result.Add(node);
            }
        }
        return result;
    }

    /// <summary>Get all nodes for a team (common + unique) in a given tier.</summary>
    public List<TechNodeDef> GetAllForTeamInTier(TeamId teamId, TechTreeTier tier, bool useCnEv = false)
    {
        var result = new List<TechNodeDef>();
        result.AddRange(GetCommonInTier(tier, useCnEv && teamId == TeamId.CN));
        result.AddRange(GetUniqueInTier(teamId, tier));
        return result;
    }

    /// <summary>Total number of nodes in the database.</summary>
    public int Count => nodes.Count;
}
