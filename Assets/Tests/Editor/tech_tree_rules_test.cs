using System.Collections.Generic;
using NUnit.Framework;

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

    // ═══════════════════════════════════════════════════════════════════
    // Test helpers — satisfy tier gates for unique tech tests
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Unlock 3 cheapest L1 common nodes to satisfy the L1 tier gate.</summary>
    private void UnlockL1TierGate(TechTreeState s)
    {
        Assert.That(TechTreeRules.UnlockNode(s, "common-l1-heat-coating", db), Is.True);
        Assert.That(TechTreeRules.UnlockNode(s, "common-l1-lightweight-chassis", db), Is.True);
        Assert.That(TechTreeRules.UnlockNode(s, "common-l1-track-memory", db), Is.True);
    }

    /// <summary>Unlock L1 prereqs + 3 L2 common nodes to satisfy the L2 tier gate.</summary>
    private void UnlockL2TierGate(TechTreeState s)
    {
        // Bump RP budget — demo 25000 isn't enough for 3×L1 common + 3×L2 common + L2 unique
        s.rpBalance = 50000;
        UnlockL1TierGate(s);
        Assert.That(TechTreeRules.UnlockNode(s, "common-l2-ceramic-coating", db), Is.True);
        Assert.That(TechTreeRules.UnlockNode(s, "common-l2-carbon-fiber", db), Is.True);
        Assert.That(TechTreeRules.UnlockNode(s, "common-l2-extreme-handling", db), Is.True);
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

        TechTreeRules.ActivateGrillSpezial(deState);
        TechTreeRules.TrackGrillSpezialHeat(deState, 3);
        TechTreeRules.TrackGrillSpezialHeat(deState, 2);

        Assert.That(TechTreeRules.GetGrillSpezialCooldown(deState), Is.EqualTo(5));
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
}
