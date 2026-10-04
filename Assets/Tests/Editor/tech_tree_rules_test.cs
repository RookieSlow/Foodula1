using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Unit tests for TechTreeRules — unlock logic, RP economy, modifier computation,
/// and key unique tech effects. All tests are EditMode (no Unity dependencies).
/// </summary>
public class TechTreeRulesTests
{
    private TechTreeDatabase db;
    private TechTreeState state;

    [SetUp]
    public void SetUp()
    {
        db = TechTreeDatabaseFactory.CreateDefault();
        state = new TechTreeState(TeamId.UK, TechTreeRules.DEMO_BUDGET);
    }

    [TestCase(TechEffectType.LightweightDoubler, "hasPizzaSottile")]
    [TestCase(TechEffectType.FishAndChips, "hasFishAndChips")]
    [TestCase(TechEffectType.FullEnglish, "hasFullEnglish")]
    [TestCase(TechEffectType.SunNeverSets, "hasSunNeverSets")]
    [TestCase(TechEffectType.SchwarzbierFuel, "hasSchwarzbierFuel")]
    [TestCase(TechEffectType.WurstplatteSuspension, "hasWurstplatteSuspension")]
    [TestCase(TechEffectType.GrillSpezial, "hasGrillSpezial")]
    [TestCase(TechEffectType.CavallinoRampante, "hasCavallinoRampante")]
    [TestCase(TechEffectType.DriveThru, "hasDriveThru")]
    [TestCase(TechEffectType.SmokedBBQ, "hasSmokedBBQ")]
    [TestCase(TechEffectType.MotherRoad, "hasMotherRoad")]
    [TestCase(TechEffectType.YinYangTea, "hasYinYangTea")]
    [TestCase(TechEffectType.DimSumCombo, "hasDimSumCombo")]
    [TestCase(TechEffectType.SomersaultCloud, "hasSomersaultCloud")]
    [TestCase(TechEffectType.Nigiri, "hasNigiri")]
    [TestCase(TechEffectType.BrothSelection, "hasBrothSelection")]
    [TestCase(TechEffectType.Bankuruwase, "hasBankuruwase")]
    public void test_boolean_effect_maps_to_its_modifier_flag(TechEffectType effectType, string fieldName)
    {
        TechModifiers modifiers = TechModifiers.Default;

        Assert.That(TechTreeRules.TryApplyBooleanModifier(ref modifiers, effectType), Is.True);
        var field = typeof(TechModifiers).GetField(fieldName);
        Assert.That(field, Is.Not.Null, "Expected TechModifiers field " + fieldName);
        Assert.That(field.GetValue(modifiers), Is.EqualTo(true));
    }

    [TestCase(TechEffectType.HeatReductionPerLap)]
    [TestCase(TechEffectType.EngineCapacityBonus)]
    public void test_boolean_effect_mapper_leaves_numeric_effects_for_numeric_aggregation(TechEffectType effectType)
    {
        TechModifiers modifiers = TechModifiers.Default;

        Assert.That(TechTreeRules.TryApplyBooleanModifier(ref modifiers, effectType), Is.False);
        Assert.That(modifiers, Is.EqualTo(TechModifiers.Default));
    }

    [TestCase(TechEffectType.HeatReductionPerLap, "heatReductionPerLap", false)]
    [TestCase(TechEffectType.SpeedBonusStraight, "speedBonusStraight", false)]
    [TestCase(TechEffectType.CornerLimitBonus, "cornerLimitBonus", false)]
    [TestCase(TechEffectType.DurabilityBonus, "durabilityBonus", false)]
    [TestCase(TechEffectType.SlipstreamRangeBonus, "slipstreamRangeBonus", false)]
    [TestCase(TechEffectType.PitExitMoveBonus, "pitExitMoveBonus", false)]
    [TestCase(TechEffectType.EngineCapacityBonus, "engineCapacityBonus", true)]
    [TestCase(TechEffectType.HandSizeBonus, "handSizeBonus", true)]
    [TestCase(TechEffectType.SpinCounterMaxBonus, "spinCounterMaxBonus", true)]
    public void test_numeric_effect_mapper_preserves_max_or_additive_policy(
        TechEffectType effectType,
        string fieldName,
        bool stacks)
    {
        TechModifiers modifiers = TechModifiers.Default;

        Assert.That(TechTreeRules.TryApplyModifierEffect(ref modifiers, new TechEffect(effectType, 2)), Is.True);
        Assert.That(TechTreeRules.TryApplyModifierEffect(ref modifiers, new TechEffect(effectType, 1)), Is.True);

        var field = typeof(TechModifiers).GetField(fieldName);
        Assert.That(field, Is.Not.Null, "Expected TechModifiers field " + fieldName);
        Assert.That(field.GetValue(modifiers), Is.EqualTo(stacks ? 3 : 2));
    }

