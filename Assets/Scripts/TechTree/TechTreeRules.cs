using System;
using System.Collections.Generic;

/// <summary>
/// Tech tree rules engine — static methods operating on mutable TechTreeState.
/// All methods are dependency-free (no Unity APIs) — testable in EditMode without Unity.
/// Follows ADR-002 layered architecture: MVPGameManager calls these at game loop hooks.
/// </summary>
public static class TechTreeRules
{
    // ═══════════════════════════════════════════════════════════════════
    // Constants
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Required common nodes unlocked to access unique techs at L1.</summary>
    public const int L1_TIER_GATE = 3;

    /// <summary>Required common nodes unlocked to access unique techs at L2.</summary>
    public const int L2_TIER_GATE = 3;

    /// <summary>Demo mode starting RP budget per team.</summary>
    public const int DEMO_BUDGET = 25000;

    /// <summary>Base RP rewards per finishing position (1-indexed).</summary>
    public static readonly int[] POSITION_RP = { 5000, 3500, 2500, 1500, 1000, 500 };

    /// <summary>IT L3 RP multiplier.</summary>
    public const float CAVALLINO_MULTIPLIER = 1.5f;

    /// <summary>US landmark BBQ zone radius (cells).</summary>
    public const int BBQ_ZONE_RADIUS = 5;

    /// <summary>JP L3 Bankuruwase rotor duration (turns).</summary>
    public const int BANKURUWASE_DURATION = 3;

    /// <summary>UK L3 Sun Never Sets — techs granted from target country.</summary>
    public static readonly TechTreeTier[] SUN_NEVER_SETS_TIERS = { TechTreeTier.L2, TechTreeTier.L3 };

