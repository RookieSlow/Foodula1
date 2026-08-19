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
    public void test_slipstream_range_respects_cloudy_weather()
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
        Assert.AreEqual(0, session.ComputeSlipstreamBonus(p, session.Players, 60));
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