    [Test]
    public void test_modifier_effect_mapper_ignores_unknown_effect_type()
    {
        TechModifiers modifiers = TechModifiers.Default;

        Assert.That(
            TechTreeRules.TryApplyModifierEffect(ref modifiers, new TechEffect((TechEffectType)(-1), 99)),
            Is.False);
        Assert.That(modifiers, Is.EqualTo(TechModifiers.Default));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Test helpers — satisfy tier gates for unique tech tests
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Unlock 3 cheapest L1 common nodes to satisfy the L1 tier gate.</summary>
    private void UnlockL1TierGate(TechTreeState s)
    {
        var ids = s.teamId == TeamId.CN
            ? new[] { "cn-ev-l1-heat-pump", "cn-ev-l1-pmsm", "cn-ev-l1-torque-vector" }
            : new[] { "common-l1-heat-coating", "common-l1-lightweight-chassis", "common-l1-track-memory" };
        foreach (var id in ids)
            Assert.That(TechTreeRules.UnlockNode(s, id, db), Is.True);
    }

    /// <summary>Unlock L1 prereqs + 3 L2 common nodes to satisfy the L2 tier gate.</summary>
    private void UnlockL2TierGate(TechTreeState s)
    {
        // Bump RP budget — demo 25000 isn't enough for 3×L1 common + 3×L2 common + L2 unique
        s.rpBalance = 50000;
        UnlockL1TierGate(s);
        var ids = s.teamId == TeamId.CN
            ? new[] { "cn-ev-l2-phase-change", "cn-ev-l2-dual-motor", "cn-ev-l2-active-suspension" }
            : new[] { "common-l2-ceramic-coating", "common-l2-carbon-fiber", "common-l2-extreme-handling" };
        foreach (var id in ids)
            Assert.That(TechTreeRules.UnlockNode(s, id, db), Is.True);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Database Integrity
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_database_contains_all_common_l1_nodes()
    {
        var l1Common = db.GetCommonInTier(TechTreeTier.L1);
        Assert.That(l1Common.Count, Is.EqualTo(4), "Should have 4 L1 common nodes");
    }

    [Test]
    public void test_database_contains_all_common_l2_nodes()
    {
        var l2Common = db.GetCommonInTier(TechTreeTier.L2);
        Assert.That(l2Common.Count, Is.EqualTo(5), "Should have 5 L2 common nodes");
    }

    [Test]
    public void test_database_contains_expected_unique_nodes_per_country()
    {
        foreach (TeamId team in System.Enum.GetValues(typeof(TeamId)))
        {
            var all = new List<TechNodeDef>();
            all.AddRange(db.GetUniqueInTier(team, TechTreeTier.L1));
            all.AddRange(db.GetUniqueInTier(team, TechTreeTier.L2));
            all.AddRange(db.GetUniqueInTier(team, TechTreeTier.L3));

            int expected = team == TeamId.CN ? 4 : 3;
            Assert.That(all.Count, Is.EqualTo(expected),
                $"Team {team} should have the expected unique tech count");
        }
    }

    // Contract from design/gdd/foodula-1-tech-tree.md: a count-only check cannot
    // detect a team node moved to the wrong tier or bound to the wrong effect.
    [TestCase(TeamId.UK, TechTreeTier.L1, "uk-l1-fish-and-chips", TechEffectType.FishAndChips, 5000, 1)]
    [TestCase(TeamId.UK, TechTreeTier.L2, "uk-l2-full-english", TechEffectType.FullEnglish, 8000, 1)]
    [TestCase(TeamId.UK, TechTreeTier.L3, "uk-l3-sun-never-sets", TechEffectType.SunNeverSets, 12000, 1)]
    [TestCase(TeamId.DE, TechTreeTier.L1, "de-l1-schwarzbier-fuel", TechEffectType.SchwarzbierFuel, 5000, 1)]
    [TestCase(TeamId.DE, TechTreeTier.L2, "de-l2-wurstplatte", TechEffectType.WurstplatteSuspension, 8000, 1)]
    [TestCase(TeamId.DE, TechTreeTier.L3, "de-l3-grill-spezial", TechEffectType.GrillSpezial, 12000, 1)]
    [TestCase(TeamId.IT, TechTreeTier.L1, "it-l1-pizza-sottile", TechEffectType.LightweightDoubler, 5000, 1)]
    [TestCase(TeamId.IT, TechTreeTier.L2, "it-l2-lasagna", TechEffectType.SpinCounterMaxBonus, 8000, 1)]
    [TestCase(TeamId.IT, TechTreeTier.L3, "it-l3-cavallino-rampante", TechEffectType.CavallinoRampante, 12000, 1)]
    [TestCase(TeamId.US, TechTreeTier.L1, "us-l1-drive-thru", TechEffectType.DriveThru, 5000, 1)]
    [TestCase(TeamId.US, TechTreeTier.L2, "us-l2-smoked-bbq", TechEffectType.SmokedBBQ, 8000, 1)]
    [TestCase(TeamId.US, TechTreeTier.L3, "us-l3-mother-road", TechEffectType.MotherRoad, 12000, 1)]
    [TestCase(TeamId.CN, TechTreeTier.L1, "cn-l1-yin-yang-tea", TechEffectType.YinYangTea, 5000, 2)]
    [TestCase(TeamId.CN, TechTreeTier.L1, "cn-l1-fast-charge", TechEffectType.PitExitMoveBonus, 5000, 2)]
    [TestCase(TeamId.CN, TechTreeTier.L2, "cn-l2-dim-sum-combo", TechEffectType.DimSumCombo, 8000, 1)]
    [TestCase(TeamId.CN, TechTreeTier.L3, "cn-l3-somersault-cloud", TechEffectType.SomersaultCloud, 12000, 1)]
    [TestCase(TeamId.JP, TechTreeTier.L1, "jp-l1-nigiri", TechEffectType.Nigiri, 5000, 1)]
    [TestCase(TeamId.JP, TechTreeTier.L2, "jp-l2-broth-selection", TechEffectType.BrothSelection, 8000, 1)]
    [TestCase(TeamId.JP, TechTreeTier.L3, "jp-l3-bankuruwase", TechEffectType.Bankuruwase, 12000, 1)]
    public void test_unique_catalog_matches_current_gdd(
        TeamId team, TechTreeTier tier, string id, TechEffectType firstEffect,
        int rpCost, int expectedTierCount)
    {
        var tierNodes = db.GetUniqueInTier(team, tier);
        Assert.That(tierNodes.Count, Is.EqualTo(expectedTierCount), team + " " + tier);
        var node = db.Get(id);
        Assert.That(node, Is.Not.Null, id);
        Assert.That(node.teamId, Is.EqualTo(team), id);
        Assert.That(node.tier, Is.EqualTo(tier), id);
        Assert.That(node.rpCost, Is.EqualTo(rpCost), id);
        Assert.That(node.prerequisites, Is.Empty, id);
        Assert.That(node.effects, Is.Not.Empty, id);
        Assert.That(node.effects[0].type, Is.EqualTo(firstEffect), id);
        Assert.That(tierNodes.Exists(candidate => candidate.id == id), Is.True, id);
    }

    [Test]
    public void test_italian_l2_catalog_includes_all_three_documented_bonuses()
    {
        var effects = db.Get("it-l2-lasagna").effects;
        Assert.That(effects.Length, Is.EqualTo(3));
        Assert.That(effects[0].type, Is.EqualTo(TechEffectType.SpinCounterMaxBonus));
        Assert.That(effects[1].type, Is.EqualTo(TechEffectType.HandSizeBonus));
        Assert.That(effects[2].type, Is.EqualTo(TechEffectType.EngineCapacityBonus));
        foreach (var effect in effects)
            Assert.That(effect.value, Is.EqualTo(1f));
    }

    [Test]
    public void test_node_count_reasonable()
    {
        Assert.That(db.Count, Is.GreaterThan(30), "Should have 30+ nodes total");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Unlock Logic
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_unlock_requires_enough_rp()
    {
        state.rpBalance = 100; // Not enough for anything
        Assert.That(TechTreeRules.CanUnlock(state, "common-l1-lightweight-chassis", db), Is.False);
    }

    [Test]
    public void test_unlock_succeeds_with_enough_rp()
    {
        Assert.That(TechTreeRules.CanUnlock(state, "common-l1-lightweight-chassis", db), Is.True);
    }

    [Test]
    public void test_unlock_deducts_rp()
    {
        int before = state.rpBalance;
        bool ok = TechTreeRules.UnlockNode(state, "common-l1-lightweight-chassis", db);
        Assert.That(ok, Is.True);
        Assert.That(state.rpBalance, Is.EqualTo(before - 2000));
        Assert.That(state.IsUnlocked("common-l1-lightweight-chassis"), Is.True);
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void test_only_own_team_can_purchase_unique_research(TeamId purchaser)
    {
        foreach (TeamId owner in System.Enum.GetValues(typeof(TeamId)))
        {
            foreach (TechTreeTier tier in System.Enum.GetValues(typeof(TechTreeTier)))
            {
                foreach (var node in db.GetUniqueInTier(owner, tier))
                {
                    var profile = new TechTreeState(purchaser, 100000);
                    var commonL1 = db.GetCommonInTier(TechTreeTier.L1, purchaser == TeamId.CN);
                    var commonL2 = db.GetCommonInTier(TechTreeTier.L2, purchaser == TeamId.CN);
                    for (int i = 0; i < 3; i++)
                    {
                        profile.unlockedNodeIds.Add(commonL1[i].id);
                        profile.unlockedNodeIds.Add(commonL2[i].id);
                    }

                    bool ownNode = owner == purchaser;
                    int balance = profile.rpBalance;
                    Assert.That(TechTreeRules.CanUnlock(profile, node.id, db), Is.EqualTo(ownNode),
                        purchaser + " purchasing " + node.id);
                    Assert.That(TechTreeRules.UnlockNode(profile, node.id, db), Is.EqualTo(ownNode),
                        purchaser + " purchasing " + node.id);
                    Assert.That(profile.rpBalance, Is.EqualTo(balance - (ownNode ? node.rpCost : 0)));
                    Assert.That(profile.IsUnlocked(node.id), Is.EqualTo(ownNode));
                }
            }
        }
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void test_new_common_purchases_use_the_team_catalogue(TeamId team)
    {
        foreach (var tier in new[] { TechTreeTier.L1, TechTreeTier.L2 })
        {
            foreach (bool evCatalogue in new[] { false, true })
            {
                foreach (var node in db.GetCommonInTier(tier, evCatalogue))
                {
                    var profile = new TechTreeState(team, 100000);
                    if (tier == TechTreeTier.L2)
                    {
                        // Meet the gate and the matching prerequisite, without
                        // creating legacy CN standard research accidentally.
                        foreach (var l1 in db.GetCommonInTier(TechTreeTier.L1, team == TeamId.CN))
                            profile.unlockedNodeIds.Add(l1.id);
                        if (team != TeamId.CN)
                            foreach (var l1 in db.GetCommonInTier(TechTreeTier.L1, true))
                                profile.unlockedNodeIds.Add(l1.id);
                    }

                    bool expected = (team == TeamId.CN) == evCatalogue;
                    int balance = profile.rpBalance;
                    Assert.That(TechTreeRules.CanUnlock(profile, node.id, db), Is.EqualTo(expected),
                        team + " purchasing " + node.id);
                    Assert.That(TechTreeRules.UnlockNode(profile, node.id, db), Is.EqualTo(expected),
                        team + " purchasing " + node.id);
                    Assert.That(profile.rpBalance, Is.EqualTo(balance - (expected ? node.rpCost : 0)));
                    Assert.That(profile.IsUnlocked(node.id), Is.EqualTo(expected));
                }
            }
        }
    }

    [TestCase("common-l2-ceramic-coating", "common-l1-heat-coating")]
    [TestCase("common-l2-carbon-fiber", "common-l1-lightweight-chassis")]
    [TestCase("common-l2-extreme-handling", "common-l1-track-memory")]
    [TestCase("common-l2-reserve-tank", "common-l1-expanded-tank")]
    public void test_legacy_chinese_standard_l1_can_still_upgrade(string l2Id, string oldL1Id)
    {
        var profile = new TechTreeState(TeamId.CN, 100000);
        profile.unlockedNodeIds.Add(oldL1Id);
        profile.unlockedNodeIds.Add("cn-ev-l1-heat-pump");
        profile.unlockedNodeIds.Add("cn-ev-l1-pmsm");
        profile.unlockedNodeIds.Add("cn-ev-l1-torque-vector");
        profile.unlockedNodeIds.Add("cn-ev-l1-solid-state");
        Assert.That(TechTreeRules.CanUnlock(profile, l2Id, db), Is.True);
        Assert.That(TechTreeRules.UnlockNode(profile, l2Id, db), Is.True);
    }

    [TestCase(TechTreeTier.L1, "common-l1-heat-coating", "cn-ev-l1-heat-pump",
        "cn-ev-l1-pmsm", "cn-ev-l1-torque-vector")]
    [TestCase(TechTreeTier.L2, "common-l2-ceramic-coating", "cn-ev-l2-phase-change",
        "cn-ev-l2-dual-motor", "cn-ev-l2-active-suspension")]
    public void test_chinese_legacy_and_ev_variants_share_one_tier_gate_slot(
        TechTreeTier tier, string legacyId, string evSameLineId, string evSecondLineId,
        string evThirdLineId)
    {
        var profile = new TechTreeState(TeamId.CN, 100000);
        profile.unlockedNodeIds.Add(legacyId);
        profile.unlockedNodeIds.Add(evSameLineId);
        profile.unlockedNodeIds.Add(evSecondLineId);

        Assert.That(TechTreeRules.CountUnlockedCommonInTier(profile, db, tier), Is.EqualTo(2));
        Assert.That(TechTreeRules.HasMetTierGate(profile, db, tier), Is.False);

        profile.unlockedNodeIds.Add(evThirdLineId);
        Assert.That(TechTreeRules.CountUnlockedCommonInTier(profile, db, tier), Is.EqualTo(3));
        Assert.That(TechTreeRules.HasMetTierGate(profile, db, tier), Is.True);
    }

    [Test]
    public void test_foreign_ev_common_nodes_do_not_count_towards_standard_gate()
    {
        state.unlockedNodeIds.Add("common-l1-heat-coating");
        state.unlockedNodeIds.Add("cn-ev-l1-pmsm");
        state.unlockedNodeIds.Add("cn-ev-l1-torque-vector");

        Assert.That(TechTreeRules.CountUnlockedCommonInTier(state, db, TechTreeTier.L1), Is.EqualTo(1));
        Assert.That(TechTreeRules.HasMetTierGate(state, db, TechTreeTier.L1), Is.False);
    }

    [Test]
    public void test_missing_profile_or_database_has_no_common_gate_progress()
    {
        Assert.That(TechTreeRules.CountUnlockedCommonInTier(null, db, TechTreeTier.L1), Is.Zero);
        Assert.That(TechTreeRules.CountUnlockedCommonInTier(state, null, TechTreeTier.L1), Is.Zero);
    }

    [Test]
    public void test_unlock_requires_prerequisites()
    {
        // L2 #1 needs L1 #1, which is not unlocked
        Assert.That(TechTreeRules.CanUnlock(state, "common-l2-ceramic-coating", db), Is.False);
    }

    [Test]
    public void test_unlock_succeeds_after_prerequisite()
    {
        TechTreeRules.UnlockNode(state, "common-l1-heat-coating", db);
        TechTreeRules.UnlockNode(state, "common-l1-lightweight-chassis", db);
        TechTreeRules.UnlockNode(state, "common-l1-track-memory", db);
        Assert.That(TechTreeRules.CanUnlock(state, "common-l2-ceramic-coating", db), Is.True);
    }

    [Test]
    public void test_tier_gate_l1_requires_three_common()
    {
        // L1 unique (UK L1) needs ≥3 L1 common unlocked
        Assert.That(TechTreeRules.CanUnlock(state, "uk-l1-fish-and-chips", db), Is.False);

        // Unlock 2 common — still not enough
        TechTreeRules.UnlockNode(state, "common-l1-heat-coating", db);
        TechTreeRules.UnlockNode(state, "common-l1-lightweight-chassis", db);
        Assert.That(TechTreeRules.CanUnlock(state, "uk-l1-fish-and-chips", db), Is.False);

        // Unlock 3rd common — gate met
        TechTreeRules.UnlockNode(state, "common-l1-track-memory", db);
        Assert.That(TechTreeRules.CanUnlock(state, "uk-l1-fish-and-chips", db), Is.True);
    }

    [Test]
    public void test_tier_gate_l2_requires_three_common()
    {
        // Bump RP — 25000 demo budget isn't enough for 3×L1 + 3×L2 common + L2 unique
        state.rpBalance = 50000;

        // Set up: unlock 3 L1 common (needed as prereqs for L2 commons)
        TechTreeRules.UnlockNode(state, "common-l1-heat-coating", db);
        TechTreeRules.UnlockNode(state, "common-l1-lightweight-chassis", db);
        TechTreeRules.UnlockNode(state, "common-l1-track-memory", db);

        // Now unlock L2 commons
        TechTreeRules.UnlockNode(state, "common-l2-ceramic-coating", db);
        TechTreeRules.UnlockNode(state, "common-l2-carbon-fiber", db);

        // Only 2 L2 commons unlocked — gate not met for UK L2
        Assert.That(TechTreeRules.CanUnlock(state, "uk-l2-full-english", db), Is.False);

        // Unlock 3rd L2 common — gate now met
        TechTreeRules.UnlockNode(state, "common-l2-extreme-handling", db);
        Assert.That(TechTreeRules.CanUnlock(state, "uk-l2-full-english", db), Is.True);
    }

    [Test]
    public void test_l3_unique_has_no_tier_gate()
    {
        // GDD: "L3 大师层（无需通用前置）→ L3 专属"
        // L3 should NOT require L3 commons (there are no L3 commons anyway)
        // L3 unique should be unlockable directly as long as you have RP
        Assert.That(TechTreeRules.CanUnlock(state, "uk-l3-sun-never-sets", db), Is.True);
    }

    [Test]
    public void test_l2_upgrades_supersede_l1()
    {
        TechTreeRules.UnlockNode(state, "common-l1-heat-coating", db);
        TechTreeRules.UnlockNode(state, "common-l1-track-memory", db);
        // Unlock L1 #2 (lightweight)
        TechTreeRules.UnlockNode(state, "common-l1-lightweight-chassis", db);
        Assert.That(state.IsUnlocked("common-l1-lightweight-chassis"), Is.True);

        // L1 should be superseded once L2 is unlocked
        Assert.That(TechTreeRules.IsSuperseded(state, "common-l1-lightweight-chassis", db), Is.False);

        TechTreeRules.UnlockNode(state, "common-l2-carbon-fiber", db);
        Assert.That(TechTreeRules.IsSuperseded(state, "common-l1-lightweight-chassis", db), Is.True);
    }

    [Test]
    public void test_cannot_unlock_already_unlocked()
    {
        TechTreeRules.UnlockNode(state, "common-l1-heat-coating", db);
        Assert.That(TechTreeRules.CanUnlock(state, "common-l1-heat-coating", db), Is.False);
    }

    // ═══════════════════════════════════════════════════════════════════
    // RP Economy
    // ═══════════════════════════════════════════════════════════════════

    [TestCase(1, 5000)]
    [TestCase(2, 3500)]
    [TestCase(3, 2500)]
    [TestCase(4, 1500)]
    [TestCase(5, 1000)]
    [TestCase(6, 500)]
    [TestCase(7, 0)] // Beyond table
    public void test_rp_reward_by_position(int position, int expectedRp)
    {
        Assert.That(TechTreeRules.CalculateRaceRP(position), Is.EqualTo(expectedRp));
    }

    [Test]
    public void test_cavallino_rampante_multiplies_win_rp()
    {
        int baseRp = TechTreeRules.CalculateRaceRP(1); // 5000
        int boosted = TechTreeRules.ApplyCavallinoRampante(baseRp, 1, false);
        Assert.That(boosted, Is.EqualTo(7500)); // 5000 × 1.5 = 7500
    }

    [Test]
    public void test_cavallino_rampante_home_race_double_multiplier()
    {
        int baseRp = TechTreeRules.CalculateRaceRP(1); // 5000
        int boosted = TechTreeRules.ApplyCavallinoRampante(baseRp, 1, true);
        // 5000 × 1.5 × 1.5 = 11250 (ceiling)
        Assert.That(boosted, Is.EqualTo(11250));
    }

    [Test]
    public void test_cavallino_rampante_only_applies_to_wins()
    {
        int baseRp = TechTreeRules.CalculateRaceRP(2); // 3500 (2nd place)
        int boosted = TechTreeRules.ApplyCavallinoRampante(baseRp, 2, true);
        Assert.That(boosted, Is.EqualTo(3500)); // No multiplier for non-wins
    }

    [Test]
    public void test_demo_budget_is_25000()
    {
        Assert.That(TechTreeRules.GetDemoBudget(), Is.EqualTo(25000));
    }

    [Test]
    public void test_demo_budget_allows_full_l1()
    {
        // Cheapest 3 L1 common + 1 L1 unique
        // 2000 + 2500 + 3000 + 5000 = 12500
        TechTreeRules.UnlockNode(state, "common-l1-lightweight-chassis", db); // 2000
        TechTreeRules.UnlockNode(state, "common-l1-heat-coating", db);       // 2500
        TechTreeRules.UnlockNode(state, "common-l1-track-memory", db);       // 3000
        TechTreeRules.UnlockNode(state, "uk-l1-fish-and-chips", db);         // 5000

        Assert.That(state.rpBalance, Is.EqualTo(25000 - 12500));
        Assert.That(state.unlockedNodeIds.Count, Is.EqualTo(4));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Modifier Computation
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_empty_state_produces_default_modifiers()
    {
        var mods = TechTreeRules.ComputeModifiers(state, db);
        Assert.That(mods.heatReductionPerLap, Is.Zero);
        Assert.That(mods.speedBonusStraight, Is.Zero);
        Assert.That(mods.cornerLimitBonus, Is.Zero);
        Assert.That(mods.EffectiveSlipstreamRange, Is.EqualTo(1));
    }

    [Test]
    public void test_heat_reduction_modifier()
    {
        // Unlock + activate L1 #1
        TechTreeRules.UnlockNode(state, "common-l1-heat-coating", db);
        TechTreeRules.SelectActiveNodes(state, new[] { "common-l1-heat-coating" }, db);

        var mods = TechTreeRules.ComputeModifiers(state, db);
        Assert.That(mods.heatReductionPerLap, Is.EqualTo(1));
    }

    [Test]
    public void test_l2_upgrade_takes_higher_value()
    {
        // Unlock both L1 #2 and L2 #2 — only activate L2 (higher value)
        TechTreeRules.UnlockNode(state, "common-l1-lightweight-chassis", db);
        TechTreeRules.UnlockNode(state, "common-l1-heat-coating", db);
        TechTreeRules.UnlockNode(state, "common-l1-track-memory", db);
        TechTreeRules.UnlockNode(state, "common-l2-carbon-fiber", db);
        TechTreeRules.SelectActiveNodes(state, new[] { "common-l2-carbon-fiber" }, db);

        var mods = TechTreeRules.ComputeModifiers(state, db);
        Assert.That(mods.speedBonusStraight, Is.EqualTo(2));
    }

    [Test]
    public void test_speed_bonus_straight_with_pizza_sottile()
    {
        var itState = new TechTreeState(TeamId.IT, TechTreeRules.DEMO_BUDGET);

        // Unlock lightweight chain + PizzaSottile
        TechTreeRules.UnlockNode(itState, "common-l1-lightweight-chassis", db);       // SpeedBonusStraight=1
        TechTreeRules.UnlockNode(itState, "common-l1-heat-coating", db);
        TechTreeRules.UnlockNode(itState, "common-l1-track-memory", db);
        TechTreeRules.UnlockNode(itState, "it-l1-pizza-sottile", db);                // Doubles lightweight

        TechTreeRules.SelectActiveNodes(itState,
            new[] { "common-l1-lightweight-chassis", "it-l1-pizza-sottile" }, db);

        var mods = TechTreeRules.ComputeModifiers(itState, db);
        Assert.That(mods.hasPizzaSottile, Is.True);
        Assert.That(mods.speedBonusStraight, Is.EqualTo(1)); // raw
        Assert.That(mods.EffectiveSpeedBonusStraight, Is.EqualTo(2)); // doubled
    }

    [Test]
    public void test_corner_limit_bonus_stacking_formula()
    {
        // Single source: just that value
        int result = TechTreeRules.ComputeEffectiveCornerLimitBonus(2, 0, 0, 0, 0);
        Assert.That(result, Is.EqualTo(2));

        // Two sources: max + floor(other/2)
        // commonBonus=2, brothBonus=1 → max=2, other=1 → 2 + 0 = 2
        result = TechTreeRules.ComputeEffectiveCornerLimitBonus(2, 1, 0, 0, 0);
        Assert.That(result, Is.EqualTo(2));

        // Three sources: common=2, broth=1, nigiri=1 → max=2, others=1+1=2 → 2 + 1 = 3
        result = TechTreeRules.ComputeEffectiveCornerLimitBonus(2, 1, 1, 0, 0);
        Assert.That(result, Is.EqualTo(3));
    }

    [Test]
    public void test_lasagna_stat_modifiers()
    {
        var itState = new TechTreeState(TeamId.IT, TechTreeRules.DEMO_BUDGET);
        UnlockL2TierGate(itState);
        TechTreeRules.UnlockNode(itState, "it-l2-lasagna", db);
        TechTreeRules.SelectActiveNodes(itState, new[] { "it-l2-lasagna" }, db);

        var mods = TechTreeRules.ComputeModifiers(itState, db);
        Assert.That(mods.spinCounterMaxBonus, Is.EqualTo(1));
        Assert.That(mods.handSizeBonus, Is.EqualTo(1));
        Assert.That(mods.engineCapacityBonus, Is.EqualTo(1));
        Assert.That(mods.EffectiveSpinCounterMax, Is.EqualTo(4)); // 3 + 1
    }

    // ═══════════════════════════════════════════════════════════════════
    // Unique Tech Effects
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_uk_l1_fish_and_chips_once_per_race()
    {
        UnlockL1TierGate(state);
        TechTreeRules.UnlockNode(state, "uk-l1-fish-and-chips", db);
        TechTreeRules.SelectActiveNodes(state, new[] { "uk-l1-fish-and-chips" }, db);

        Assert.That(TechTreeRules.CanUseFishAndChips(state, db), Is.True);

        TechTreeRules.UseFishAndChips(state);
        Assert.That(TechTreeRules.CanUseFishAndChips(state, db), Is.False);
    }

    [Test]
    public void test_uk_l2_full_english_trigger()
    {
        Assert.That(TechTreeRules.ShouldTriggerFullEnglish(true, true, true), Is.True);
        Assert.That(TechTreeRules.ShouldTriggerFullEnglish(false, true, true), Is.False);
        Assert.That(TechTreeRules.ShouldTriggerFullEnglish(true, false, true), Is.False);
        Assert.That(TechTreeRules.ShouldTriggerFullEnglish(true, true, false), Is.False);
    }

    [Test]
    public void test_de_l1_schwarzbier_fuel_is_limited_to_once_per_lap()
    {
        var deState = new TechTreeState(TeamId.DE, TechTreeRules.DEMO_BUDGET);
        UnlockL1TierGate(deState);
        Assert.That(TechTreeRules.UnlockNode(deState, "de-l1-schwarzbier-fuel", db), Is.True);
        TechTreeRules.SelectActiveNodes(deState, new[] { "de-l1-schwarzbier-fuel" }, db);
        Assert.That(deState.IsActive("de-l1-schwarzbier-fuel"), Is.True);

        Assert.That(TechTreeRules.CanUseSchwarzbierFuel(deState, db, 1, 0), Is.True);
        TechTreeRules.UseSchwarzbierFuel(deState, 0);
        Assert.That(TechTreeRules.CanUseSchwarzbierFuel(deState, db, 1, 0), Is.False);
        Assert.That(TechTreeRules.CanUseSchwarzbierFuel(deState, db, 1, 1), Is.True);

        TechTreeRules.UseSchwarzbierFuel(deState, 1);
        TechTreeRules.ResetPerRaceState(deState);
        Assert.That(TechTreeRules.CanUseSchwarzbierFuel(deState, db, 1, 0), Is.True);
        Assert.That(TechTreeRules.CanUseSchwarzbierFuel(deState, db, 0, 0), Is.False);
    }

    [Test]
    public void test_de_l3_grill_spezial_once_per_race()
    {
        var deState = new TechTreeState(TeamId.DE, TechTreeRules.DEMO_BUDGET);
        TechTreeRules.UnlockNode(deState, "de-l3-grill-spezial", db);
        TechTreeRules.SelectActiveNodes(deState, new[] { "de-l3-grill-spezial" }, db);

        Assert.That(TechTreeRules.CanUseGrillSpezial(deState, db), Is.True);

        TechTreeRules.ActivateGrillSpezial(deState);
        Assert.That(TechTreeRules.CanUseGrillSpezial(deState, db), Is.False);
    }

    [Test]
    public void test_de_l3_grill_spezial_tracks_heat_paid()
    {
        var deState = new TechTreeState(TeamId.DE, TechTreeRules.DEMO_BUDGET);
        TechTreeRules.UnlockNode(deState, "de-l3-grill-spezial", db);
        TechTreeRules.SelectActiveNodes(deState, new[] { "de-l3-grill-spezial" }, db);

        TechTreeRules.TrackGrillSpezialHeat(deState, 3);
        TechTreeRules.TrackGrillSpezialHeat(deState, 2);

        Assert.That(TechTreeRules.GetGrillSpezialCooldown(deState), Is.EqualTo(5));
        TechTreeRules.ActivateGrillSpezial(deState);
        Assert.That(TechTreeRules.GetGrillSpezialCooldown(deState), Is.Zero);
    }

    [Test]
    public void test_jp_l1_nigiri_exact_match()
    {
        var jpState = new TechTreeState(TeamId.JP, TechTreeRules.DEMO_BUDGET);
        UnlockL1TierGate(jpState);
        TechTreeRules.UnlockNode(jpState, "jp-l1-nigiri", db);
        TechTreeRules.SelectActiveNodes(jpState, new[] { "jp-l1-nigiri" }, db);

        Assert.That(TechTreeRules.ShouldTriggerNigiri(jpState, db, 3, 3), Is.True);
        Assert.That(TechTreeRules.ShouldTriggerNigiri(jpState, db, 3, 4), Is.False);
        Assert.That(TechTreeRules.ShouldTriggerNigiri(jpState, db, 4, 3), Is.False);
    }

    [Test]
    public void test_cn_l1_yin_yang_cold_state()
    {
        var cnState = new TechTreeState(TeamId.CN, TechTreeRules.DEMO_BUDGET);
        UnlockL1TierGate(cnState);
        TechTreeRules.UnlockNode(cnState, "cn-l1-yin-yang-tea", db);
        TechTreeRules.SelectActiveNodes(cnState, new[] { "cn-l1-yin-yang-tea" }, db);

        // Go mode = Yin
        var result = TechTreeRules.ResolveYinYang(cnState, db, isGoMode: true);
        Assert.That(result.triggered, Is.True);
        Assert.That(result.isYin, Is.True);
        Assert.That(result.isYang, Is.False);
        Assert.That(result.extraMovement, Is.EqualTo(1));
    }

    [Test]
    public void test_cn_l1_yin_yang_hot_state()
    {
        var cnState = new TechTreeState(TeamId.CN, TechTreeRules.DEMO_BUDGET);
        UnlockL1TierGate(cnState);
        TechTreeRules.UnlockNode(cnState, "cn-l1-yin-yang-tea", db);
        TechTreeRules.SelectActiveNodes(cnState, new[] { "cn-l1-yin-yang-tea" }, db);

        // Recover mode = Yang
        var result = TechTreeRules.ResolveYinYang(cnState, db, isGoMode: false);
        Assert.That(result.triggered, Is.True);
        Assert.That(result.isYang, Is.True);
        Assert.That(result.isYin, Is.False);
        Assert.That(result.heatToCool, Is.EqualTo(1));
    }

    [Test]
    public void test_cn_l1_yin_yang_no_tech_no_trigger()
    {
        var cnState = new TechTreeState(TeamId.CN, TechTreeRules.DEMO_BUDGET);
        // No tech unlocked
        var result = TechTreeRules.ResolveYinYang(cnState, db, isGoMode: true);
        Assert.That(result.triggered, Is.False);
    }

    [Test]
    public void test_cn_fast_charge_adds_pit_exit_move_bonus()
    {
        var cnState = new TechTreeState(TeamId.CN, TechTreeRules.DEMO_BUDGET);
        UnlockL1TierGate(cnState);
        Assert.That(TechTreeRules.UnlockNode(cnState, "cn-l1-fast-charge", db), Is.True);
        TechTreeRules.SelectActiveNodes(cnState, new[] { "cn-l1-fast-charge" }, db);

        var modifiers = TechTreeRules.ComputeModifiers(cnState, db);

        Assert.That(modifiers.pitExitMoveBonus, Is.EqualTo(1));
    }

    [Test]
    public void test_cn_l2_dim_sum_combo_sequence()
    {
        var cnState = new TechTreeState(TeamId.CN, TechTreeRules.DEMO_BUDGET);
        UnlockL2TierGate(cnState);
        TechTreeRules.UnlockNode(cnState, "cn-l2-dim-sum-combo", db);
        TechTreeRules.SelectActiveNodes(cnState, new[] { "cn-l2-dim-sum-combo" }, db);

        // Step 1: Play trick
        TechTreeRules.TrackDimSumCombo(cnState, playedTrick: true, playedSpeed: false, paidHeat: false);
        Assert.That(TechTreeRules.CheckDimSumCombo(cnState, db), Is.False);

        // Step 2: Play speed (after trick)
        TechTreeRules.TrackDimSumCombo(cnState, playedTrick: false, playedSpeed: true, paidHeat: false);
        Assert.That(TechTreeRules.CheckDimSumCombo(cnState, db), Is.False);

        // Step 3: Pay heat (after trick + speed) — combo complete!
        TechTreeRules.TrackDimSumCombo(cnState, playedTrick: false, playedSpeed: false, paidHeat: true);
        Assert.That(TechTreeRules.CheckDimSumCombo(cnState, db), Is.True);
    }

    [Test]
    public void test_cn_l2_dim_sum_combo_wrong_order()
    {
        var cnState = new TechTreeState(TeamId.CN, TechTreeRules.DEMO_BUDGET);
        UnlockL2TierGate(cnState);
        TechTreeRules.UnlockNode(cnState, "cn-l2-dim-sum-combo", db);
        TechTreeRules.SelectActiveNodes(cnState, new[] { "cn-l2-dim-sum-combo" }, db);

        // Play speed first — this breaks the sequence (trick must come first)
        TechTreeRules.TrackDimSumCombo(cnState, playedTrick: false, playedSpeed: true, paidHeat: false);
        // Then play trick — resets sequence (trick restarts)
        TechTreeRules.TrackDimSumCombo(cnState, playedTrick: true, playedSpeed: false, paidHeat: false);
        // Pay heat — but speed flag was cleared by the trick restart
        TechTreeRules.TrackDimSumCombo(cnState, playedTrick: false, playedSpeed: false, paidHeat: true);

        Assert.That(TechTreeRules.CheckDimSumCombo(cnState, db), Is.False);
    }

    [Test]
    public void test_jp_l2_broth_selection_passives()
    {
        var jpState = new TechTreeState(TeamId.JP, TechTreeRules.DEMO_BUDGET);
        UnlockL2TierGate(jpState);
        TechTreeRules.UnlockNode(jpState, "jp-l2-broth-selection", db);
        TechTreeRules.SelectActiveNodes(jpState, new[] { "jp-l2-broth-selection" }, db);

        // Select Tonkotsu (straight bonus)
        TechTreeRules.SelectBroth(jpState, BrothType.Tonkotsu);
        var mods = TechTreeRules.ComputeModifiers(jpState, db);
        Assert.That(mods.speedBonusStraight, Is.EqualTo(1)); // from broth

        // Select Shoyu (corner bonus)
        TechTreeRules.SelectBroth(jpState, BrothType.Shoyu);
        mods = TechTreeRules.ComputeModifiers(jpState, db);
        Assert.That(mods.cornerLimitBonus, Is.EqualTo(1)); // from broth

        // Select Shio (cooldown)
        TechTreeRules.SelectBroth(jpState, BrothType.Shio);
        Assert.That(TechTreeRules.GetBrothCooldownPerTurn(jpState), Is.EqualTo(1));
    }

    [Test]
    public void test_jp_l3_bankuruwase_trigger_condition()
    {
        var jpState = new TechTreeState(TeamId.JP, TechTreeRules.DEMO_BUDGET);
        TechTreeRules.UnlockNode(jpState, "jp-l3-bankuruwase", db);
        TechTreeRules.SelectActiveNodes(jpState, new[] { "jp-l3-bankuruwase" }, db);

        // 6 players, rank 6 (last) — should trigger
        Assert.That(TechTreeRules.ShouldTriggerBankuruwase(jpState, db, 6, 6), Is.True);

        // 6 players, rank 5 (second-to-last) — should trigger
        Assert.That(TechTreeRules.ShouldTriggerBankuruwase(jpState, db, 5, 6), Is.True);

        // 6 players, rank 4 (mid-field) — should NOT trigger
        Assert.That(TechTreeRules.ShouldTriggerBankuruwase(jpState, db, 4, 6), Is.False);
    }

    [Test]
    public void test_jp_l3_bankuruwase_expires_after_three_turns()
    {
        var jpState = new TechTreeState(TeamId.JP, TechTreeRules.DEMO_BUDGET);
        TechTreeRules.UnlockNode(jpState, "jp-l3-bankuruwase", db);
        TechTreeRules.SelectActiveNodes(jpState, new[] { "jp-l3-bankuruwase" }, db);

        TechTreeRules.ActivateBankuruwase(jpState);
        Assert.That(jpState.bankuruwaseActive, Is.True);
        Assert.That(jpState.bankuruwaseTurnsLeft, Is.EqualTo(3));

        // Tick 1
        Assert.That(TechTreeRules.TickBankuruwase(jpState), Is.True);
        Assert.That(jpState.bankuruwaseTurnsLeft, Is.EqualTo(2));

        // Tick 2
        Assert.That(TechTreeRules.TickBankuruwase(jpState), Is.True);
        Assert.That(jpState.bankuruwaseTurnsLeft, Is.EqualTo(1));

        // Tick 3 — last turn, expires after this
        Assert.That(TechTreeRules.TickBankuruwase(jpState), Is.False);
        Assert.That(jpState.bankuruwaseActive, Is.False);
        Assert.That(jpState.bankuruwaseTurnsLeft, Is.EqualTo(0));
    }

    [TestCase(BrothType.Tonkotsu)]
    [TestCase(BrothType.Shoyu)]
    [TestCase(BrothType.Miso)]
    [TestCase(BrothType.Shio)]
    public void BankuruwaseTemporarilyGrantsEachBrothPassiveOnce(BrothType chosenBroth)
    {
        var jpState = new TechTreeState(TeamId.JP, TechTreeRules.DEMO_BUDGET);
        jpState.activeNodeIds.Add("jp-l2-broth-selection");
        jpState.activeNodeIds.Add("jp-l3-bankuruwase");
        TechTreeRules.SelectBroth(jpState, chosenBroth);

        var before = TechTreeRules.ComputeModifiers(jpState, db);
        Assert.AreEqual(chosenBroth == BrothType.Tonkotsu ? 1 : 0, before.speedBonusStraight);
        Assert.AreEqual(chosenBroth == BrothType.Miso ? 1 : 0, before.slipstreamMovementBonus);

        TechTreeRules.ActivateBankuruwase(jpState);
        var during = TechTreeRules.ComputeModifiers(jpState, db);
        Assert.AreEqual(1, during.speedBonusStraight, "Tonkotsu must be available exactly once");
        Assert.AreEqual(1, during.slipstreamMovementBonus, "Miso must be available exactly once");
        Assert.AreEqual(chosenBroth == BrothType.Shoyu ? 1 : 0, during.cornerLimitBonus,
            "Shoyu's selected bonus stays separate from the rotor corner source");
        Assert.AreEqual(1, TechTreeRules.ComputeEffectiveCornerLimitBonus(
            during.cornerLimitBonus, 0, 0, 0, during.hasBankuruwase ? 1 : 0));
        Assert.AreEqual(1, TechTreeRules.GetBrothCooldownPerTurn(jpState),
            "The rotor grants Shio once even when Shio was not selected");
        Assert.AreEqual(2, TechTreeRules.GetBrothCooldownPerTurn(jpState) +
            TechTreeRules.GetBankuruwaseCooldownPerTurn(jpState),
            "Shio passive and the rotor's own cooling are distinct effects");

        for (int turn = 0; turn < TechTreeRules.BANKURUWASE_DURATION; turn++)
            TechTreeRules.TickBankuruwase(jpState);
        var after = TechTreeRules.ComputeModifiers(jpState, db);
        Assert.AreEqual(before.speedBonusStraight, after.speedBonusStraight);
        Assert.AreEqual(before.slipstreamMovementBonus, after.slipstreamMovementBonus);
        Assert.AreEqual(chosenBroth == BrothType.Shio ? 1 : 0,
            TechTreeRules.GetBrothCooldownPerTurn(jpState));
        Assert.AreEqual(0, TechTreeRules.GetBankuruwaseCooldownPerTurn(jpState));
    }

    [Test]
    public void BankuruwaseWithoutBrothSelectionStillGrantsTemporaryBroths()
    {
        var jpState = new TechTreeState(TeamId.JP, TechTreeRules.DEMO_BUDGET);
        jpState.activeNodeIds.Add("jp-l3-bankuruwase");
        TechTreeRules.ActivateBankuruwase(jpState);

        var modifiers = TechTreeRules.ComputeModifiers(jpState, db);
        Assert.IsFalse(modifiers.hasBrothSelection);
        Assert.AreEqual(1, modifiers.speedBonusStraight);
        Assert.AreEqual(1, modifiers.slipstreamMovementBonus);
        Assert.AreEqual(1, TechTreeRules.GetBrothCooldownPerTurn(jpState));
    }

    [Test]
    public void test_jp_l3_bankuruwase_cannot_reactivate_while_active()
    {
        var jpState = new TechTreeState(TeamId.JP, TechTreeRules.DEMO_BUDGET);
        TechTreeRules.UnlockNode(jpState, "jp-l3-bankuruwase", db);
        TechTreeRules.SelectActiveNodes(jpState, new[] { "jp-l3-bankuruwase" }, db);

        TechTreeRules.ActivateBankuruwase(jpState);
        Assert.That(jpState.bankuruwaseActive, Is.True);

        // Try to trigger again while active — should be denied
        Assert.That(TechTreeRules.ShouldTriggerBankuruwase(jpState, db, 5, 6), Is.False);
    }

    [Test]
    public void test_us_l3_mother_road_phases()
    {
        var usState = new TechTreeState(TeamId.US, TechTreeRules.DEMO_BUDGET);
        TechTreeRules.UnlockNode(usState, "us-l3-mother-road", db);
        TechTreeRules.SelectActiveNodes(usState, new[] { "us-l3-mother-road" }, db);

        // Pass 1: Prosperity
        var result = TechTreeRules.ResolveMotherRoadPass(usState, landmarkIndex: 0, totalLaps: 4);
        Assert.That(result.phase, Is.EqualTo(MotherRoadResult.MotherRoadPhase.Prosperity));
        Assert.That(result.freeCooldown, Is.EqualTo(2));
        Assert.That(usState.landmark1PassCount, Is.EqualTo(1));

        // Pass 2: Still Prosperity
        result = TechTreeRules.ResolveMotherRoadPass(usState, landmarkIndex: 0, totalLaps: 4);
        Assert.That(result.phase, Is.EqualTo(MotherRoadResult.MotherRoadPhase.Prosperity));
        Assert.That(usState.landmark1PassCount, Is.EqualTo(2));

        // Pass 3: Decline (needs repair)
        result = TechTreeRules.ResolveMotherRoadPass(usState, landmarkIndex: 0, totalLaps: 4);
        Assert.That(result.phase, Is.EqualTo(MotherRoadResult.MotherRoadPhase.Decline));
        Assert.That(result.needsRepair, Is.True);
    }

    [Test]
    public void test_us_mother_road_repairs_needed()
    {
        Assert.That(TechTreeRules.GetMotherRoadRepairsNeeded(3), Is.EqualTo(1));  // Short race
        Assert.That(TechTreeRules.GetMotherRoadRepairsNeeded(4), Is.EqualTo(2));  // Standard
        Assert.That(TechTreeRules.GetMotherRoadRepairsNeeded(5), Is.EqualTo(2));  // Standard
        Assert.That(TechTreeRules.GetMotherRoadRepairsNeeded(6), Is.EqualTo(3));  // Long race
    }

    [TestCase(0, 1)]
    [TestCase(1, 0)]
    public void test_us_mother_road_ultimate_is_once_per_race_across_both_landmarks(
        int firstLandmark, int secondLandmark)
    {
        var usState = new TechTreeState(TeamId.US);
        usState.landmark1PassCount = 2;
        usState.landmark2PassCount = 2;
        usState.totalRepairs = 2;

        var first = TechTreeRules.ResolveMotherRoadPass(usState, firstLandmark, 4);
        Assert.That(first.phase, Is.EqualTo(MotherRoadResult.MotherRoadPhase.Revival));
        Assert.That(TechTreeRules.UseMotherRoadUltimate(usState, firstLandmark, 2), Is.EqualTo(2));

        var second = TechTreeRules.ResolveMotherRoadPass(usState, secondLandmark, 4);
        Assert.That(second.phase, Is.EqualTo(MotherRoadResult.MotherRoadPhase.Decline));
        Assert.That(second.ultimateAvailable, Is.False);
        Assert.That(TechTreeRules.UseMotherRoadUltimate(usState, secondLandmark, 2), Is.Zero);
        Assert.That(usState.landmark1PassCount, Is.EqualTo(3));
        Assert.That(usState.landmark2PassCount, Is.EqualTo(3));
        Assert.That(usState.totalRepairs, Is.EqualTo(2));
    }

    [Test]
    public void test_us_mother_road_empty_hand_preserves_ultimate_until_later_pass_and_resets_next_race()
    {
        var usState = new TechTreeState(TeamId.US);
        usState.landmark1PassCount = 2;
        usState.landmark2PassCount = 2;
        usState.totalRepairs = 2;

        Assert.That(TechTreeRules.ResolveMotherRoadPass(usState, 0, 4).ultimateAvailable, Is.True);
        Assert.That(TechTreeRules.UseMotherRoadUltimate(usState, 0, 0), Is.Zero);
        Assert.That(usState.landmark1UltUsed, Is.False);
        Assert.That(usState.landmark2UltUsed, Is.False);
        Assert.That(TechTreeRules.ResolveMotherRoadPass(usState, 1, 4).ultimateAvailable, Is.True);
        Assert.That(TechTreeRules.UseMotherRoadUltimate(usState, 1, 3), Is.EqualTo(3));
        Assert.That(usState.landmark2UltUsed, Is.True);

        TechTreeRules.ResetPerRaceState(usState);
        Assert.That(usState.landmark1PassCount, Is.Zero);
        Assert.That(usState.landmark2PassCount, Is.Zero);
        Assert.That(usState.totalRepairs, Is.Zero);
        Assert.That(usState.landmark1UltUsed, Is.False);
        Assert.That(usState.landmark2UltUsed, Is.False);
    }

    [Test]
    public void test_us_landmark_positions()
    {
        var (lm1, lm2) = TechTreeRules.GetLandmarkPositions(60);
        Assert.That(lm1, Is.EqualTo(0));   // Start line
        Assert.That(lm2, Is.EqualTo(30));  // Midpoint
    }

    [Test]
    public void test_us_bbq_zone_check()
    {
        // Landmarks at 0 and 30 on a 60-cell track
        Assert.That(TechTreeRules.IsInBBQZone(3, 0, 30, 60), Is.True);   // Near start
        Assert.That(TechTreeRules.IsInBBQZone(28, 0, 30, 60), Is.True);  // Near midpoint
        Assert.That(TechTreeRules.IsInBBQZone(15, 0, 30, 60), Is.False); // Outside both zones
        Assert.That(TechTreeRules.IsInBBQZone(55, 0, 30, 60), Is.True);  // Wrap: near start (55→0=5)
    }

    // ═══════════════════════════════════════════════════════════════════
    // State Management
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_reset_per_race_state_clears_all_flags()
    {
        state.fishAndChipsUsed = true;
        state.brothSelection = BrothType.Tonkotsu;
        state.bankuruwaseActive = true;
        state.bankuruwaseTurnsLeft = 2;
        state.landmark1PassCount = 5;
        state.dimSumPlayedTrick = true;

        TechTreeRules.ResetPerRaceState(state);

        Assert.That(state.fishAndChipsUsed, Is.False);
        Assert.That(state.brothSelection, Is.EqualTo(BrothType.None));
        Assert.That(state.bankuruwaseActive, Is.False);
        Assert.That(state.bankuruwaseTurnsLeft, Is.Zero);
        Assert.That(state.landmark1PassCount, Is.Zero);
        Assert.That(state.dimSumPlayedTrick, Is.False);
    }

    [Test]
    public void test_select_active_nodes_only_unlocked()
    {
        TechTreeRules.UnlockNode(state, "common-l1-heat-coating", db);
        TechTreeRules.UnlockNode(state, "common-l1-lightweight-chassis", db);

        // Try to select one unlocked and one not-unlocked
        TechTreeRules.SelectActiveNodes(state,
            new[] { "common-l1-heat-coating", "common-l1-track-memory" }, db);

        Assert.That(state.IsActive("common-l1-heat-coating"), Is.True);
        Assert.That(state.IsActive("common-l1-track-memory"), Is.False, "Should not activate un-unlocked node");
    }

    [Test]
    public void test_select_active_nodes_accepts_its_current_set_as_input()
    {
        state.unlockedNodeIds.Add("common-l1-heat-coating");
        state.unlockedNodeIds.Add("common-l1-lightweight-chassis");
        state.activeNodeIds.Add("common-l1-heat-coating");
        state.activeNodeIds.Add("common-l1-lightweight-chassis");
        var originalSet = state.activeNodeIds;

        TechTreeRules.SelectActiveNodes(state, state.activeNodeIds, db);

        Assert.That(state.activeNodeIds, Is.SameAs(originalSet));
        CollectionAssert.AreEquivalent(new[]
        {
            "common-l1-heat-coating", "common-l1-lightweight-chassis"
        }, state.activeNodeIds);
    }

    [Test]
    public void test_select_active_nodes_accepts_lazy_view_of_its_current_set()
    {
        state.unlockedNodeIds.Add("common-l1-heat-coating");
        state.unlockedNodeIds.Add("common-l1-lightweight-chassis");
        state.activeNodeIds.Add("common-l1-heat-coating");
        state.activeNodeIds.Add("common-l1-lightweight-chassis");

        TechTreeRules.SelectActiveNodes(state,
            state.activeNodeIds.Where(id => id == "common-l1-heat-coating"), db);

        CollectionAssert.AreEquivalent(new[] { "common-l1-heat-coating" }, state.activeNodeIds);
    }

    [Test]
    public void test_select_active_nodes_ignores_null_empty_and_locked_candidates()
    {
        state.unlockedNodeIds.Add("common-l1-heat-coating");

        TechTreeRules.SelectActiveNodes(state,
            new[] { null, "", "common-l1-track-memory", "common-l1-heat-coating" }, db);

        CollectionAssert.AreEquivalent(new[] { "common-l1-heat-coating" }, state.activeNodeIds);
    }

    [Test]
    public void test_select_active_nodes_does_not_clear_current_set_when_input_iteration_fails()
    {
        state.unlockedNodeIds.Add("common-l1-heat-coating");
        state.activeNodeIds.Add("common-l1-heat-coating");

        Assert.Throws<System.InvalidOperationException>(() =>
            TechTreeRules.SelectActiveNodes(state, FailingActiveSelection(), db));

        CollectionAssert.AreEquivalent(new[] { "common-l1-heat-coating" }, state.activeNodeIds);
    }

    private static IEnumerable<string> FailingActiveSelection()
    {
        yield return "common-l1-heat-coating";
        throw new System.InvalidOperationException("Selection source failed");
    }

    [Test]
    public void test_activate_all_unlocked()
    {
        TechTreeRules.UnlockNode(state, "common-l1-heat-coating", db);
        TechTreeRules.UnlockNode(state, "common-l1-lightweight-chassis", db);
        TechTreeRules.UnlockNode(state, "common-l1-track-memory", db);

        TechTreeRules.ActivateAllUnlocked(state);

        Assert.That(state.activeNodeIds.Count, Is.EqualTo(3));
    }

    [Test]
    public void test_per_lap_heat_reduction_resets()
    {
        TechTreeRules.UnlockNode(state, "common-l1-heat-coating", db);
        TechTreeRules.SelectActiveNodes(state, new[] { "common-l1-heat-coating" }, db);

        Assert.That(TechTreeRules.GetHeatReductionThisLap(state, db), Is.EqualTo(1));

        TechTreeRules.ConsumeHeatReduction(state);
        Assert.That(TechTreeRules.GetHeatReductionThisLap(state, db), Is.Zero);

        TechTreeRules.ResetHeatReductionForLap(state);
        Assert.That(TechTreeRules.GetHeatReductionThisLap(state, db), Is.EqualTo(1));
    }

    [Test]
    public void test_uk_l3_sun_never_sets_target_techs()
    {
        TechTreeRules.UnlockNode(state, "uk-l3-sun-never-sets", db);
        TechTreeRules.SelectActiveNodes(state, new[] { "uk-l3-sun-never-sets" }, db);
        state.sunNeverSetsTarget = TeamId.DE;

        var granted = TechTreeRules.GetSunNeverSetsTargetTechs(state, db);
        Assert.That(granted.Count, Is.EqualTo(2)); // DE L2 + DE L3
        Assert.That(granted.Exists(n => n.id == "de-l2-wurstplatte"), Is.True);
        Assert.That(granted.Exists(n => n.id == "de-l3-grill-spezial"), Is.True);
    }

    [Test]
    public void test_distance_on_track()
    {
        Assert.That(TechTreeRules.DistanceOnTrack(0, 5, 60), Is.EqualTo(5));
        Assert.That(TechTreeRules.DistanceOnTrack(58, 2, 60), Is.EqualTo(4)); // Wrap: 58→0→2 = 4
        Assert.That(TechTreeRules.DistanceOnTrack(0, 0, 60), Is.EqualTo(0));
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void L2CommonRequiresThreeL1NodesForEveryTeam(TeamId team)
    {
        var profile = new TechTreeState(team, 100000);
        var l1 = db.GetCommonInTier(TechTreeTier.L1, team == TeamId.CN);
        var l2 = db.GetCommonInTier(TechTreeTier.L2, team == TeamId.CN);
        for (int count = 0; count < 3; count++)
        {
            foreach (var node in l2)
                Assert.IsFalse(TechTreeRules.CanUnlock(profile, node.id, db));
            Assert.IsTrue(TechTreeRules.UnlockNode(profile, l1[count].id, db));
        }
        Assert.IsTrue(TechTreeRules.CanUnlock(profile, l2[0].id, db));
        Assert.IsTrue(TechTreeRules.CanUnlock(profile, l2[4].id, db));
        Assert.IsFalse(TechTreeRules.CanUnlock(profile, l2[3].id, db), "Tier gate does not replace the individual predecessor");
    }

    [TestCase("CN", TeamId.CN)] [TestCase("DE", TeamId.DE)]
    [TestCase("IT", TeamId.IT)] [TestCase("US", TeamId.US)]
    [TestCase(" jp ", TeamId.JP)]
    public void SunNeverSetsUsesTrackCountry(string country, TeamId expected)
    {
        Assert.AreEqual(expected, TechTreeRules.ResolveSunNeverSetsTarget(country));
    }

    [TestCase("GB")] [TestCase("UK")] [TestCase("FR")]
    [TestCase("XX")] [TestCase(null)]
    public void SunNeverSetsHasNoInventedTargetForHomeOrUnknownCountry(string country)
    {
        Assert.IsNull(TechTreeRules.ResolveSunNeverSetsTarget(country));
    }

    [Test]
    public void SunNeverSetsVirtualEffectsAreSharedDeduplicatedAndDoNotPersist()
    {
        state.activeNodeIds.Add("uk-l3-sun-never-sets");
        state.activeNodeIds.Add("it-l2-lasagna");
        state.sunNeverSetsTarget = TeamId.IT;
        var before = new HashSet<string>(state.activeNodeIds);
        var modifiers = TechTreeRules.ComputeModifiers(state, db);
        Assert.AreEqual(1, modifiers.handSizeBonus);
        Assert.AreEqual(1, modifiers.engineCapacityBonus);
        Assert.IsTrue(TechTreeRules.HasEffect(state, db, TechEffectType.CavallinoRampante));
        CollectionAssert.AreEquivalent(before, state.activeNodeIds);
        Assert.IsEmpty(state.unlockedNodeIds);
        Assert.AreEqual(TechTreeRules.DEMO_BUDGET, state.rpBalance);
        state.activeNodeIds.Remove("uk-l3-sun-never-sets");
        Assert.IsFalse(TechTreeRules.HasEffect(state, db, TechEffectType.CavallinoRampante));
    }

    [Test]
    public void MisoAddsMovementNotTriggerDistance()
    {
        state.teamId = TeamId.JP;
        state.activeNodeIds.Add("jp-l2-broth-selection");
        state.brothSelection = BrothType.Miso;
        var modifiers = TechTreeRules.ComputeModifiers(state, db);
        Assert.AreEqual(1, modifiers.EffectiveSlipstreamRange);
        Assert.AreEqual(1, modifiers.slipstreamMovementBonus);
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void EffectiveQueriesPreserveSelectedOnlyProfiles(TeamId team)
    {
        state.teamId = team;
        foreach (var tier in new[] { TechTreeTier.L1, TechTreeTier.L2, TechTreeTier.L3 })
        {
            foreach (var node in db.GetUniqueInTier(team, tier)) state.activeNodeIds.Add(node.id);
            foreach (var node in db.GetCommonInTier(tier, team == TeamId.CN)) state.activeNodeIds.Add(node.id);
        }
        state.activeNodeIds.Add("unknown-saved-node");
        // A missing track target must not invent virtual grants, even for UK L3.
        state.sunNeverSetsTarget = null;
        AssertEffectiveQueriesMatchNodes(new HashSet<string>(state.activeNodeIds));
    }

    [TestCase(TeamId.DE)] [TestCase(TeamId.IT)] [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void EffectiveQueriesMergeOnlyTargetL2L3WithoutWritingProfile(TeamId target)
    {
        state.activeNodeIds.Add("uk-l3-sun-never-sets");
        state.activeNodeIds.Add("common-l1-track-memory");
        state.activeNodeIds.Add("common-l2-extreme-handling");
        state.activeNodeIds.Add("unknown-saved-node");
        state.unlockedNodeIds.Add("uk-l3-sun-never-sets");
        state.sunNeverSetsTarget = target;
        var beforeActive = new HashSet<string>(state.activeNodeIds);
        var beforeUnlocked = new HashSet<string>(state.unlockedNodeIds);
        var expected = new HashSet<string>(beforeActive);
        foreach (var tier in new[] { TechTreeTier.L2, TechTreeTier.L3 })
            foreach (var node in db.GetUniqueInTier(target, tier)) expected.Add(node.id);

        AssertEffectiveQueriesMatchNodes(expected);
        foreach (var node in db.GetUniqueInTier(target, TechTreeTier.L1))
            foreach (var effect in node.effects)
                Assert.IsFalse(TechTreeRules.HasEffect(state, db, effect.type), "L1 is not a Sun Never Sets grant");
        CollectionAssert.AreEquivalent(beforeActive, state.activeNodeIds);
        CollectionAssert.AreEquivalent(beforeUnlocked, state.unlockedNodeIds);
        Assert.AreEqual(TechTreeRules.DEMO_BUDGET, state.rpBalance);
    }

    [TestCase(TeamId.DE)] [TestCase(TeamId.IT)] [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void ForeignSunNeverSetsSelectionDoesNotGrantToNonUkHost(TeamId team)
    {
        state.teamId = team;
        state.activeNodeIds.Add("uk-l3-sun-never-sets");
        state.sunNeverSetsTarget = TeamId.IT;
        AssertEffectiveQueriesMatchNodes(new HashSet<string>(state.activeNodeIds));
        Assert.IsFalse(TechTreeRules.HasEffect(state, db, TechEffectType.CavallinoRampante));
        Assert.AreEqual(0, TechTreeRules.ComputeModifiers(state, db).handSizeBonus);
    }

    [Test]
    public void EffectiveQueriesObserveTargetAndSelectionChangesWithoutStaleGrants()
    {
        state.activeNodeIds.Add("uk-l3-sun-never-sets");
        state.sunNeverSetsTarget = TeamId.IT;
        Assert.AreEqual(1, TechTreeRules.ComputeModifiers(state, db).handSizeBonus);
        // Selecting a node already granted must not double additive capacity/hand size.
        state.activeNodeIds.Add("it-l2-lasagna");
        Assert.AreEqual(1, TechTreeRules.ComputeModifiers(state, db).handSizeBonus);
        Assert.AreEqual(1, TechTreeRules.ComputeModifiers(state, db).engineCapacityBonus);

        state.sunNeverSetsTarget = TeamId.US;
        Assert.IsFalse(TechTreeRules.HasEffect(state, db, TechEffectType.CavallinoRampante));
        Assert.IsTrue(TechTreeRules.HasEffect(state, db, TechEffectType.MotherRoad));
        Assert.AreEqual(1, TechTreeRules.ComputeModifiers(state, db).handSizeBonus, "Explicit selection survives target change");
        state.activeNodeIds.Remove("it-l2-lasagna");
        Assert.AreEqual(0, TechTreeRules.ComputeModifiers(state, db).handSizeBonus);
        state.activeNodeIds.Remove("uk-l3-sun-never-sets");
        Assert.IsFalse(TechTreeRules.HasEffect(state, db, TechEffectType.MotherRoad));
        state.activeNodeIds.Add("uk-l3-sun-never-sets");
        Assert.IsTrue(TechTreeRules.HasEffect(state, db, TechEffectType.MotherRoad));
        state.sunNeverSetsTarget = null;
        Assert.IsFalse(TechTreeRules.HasEffect(state, db, TechEffectType.MotherRoad));
    }

    [TestCase(BrothType.Tonkotsu, 1, 0, 0)]
    [TestCase(BrothType.Shoyu, 0, 1, 0)]
    [TestCase(BrothType.Miso, 0, 0, 1)]
    [TestCase(BrothType.Shio, 0, 0, 0)]
    public void VirtualBrothSelectionAppliesChosenNumericEffectOnce(
        BrothType broth, int straight, int corner, int slipstreamMove)
    {
        state.activeNodeIds.Add("uk-l3-sun-never-sets");
        state.activeNodeIds.Add("jp-l2-broth-selection");
        state.sunNeverSetsTarget = TeamId.JP;
        state.brothSelection = broth;
        var modifiers = TechTreeRules.ComputeModifiers(state, db);
        Assert.IsTrue(modifiers.hasBrothSelection);
        Assert.IsTrue(modifiers.hasBankuruwase);
        Assert.AreEqual(straight, modifiers.speedBonusStraight);
        Assert.AreEqual(corner, modifiers.cornerLimitBonus);
        Assert.AreEqual(slipstreamMove, modifiers.slipstreamMovementBonus);
        Assert.AreEqual(1, modifiers.EffectiveSlipstreamRange);
    }

    [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
    public void EffectiveQueriesKeepMissingDependencyDefaults(bool missingState, bool missingDatabase)
    {
        Assert.AreEqual(TechModifiers.Default,
            TechTreeRules.ComputeModifiers(missingState ? null : state, missingDatabase ? null : db));
        Assert.IsFalse(TechTreeRules.HasEffect(missingState ? null : state,
            missingDatabase ? null : db, TechEffectType.SunNeverSets));
    }

    [TestCase(false)] [TestCase(true)]
    public void VirtualEffectLookupRetainsOncePerRaceEligibility(bool used)
    {
        state.activeNodeIds.Add("uk-l3-sun-never-sets");
        state.sunNeverSetsTarget = TeamId.DE;
        state.grillSpezialUsed = used;
        Assert.IsTrue(TechTreeRules.HasEffect(state, db, TechEffectType.GrillSpezial));
        Assert.IsTrue(TechTreeRules.ComputeModifiers(state, db).hasGrillSpezial);
        Assert.AreEqual(!used, TechTreeRules.CanUseGrillSpezial(state, db));
        Assert.AreEqual(used, state.grillSpezialUsed);
    }

    // Independent snapshot oracle: selected and explicitly specified grant IDs,
    // not the production traversal. Aggregation itself is unchanged by this refactor.
    private void AssertEffectiveQueriesMatchNodes(HashSet<string> expectedIds)
    {
        var expectedModifiers = TechModifiers.Default;
        var expectedEffects = new HashSet<TechEffectType>();
        foreach (var id in expectedIds)
        {
            var node = db.Get(id);
            if (node == null) continue;
            foreach (var effect in node.effects)
            {
                expectedEffects.Add(effect.type);
                TechTreeRules.TryApplyModifierEffect(ref expectedModifiers, effect);
            }
        }
        Assert.AreEqual(expectedModifiers, TechTreeRules.ComputeModifiers(state, db));
        foreach (TechEffectType type in System.Enum.GetValues(typeof(TechEffectType)))
            Assert.AreEqual(expectedEffects.Contains(type), TechTreeRules.HasEffect(state, db, type), type.ToString());
        Assert.IsFalse(TechTreeRules.HasEffect(state, db, (TechEffectType)999));
    }
}

/// <summary>Actual Mother Road decline adapter without scene startup or persistence.</summary>
public class MotherRoadDeclineBoundaryTests
{
    private GameObject host;
    private GameConfigSO config;
    private MVPGameManager manager;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Mother Road decline boundary");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.totalLaps = 4;
        manager.config = config;
        typeof(MVPGameManager).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, new RaceSession(new SystemRandomSource(17)));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(config);
    }

    private void ConfigureTrack(
        int nodeCount, int startFinishIndex = -1, int pitEntryIndex = -1, int pitExitIndex = -1)
    {
        var track = host.AddComponent<TrackManager>();
        var nodes = new List<TrackNode>();
        for (int i = 0; i < nodeCount; i++)
            nodes.Add(new TrackNode(i, 99,
                isStartFinish: i == startFinishIndex,
                isPitEntry: i == pitEntryIndex,
                isPitExit: i == pitExitIndex));
        typeof(TrackManager).GetField("nodes", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(track, nodes);
        manager.trackManager = track;
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    public void DeclineOnlyRepairsWhenEngineCanPayWithoutSpinning(int engineHeat, bool repaired)
    {
        var player = new PlayerState("US", true, 12, 3)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US)
        };
        player.techState.landmark1PassCount = 2;
        player.deck.heatPool = new HeatPool(engineHeat);

        typeof(MVPGameManager).GetMethod("ResolveMotherRoadPass",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager,
            new object[] { player, 0, 7 });

        Assert.AreEqual(3, player.techState.landmark1PassCount);
        Assert.AreEqual(repaired ? 1 : 0, player.techState.totalRepairs);
        Assert.AreEqual(repaired ? 1 : 0, player.deck.CountHeatInHand());
        Assert.AreEqual(0, player.deck.heatPool.remaining);
        Assert.AreEqual(12, player.position);
        Assert.AreEqual(3, player.gear);
        Assert.AreEqual(0, player.spinCounter);
        Assert.IsFalse(player.skipNextTurn);
        Assert.IsFalse(player.isBlown);
    }

    [TestCase(false, 0, false)]
    [TestCase(true, 1, true)]
    public void RevivalMovesOnlyForHeatActuallyReturnedToEngine(
        bool includePermanentHeat, int expectedMovement, bool ultimateUsed)
    {
        ConfigureTrack(30);

        var player = new PlayerState("US", true, 12, 3)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US)
        };
        player.techState.landmark1PassCount = 2;
        player.techState.totalRepairs = 2;
        player.deck.heatPool = new HeatPool(3);
        if (includePermanentHeat)
            Assert.AreEqual(1, player.deck.DrawHeatFromPoolToHand(1));
        player.deck.AddCardsToHand(new List<CardData> { CardData.CreateTempHeat() });
        int poolBefore = player.deck.heatPool.remaining;

        typeof(MVPGameManager).GetMethod("ResolveMotherRoadPass",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager,
            new object[] { player, 0, 7 });

        Assert.AreEqual(12 + expectedMovement, player.position);
        Assert.AreEqual(poolBefore + expectedMovement, player.deck.heatPool.remaining);
        Assert.AreEqual(ultimateUsed, player.techState.landmark1UltUsed);
        Assert.AreEqual(includePermanentHeat ? 0 : 1, player.deck.CountHeatInHand(),
            "No valid conversion must leave the once-per-race opportunity and hand intact");
    }

    [Test]
    public void RevivalRegionalDebtOnlyMovesForNetEngineReturn()
    {
        ConfigureTrack(30);

        var player = new PlayerState("US", true, 12, 3)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US)
        };
        player.techState.landmark1PassCount = 2;
        player.techState.totalRepairs = 2;
        player.deck.heatPool = new HeatPool(1);
        player.deck.SetRegionalCapacityBonus(2);
        Assert.AreEqual(3, player.deck.DrawHeatFromPoolToHand(3));
        player.deck.SetRegionalCapacityBonus(0);

        typeof(MVPGameManager).GetMethod("ResolveMotherRoadPass",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager,
            new object[] { player, 0, 7 });

        Assert.AreEqual(13, player.position);
        Assert.AreEqual(1, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.RegionalHeatPendingRetirement);
        Assert.AreEqual(0, player.deck.CountHeatInHand());
        Assert.IsTrue(player.techState.landmark1UltUsed);
    }

    [TestCase(4, false)]
    [TestCase(2, true)]
    public void RevivalInstantMovementRegistersCrossedFinishOnce(int totalLaps, bool finishes)
    {
        ConfigureTrack(10, startFinishIndex: 0);
        config.totalLaps = totalLaps;
        var player = new PlayerState("US", true, 9, 3)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US),
            lap = 1
        };
        player.techState.landmark2PassCount = 2;
        player.techState.totalRepairs = 2;
        player.deck.heatPool = new HeatPool(3);
        Assert.AreEqual(2, player.deck.DrawHeatFromPoolToHand(2));
        var session = (RaceSession)typeof(MVPGameManager)
            .GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        session.Players.Add(player);

        typeof(MVPGameManager).GetMethod("ResolveMotherRoadPass",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager,
            new object[] { player, 1, 4 });

        Assert.AreEqual(1, player.position);
        Assert.AreEqual(2, player.lap);
        Assert.AreEqual(finishes, player.hasFinished);
        Assert.AreEqual(finishes ? 1 : 0, player.finishOrder);
        Assert.AreEqual(3, player.deck.heatPool.remaining);
        Assert.IsTrue(player.techState.landmark2UltUsed);
    }

    [Test]
    public void ChinaGoInstantMovementKeepsItsExistingFinishSettlement()
    {
        ConfigureTrack(10, startFinishIndex: 0);
        var player = new PlayerState("CN", true, 9, 3)
        {
            teamId = TeamId.CN,
            techState = new TechTreeState(TeamId.CN),
            lap = 1,
            positionAtTurnStart = 9
        };
        player.deck.heatPool = new HeatPool(2);
        var session = (RaceSession)typeof(MVPGameManager)
            .GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        session.Players.Add(player);

        typeof(MVPGameManager).GetMethod("ApplyYinYang",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager,
            new object[] { player, YinYangResult.Yin });

        Assert.AreEqual(0, player.position);
        Assert.AreEqual(2, player.lap);
        Assert.IsFalse(player.hasFinished);
        Assert.AreEqual(1, player.deck.heatPool.remaining);
        Assert.AreEqual(1, player.deck.CountHeatInDiscardPile());
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void RevivalInstantMovementSettlesPitEntryReservation(
        bool requested, bool scheduled)
    {
        ConfigureTrack(20, pitEntryIndex: 10, pitExitIndex: 15);
        config.enablePitLane = true;
        var player = new PlayerState("US pit", true, 8, 3)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US),
            pitStopRequested = requested,
            pitChoiceResolvedThisLap = true
        };
        player.techState.landmark1PassCount = 2;
        player.techState.totalRepairs = 2;
        player.deck.heatPool = new HeatPool(3);
        Assert.AreEqual(3, player.deck.DrawHeatFromPoolToHand(3));

        typeof(MVPGameManager).GetMethod("ResolveMotherRoadPass",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager,
            new object[] { player, 0, 4 });

        Assert.AreEqual(11, player.position);
        Assert.IsFalse(player.pitStopRequested);
        Assert.AreEqual(scheduled, player.pitStopScheduled);
        Assert.AreEqual(scheduled, player.pitChoiceResolvedThisLap);
        Assert.AreEqual(3, player.deck.heatPool.remaining);
    }

    [Test]
    public void ChinaGoInstantMovementSettlesPitEntryReservation()
    {
        ConfigureTrack(20, pitEntryIndex: 10, pitExitIndex: 15);
        config.enablePitLane = true;
        var player = new PlayerState("CN pit", true, 9, 3)
        {
            teamId = TeamId.CN,
            techState = new TechTreeState(TeamId.CN),
            positionAtTurnStart = 9,
            pitStopRequested = true,
            pitChoiceResolvedThisLap = true
        };
        player.deck.heatPool = new HeatPool(2);

        typeof(MVPGameManager).GetMethod("ApplyYinYang",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager,
            new object[] { player, YinYangResult.Yin });

        Assert.AreEqual(10, player.position);
        Assert.IsFalse(player.pitStopRequested);
        Assert.IsTrue(player.pitStopScheduled);
        Assert.AreEqual(1, player.deck.heatPool.remaining);
        Assert.AreEqual(1, player.deck.CountHeatInDiscardPile());
    }

    [Test]
    public void RevivalCrossingDoesNotCancelPreviouslyScheduledPitStop()
    {
        ConfigureTrack(20, pitEntryIndex: 10, pitExitIndex: 15);
        config.enablePitLane = true;
        var player = new PlayerState("US scheduled pit", true, 8, 3)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US),
            pitStopScheduled = true,
            pitChoiceResolvedThisLap = true
        };
        player.techState.landmark1PassCount = 2;
        player.techState.totalRepairs = 2;
        player.deck.heatPool = new HeatPool(3);
        Assert.AreEqual(3, player.deck.DrawHeatFromPoolToHand(3));

        typeof(MVPGameManager).GetMethod("ResolveMotherRoadPass",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager,
            new object[] { player, 0, 4 });

        Assert.AreEqual(11, player.position);
        Assert.IsTrue(player.pitStopScheduled);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(3, player.deck.heatPool.remaining);
    }