    // ═══════════════════════════════════════════════════════════════════
    // Unlock Logic
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check whether a tech node can be unlocked given current state.
    /// Checks: node exists, not already unlocked, enough RP, prerequisites met, tier gate satisfied.
    /// </summary>
    public static bool CanUnlock(TechTreeState state, string nodeId, TechTreeDatabase db)
    {
        if (state == null || db == null) return false;

        var node = db.Get(nodeId);
        if (node == null) return false;
        if (state.IsUnlocked(nodeId)) return false;
        if (state.rpBalance < node.rpCost) return false;

        // Check prerequisites
        foreach (var prereq in node.prerequisites)
        {
            if (!state.IsUnlocked(prereq))
                return false;
        }

        // Tier gate: unique techs require N common techs in same tier
        if (node.IsUnique && (node.tier == TechTreeTier.L1 || node.tier == TechTreeTier.L2))
        {
            if (!HasMetTierGate(state, db, node.tier))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Check if the tier gate is satisfied for unlocking unique techs at the given tier.
    /// L1 gate: ≥3 L1 common unlocked. L2 gate: ≥3 L2 common unlocked. L3: no gate.
    /// </summary>
    public static bool HasMetTierGate(TechTreeState state, TechTreeDatabase db, TechTreeTier tier)
    {
        int required = tier == TechTreeTier.L1 ? L1_TIER_GATE : L2_TIER_GATE;
        return CountUnlockedCommonInTier(state, db, tier) >= required;
    }

    /// <summary>Count how many common nodes in a tier are unlocked.</summary>
    public static int CountUnlockedCommonInTier(TechTreeState state, TechTreeDatabase db, TechTreeTier tier)
    {
        int count = 0;
        var ids = new List<string>(db.commonNodeIds);
        // China uses the EV catalogue, but legacy saves/tests may contain
        // standard IDs. Counting both keeps old states valid while the UI
        // presents only the EV names for new China profiles.
        if (state != null && state.teamId == TeamId.CN)
            ids.AddRange(db.cnEvNodeIds);

        foreach (var nodeId in ids)
        {
            var node = db.Get(nodeId);
            if (node != null && node.tier == tier && state.IsUnlocked(nodeId))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Get all nodes the player can currently unlock.
    /// </summary>
    public static List<TechNodeDef> GetAvailableNodes(TechTreeState state, TechTreeDatabase db)
    {
        var available = new List<TechNodeDef>();
        if (state == null || db == null) return available;

        foreach (var kv in db.nodes)
        {
            if (CanUnlock(state, kv.Key, db))
                available.Add(kv.Value);
        }
        return available;
    }

    /// <summary>
    /// Attempt to unlock a tech node. Deducts RP and adds to unlocked set.
    /// Handles L1→L2 upgrade chain: when unlocking L2 #N, the corresponding L1 node
    /// is kept unlocked (counts for tier gate) but its effects are superseded by L2.
    /// Returns true on success.
    /// </summary>
    public static bool UnlockNode(TechTreeState state, string nodeId, TechTreeDatabase db)
    {
        if (!CanUnlock(state, nodeId, db)) return false;

        var node = db.Get(nodeId);
        state.rpBalance -= node.rpCost;
        state.unlockedNodeIds.Add(nodeId);

        // Handle L1→L2 upgrade: check if any L1 node's upgradesTo points to this L2 node.
        // The L1 stays unlocked (for tier gate) but effects are taken from L2 (higher value).
        // This is handled automatically by ComputeModifiers using max-value logic per effect type.

        return true;
    }

    /// <summary>Get the RP cost to unlock a node.</summary>
    public static int GetUnlockCost(string nodeId, TechTreeDatabase db)
    {
        var node = db?.Get(nodeId);
        return node?.rpCost ?? int.MaxValue;
    }

    /// <summary>
    /// Check if a node's L1 predecessor was superseded by the unlocked L2 upgrade.
    /// Used by ComputeModifiers to pick the higher-tier value.
    /// </summary>
    public static bool IsSuperseded(TechTreeState state, string l1NodeId, TechTreeDatabase db)
    {
        var l1Node = db?.Get(l1NodeId);
        if (l1Node == null || string.IsNullOrEmpty(l1Node.upgradesTo)) return false;
        return state.IsUnlocked(l1Node.upgradesTo);
    }

    // ═══════════════════════════════════════════════════════════════════
    // RP Economy
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate base RP reward for a finishing position (1-indexed).
    /// Returns 0 for positions beyond the reward table.
    /// </summary>
    public static int CalculateRaceRP(int position)
    {
        int idx = Math.Max(0, position - 1);
        return idx < POSITION_RP.Length ? POSITION_RP[idx] : 0;
    }

    /// <summary>
    /// Apply IT L3 Cavallino Rampante RP multiplier.
    /// Base: ×1.5 on win (position 1). Home race (Monza) + IT driver: ×1.5 again (×2.25 total).
    /// All rounding is ceiling.
    /// </summary>
    public static int ApplyCavallinoRampante(int baseRp, int position, bool isHomeRace)
    {
        if (position != 1) return baseRp; // Only applies to wins

        float multiplier = CAVALLINO_MULTIPLIER;
        if (isHomeRace)
            multiplier *= CAVALLINO_MULTIPLIER;

        return (int)Math.Ceiling(baseRp * multiplier);
    }

    /// <summary>Demo mode starting budget.</summary>
    public static int GetDemoBudget() => DEMO_BUDGET;

    /// <summary>
    /// Initialize a TechTreeState with the demo budget.
    /// </summary>
    public static TechTreeState CreateDemoState(TeamId teamId)
    {
        return new TechTreeState(teamId, DEMO_BUDGET);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Modifier Computation
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Compute all active modifiers from the player's selected tech nodes.
    /// Handles L1→L2 upgrade chains (L2 value supersedes L1 value for the same effect).
    /// </summary>
    public static TechModifiers ComputeModifiers(TechTreeState state, TechTreeDatabase db)
    {
        if (state == null || db == null) return TechModifiers.Default;

        var m = TechModifiers.Default;

        // Collect all effects from active nodes.
        // For numeric effects of the same type across an upgrade chain (L1→L2),
        // we take the max value (L2 naturally has higher value).
        var effectMaxValues = new Dictionary<TechEffectType, float>();

        foreach (var nodeId in state.activeNodeIds)
        {
            var node = db.Get(nodeId);
            if (node == null) continue;

            foreach (var effect in node.effects)
            {
                switch (effect.type)
                {
                    case TechEffectType.HeatReductionPerLap:
                        effectMaxValues[effect.type] = Math.Max(
                            effectMaxValues.GetValueOrDefault(effect.type, 0), effect.value);
                        break;

                    case TechEffectType.SpeedBonusStraight:
                        effectMaxValues[effect.type] = Math.Max(
                            effectMaxValues.GetValueOrDefault(effect.type, 0), effect.value);
                        break;

                    case TechEffectType.CornerLimitBonus:
                        effectMaxValues[effect.type] = Math.Max(
                            effectMaxValues.GetValueOrDefault(effect.type, 0), effect.value);
                        break;

                    case TechEffectType.DurabilityBonus:
                        effectMaxValues[effect.type] = Math.Max(
                            effectMaxValues.GetValueOrDefault(effect.type, 0), effect.value);
                        break;

                    case TechEffectType.SlipstreamRangeBonus:
                        effectMaxValues[effect.type] = Math.Max(
                            effectMaxValues.GetValueOrDefault(effect.type, 0), effect.value);
                        break;

                    case TechEffectType.EngineCapacityBonus:
                        m.engineCapacityBonus += (int)effect.value;
                        break;

                    case TechEffectType.HandSizeBonus:
                        m.handSizeBonus += (int)effect.value;
                        break;

                    case TechEffectType.SpinCounterMaxBonus:
                        m.spinCounterMaxBonus += (int)effect.value;
                        break;

                    case TechEffectType.LightweightDoubler:
                        m.hasPizzaSottile = true;
                        break;

                    // ── Unique Tech Flags ──
                    case TechEffectType.FishAndChips:       m.hasFishAndChips = true; break;
                    case TechEffectType.FullEnglish:        m.hasFullEnglish = true; break;
                    case TechEffectType.SunNeverSets:       m.hasSunNeverSets = true; break;
                    case TechEffectType.SchwarzbierFuel:    m.hasSchwarzbierFuel = true; break;
                    case TechEffectType.WurstplatteSuspension: m.hasWurstplatteSuspension = true; break;
                    case TechEffectType.GrillSpezial:       m.hasGrillSpezial = true; break;
                    case TechEffectType.CavallinoRampante:  m.hasCavallinoRampante = true; break;
                    case TechEffectType.DriveThru:          m.hasDriveThru = true; break;
                    case TechEffectType.SmokedBBQ:          m.hasSmokedBBQ = true; break;
                    case TechEffectType.MotherRoad:         m.hasMotherRoad = true; break;
                    case TechEffectType.YinYangTea:         m.hasYinYangTea = true; break;
                    case TechEffectType.DimSumCombo:        m.hasDimSumCombo = true; break;
                    case TechEffectType.SomersaultCloud:    m.hasSomersaultCloud = true; break;
                    case TechEffectType.Nigiri:             m.hasNigiri = true; break;
                    case TechEffectType.BrothSelection:     m.hasBrothSelection = true; break;
                    case TechEffectType.Bankuruwase:        m.hasBankuruwase = true; break;
                }
            }
        }

        // Apply max values
        m.heatReductionPerLap = (int)effectMaxValues.GetValueOrDefault(TechEffectType.HeatReductionPerLap, 0);
        m.speedBonusStraight = (int)effectMaxValues.GetValueOrDefault(TechEffectType.SpeedBonusStraight, 0);
        m.cornerLimitBonus = (int)effectMaxValues.GetValueOrDefault(TechEffectType.CornerLimitBonus, 0);
        m.durabilityBonus = (int)effectMaxValues.GetValueOrDefault(TechEffectType.DurabilityBonus, 0);
        m.slipstreamRangeBonus = (int)effectMaxValues.GetValueOrDefault(TechEffectType.SlipstreamRangeBonus, 0);

        // JP L2 Broth: add broth passive values to modifiers
        if (m.hasBrothSelection && state.brothSelection != BrothType.None)
        {
            var broth = BrothModifiers.FromBrothType(state.brothSelection);
            m.speedBonusStraight += broth.straightBonus;
            m.cornerLimitBonus += broth.cornerLimitBonus;
            m.slipstreamRangeBonus += broth.slipstreamBonus;
            // broth.cooldownPerTurn is handled separately in GetBrothCooldownPerTurn()
        }

        return m;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Corner Limit Stacking (GDD rule)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Apply corner limit bonuses from multiple sources using the GDD stacking formula:
    /// effective = max(sources) + floor(sum(other sources) / 2)
    /// </summary>
    public static int ComputeEffectiveCornerLimitBonus(
        int commonBonus, int brothBonus, int nigiriActive, int somersaultCloudActive,
        int bankuruwaseActive)
    {
        var sources = new List<int>();
        if (commonBonus > 0) sources.Add(commonBonus);
        if (brothBonus > 0) sources.Add(brothBonus);
        if (nigiriActive > 0) sources.Add(nigiriActive);
        if (somersaultCloudActive > 0) sources.Add(somersaultCloudActive);
        if (bankuruwaseActive > 0) sources.Add(bankuruwaseActive);

        if (sources.Count == 0) return 0;
        if (sources.Count == 1) return sources[0];

        sources.Sort((a, b) => b.CompareTo(a)); // descending
        int maxSource = sources[0];
        int otherSum = 0;
        for (int i = 1; i < sources.Count; i++)
            otherSum += sources[i];

        return maxSource + otherSum / 2; // floor division
    }

    // ═══════════════════════════════════════════════════════════════════
    // Tech Query Utilities
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Check if a specific tech is active.</summary>
    public static bool HasTech(TechTreeState state, TechTreeDatabase db, string nodeId)
    {
        return state != null && state.IsActive(nodeId);
    }

    /// <summary>Check if a tech effect type is present among active nodes.</summary>
    public static bool HasEffect(TechTreeState state, TechTreeDatabase db, TechEffectType effectType)
    {
        if (state == null || db == null) return false;
        foreach (var nodeId in state.activeNodeIds)
        {
            var node = db.Get(nodeId);
            if (node == null) continue;
            foreach (var e in node.effects)
                if (e.type == effectType) return true;
        }
        return false;
    }

    // ═══════════════════════════════════════════════════════════════════
    // UK Unique Tech Logic
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>UK L1: Can FishAndChips be used this race? (once per race, not yet used)</summary>
    public static bool CanUseFishAndChips(TechTreeState state, TechTreeDatabase db)
    {
        return HasEffect(state, db, TechEffectType.FishAndChips) && !state.fishAndChipsUsed;
    }

    /// <summary>Mark FishAndChips as used for this race.</summary>
    public static void UseFishAndChips(TechTreeState state)
    {
        state.fishAndChipsUsed = true;
    }

    /// <summary>
    /// UK L2: Check if FullEnglish should trigger.
    /// Hand must contain at least one heat card, one speed card, and one trick card.
    /// </summary>
    public static bool ShouldTriggerFullEnglish(bool hasHeatInHand, bool hasSpeedInHand, bool hasTrickInHand)
    {
        return hasHeatInHand && hasSpeedInHand && hasTrickInHand;
    }

    /// <summary>
    /// UK L3: Get the tech nodes granted by Sun Never Sets.
    /// Returns the target country's L2 and L3 unique techs.
    ///
    /// IMPORTANT: The returned nodes are NOT automatically applied by ComputeModifiers().
    /// The caller (MVPGameManager) must either inject these as virtual nodes or
    /// manually merge their effects into the race loop (e.g., for CavallinoRampante,
    /// call ApplyCavallinoRampante() at race end; for Wurstplatte, check the flag
    /// at corner exit).
    /// </summary>
    public static List<TechNodeDef> GetSunNeverSetsTargetTechs(TechTreeState state, TechTreeDatabase db)
    {
        var result = new List<TechNodeDef>();
        if (state?.sunNeverSetsTarget == null || db == null) return result;

        var target = state.sunNeverSetsTarget.Value;
        foreach (var tier in SUN_NEVER_SETS_TIERS)
        {
            result.AddRange(db.GetUniqueInTier(target, tier));
        }
        return result;
    }

    /// <summary>UK L3: Check if home race (UK track) triggers extra FullEnglish.</summary>
    public static bool IsSunNeverSetsHomeBonus(string trackCountry)
    {
        return string.Equals(trackCountry, "UK", StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════
    // DE Unique Tech Logic
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// DE L1: Schwarzbier Fuel — pay 1 heat from engine for +2 move.
    /// Returns true if the tech is active (caller checks engine heat availability).
    /// </summary>
    public static bool CanUseSchwarzbierFuel(TechTreeState state, TechTreeDatabase db, int engineHeatRemaining)
    {
        return HasEffect(state, db, TechEffectType.SchwarzbierFuel) && engineHeatRemaining > 0;
    }

    /// <summary>DE L2: Wurstplatte Suspension triggers after exiting a corner.</summary>
    public static bool ShouldTriggerWurstplatte(TechTreeState state, TechTreeDatabase db, bool crossedCorner)
    {
        return HasEffect(state, db, TechEffectType.WurstplatteSuspension) && crossedCorner;
    }

    /// <summary>DE L3: Can GrillSpezial be used this race? (once per race, not yet used)</summary>
    public static bool CanUseGrillSpezial(TechTreeState state, TechTreeDatabase db)
    {
        return HasEffect(state, db, TechEffectType.GrillSpezial) && !state.grillSpezialUsed;
    }

    /// <summary>Mark GrillSpezial as active for this turn.</summary>
    public static void ActivateGrillSpezial(TechTreeState state)
    {
        state.grillSpezialUsed = true;
        state.grillSpezialHeatPaidThisTurn = 0;
    }

    /// <summary>DE L3: Track heat paid this turn for grill spezial auto-cool.</summary>
    public static void TrackGrillSpezialHeat(TechTreeState state, int heatPaid)
    {
        state.grillSpezialHeatPaidThisTurn += heatPaid;
    }

    /// <summary>DE L3: Get heat to auto-cool at end of turn.</summary>
    public static int GetGrillSpezialCooldown(TechTreeState state)
    {
        return state.grillSpezialUsed ? state.grillSpezialHeatPaidThisTurn : 0;
    }

    // ═══════════════════════════════════════════════════════════════════
    // IT Unique Tech Logic
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>IT L3: Check if Cavallino Rampante applies (position 1 + tech active).</summary>
    public static bool ShouldApplyCavallino(TechTreeState state, TechTreeDatabase db, int position)
    {
        return position == 1 && HasEffect(state, db, TechEffectType.CavallinoRampante);
    }

    /// <summary>IT L3: Check if home race bonus applies (Monza track).</summary>
    public static bool IsCavallinoHomeRace(string trackCountry)
    {
        return string.Equals(trackCountry, "IT", StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════
    // US Unique Tech Logic
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// US landmark positions. In the GDD: landmark at start cell + midpoint.
    /// Returns the two landmark positions given total track cell count.
    /// </summary>
    public static (int lm1, int lm2) GetLandmarkPositions(int totalTrackCells)
    {
        return (0, totalTrackCells / 2);
    }

    /// <summary>US L1: Check if DriveThru triggers when passing a landmark.</summary>
    public static bool ShouldTriggerDriveThru(
        TechTreeState state, TechTreeDatabase db,
        int oldPosition, int newPosition, int landmarkPosition, int totalCells)
    {
        if (!HasEffect(state, db, TechEffectType.DriveThru)) return false;
        return CrossedPositionForward(oldPosition, newPosition, landmarkPosition, totalCells);
    }

    /// <summary>
    /// US L2: Check if position is within BBQ zone of either landmark.
    /// </summary>
    public static bool IsInBBQZone(int position, int landmark1, int landmark2, int totalCells)
    {
        return DistanceOnTrack(position, landmark1, totalCells) <= BBQ_ZONE_RADIUS ||
               DistanceOnTrack(position, landmark2, totalCells) <= BBQ_ZONE_RADIUS;
    }

    /// <summary>US L2: Check if SmokedBBQ is active.</summary>
    public static bool HasSmokedBBQ(TechTreeState state, TechTreeDatabase db)
    {
        return HasEffect(state, db, TechEffectType.SmokedBBQ);
    }

    /// <summary>
    /// US L3: Resolve a landmark passing for Mother Road.
    /// Updates landmark pass count and determines phase + effects.
    /// </summary>
    public static MotherRoadResult ResolveMotherRoadPass(
        TechTreeState state, int landmarkIndex, int totalLaps)
    {
        var result = new MotherRoadResult();
        if (state == null) return result;

        int passCount = landmarkIndex == 0
            ? ++state.landmark1PassCount
            : ++state.landmark2PassCount;
        bool ultUsed = landmarkIndex == 0 ? state.landmark1UltUsed : state.landmark2UltUsed;

        if (passCount <= 2)
        {
            // Prosperity: free cool 2
            result.phase = MotherRoadResult.MotherRoadPhase.Prosperity;
            result.freeCooldown = 2;
        }
        else if (!ultUsed)
        {
            // Decline: need repairs. Check if enough repairs for revival.
            int repairsNeeded = GetMotherRoadRepairsNeeded(totalLaps);
            if (state.totalRepairs >= repairsNeeded)
            {
                // Revival! Ultimate available
                result.phase = MotherRoadResult.MotherRoadPhase.Revival;
                result.ultimateAvailable = true;
            }
            else
            {
                // Still in decline, needs repair
                result.phase = MotherRoadResult.MotherRoadPhase.Decline;
                result.needsRepair = true;
            }
        }
        else
        {
            // Ultimate already used, back to decline loop
            result.phase = MotherRoadResult.MotherRoadPhase.Decline;
            result.needsRepair = true;
        }

        return result;
    }

    /// <summary>US L3: Pay 1 heat to repair a landmark.</summary>
    public static void RepairLandmark(TechTreeState state)
    {
        if (state != null)
            state.totalRepairs++;
    }

    /// <summary>
    /// US L3: Use revival ultimate — convert hand heat to movement.
    /// Returns how many heat cards to convert (caller handles the actual conversion).
    /// </summary>
    public static int UseMotherRoadUltimate(TechTreeState state, int landmarkIndex, int heatCardsInHand)
    {
        if (state == null) return 0;

        if (landmarkIndex == 0)
            state.landmark1UltUsed = true;
        else
            state.landmark2UltUsed = true;

        return heatCardsInHand; // Each heat → +1 move
    }

    /// <summary>US L3: Repairs needed for revival based on total laps.</summary>
    public static int GetMotherRoadRepairsNeeded(int totalLaps)
    {
        if (totalLaps <= 3) return 1;
        if (totalLaps <= 5) return 2;
        return 3;
    }

    // ═══════════════════════════════════════════════════════════════════
    // CN Unique Tech Logic
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// CN L1: Resolve YinYangTea at end of turn.
    /// Yin (cold, no heat in hand): can pay 1 heat from engine → +1 move.
    /// Yang (hot, has heat in hand): auto cool 1 heat.
    /// </summary>
    public static YinYangResult ResolveYinYang(TechTreeState state, TechTreeDatabase db, bool hasHeatInHand)
    {
        if (!HasEffect(state, db, TechEffectType.YinYangTea))
            return YinYangResult.NoTrigger;

        return hasHeatInHand ? YinYangResult.Yang : YinYangResult.Yin;
    }

    /// <summary>
    /// CN L2: Record a card play for DimSumCombo sequence tracking.
    /// Sequence: Trick → Speed → Pay Heat
    /// Call this each time a card is played or heat is paid.
    /// </summary>
    public static void TrackDimSumCombo(TechTreeState state, bool playedTrick, bool playedSpeed, bool paidHeat)
    {
        if (state == null) return;

        if (playedTrick)
        {
            state.dimSumPlayedTrick = true;
            state.dimSumPlayedSpeed = false;
            state.dimSumPaidHeat = false;
        }
        else if (playedSpeed && state.dimSumPlayedTrick)
        {
            state.dimSumPlayedSpeed = true;
        }
        else if (paidHeat && state.dimSumPlayedTrick && state.dimSumPlayedSpeed)
        {
            state.dimSumPaidHeat = true;
        }
    }

    /// <summary>CN L2: Check if DimSumCombo sequence was completed this turn.</summary>
    public static bool CheckDimSumCombo(TechTreeState state, TechTreeDatabase db)
    {
        if (!HasEffect(state, db, TechEffectType.DimSumCombo)) return false;
        return state.dimSumPlayedTrick && state.dimSumPlayedSpeed && state.dimSumPaidHeat;
    }

    /// <summary>
    /// CN L3: Check if SomersaultCloud is active (ATTACK cards get +2 slipstream, +1 corner).
    /// </summary>
    public static bool HasSomersaultCloud(TechTreeState state, TechTreeDatabase db)
    {
        return HasEffect(state, db, TechEffectType.SomersaultCloud);
    }

    /// <summary>CN L3: Get the slipstream bonus for SomersaultCloud-upgraded ATTACK cards.</summary>
    public static int GetSomersaultCloudSlipstreamBonus() => 2;

    /// <summary>CN L3: Get the corner limit bonus for SomersaultCloud-upgraded ATTACK cards.</summary>
    public static int GetSomersaultCloudCornerBonus() => 1;

    // ═══════════════════════════════════════════════════════════════════
    // JP Unique Tech Logic
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// JP L1: Check if Nigiri triggers — speed sum exactly equals corner limit.
    /// </summary>
    public static bool ShouldTriggerNigiri(TechTreeState state, TechTreeDatabase db, int speedSum, int cornerLimit)
    {
        return HasEffect(state, db, TechEffectType.Nigiri) && speedSum == cornerLimit;
    }

    /// <summary>JP L2: Set the chosen broth for this race.</summary>
    public static void SelectBroth(TechTreeState state, BrothType broth)
    {
        state.brothSelection = broth;
    }

    /// <summary>JP L2: Get cooldown per turn from Shio broth.</summary>
    public static int GetBrothCooldownPerTurn(TechTreeState state)
    {
        return state.brothSelection == BrothType.Shio ? 1 : 0;
    }

    /// <summary>JP L2: Get the active broth modifiers.</summary>
    public static BrothModifiers GetBrothModifiers(TechTreeState state)
    {
        return BrothModifiers.FromBrothType(state.brothSelection);
    }

    /// <summary>
    /// JP L3: Check if Bankuruwase should trigger.
    /// Condition: rank is last (totalPlayers) or second-to-last (totalPlayers-1).
    /// </summary>
    public static bool ShouldTriggerBankuruwase(
        TechTreeState state, TechTreeDatabase db, int currentRank, int totalPlayers)
    {
        if (!HasEffect(state, db, TechEffectType.Bankuruwase)) return false;
        if (state.bankuruwaseActive) return false; // Already active
        return currentRank >= totalPlayers - 1; // Last or second-to-last
    }

    /// <summary>JP L3: Activate Bankuruwase rotor mode (3 turns of all buffs + 1 cool/turn).</summary>
    public static void ActivateBankuruwase(TechTreeState state)
    {
        state.bankuruwaseActive = true;
        state.bankuruwaseTurnsLeft = BANKURUWASE_DURATION;
    }

    /// <summary>JP L3: Tick one turn of Bankuruwase. Returns false if expired.</summary>
    public static bool TickBankuruwase(TechTreeState state)
    {
        if (!state.bankuruwaseActive) return false;
        state.bankuruwaseTurnsLeft--;
        if (state.bankuruwaseTurnsLeft <= 0)
        {
            state.bankuruwaseActive = false;
            return false;
        }
        return true;
    }

    /// <summary>JP L3: Get per-turn cooldown from Bankuruwase (1 per turn while active).</summary>
    public static int GetBankuruwaseCooldownPerTurn(TechTreeState state)
    {
        return state.bankuruwaseActive ? 1 : 0;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Per-Lap Heat Reduction (Common L1#1 / L2#1)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if heat reduction can be used this lap. Reset per lap.
    /// Returns the amount of heat to reduce (0 if none available).
    /// </summary>
    public static int GetHeatReductionThisLap(TechTreeState state, TechTreeDatabase db)
    {
        if (state.heatReductionUsedThisLap) return 0;
        var mods = ComputeModifiers(state, db);
        return mods.heatReductionPerLap;
    }

    /// <summary>Consume the per-lap heat reduction (call after applying it).</summary>
    public static void ConsumeHeatReduction(TechTreeState state)
    {
        state.heatReductionUsedThisLap = true;
    }

    /// <summary>Reset per-lap heat reduction tracker (call at start of each new lap).</summary>
    public static void ResetHeatReductionForLap(TechTreeState state)
    {
        state.heatReductionUsedThisLap = false;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Universal Unique Tech Query
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if any unique tech flag is active by effect type.
    /// Convenience method for game loop hooks.
    /// </summary>
    public static bool HasUniqueTech(TechTreeState state, TechTreeDatabase db, TechEffectType effectType)
    {
        return HasEffect(state, db, effectType);
    }

    // ═══════════════════════════════════════════════════════════════════
    // State Management
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reset all per-race state flags. Call at the start of each race.
    /// </summary>
    public static void ResetPerRaceState(TechTreeState state)
    {
        if (state == null) return;

        state.heatReductionUsedThisLap = false;
        state.fishAndChipsUsed = false;
        state.grillSpezialUsed = false;
        state.grillSpezialHeatPaidThisTurn = 0;
        state.brothSelection = BrothType.None;
        state.bankuruwaseActive = false;
        state.bankuruwaseTurnsLeft = 0;
        state.landmark1PassCount = 0;
        state.landmark2PassCount = 0;
        state.landmark1UltUsed = false;
        state.landmark2UltUsed = false;
        state.totalRepairs = 0;
        state.sunNeverSetsTarget = null;
        ResetDimSumCombo(state);
    }

    /// <summary>
    /// Reset per-turn tracking. Call at the start of each turn.
    /// </summary>
    public static void ResetPerTurnState(TechTreeState state)
    {
        if (state == null) return;
        state.grillSpezialHeatPaidThisTurn = 0;
        ResetDimSumCombo(state);
    }

    /// <summary>Reset CN L2 combo sequence tracking.</summary>
    private static void ResetDimSumCombo(TechTreeState state)
    {
        state.dimSumPlayedTrick = false;
        state.dimSumPlayedSpeed = false;
        state.dimSumPaidHeat = false;
    }

    /// <summary>
    /// Select which unlocked nodes to activate for this race.
    /// Typically called before race start. All selected nodes must be unlocked.
    /// </summary>
    public static void SelectActiveNodes(TechTreeState state, IEnumerable<string> nodeIds, TechTreeDatabase db)
    {
        state.activeNodeIds.Clear();
        foreach (var id in nodeIds)
        {
            if (state.IsUnlocked(id) || id.StartsWith("sun-never-sets-granted-"))
            {
                state.activeNodeIds.Add(id);
            }
        }
    }

    /// <summary>
    /// Activate all unlocked nodes (simple mode: use everything you've unlocked).
    /// </summary>
    public static void ActivateAllUnlocked(TechTreeState state)
    {
        state.activeNodeIds = new HashSet<string>(state.unlockedNodeIds);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Track Utility Helpers (used by US landmark techs)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if a position crosses a landmark going forward (clockwise).
    /// NOTE: Wrap-around detection only handles targetPos == 0 (start line).
    /// If landmarks are ever placed at non-zero positions, generalize the wrap check.
    /// </summary>
    public static bool CrossedPositionForward(int oldPos, int newPos, int targetPos, int totalCells)
    {
        if (oldPos <= targetPos && newPos >= targetPos) return true;
        // Wrap-around case: crossing the origin (targetPos == 0) from end of track
        if (oldPos > newPos && targetPos == 0) return true;
        return false;
    }

    /// <summary>
    /// Shortest distance between two positions on a circular track.
    /// </summary>
    public static int DistanceOnTrack(int posA, int posB, int totalCells)
    {
        int diff = Math.Abs(posA - posB);
        int wrapDist = totalCells - diff;
        return diff < wrapDist ? diff : wrapDist;
    }
}
