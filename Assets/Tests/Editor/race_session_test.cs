using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// RaceSession 单元测试 — 5 系统接入后的纯 C# 聚合层。
/// 覆盖：demo 科技状态、弯道限速修正（科技+天气）、特技牌校验、移动加成、排名。
/// </summary>
public class RaceSessionTest
{
    private const int BASE_LIMIT = 3;
    private const int BASE_HAND_SIZE = 7;
    private const int BASE_POOL_SIZE = 6;

    private RaceSession CreateSession(int seed = 7)
    {
        return new RaceSession(new SystemRandomSource(seed));
    }

    private PlayerState CreatePlayer(RaceSession session, TeamId team, int startPos = 0)
    {
        var p = new PlayerState("P", false, startPos, 1) { teamId = team };
        p.deck.heatPool = new HeatPool(5); // 测试用引擎热量池
        if (session != null && session.TechDb != null)
            p.techState = session.CreateDemoTechState(team);
        return p;
    }

    private CardData GiveTrick(PlayerState player, string trickId)
    {
        var card = CardData.CreateTrick(trickId);
        player.deck.AddCardsToHand(new List<CardData> { card });
        return card;
    }

    // ===== Demo 科技状态 =====

    [TestCase(TeamId.US, 3, true)]
    [TestCase(TeamId.US, 4, false)]
    [TestCase(TeamId.US, 24, true)]
    [TestCase(TeamId.UK, 33, true)]
    [TestCase(TeamId.UK, 34, false)]
    [TestCase(TeamId.UK, 54, true)]
    public void BBQSmokeUsesLeaderEndPositionAndCannotBeBypassed(TeamId leaderTeam, int start, bool blocked)
    {
        var session = CreateSession();
        session.TeamVehicleBonusesEnabled = false;
        var follower = AddRacer(session, "Follower", start - 1, TeamId.JP, false);
        var leader = AddRacer(session, "Smoke", start, leaderTeam);
        var distant = AddRacer(session, "Distant", start + 1, TeamId.IT);
        follower.techState = new TechTreeState(TeamId.JP);
        follower.slipstreamRangeBonusThisTurn = 1;
        leader.techState = new TechTreeState(leaderTeam);
        leader.techState.activeNodeIds.Add(leaderTeam == TeamId.UK ? "uk-l3-sun-never-sets" : "us-l2-smoked-bbq");
        leader.techState.sunNeverSetsTarget = TeamId.US;
        var settled = new Dictionary<PlayerState, int> { { follower, 2 }, { leader, 2 }, { distant, 2 } };
        var chain = session.ComputeSlipstreamChain(follower, session.Players, 60, settled);
        Assert.AreEqual(!blocked, chain.Triggered);
        if (blocked) Assert.AreEqual(0, chain.TotalBonus);
    }

    [TestCase(1, 3)]
    [TestCase(2, 0)]
    public void MisoChangesSlipstreamMovementButNotEligibility(int gap, int expected)
    {
        var session = CreateSession();
        session.TeamVehicleBonusesEnabled = false;
        var follower = AddRacer(session, "Miso", 10, TeamId.JP, false);
        var leader = AddRacer(session, "Leader", 10 + gap, TeamId.DE);
        follower.techState = new TechTreeState(TeamId.JP);
        follower.techState.activeNodeIds.Add("jp-l2-broth-selection");
        follower.techState.brothSelection = BrothType.Miso;
        follower.cornerTotalThisTurn = leader.cornerTotalThisTurn = 3;
        Assert.AreEqual(expected, session.ComputeSlipstreamBonus(follower, session.Players, 60));
    }

    [Test]
    public void BankuruwaseTemporaryBrothsReachMovementCornerAndSlipstreamResolution()
    {
        var session = CreateSession();
        session.TeamVehicleBonusesEnabled = false;
        var follower = AddRacer(session, "Rotor", 10, TeamId.JP, false);
        var leader = AddRacer(session, "Leader", 11, TeamId.DE);
        follower.techState = new TechTreeState(TeamId.JP);
        follower.techState.activeNodeIds.Add("jp-l2-broth-selection");
        follower.techState.activeNodeIds.Add("jp-l3-bankuruwase");
        follower.techState.brothSelection = BrothType.Shio;
        follower.cornerTotalThisTurn = leader.cornerTotalThisTurn = 3;

        Assert.AreEqual(0, session.ComputeMovementBonus(follower, false));
        Assert.AreEqual(BASE_LIMIT, session.EffectiveCornerLimit(follower, BASE_LIMIT));
        Assert.AreEqual(2, session.ComputeSlipstreamBonus(follower, session.Players, 60));

        TechTreeRules.ActivateBankuruwase(follower.techState);
        Assert.AreEqual(1, session.ComputeMovementBonus(follower, false));
        Assert.AreEqual(0, session.ComputeMovementBonus(follower, true));
        Assert.AreEqual(BASE_LIMIT + 1, session.EffectiveCornerLimit(follower, BASE_LIMIT));
        Assert.AreEqual(3, session.ComputeSlipstreamBonus(follower, session.Players, 60));

        for (int turn = 0; turn < TechTreeRules.BANKURUWASE_DURATION; turn++)
            TechTreeRules.TickBankuruwase(follower.techState);
        Assert.AreEqual(0, session.ComputeMovementBonus(follower, false));
        Assert.AreEqual(BASE_LIMIT, session.EffectiveCornerLimit(follower, BASE_LIMIT));
        Assert.AreEqual(2, session.ComputeSlipstreamBonus(follower, session.Players, 60));
    }

    [Test]
    public void test_sun_never_sets_applies_target_team_flags_through_shared_mapper()
    {
        var session = CreateSession();
        var player = CreatePlayer(session, TeamId.UK);
        player.techState.activeNodeIds.Clear();
        player.techState.activeNodeIds.Add("uk-l3-sun-never-sets");
        player.techState.sunNeverSetsTarget = TeamId.US;

        TechModifiers modifiers = session.GetModifiers(player);

        Assert.IsTrue(modifiers.hasSunNeverSets);
        Assert.IsTrue(modifiers.hasSmokedBBQ);
        Assert.IsTrue(modifiers.hasMotherRoad);
        Assert.AreEqual(0, modifiers.EffectiveEngineCapacityBonus);
        Assert.AreEqual(6, session.EffectiveHeatPoolSize(player, 6));
        Assert.IsTrue(session.HasSmokedBBQAtPosition(player, 5, 60));
        Assert.IsFalse(session.HasSmokedBBQAtPosition(player, 6, 60));
    }

    [TestCase(false, false, "UK", 0)]
    [TestCase(true, false, "UK", 1)]
    [TestCase(false, true, "UK", 1)]
    [TestCase(true, true, "GB", 2)]
    [TestCase(true, true, "DE", 1)]
    public void FullEnglishOnDrawAppliesOnlySelectedTechAndUkHomeBonus(
        bool hasL2, bool hasL3, string country, int expectedTriggers)
    {
        var session = CreateSession();
        var player = CreatePlayer(session, TeamId.UK);
        player.techState.activeNodeIds.Clear();
        if (hasL2) player.techState.activeNodeIds.Add("uk-l2-full-english");
        if (hasL3) player.techState.activeNodeIds.Add("uk-l3-sun-never-sets");
        Assert.AreEqual(1, player.deck.DrawHeatFromPoolToHand(1));
        player.deck.AddCardsToHand(new List<CardData>
        {
            new CardData(CardType.Speed, 1),
            CardData.CreateTrick("uk-english-breakfast-tea")
        });

        Assert.AreEqual(expectedTriggers, session.ResolveFullEnglishOnDraw(player, country));
        Assert.AreEqual(expectedTriggers, player.slipstreamRangeBonusThisTurn);
        Assert.AreEqual(1 + expectedTriggers, player.deck.CountHeatInHand());
        Assert.AreEqual(3 + expectedTriggers, player.deck.HandCount);
    }