    [TestCase(2, true, 0)]
    [TestCase(4, false, 1)]
    public void RevivalFinishStopsLaterLandmarkPassInSameMovement(
        int totalLaps, bool finishes, int secondLandmarkPasses)
    {
        ConfigureTrack(20, startFinishIndex: 12);
        config.enableTechTree = true;
        config.totalLaps = totalLaps;
        var player = new PlayerState("US two landmarks", true, 11, 3)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US),
            lap = 1
        };
        player.techState.activeNodeIds.Add("us-l3-mother-road");
        player.techState.landmark1PassCount = 2;
        player.techState.totalRepairs = 2;
        player.deck.heatPool = new HeatPool(2);
        Assert.AreEqual(2, player.deck.DrawHeatFromPoolToHand(2));
        var session = (RaceSession)typeof(MVPGameManager)
            .GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        session.Players.Add(player);

        // The base movement 19 -> 31 crossed landmarks 0 and 10 and ended at 11.
        // Revival at landmark 0 adds two cells, crossing finish at 12.
        typeof(MVPGameManager).GetMethod("ResolveLandmarkPasses",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager,
            new object[] { player, 19, 31 });

        Assert.AreEqual(13, player.position);
        Assert.AreEqual(2, player.lap);
        Assert.AreEqual(finishes, player.hasFinished);
        Assert.AreEqual(3, player.techState.landmark1PassCount);
        Assert.AreEqual(secondLandmarkPasses, player.techState.landmark2PassCount);
        Assert.IsTrue(player.techState.landmark1UltUsed);
    }

    [Test]
    public void RevivalAtMiddleLandmarkFinishesBeforeLaterStartLandmarkPass()
    {
        ConfigureTrack(20, startFinishIndex: 3);
        config.enableTechTree = true;
        config.totalLaps = 2;
        var player = new PlayerState("US middle first", true, 2, 3)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US),
            lap = 1
        };
        player.techState.activeNodeIds.Add("us-l3-mother-road");
        player.techState.landmark2PassCount = 2;
        player.techState.totalRepairs = 2;
        player.deck.heatPool = new HeatPool(2);
        Assert.AreEqual(2, player.deck.DrawHeatFromPoolToHand(2));
        var session = (RaceSession)typeof(MVPGameManager)
            .GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        session.Players.Add(player);

        // Base movement 5 -> 22 reaches middle landmark 10 before start landmark 0.
        // Revival at 10 adds two cells from position 2 and crosses finish at 3.
        typeof(MVPGameManager).GetMethod("ResolveLandmarkPasses",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager,
            new object[] { player, 5, 22 });

        Assert.AreEqual(4, player.position);
        Assert.AreEqual(2, player.lap);
        Assert.IsTrue(player.hasFinished);
        Assert.AreEqual(0, player.techState.landmark1PassCount);
        Assert.AreEqual(3, player.techState.landmark2PassCount);
        Assert.IsTrue(player.techState.landmark2UltUsed);
    }
}
