using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Unit tests for TrickCardRules — database integrity, play validation,
/// and effect resolution for all 12 trick cards.
/// </summary>
public class TrickCardRulesTests
{
    private TrickCardDatabase db;
    private TrickCardState state;

    [SetUp]
    public void SetUp()
    {
        db = TrickCardDatabaseFactory.CreateDefault();
        state = new TrickCardState();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Database Integrity
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_database_has_12_cards()
    {
        Assert.That(db.cards.Count, Is.EqualTo(12));
    }

    [Test]
    public void test_each_team_has_attack_and_defense()
    {
        foreach (TeamId team in System.Enum.GetValues(typeof(TeamId)))
        {
            var cards = db.GetForTeam(team);
            Assert.That(cards.Count, Is.EqualTo(2), $"Team {team} should have 2 trick cards");

            bool hasAttack = cards.Exists(c => c.IsAttack);
            bool hasDefense = cards.Exists(c => c.IsDefense);
            Assert.That(hasAttack, Is.True, $"Team {team} missing attack card");
            Assert.That(hasDefense, Is.True, $"Team {team} missing defense card");
        }
    }

    [Test]
    public void test_trick_ids_are_unique()
    {
        var seen = new HashSet<string>();
        foreach (var kv in db.cards)
        {
            Assert.That(seen.Contains(kv.Key), Is.False, $"Duplicate trick ID: {kv.Key}");
            seen.Add(kv.Key);
        }
    }

    [Test]
    public void test_create_initial_trick_cards_returns_four()
    {
        var cards = TrickCardRules.CreateInitialTrickCards(TeamId.UK, db);
        Assert.That(cards.Count, Is.EqualTo(4));
        Assert.That(cards.FindAll(c => c.IsTrick).Count, Is.EqualTo(4));
    }

    [Test]
    public void test_initial_trick_cards_two_attack_two_defense()
    {
        var cards = TrickCardRules.CreateInitialTrickCards(TeamId.DE, db);
        string attackId = db.GetAttackId(TeamId.DE);
        string defenseId = db.GetDefenseId(TeamId.DE);

        int attackCount = cards.FindAll(c => c.trickId == attackId).Count;
        int defenseCount = cards.FindAll(c => c.trickId == defenseId).Count;

        Assert.That(attackCount, Is.EqualTo(2));
        Assert.That(defenseCount, Is.EqualTo(2));
    }

    [Test]
    public void test_china_initial_trick_cards_include_hotpot_attack()
    {
        var cards = TrickCardRules.CreateInitialTrickCards(TeamId.CN, db);
        string attackId = db.GetAttackId(TeamId.CN);

        Assert.That(attackId, Is.EqualTo("cn-hotpot-base"));
        Assert.That(cards.FindAll(c => c.trickId == attackId).Count, Is.EqualTo(2));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Play Validation
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_can_play_one_trick_per_turn()
    {
        Assert.That(TrickCardRules.CanPlayTrick(state), Is.True);

        state.trickPlayedThisTurn = true;
        Assert.That(TrickCardRules.CanPlayTrick(state), Is.False);
    }

    [Test]
    public void test_cannot_play_during_kanto_oden_skip()
    {
        state.kantoOdenActive = true;
        Assert.That(TrickCardRules.CanPlayTrick(state), Is.False);
    }

    [Test]
    public void test_hotpot_base_requires_go_mode()
    {
        var def = db.Get("cn-hotpot-base");
        Assert.That(TrickCardRules.CanPlaySpecificTrick(def, isGoMode: true, isRecoverMode: false), Is.True);
        Assert.That(TrickCardRules.CanPlaySpecificTrick(def, isGoMode: false, isRecoverMode: false), Is.False);
    }

    [Test]
    public void test_ice_jelly_requires_recover_mode()
    {
        var def = db.Get("cn-ice-jelly");
        Assert.That(TrickCardRules.CanPlaySpecificTrick(def, isGoMode: false, isRecoverMode: true), Is.True);
        Assert.That(TrickCardRules.CanPlaySpecificTrick(def, isGoMode: false, isRecoverMode: false), Is.False);
    }

    [Test]
    public void test_unconditional_tricks_always_playable()
    {
        var def = db.Get("uk-scone");
        Assert.That(TrickCardRules.CanPlaySpecificTrick(def, isGoMode: false, isRecoverMode: false), Is.True);
    }

    // ═══════════════════════════════════════════════════════════════════
    // UK: Scone (Attack) — pay 1 heat from engine → +2 move
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_scone_success_with_engine_heat()
    {
        var def = db.Get("uk-scone");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: true, hasHeatInHand: false, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.True);
        Assert.That(result.heatToPay, Is.EqualTo(1));
        Assert.That(result.extraMovement, Is.EqualTo(2));
    }

    [Test]
    public void test_scone_fails_without_engine_heat()
    {
        var def = db.Get("uk-scone");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: false, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.False);
    }