    [Test]
    public void FullEnglishOnDrawNeedsAllThreeOriginalCardTypesAndUkOwner()
    {
        var session = CreateSession();
        var uk = CreatePlayer(session, TeamId.UK);
        uk.techState.activeNodeIds.Clear();
        uk.techState.activeNodeIds.Add("uk-l2-full-english");
        uk.techState.activeNodeIds.Add("uk-l3-sun-never-sets");
        uk.deck.AddCardsToHand(new List<CardData>
        {
            new CardData(CardType.Speed, 1), CardData.CreateTrick("uk-english-breakfast-tea")
        });
        Assert.AreEqual(0, session.ResolveFullEnglishOnDraw(uk, "UK"));
        Assert.AreEqual(2, uk.deck.HandCount);

        var foreign = CreatePlayer(session, TeamId.DE);
        foreign.techState.activeNodeIds.Clear();
        foreign.techState.activeNodeIds.Add("uk-l3-sun-never-sets");
        Assert.AreEqual(1, foreign.deck.DrawHeatFromPoolToHand(1));
        foreign.deck.AddCardsToHand(new List<CardData>
        {
            new CardData(CardType.Speed, 1),
            CardData.CreateTrick("uk-english-breakfast-tea")
        });
        Assert.AreEqual(0, session.ResolveFullEnglishOnDraw(foreign, "UK"));
        Assert.AreEqual(3, foreign.deck.HandCount);
    }

    [Test]
    public void test_begin_turn_consumes_kanto_oden_without_tech_tree()
    {
        var session = CreateSession();
        var player = CreatePlayer(session, TeamId.JP);
        player.techState = null;
        player.kantoOdenSkipThisTurn = true;
        player.trickState.kantoOdenActive = true;
        player.trickState.kantoOdenAccumulatedCards = 3;

        session.BeginTurn(player);

        Assert.IsFalse(player.kantoOdenSkipThisTurn);
        Assert.IsFalse(player.trickState.kantoOdenActive);
        Assert.AreEqual(0, player.trickState.kantoOdenAccumulatedCards);
        Assert.AreEqual(3, player.extraCardSlotsThisTurn);
    }

    [Test]
    public void test_demo_tech_unlocks_l1_commons_and_team_unique()
    {
        var session = CreateSession();
        var state = session.CreateDemoTechState(TeamId.UK);

        Assert.IsTrue(state.IsUnlocked("common-l1-heat-coating"));
        Assert.IsTrue(state.IsUnlocked("common-l1-lightweight-chassis"));
        Assert.IsTrue(state.IsUnlocked("common-l1-track-memory"));
        Assert.IsTrue(state.IsUnlocked("common-l1-expanded-tank"));
        Assert.IsTrue(state.IsUnlocked("uk-l1-fish-and-chips"));
        // 全部激活
        Assert.IsTrue(state.IsActive("common-l1-track-memory"));
        // RP 有剩余
        Assert.IsTrue(state.rpBalance >= 0);
    }

    [Test]
    public void test_demo_tech_state_is_team_specific()
    {
        var session = CreateSession();
        var cn = session.CreateDemoTechState(TeamId.CN);
        var de = session.CreateDemoTechState(TeamId.DE);

        Assert.IsTrue(cn.IsUnlocked("cn-ev-l1-heat-pump"));
        Assert.IsTrue(cn.IsUnlocked("cn-ev-l1-pmsm"));
        Assert.IsFalse(cn.IsUnlocked("common-l1-heat-coating"));
        Assert.IsTrue(cn.IsUnlocked("cn-l1-yin-yang-tea"));
        Assert.IsFalse(cn.IsUnlocked("de-l1-schwarzbier-fuel"));
        Assert.IsTrue(de.IsUnlocked("de-l1-schwarzbier-fuel"));
    }

    [Test]
    public void test_yin_yang_resolution_follows_china_drivetrain_mode()
    {
        var session = CreateSession();
        var player = CreatePlayer(session, TeamId.CN);
        player.usesChinaGearSystem = true;

        player.gear = ChinaGearShiftRules.GoGear;
        var go = session.ResolveEndOfTurn(player);
        Assert.IsTrue(go.triggered);
        Assert.IsTrue(go.isYin);
        Assert.IsFalse(go.isYang);

        player.gear = ChinaGearShiftRules.RecoverGear;
        var recover = session.ResolveEndOfTurn(player);
        Assert.IsTrue(recover.triggered);
        Assert.IsFalse(recover.isYin);
        Assert.IsTrue(recover.isYang);
        Assert.AreEqual(1, recover.heatToCool);
    }

    [Test]
    public void test_finished_player_does_not_resolve_end_of_turn_tech()
    {
        var session = CreateSession();
        var player = CreatePlayer(session, TeamId.CN);
        player.usesChinaGearSystem = true;
        player.gear = ChinaGearShiftRules.GoGear;
        player.hasFinished = true;

        YinYangResult result = session.ResolveEndOfTurn(player);

        Assert.IsFalse(result.triggered,
            "A locked finisher must not pay heat or move through Yin Yang Tea on later turns.");
        Assert.AreEqual(0, session.GetGrillSpezialCooldown(player));
    }

    [Test]
    public void test_effective_hand_size_and_pool_add_bonuses()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.CN);

        // demo 科技无手牌/容量加成 → 等于基础值
        Assert.AreEqual(BASE_HAND_SIZE, session.EffectiveHandSize(p, BASE_HAND_SIZE));
        Assert.AreEqual(BASE_POOL_SIZE + 1, session.EffectiveHeatPoolSize(p, BASE_POOL_SIZE));
    }

    [Test]
    public void test_effective_spin_max_defaults_to_3()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.CN);
        Assert.AreEqual(3, session.EffectiveSpinMax(p));

        var noTech = new PlayerState("X", false, 0, 1);
        Assert.AreEqual(3, session.EffectiveSpinMax(noTech));
    }

    // ===== 弯道限速：科技 + 天气 =====

    [Test]
    public void test_effective_corner_limit_ignores_no_corner_limits()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.CN);
        Assert.AreEqual(99, session.EffectiveCornerLimit(p, 99));
    }

    [Test]
    public void test_effective_corner_limit_rainy_reduces_by_one()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.CN);
        session.Weather = WeatherType.Rainy;
        p.techState = null;

        // China keeps neutral handling; rain contributes the only -1.
        Assert.AreEqual(BASE_LIMIT - 1, session.EffectiveCornerLimit(p, BASE_LIMIT));
    }

    [Test]
    public void test_effective_corner_limit_sunny_keeps_base_plus_tech()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.CN);
        session.Weather = WeatherType.Sunny;

        // demo L1 赛道记忆 → 弯速 +1; neutral China handling leaves +1 net.
        Assert.AreEqual(BASE_LIMIT + 1, session.EffectiveCornerLimit(p, BASE_LIMIT));
    }

    [Test]
    public void test_effective_corner_limit_never_below_one()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.CN);
        session.Weather = WeatherType.Rainy;
        Assert.AreEqual(1, session.EffectiveCornerLimit(p, 1));
    }

    [Test]
    public void test_driver_passives_layer_into_weather_and_corner_resolution()
    {
        var session = CreateSession();
        var zhou = CreatePlayer(session, TeamId.CN);
        DriverCatalog.TryGet("cn_zhou_guanyu", out DriverProfile zhouDriver);
        zhou.driverSkill.Initialize(zhouDriver, 2, true);
        zhou.techState = null;
        zhou.driverSkill.BeginTurn();
        session.Weather = WeatherType.Rainy;

        Assert.AreEqual(BASE_LIMIT, session.EffectiveCornerLimit(zhou, BASE_LIMIT));
        Assert.AreEqual(BASE_LIMIT - 1, session.EffectiveCornerLimit(zhou, BASE_LIMIT),
            "Zhou's weather protection is consumed only once per turn.");

        var ma = CreatePlayer(session, TeamId.CN);
        DriverCatalog.TryGet("cn_ma_qinghua", out DriverProfile maDriver);
        ma.driverSkill.Initialize(maDriver, 4, true);
        ma.techState = null;
        ma.driverSkill.BeginTurn();
        session.Weather = WeatherType.Sunny;
        Assert.AreEqual(BASE_LIMIT + 1, session.EffectiveCornerLimit(ma, BASE_LIMIT));
        Assert.AreEqual(0, DriverSkillRules.ReduceCornerHeat(ma.driverSkill, 1));
    }

    [Test]
    public void test_corner_limit_breakdown_exposes_all_live_modifiers()
    {
        var session = CreateSession();
        var cn = CreatePlayer(session, TeamId.CN);
        session.Weather = WeatherType.Rainy;

        CornerLimitBreakdown cnBreakdown = session.GetCornerLimitBreakdown(cn, BASE_LIMIT, false);

        Assert.AreEqual(BASE_LIMIT, cnBreakdown.BaseLimit);
        Assert.AreEqual(-1, cnBreakdown.WeatherModifier);
        Assert.AreEqual(0, cnBreakdown.DriverModifier);
        Assert.AreEqual(0, cnBreakdown.TeamModifier);
        Assert.AreEqual(1, cnBreakdown.TechnologyModifier);
        Assert.AreEqual(BASE_LIMIT, cnBreakdown.EffectiveLimit);

        var italy = CreatePlayer(session, TeamId.IT);
        italy.techState = null;
        session.Weather = WeatherType.Sunny;
        CornerLimitBreakdown italyBreakdown = session.GetCornerLimitBreakdown(italy, BASE_LIMIT, false);

        Assert.AreEqual(0, italyBreakdown.WeatherModifier);
        Assert.AreEqual(2, italyBreakdown.TeamModifier);
        Assert.AreEqual(BASE_LIMIT + 2, italyBreakdown.EffectiveLimit);
    }

    [Test]
    public void test_weather_initialization_from_pool()
    {
        var session = CreateSession();
        session.InitializeWeather(new[] { "rainy", "rainy", "rainy" }, null);
        Assert.AreEqual(WeatherType.Rainy, session.Weather);
    }

    [Test]
    public void test_weather_default_used_when_valid()
    {
        var session = CreateSession();
        session.InitializeWeather(new[] { "rainy" }, "sunny");
        Assert.AreEqual(WeatherType.Sunny, session.Weather);
    }

    // ===== 特技牌 =====

    [Test]
    public void test_play_trick_fails_for_non_trick_card()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.CN);
        var result = session.PlayTrick(p, new CardData(CardType.Speed, 2));
        Assert.IsFalse(result.success);
    }

    [Test]
    public void test_play_trick_fails_when_already_played_this_turn()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.CN);
        p.trickState.trickPlayedThisTurn = true;
        var card = GiveTrick(p, "cn-hotpot-base");

        var result = session.PlayTrick(p, card);
        Assert.IsFalse(result.success);
    }

    [Test]
    public void test_play_trick_hotpot_requires_go_mode()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.CN);
        var card = GiveTrick(p, "cn-hotpot-base");

        p.gear = 2; // 非 Go 模式
        var fail = session.PlayTrick(p, card);
        Assert.IsFalse(fail.success);

        p.gear = 3; // Go 模式
        var ok = session.PlayTrick(p, card);
        Assert.IsTrue(ok.success);
        Assert.IsTrue(TrickCardRules.HasHotpotAttack(p.trickState));
    }

    [Test]
    public void test_play_trick_scone_requires_engine_heat()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.UK);
        var card = GiveTrick(p, "uk-scone");
        p.deck.heatPool.remaining = 0;

        var fail = session.PlayTrick(p, card);
        Assert.IsFalse(fail.success);

        p.deck.heatPool.remaining = 2;
        var ok = session.PlayTrick(p, card);
        Assert.IsTrue(ok.success);
        Assert.AreEqual(1, ok.heatToPay);
        Assert.AreEqual(2, ok.extraMovement);
    }

    [Test]
    public void test_play_trick_tea_requires_heat_in_hand()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.UK);
        var card = GiveTrick(p, "uk-english-breakfast-tea");

        var fail = session.PlayTrick(p, card);
        Assert.IsFalse(fail.success);
    }

    [Test]
    public void test_play_trick_requires_the_exact_card_to_be_in_hand()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.DE);
        var held = GiveTrick(p, "de-sauerkraut");
        var ghost = CardData.CreateTrick("de-sauerkraut");

        var result = session.PlayTrick(p, ghost);

        Assert.IsFalse(result.success);
        Assert.IsFalse(p.trickState.trickPlayedThisTurn);
        Assert.IsTrue(p.deck.ContainsInHand(held));
        Assert.AreEqual(0, p.deck.DiscardPileCount);
    }

    // ===== 移动加成 =====

    [Test]
    public void test_compute_movement_bonus_straight_gains_tech_bonus()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.CN);

        // 直道（未过弯）→ 中国双档由 Go/Recover 出牌数表达，测试状态
        // 未启用该模块时叠加中国车体的 +3（极速 +1、加速 +2）。
        int bonus = session.ComputeMovementBonus(p, crossedCorner: false);
        Assert.AreEqual(4, bonus);

        // 过弯 → 无直道加成
        int cornerBonus = session.ComputeMovementBonus(p, crossedCorner: true);
        Assert.AreEqual(0, cornerBonus);
    }

    [Test]
    public void test_compute_movement_bonus_no_tech_no_bonus()
    {
        var session = CreateSession();
        var noTech = new PlayerState("X", false, 0, 1);
        Assert.AreEqual(0, session.ComputeMovementBonus(noTech, false));
    }

    [Test]
    public void test_us_straight_roar_is_one_flat_bonus_without_profile_stacking()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.US);
        p.techState = null;

        // The profile's +2 top-speed/+1 acceleration values are descriptive;
        // runtime Straight Roar is the tuned flat +1 once any speed card is played.
        Assert.AreEqual(0, session.ComputeMovementBonus(p, crossedCorner: false), "no-card straight bonus");
        p.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 4));
        Assert.AreEqual(1, session.ComputeMovementBonus(p, crossedCorner: false), "played-card straight bonus");
        Assert.AreEqual(0, session.ComputeMovementBonus(p, crossedCorner: true), "corner bonus");
    }

    [Test]
    public void test_italy_corner_exit_bonus_arms_after_corner_and_applies_next_turn()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.IT);
        p.techState = null;
        p.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 2));

        Assert.AreEqual(0, session.ComputeMovementBonus(p, crossedCorner: false),
            "Italy must not receive a permanent straight bonus");
        Assert.AreEqual(0, session.ComputeMovementBonus(p, crossedCorner: true));
        Assert.AreEqual(0, session.ConsumeItalyCornerExitBonus(p));

        session.ArmItalyCornerExitBonus(p, completedCorner: true);
        Assert.IsTrue(p.italyCornerExitBoostReady);

        p.playedSpeedCardsThisTurn.Clear();
        Assert.AreEqual(0, session.ConsumeItalyCornerExitBonus(p), "empty turn must preserve the boost");
        Assert.IsTrue(p.italyCornerExitBoostReady);

        p.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 3));
        Assert.AreEqual(1, session.ConsumeItalyCornerExitBonus(p));
        Assert.IsFalse(p.italyCornerExitBoostReady);
        Assert.AreEqual(0, session.ConsumeItalyCornerExitBonus(p), "boost is one-shot");
    }

    [Test]
    public void test_compute_movement_bonus_sauerkraut_crossed_corner()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.DE);

        // 未打酸菜 → 过弯无加成（直道加成不计）
        Assert.AreEqual(0, session.ComputeMovementBonus(p, true));

        // 打酸菜（DE L1 demo 状态存在，但特技牌需手动打出）
        session.PlayTrick(p, GiveTrick(p, "de-sauerkraut"));
        // 过弯 → 酸菜 +2（意大利才有基础出弯加速）。
        Assert.AreEqual(2, session.ComputeMovementBonus(p, true));
        // 直道 → 德国基础直线 +1、酸菜 +1、轻量化底盘 +1 = 3
        Assert.AreEqual(3, session.ComputeMovementBonus(p, false));
    }

    // ===== 排名 =====

    [Test]
    public void test_rankings_sort_by_progress()
    {
        var session = CreateSession();
        var a = new PlayerState("A", false, 0, 1);
        var b = new PlayerState("B", true, 0, 1);
        a.lap = 1; a.position = 5;
        b.lap = 0; b.position = 40;
        session.Players.Add(a);
        session.Players.Add(b);

        var rankings = session.GetRankings();
        Assert.AreEqual(1, rankings[0].rank);
        Assert.AreEqual(a, rankings[0].player); // 圈数优先
    }

    [Test]
    public void test_turn_order_reverse_of_position()
    {
        var session = CreateSession();
        var a = new PlayerState("A", false, 10, 1);
        var b = new PlayerState("B", true, 30, 1);
        session.Players.Add(a);
        session.Players.Add(b);

        var order = session.GetTurnOrder();
        Assert.AreEqual(a, order[0]); // 末位先行
    }

    [Test]
    public void test_assign_finish_increments_order()
    {
        var session = CreateSession();
        var a = new PlayerState("A", false, 0, 1);
        var b = new PlayerState("B", true, 0, 1);
        session.Players.Add(a);
        session.Players.Add(b);

        Assert.AreEqual(1, session.AssignFinish(a));
        Assert.AreEqual(2, session.AssignFinish(b));
    }

    // ===== 圈数结算 =====

    [Test]
    public void test_heat_reduction_consumed_once_per_lap()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.CN);

        // demo L1 耐热涂层 → 每圈 1 次减免 1
        Assert.AreEqual(1, session.ConsumeHeatReduction(p));
        Assert.AreEqual(0, session.ConsumeHeatReduction(p)); // 已用

        session.OnNewLap(p);
        Assert.AreEqual(1, session.ConsumeHeatReduction(p));
    }

    [Test]
    public void OverspeedHeatCombinesOneUseTechnologyWithVehiclePenaltyInRuntimeOrder()
    {
        var session = CreateSession();
        var player = new PlayerState("US corner", false, 0, 1)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US)
        };
        player.techState.activeNodeIds.Add("common-l1-heat-coating");

        Assert.AreEqual(3, session.ResolveOverspeedHeatCost(player, 3));
        Assert.AreEqual(4, session.ResolveOverspeedHeatCost(player, 3));
        session.OnNewLap(player);
        Assert.AreEqual(3, session.ResolveOverspeedHeatCost(player, 3));
    }

    [Test]
    public void OverspeedHeatFloorsTechnologyBeforeVehicleAndCanDisableVehicleBonuses()
    {
        var session = CreateSession();
        var player = new PlayerState("US corner", false, 0, 1)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US)
        };
        player.techState.activeNodeIds.Add("common-l1-heat-coating");

        Assert.AreEqual(2, session.ResolveOverspeedHeatCost(player, 1));
        session.OnNewLap(player);
        session.TeamVehicleBonusesEnabled = false;
        Assert.AreEqual(1, session.ResolveOverspeedHeatCost(player, 1));
    }

    [Test]
    public void OverspeedHeatAppliesDriverPassiveAfterTechnologyAndVehicle()
    {
        var session = CreateSession();
        var player = new PlayerState("Ma corner", false, 0, 1) { teamId = TeamId.CN };
        DriverCatalog.TryGet("cn_ma_qinghua", out DriverProfile driver);
        player.driverSkill.Initialize(driver, 4, true);
        player.driverSkill.BeginTurn();

        Assert.AreEqual(0, session.ResolveOverspeedHeatCost(player, 1));
    }

    [Test]
    public void test_grill_spezial_cooldown_tracks_heat_paid()
    {
        var session = CreateSession();
        var p = CreatePlayer(session, TeamId.DE);

        Assert.AreEqual(0, session.GetGrillSpezialCooldown(p)); // demo 无 L3

        session.TrackHeatPaid(p, 2);
        Assert.AreEqual(0, session.GetGrillSpezialCooldown(p));
    }

    // ===== 尾流系统 =====

    private PlayerState AddRacer(RaceSession session, string name, int pos, TeamId team, bool isAI = true)
    {
        var p = new PlayerState(name, isAI, pos, 1) { teamId = team };
        p.deck.heatPool = new HeatPool(5);
        session.Players.Add(p);
        return p;
    }

    [Test]
    public void test_slipstream_no_bonus_when_alone()
    {
        var session = CreateSession();
        var p = AddRacer(session, "Solo", 10, TeamId.CN, false);
        Assert.AreEqual(0, session.ComputeSlipstreamBonus(p, session.Players, 60));
    }

    [Test]
    public void test_slipstream_chain_can_resolve_from_settled_positions()
    {
        var session = CreateSession();
        var follower = AddRacer(session, "Follower", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 11, TeamId.UK);
        follower.cornerTotalThisTurn = 8;
        leader.cornerTotalThisTurn = 0;

        var settledMovements = new Dictionary<PlayerState, int>
        {
            [follower] = 0,
            [leader] = 0
        };
        SlipstreamChainResult chain = session.ComputeSlipstreamChain(
            follower, session.Players, 60, settledMovements);

        Assert.IsTrue(chain.Triggered);
        Assert.AreEqual(RaceSession.SLIPSTREAM_BASE_BONUS, chain.TotalBonus);
        Assert.AreSame(leader, chain.Steps[0].Leader);
    }

    [Test]
    public void SettledSlipstreamBatchUsesOnePositionSnapshotWithoutApplyingBonuses()
    {
        var session = CreateSession();
        var rear = AddRacer(session, "Rear", 10, TeamId.CN, false);
        var middle = AddRacer(session, "Middle", 11, TeamId.JP);
        var front = AddRacer(session, "Front", 12, TeamId.DE);
        rear.totalMovementThisTurn = 5;
        middle.totalMovementThisTurn = 6;
        front.totalMovementThisTurn = 7;
        var order = new List<PlayerState> { rear, middle, front };

        var chains = session.ComputeSettledSlipstreamChains(order, order, 60);

        Assert.That(chains[rear].Triggered, Is.True);
        Assert.That(chains[rear].Steps[0].Leader, Is.SameAs(middle));
        Assert.That(chains[middle].Triggered, Is.True);
        Assert.That(chains[middle].Steps[0].Leader, Is.SameAs(front));
        Assert.That(rear.totalMovementThisTurn, Is.EqualTo(5));
        Assert.That(middle.totalMovementThisTurn, Is.EqualTo(6));
        Assert.That(front.totalMovementThisTurn, Is.EqualTo(7));
    }

    [Test]
    public void SettledSlipstreamBatchPreservesSameCellArrivalOrder()
    {
        var session = CreateSession();
        var first = AddRacer(session, "First", 9, TeamId.UK);
        var later = AddRacer(session, "Later", 9, TeamId.CN, false);
        first.totalMovementThisTurn = later.totalMovementThisTurn = 9;
        var order = new List<PlayerState> { first, later };

        var chains = session.ComputeSettledSlipstreamChains(order, order, 60);

        Assert.That(chains[first].Triggered, Is.False);
        Assert.That(chains[later].Triggered, Is.True);
        Assert.That(chains[later].Steps[0].Leader, Is.SameAs(first));
    }

    [Test]
    public void SettledSlipstreamBatchOnlyReturnsCallerEligibleFollowers()
    {
        var session = CreateSession();
        var follower = AddRacer(session, "Follower", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 11, TeamId.UK);
        var order = new List<PlayerState> { follower, leader };

        var chains = session.ComputeSettledSlipstreamChains(
            new List<PlayerState> { follower }, order, 60);

        Assert.That(chains.Count, Is.EqualTo(1));
        Assert.That(chains[follower].Steps[0].Leader, Is.SameAs(leader));
        Assert.That(session.ComputeSettledSlipstreamChains(null, order, 60), Is.Empty);
    }

    [Test]
    public void test_same_cell_slipstream_only_later_arrival_gets_bonus()
    {
        var session = CreateSession();
        var first = AddRacer(session, "First", 9, TeamId.UK);
        var later = AddRacer(session, "Later", 9, TeamId.CN, false);
        first.totalMovementThisTurn = 9;
        later.totalMovementThisTurn = 9;

        var settledMovements = new Dictionary<PlayerState, int>
        {
            [first] = 0,
            [later] = 0
        };
        var arrivalOrder = new List<PlayerState> { first, later };

        SlipstreamChainResult firstChain = session.ComputeSlipstreamChain(
            first, session.Players, 60, settledMovements, 2, arrivalOrder);
        SlipstreamChainResult laterChain = session.ComputeSlipstreamChain(
            later, session.Players, 60, settledMovements, 2, arrivalOrder);

        Assert.IsFalse(firstChain.Triggered, "The first car to arrive is the same-cell leader");
        Assert.IsTrue(laterChain.Triggered, "Only the later car should follow from the same cell");
        Assert.AreSame(first, laterChain.Steps[0].Leader);
    }

    [Test]
    public void test_same_cell_slipstream_prefers_higher_base_movement_before_arrival_order()
    {
        var session = CreateSession();
        var lower = AddRacer(session, "Lower", 12, TeamId.CN, false);
        var higher = AddRacer(session, "Higher", 12, TeamId.UK);
        lower.totalMovementThisTurn = 5;
        higher.totalMovementThisTurn = 8;

        var settledMovements = new Dictionary<PlayerState, int>
        {
            [lower] = 0,
            [higher] = 0
        };
        var arrivalOrder = new List<PlayerState> { lower, higher };

        SlipstreamChainResult lowerChain = session.ComputeSlipstreamChain(
            lower, session.Players, 60, settledMovements, 2, arrivalOrder);
        SlipstreamChainResult higherChain = session.ComputeSlipstreamChain(
            higher, session.Players, 60, settledMovements, 2, arrivalOrder);

        Assert.IsTrue(lowerChain.Triggered, "The lower-movement car should follow the higher-movement car");
        Assert.AreSame(higher, lowerChain.Steps[0].Leader);
        Assert.IsFalse(higherChain.Triggered, "The higher-movement car is the same-cell leader");
    }

    [Test]
    public void test_slipstream_bonus_when_within_range()
    {
        var session = CreateSession();
        var p = AddRacer(session, "Behind", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 11, TeamId.UK);
        // 模拟移动后：p → 13，leader → 14 → 距离 1 ≤ 范围 1
        p.cornerTotalThisTurn = 3;
        leader.cornerTotalThisTurn = 3;

        Assert.AreEqual(RaceSession.SLIPSTREAM_BASE_BONUS, session.ComputeSlipstreamBonus(p, session.Players, 60));
    }

    [Test]
    public void test_slipstream_result_identifies_the_nearest_leader()
    {
        var session = CreateSession();
        var follower = AddRacer(session, "Follower", 10, TeamId.CN, false);
        var nearest = AddRacer(session, "Nearest", 11, TeamId.UK);
        var farther = AddRacer(session, "Farther", 12, TeamId.DE);
        follower.cornerTotalThisTurn = 3;
        nearest.cornerTotalThisTurn = 3;
        farther.cornerTotalThisTurn = 3;

        SlipstreamResult result = session.ComputeSlipstream(follower, session.Players, 60);

        Assert.IsTrue(result.Triggered);
        Assert.AreSame(nearest, result.Leader);
        Assert.AreEqual(RaceSession.SLIPSTREAM_BASE_BONUS, result.Bonus);
    }

    [Test]
    public void test_slipstream_result_is_empty_when_effect_is_blocked()
    {
        var session = CreateSession();
        var follower = AddRacer(session, "Follower", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 11, TeamId.UK);
        follower.cornerTotalThisTurn = 3;
        leader.cornerTotalThisTurn = 3;
        leader.trickState.iceJellyActive = true;

        SlipstreamResult result = session.ComputeSlipstream(follower, session.Players, 60);

        Assert.IsFalse(result.Triggered);
        Assert.IsNull(result.Leader);
        Assert.AreEqual(0, result.Bonus);
    }

    [Test]
    public void test_cloudy_slipstream_keeps_range_and_reduces_final_bonus()
    {
        var session = CreateSession();
        var p = AddRacer(session, "Behind", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 12, TeamId.UK);
        p.cornerTotalThisTurn = 3;
        leader.cornerTotalThisTurn = 3;
        p.slipstreamRangeBonusThisTurn = 1;

        session.Weather = WeatherType.Sunny;
        Assert.AreEqual(RaceSession.SLIPSTREAM_BASE_BONUS, session.ComputeSlipstreamBonus(p, session.Players, 60));

        session.Weather = WeatherType.Cloudy;
        Assert.AreEqual(RaceSession.SLIPSTREAM_BASE_BONUS - 1, session.ComputeSlipstreamBonus(p, session.Players, 60));
    }

    [Test]
    public void test_cloudy_still_allows_base_range_slipstream()
    {
        var session = CreateSession();
        var p = AddRacer(session, "Behind", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 11, TeamId.UK);
        p.cornerTotalThisTurn = 3;
        leader.cornerTotalThisTurn = 3;
        session.Weather = WeatherType.Cloudy;

        SlipstreamResult result = session.ComputeSlipstream(p, session.Players, 60);

        Assert.IsTrue(result.Triggered);
        Assert.AreSame(leader, result.Leader);
        Assert.AreEqual(1, result.Bonus);
    }

    [Test]
    public void test_heavy_rain_disables_slipstream_in_session()
    {
        var session = CreateSession();
        var p = AddRacer(session, "Behind", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 11, TeamId.UK);
        p.cornerTotalThisTurn = 3;
        leader.cornerTotalThisTurn = 3;
        session.Weather = WeatherType.HeavyRain;

        Assert.AreEqual(0, session.ComputeSlipstreamBonus(p, session.Players, 60));
    }

    [Test]
    public void test_slipstream_no_bonus_beyond_range()
    {
        var session = CreateSession();
        var p = AddRacer(session, "Behind", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 15, TeamId.UK);
        // 模拟移动后：p → 13，leader → 19 → 距离 6 > 1
        p.cornerTotalThisTurn = 3;
        leader.cornerTotalThisTurn = 4;

        Assert.AreEqual(0, session.ComputeSlipstreamBonus(p, session.Players, 60));
    }

    [Test]
    public void test_slipstream_range_extended_by_temp_bonus()
    {
        var session = CreateSession();
        var p = AddRacer(session, "Behind", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 12, TeamId.UK);
        // 模拟移动后：p → 13，leader → 15 → 距离 2
        p.cornerTotalThisTurn = 3;
        leader.cornerTotalThisTurn = 3;

        Assert.AreEqual(0, session.ComputeSlipstreamBonus(p, session.Players, 60));

        p.slipstreamRangeBonusThisTurn = 1; // FullEnglish 等临时加成 → 范围 2
        Assert.AreEqual(RaceSession.SLIPSTREAM_BASE_BONUS, session.ComputeSlipstreamBonus(p, session.Players, 60));
    }

    [TestCase(0, 1)]
    [TestCase(1, 0)]
    public void test_slipstream_requires_same_lap(int followerLap, int leaderLap)
    {
        var session = CreateSession();
        var follower = AddRacer(session, "Follower", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 11, TeamId.UK);
        follower.lap = followerLap;
        leader.lap = leaderLap;
        follower.cornerTotalThisTurn = 3;
        leader.cornerTotalThisTurn = 3;

        // Both cars would be one cell apart after movement, but a different
        // lap means they are not physically in the same race group.
        Assert.AreEqual(0, session.ComputeSlipstreamBonus(follower, session.Players, 60));
    }

    [Test]
    public void test_slipstream_blocked_by_ice_jelly()
    {
        var session = CreateSession();
        var p = AddRacer(session, "Behind", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 11, TeamId.UK);
        // 模拟移动后：p → 13，leader → 14 → 距离 1（无冰糕时会吃到尾流）
        p.cornerTotalThisTurn = 3;
        leader.cornerTotalThisTurn = 3;
        Assert.AreEqual(RaceSession.SLIPSTREAM_BASE_BONUS, session.ComputeSlipstreamBonus(p, session.Players, 60));

        leader.trickState.iceJellyActive = true; // 冰糕：身后车无法享受尾流
        Assert.AreEqual(0, session.ComputeSlipstreamBonus(p, session.Players, 60));
    }

    [Test]
    public void test_slipstream_parmigiano_adds_bonus()
    {
        var session = CreateSession();
        var p = AddRacer(session, "Behind", 10, TeamId.IT, false);
        var leader = AddRacer(session, "Leader", 11, TeamId.UK);
        // 模拟移动后：p → 13，leader → 14 → 距离 1
        p.cornerTotalThisTurn = 3;
        leader.cornerTotalThisTurn = 3;

        // 帕尔玛干酪：尾流 +2（共 +4）
        session.PlayTrick(p, GiveTrick(p, "it-parmigiano"));
        Assert.AreEqual(RaceSession.SLIPSTREAM_BASE_BONUS + 2, session.ComputeSlipstreamBonus(p, session.Players, 60));
    }

    [Test]
    public void test_slipstream_simulated_positions_used()
    {
        var session = CreateSession();
        var p = AddRacer(session, "Behind", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 11, TeamId.UK);
        // 模拟移动后：p 到 13，leader 到 20 → 距离 7 > 1 → 无尾流
        p.cornerTotalThisTurn = 3;
        leader.cornerTotalThisTurn = 9;
        Assert.AreEqual(0, session.ComputeSlipstreamBonus(p, session.Players, 60));

        // 移动后相邻 → 有尾流（即便初始相距远）
        leader.cornerTotalThisTurn = 4; // leader 到 15, p 到 13 → 距离 2... >1 无
        p.cornerTotalThisTurn = 3;      // p 到 13, leader 到 15 → 距离 2
        Assert.AreEqual(0, session.ComputeSlipstreamBonus(p, session.Players, 60));
    }

    [Test]
    public void test_slipstream_chain_can_trigger_twice_against_two_leaders()
    {
        var session = CreateSession();
        var follower = AddRacer(session, "Follower", 10, TeamId.CN, false);
        var firstLeader = AddRacer(session, "First", 11, TeamId.UK);
        var secondLeader = AddRacer(session, "Second", 13, TeamId.DE);
        follower.cornerTotalThisTurn = 3;     // 13
        firstLeader.cornerTotalThisTurn = 3;  // 14：第一段 +2 后到 15
        secondLeader.cornerTotalThisTurn = 3; // 16：第二段再次命中

        SlipstreamChainResult chain = session.ComputeSlipstreamChain(
            follower, session.Players, 60);

        Assert.IsTrue(chain.Triggered);
        Assert.AreEqual(2, chain.Steps.Count);
        Assert.AreSame(firstLeader, chain.Steps[0].Leader);
        Assert.AreSame(secondLeader, chain.Steps[1].Leader);
        Assert.AreEqual(RaceSession.SLIPSTREAM_BASE_BONUS * 2, chain.TotalBonus);
    }

    [Test]
    public void test_slipstream_chain_is_capped_at_two_triggers()
    {
        var session = CreateSession();
        var follower = AddRacer(session, "Follower", 10, TeamId.CN, false);
        AddRacer(session, "First", 11, TeamId.UK).cornerTotalThisTurn = 3;
        AddRacer(session, "Second", 13, TeamId.DE).cornerTotalThisTurn = 3;
        var thirdLeader = AddRacer(session, "Third", 15, TeamId.US);
        thirdLeader.cornerTotalThisTurn = 3;
        follower.cornerTotalThisTurn = 3;

        SlipstreamChainResult chain = session.ComputeSlipstreamChain(
            follower, session.Players, 60, null, 99);

        Assert.AreEqual(2, chain.Steps.Count);
        Assert.AreEqual(RaceSession.SLIPSTREAM_BASE_BONUS * 2, chain.TotalBonus);
        Assert.AreNotSame(thirdLeader, chain.Steps[chain.Steps.Count - 1].Leader);
    }

    [Test]
    public void test_cloudy_chain_does_not_reuse_same_leader_after_one_cell_bonus()
    {
        var session = CreateSession();
        var follower = AddRacer(session, "Follower", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 11, TeamId.UK);
        follower.cornerTotalThisTurn = 3;
        leader.cornerTotalThisTurn = 3;
        session.Weather = WeatherType.Cloudy;

        SlipstreamChainResult chain = session.ComputeSlipstreamChain(
            follower, session.Players, 60);

        Assert.AreEqual(1, chain.Steps.Count);
        Assert.AreSame(leader, chain.Steps[0].Leader);
        Assert.AreEqual(1, chain.TotalBonus);
    }

    [Test]
    public void test_slipstream_chain_uses_complete_non_slipstream_movement_plan()
    {
        var session = CreateSession();
        var follower = AddRacer(session, "Follower", 10, TeamId.CN, false);
        var leader = AddRacer(session, "Leader", 11, TeamId.UK);
        follower.cornerTotalThisTurn = 3;
        leader.cornerTotalThisTurn = 3;
        var plannedMovements = new Dictionary<PlayerState, int>
        {
            [follower] = 5, // 科技/特技把最终非尾流终点推进到 15，已超过前车的 14。
            [leader] = 3
        };

        SlipstreamChainResult chain = session.ComputeSlipstreamChain(
            follower, session.Players, 60, plannedMovements);

        Assert.IsFalse(chain.Triggered);
        Assert.AreEqual(0, chain.TotalBonus);
    }

    [Test]
    public void test_ice_jelly_on_nearest_leader_stops_chain_instead_of_skipping_ahead()
    {
        var session = CreateSession();
        var follower = AddRacer(session, "Follower", 10, TeamId.CN, false);
        var blocker = AddRacer(session, "Blocker", 11, TeamId.UK);
        AddRacer(session, "Farther", 13, TeamId.DE).cornerTotalThisTurn = 3;
        follower.cornerTotalThisTurn = 3;
        blocker.cornerTotalThisTurn = 3;
        blocker.trickState.iceJellyActive = true;
        follower.slipstreamRangeBonusThisTurn = 2;

        SlipstreamChainResult chain = session.ComputeSlipstreamChain(
            follower, session.Players, 60);

        Assert.IsFalse(chain.Triggered);
        Assert.AreEqual(0, chain.TotalBonus);
    }

    // ===== 地标（US 科技/特技） =====

    [Test]
    public void test_landmarks_at_start_and_midpoint()
    {
        var (lm1, lm2) = RaceSession.GetLandmarks(60);
        Assert.AreEqual(0, lm1);
        Assert.AreEqual(30, lm2);
    }

    [Test]
    public void test_crossed_landmark_wrap_around_start_line()
    {
        // 从 58 前进到 2 → 跨过地标 1（起点线 0）
        Assert.IsTrue(RaceSession.CrossedLandmark(58, 2, 0, 60));
        // 从 58 前进到 2 → 未跨过地标 2（中点 30）
        Assert.IsFalse(RaceSession.CrossedLandmark(58, 2, 30, 60));
    }

    [Test]
    public void test_crossed_landmark_unwrapped_forward_end_counts_each_landmark()
    {
        Assert.IsTrue(RaceSession.CrossedLandmark(58, 62, 0, 60));
        Assert.IsFalse(RaceSession.CrossedLandmark(58, 62, 30, 60));
        Assert.IsTrue(RaceSession.CrossedLandmark(58, 95, 30, 60));
        Assert.IsFalse(RaceSession.CrossedLandmark(30, 30, 30, 60));
        Assert.IsTrue(RaceSession.CrossedLandmark(30, 90, 30, 60));
    }

    [TestCase(0, 1)] [TestCase(5, 1)] [TestCase(9, 1)]
    [TestCase(10, 0)] [TestCase(15, 0)] [TestCase(19, 0)]
    public void test_first_landmark_ahead_follows_forward_route(int oldPosition, int firstIndex)
    {
        Assert.AreEqual(firstIndex, RaceSession.FirstLandmarkAhead(oldPosition, 20));
    }

    [TestCase(5, 9, new int[0])]
    [TestCase(5, 10, new[] { 1 })]
    [TestCase(18, 20, new[] { 0 })]
    [TestCase(5, 20, new[] { 1, 0 })]
    [TestCase(15, 30, new[] { 0, 1 })]
    [TestCase(10, 30, new[] { 0, 1 })]
    [TestCase(0, 20, new[] { 1, 0 })]
    public void test_crossed_landmark_pass_plan_follows_physical_order(
        int oldPosition, int rawEnd, int[] expected)
    {
        CollectionAssert.AreEqual(expected,
            RaceSession.CrossedLandmarkIndicesInOrder(oldPosition, rawEnd, 20));
    }

    [TestCase(10, 10, 20)]
    [TestCase(15, 14, 20)]
    [TestCase(0, 20, 1)]
    public void test_crossed_landmark_pass_plan_excludes_zero_reverse_and_degenerate_moves(
        int oldPosition, int rawEnd, int totalCells)
    {
        Assert.IsEmpty(RaceSession.CrossedLandmarkIndicesInOrder(
            oldPosition, rawEnd, totalCells));
    }

    [TestCase(5, 9, 0)]
    [TestCase(5, 10, 1)]
    [TestCase(18, 20, 1)]
    [TestCase(5, 20, 2)]
    [TestCase(15, 30, 2)]
    public void test_drive_thru_counts_distinct_landmarks_crossed_by_base_move(
        int oldPosition, int rawEnd, int expectedBonus)
    {
        Assert.AreEqual(expectedBonus, RaceSession.CountCrossedLandmarks(oldPosition, rawEnd, 20));
    }

    [Test]
    public void test_crossed_landmark_forward_only()
    {
        Assert.IsTrue(RaceSession.CrossedLandmark(20, 35, 30, 60));
        Assert.IsFalse(RaceSession.CrossedLandmark(35, 20, 30, 60));
    }

    [Test]
    public void test_in_bbq_zone_near_landmark()
    {
        Assert.IsTrue(RaceSession.IsInBBQZone(4, 60));   // 靠近起点线
        Assert.IsTrue(RaceSession.IsInBBQZone(28, 60));  // 靠近中点
        Assert.IsFalse(RaceSession.IsInBBQZone(15, 60)); // 两者之间（> 5 格）
    }
}