    // ═══════════════════════════════════════════════════════════════════
    // UK: English Breakfast Tea (Defense) — cool 1 heat from hand
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_tea_cools_heat_from_hand()
    {
        var def = db.Get("uk-english-breakfast-tea");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: true, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.True);
        Assert.That(result.heatToCool, Is.EqualTo(1));
    }

    [Test]
    public void test_tea_fails_without_hand_heat()
    {
        var def = db.Get("uk-english-breakfast-tea");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: false, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.False);
    }

    // ═══════════════════════════════════════════════════════════════════
    // DE: Sauerkraut (Attack) — crossed corner → +2; else +1
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_sauerkraut_sets_flag()
    {
        var def = db.Get("de-sauerkraut");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: false, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.True);
        Assert.That(state.sauerkrautPlayed, Is.True);
    }

    [Test]
    public void test_sauerkraut_bonus_with_corner()
    {
        state.sauerkrautPlayed = true;
        Assert.That(TrickCardRules.GetSauerkrautBonus(state, crossedCorner: true), Is.EqualTo(2));
    }

    [Test]
    public void test_sauerkraut_bonus_without_corner()
    {
        state.sauerkrautPlayed = true;
        Assert.That(TrickCardRules.GetSauerkrautBonus(state, crossedCorner: false), Is.EqualTo(1));
    }

    [Test]
    public void test_sauerkraut_no_bonus_if_not_played()
    {
        Assert.That(TrickCardRules.GetSauerkrautBonus(state, crossedCorner: true), Is.Zero);
    }

    // ═══════════════════════════════════════════════════════════════════
    // DE: Schwarzbrot (Defense) — next heat payment -1 (min 1)
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_schwarzbrot_reduces_heat_payment()
    {
        var def = db.Get("de-schwarzbrot");
        TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: false, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(state.schwarzbrotActive, Is.True);

        int reduced = TrickCardRules.ApplySchwarzbrot(state, 3);
        Assert.That(reduced, Is.EqualTo(2));
    }

    [Test]
    public void test_schwarzbrot_keeps_min_one()
    {
        state.schwarzbrotActive = true;
        state.schwarzbrotRemaining = 1;

        int reduced = TrickCardRules.ApplySchwarzbrot(state, 1);
        Assert.That(reduced, Is.EqualTo(1)); // min 1, even with reduction
    }

    [Test]
    public void test_schwarzbrot_single_use()
    {
        state.schwarzbrotActive = true;
        state.schwarzbrotRemaining = 1;

        TrickCardRules.ApplySchwarzbrot(state, 3);
        Assert.That(state.schwarzbrotActive, Is.False); // Consumed
    }

    // ═══════════════════════════════════════════════════════════════════
    // IT: Parmigiano (Attack) — slipstream +2 this turn
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_parmigiano_adds_slipstream_bonus()
    {
        var def = db.Get("it-parmigiano");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: false, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.True);
        Assert.That(result.slipstreamBonus, Is.EqualTo(2));
        Assert.That(state.parmigianoActive, Is.True);
        Assert.That(TrickCardRules.GetParmigianoBonus(state), Is.EqualTo(2));
    }

    // ═══════════════════════════════════════════════════════════════════
    // IT: Chianti (Defense) — discard 1 speed → cool 1 heat
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_chianti_requires_speed_card()
    {
        var def = db.Get("it-chianti");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: true, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.False);
    }

    [Test]
    public void test_chianti_succeeds_with_speed_card()
    {
        var def = db.Get("it-chianti");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: true, hasSpeedInHand: true,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.True);
        Assert.That(result.requiresSpeedDiscard, Is.True);
        Assert.That(result.heatToCool, Is.EqualTo(1));
    }

    // ═══════════════════════════════════════════════════════════════════
    // US: Fries (Attack) — crossed landmark → temp heat
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_fries_requires_landmark()
    {
        var def = db.Get("us-fries");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: false, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.False);
    }

    [Test]
    public void test_fries_gives_temp_heat_when_landmark_crossed()
    {
        var def = db.Get("us-fries");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: false, hasSpeedInHand: false,
            crossedLandmarkLastTurn: true);

        Assert.That(result.success, Is.True);
        Assert.That(TrickCardRules.HasTempHeat(state), Is.True);

        TrickCardRules.ConsumeTempHeat(state);
        Assert.That(TrickCardRules.HasTempHeat(state), Is.False);
    }

    // ═══════════════════════════════════════════════════════════════════
    // US: Cola (Defense) — crossed landmark → draw 1
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_cola_requires_landmark()
    {
        var def = db.Get("us-cola");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: false, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.False);
    }

    [Test]
    public void test_cola_draws_when_landmark_crossed()
    {
        var def = db.Get("us-cola");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: false, hasSpeedInHand: false,
            crossedLandmarkLastTurn: true);

        Assert.That(result.success, Is.True);
        Assert.That(result.cardsToDraw, Is.EqualTo(1));
    }

    // ═══════════════════════════════════════════════════════════════════
    // CN: Hotpot Base (Attack) — empower the next normal speed card
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_hotpot_base_activates_flag()
    {
        var def = db.Get("cn-hotpot-base");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: false, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.True);
        Assert.That(TrickCardRules.HasHotpotAttack(state), Is.True);
        Assert.That(TrickCardRules.GetHotpotSpeedBonus(), Is.EqualTo(1));

        Assert.That(TrickCardRules.ConsumeHotpotAttack(state), Is.True);
        Assert.That(TrickCardRules.HasHotpotAttack(state), Is.False);
        Assert.That(TrickCardRules.ConsumeHotpotAttack(state), Is.False);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CN: Ice Jelly (Defense) — block slipstream
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_ice_jelly_blocks_slipstream()
    {
        var def = db.Get("cn-ice-jelly");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: false, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.True);
        Assert.That(TrickCardRules.IsIceJellyActive(state), Is.True);
    }

    // ═══════════════════════════════════════════════════════════════════
    // JP: Torpedo Tempura (Attack) — overtake bonus
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_torpedo_tempura_activates()
    {
        var def = db.Get("jp-torpedo-tempura");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: false, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.True);
        Assert.That(TrickCardRules.IsTorpedoTempuraActive(state), Is.True);
        Assert.That(TrickCardRules.GetTorpedoOvertakeBonus(), Is.EqualTo(1));
    }

    // ═══════════════════════════════════════════════════════════════════
    // JP: Kanto Oden (Defense) — skip turn, accumulate
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_kanto_oden_skips_turn()
    {
        var def = db.Get("jp-kanto-oden");
        var result = TrickCardRules.ResolvePlay(def, state,
            hasHeatInEngine: false, hasHeatInHand: true, hasSpeedInHand: false,
            crossedLandmarkLastTurn: false);

        Assert.That(result.success, Is.True);
        Assert.That(state.kantoOdenActive, Is.True);
        Assert.That(TrickCardRules.ShouldSkipTurn(state), Is.True);
        Assert.That(result.heatToCool, Is.EqualTo(1)); // Slow cooking: cool 1
    }

    [Test]
    public void test_kanto_oden_accumulates_and_consumes()
    {
        state.kantoOdenActive = true;
        TrickCardRules.AccumulateKantoOden(state, gear: 3);
        TrickCardRules.AccumulateKantoOden(state, gear: 2); // If re-triggered somehow

        Assert.That(state.kantoOdenAccumulatedCards, Is.EqualTo(5));

        int consumed = TrickCardRules.ConsumeKantoOden(state);
        Assert.That(consumed, Is.EqualTo(5));
        Assert.That(state.kantoOdenActive, Is.False);
        Assert.That(state.kantoOdenAccumulatedCards, Is.Zero);
    }

    // ═══════════════════════════════════════════════════════════════════
    // State Reset
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_reset_per_turn_clears_modifiers()
    {
        state.trickPlayedThisTurn = true;
        state.schwarzbrotActive = true;
        state.sauerkrautPlayed = true;
        state.parmigianoActive = true;
        state.torpedoTempuraActive = true;
        state.iceJellyActive = true;
        state.hotpotBaseActive = true;

        state.ResetPerTurn();

        Assert.That(state.trickPlayedThisTurn, Is.False);
        Assert.That(state.schwarzbrotActive, Is.False);
        Assert.That(state.sauerkrautPlayed, Is.False);
        Assert.That(state.parmigianoActive, Is.False);
        Assert.That(state.torpedoTempuraActive, Is.False);
        Assert.That(state.iceJellyActive, Is.False);
        Assert.That(state.hotpotBaseActive, Is.False);

        // Kanto Oden persists across turns
    }

    [Test]
    public void test_reset_per_race_clears_all()
    {
        state.kantoOdenActive = true;
        state.kantoOdenAccumulatedCards = 5;
        state.crossedLandmarkLastTurn = true;
        state.tempHeatAvailable = true;

        state.ResetPerRace();

        Assert.That(state.kantoOdenActive, Is.False);
        Assert.That(state.kantoOdenAccumulatedCards, Is.Zero);
        Assert.That(state.crossedLandmarkLastTurn, Is.False);
        Assert.That(state.tempHeatAvailable, Is.False);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CardData integration
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_trick_card_data_is_trick_type()
    {
        var card = CardData.CreateTrick("uk-scone");
        Assert.That(card.IsTrick, Is.True);
        Assert.That(card.IsSpeed, Is.False);
        Assert.That(card.IsHeat, Is.False);
        Assert.That(card.trickId, Is.EqualTo("uk-scone"));
        Assert.That(card.value, Is.Zero);
    }

    [Test]
    public void test_speed_card_is_not_trick()
    {
        var card = new CardData(CardType.Speed, 3);
        Assert.That(card.IsTrick, Is.False);
        Assert.That(card.IsSpeed, Is.True);
    }

    [Test]
    public void test_heat_card_is_not_trick()
    {
        var card = new CardData(CardType.Heat, 0);
        Assert.That(card.IsTrick, Is.False);
        Assert.That(card.IsHeat, Is.True);
    }
}