/// <summary>Shared BBQ eligibility: real map sizes, virtual grants and caller-owned timing.</summary>
public sealed class BBQEligibilityBoundaryTests
{
    private static IEnumerable<TestCaseData> OfficialMapCases()
    {
        foreach (TrackSelectionOption track in OfficialTrackCatalog.Tracks)
        {
            yield return new TestCaseData(track.TrackId, TeamId.US);
            yield return new TestCaseData(track.TrackId, TeamId.UK);
        }
    }

    [TestCaseSource(nameof(OfficialMapCases))]
    public void EveryOfficialMapCellMatchesCircularRadiusWithoutMutatingPlayer(string trackId, TeamId owner)
    {
        TrackConfig track = TrackDataLoader.LoadConfig(trackId);
        Assert.That(track, Is.Not.Null);
        int total = track.cells.Length;
        var session = new RaceSession(new SystemRandomSource(7));
        PlayerState player = CreateBBQPlayer(owner);
        player.position = 0;
        player.deck.heatPool = new HeatPool(8);
        player.deck.DrawHeatFromPoolToHand(2);
        player.totalMovementThisTurn = 17;
        for (int position = 0; position < total; position++)
        {
            int originDistance = System.Math.Min(position, total - position);
            int midpointDistance = System.Math.Abs(position - total / 2);
            midpointDistance = System.Math.Min(midpointDistance, total - midpointDistance);
            bool expected = originDistance <= 5 || midpointDistance <= 5;
            Assert.AreEqual(expected, session.HasSmokedBBQAtPosition(player, position, total),
                trackId + " cell " + position);
        }
        Assert.AreEqual(0, player.position);
        Assert.AreEqual(17, player.totalMovementThisTurn);
        Assert.AreEqual(2, player.deck.CountHeatInHand());
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.AreEqual(77, player.techState.rpBalance);
        Assert.That(player.techState.unlockedNodeIds, Is.Empty);
        CollectionAssert.AreEqual(new[] { owner == TeamId.US ? "us-l2-smoked-bbq" : "uk-l3-sun-never-sets" },
            player.techState.activeNodeIds);
    }

    [TestCase(TeamId.US)]
    [TestCase(TeamId.UK)]
    public void PositionAndTechnologyAreReadLiveWithoutCachedBenefits(TeamId owner)
    {
        var session = new RaceSession(new SystemRandomSource(7));
        PlayerState player = CreateBBQPlayer(owner);
        player.position = 15;
        Assert.IsTrue(session.HasSmokedBBQAtPosition(player, 5, 60));
        Assert.IsFalse(session.HasSmokedBBQAtPosition(player, 6, 60));
        player.position = 0;
        Assert.IsFalse(session.HasSmokedBBQAtPosition(player, 15, 60));
        if (owner == TeamId.UK)
        {
            player.techState.sunNeverSetsTarget = TeamId.IT;
            Assert.IsFalse(session.HasSmokedBBQAtPosition(player, 0, 60));
            player.techState.sunNeverSetsTarget = TeamId.US;
        }
        player.techState.activeNodeIds.Clear();
        Assert.IsFalse(session.HasSmokedBBQAtPosition(player, 0, 60));
        player.techState.activeNodeIds.Add(owner == TeamId.US ? "us-l2-smoked-bbq" : "uk-l3-sun-never-sets");
        Assert.IsTrue(session.HasSmokedBBQAtPosition(player, 0, 60));
    }

    [TestCase(8)]
    [TestCase(10)]
    [TestCase(20)]
    public void OverlappingZonesStillHaveOneBooleanEligibility(int total)
    {
        var session = new RaceSession(new SystemRandomSource(7));
        PlayerState player = CreateBBQPlayer(TeamId.US);
        for (int position = 0; position < total; position++)
            Assert.IsTrue(session.HasSmokedBBQAtPosition(player, position, total));
        Assert.AreEqual(77, player.techState.rpBalance);
    }

    [Test]
    public void NoTechnologyOrNoPlayerCannotReceiveBBQ()
    {
        var session = new RaceSession(new SystemRandomSource(7));
        PlayerState player = CreateBBQPlayer(TeamId.US);
        player.techState = null;
        Assert.IsFalse(session.HasSmokedBBQAtPosition(player, 0, 60));
        Assert.IsFalse(session.HasSmokedBBQAtPosition(null, 0, 60));
        player.techState = new TechTreeState(TeamId.US);
        player.techState.activeNodeIds.Add("us-l1-drive-thru");
        Assert.IsFalse(session.HasSmokedBBQAtPosition(player, 0, 60));
    }

    [TestCase(TeamId.US, 6, 0, false)]
    [TestCase(TeamId.US, 6, -1, true)]
    [TestCase(TeamId.US, 5, 0, true)]
    [TestCase(TeamId.US, 5, 1, false)]
    [TestCase(TeamId.US, 54, 0, false)]
    [TestCase(TeamId.US, 54, 1, true)]
    [TestCase(TeamId.UK, 6, 0, false)]
    [TestCase(TeamId.UK, 6, -1, true)]
    [TestCase(TeamId.UK, 5, 0, true)]
    [TestCase(TeamId.UK, 5, 1, false)]
    [TestCase(TeamId.UK, 54, 0, false)]
    [TestCase(TeamId.UK, 54, 1, true)]
    public void SmokeUsesCallerEndpointWithoutGivingLeaderBonus(TeamId owner, int start, int movement, bool blocked)
    {
        var session = new RaceSession(new SystemRandomSource(7));
        session.TeamVehicleBonusesEnabled = false;
        PlayerState leader = CreateBBQPlayer(owner);
        leader.position = start;
        int end = start + movement;
        var follower = new PlayerState("Follower", false, end - 1, 1) { teamId = TeamId.DE };
        session.Players.Add(follower);
        session.Players.Add(leader);
        var planned = new Dictionary<PlayerState, int> { { leader, movement }, { follower, 0 } };
        SlipstreamChainResult result = session.ComputeSlipstreamChain(follower, session.Players, 60, planned);
        Assert.AreEqual(!blocked, result.Triggered);
        Assert.AreEqual(blocked ? 0 : 2, result.TotalBonus);
        Assert.IsFalse(session.ComputeSlipstreamChain(leader, session.Players, 60, planned).Triggered);
        Assert.AreEqual(start, leader.position);
        Assert.AreEqual(0, leader.totalMovementThisTurn);
    }

    [TestCase(TeamId.US)]
    [TestCase(TeamId.UK)]
    public void RegionalCapacitySyncUsesLivePositionAndDoesNotStack(TeamId owner)
    {
        var session = new RaceSession(new SystemRandomSource(7));
        PlayerState player = CreateBBQPlayer(owner);
        player.deck.heatPool = new HeatPool(5);

        session.SyncRegionalHeatCapacity(player, 60);
        session.SyncRegionalHeatCapacity(player, 60);
        Assert.AreEqual(2, player.deck.RegionalCapacityBonus);
        Assert.AreEqual(7, player.deck.heatPool.remaining);

        player.position = 6;
        session.SyncRegionalHeatCapacity(player, 60);
        Assert.AreEqual(0, player.deck.RegionalCapacityBonus);
        Assert.AreEqual(5, player.deck.heatPool.remaining);

        player.position = 30;
        session.SyncRegionalHeatCapacity(player, 60);
        Assert.AreEqual(2, player.deck.RegionalCapacityBonus);
        Assert.AreEqual(7, player.deck.heatPool.remaining);

        session.SyncRegionalHeatCapacity(player, 60, false);
        Assert.AreEqual(0, player.deck.RegionalCapacityBonus);
        Assert.AreEqual(5, player.deck.heatPool.remaining);
    }

    [Test]
    public void RegionalCapacitySyncIgnoresMissingEngineAndInvalidTrack()
    {
        var session = new RaceSession(new SystemRandomSource(7));
        PlayerState player = CreateBBQPlayer(TeamId.US);
        session.SyncRegionalHeatCapacity(null, 60);
        session.SyncRegionalHeatCapacity(player, 60);
        player.deck.heatPool = new HeatPool(5);
        session.SyncRegionalHeatCapacity(player, 0);
        Assert.AreEqual(0, player.deck.RegionalCapacityBonus);
        Assert.AreEqual(5, player.deck.heatPool.remaining);
    }

    private static PlayerState CreateBBQPlayer(TeamId owner)
    {
        var player = new PlayerState("BBQ", true, 0, 1)
        {
            teamId = owner, techState = new TechTreeState(owner, 77)
        };
        player.techState.activeNodeIds.Add(owner == TeamId.US ? "us-l2-smoked-bbq" : "uk-l3-sun-never-sets");
        if (owner == TeamId.UK) player.techState.sunNeverSetsTarget = TeamId.US;
        return player;
    }
}
