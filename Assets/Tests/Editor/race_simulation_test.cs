using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 纯层全比赛模拟测试 — 3 玩家完整跑完一场比赛，验证集成不变量。
/// 不使用 MonoBehaviour / 协程：直接驱动 RaceSession + 纯规则层 + 简单 AI 策略。
/// 覆盖路径：多车回合顺序、天气换圈、维修区、科技修正（L1+L2+L3 全解锁）、
/// 弯道判定、失控/爆缸、完赛顺位、排名一致性。
/// </summary>
public class RaceSimulationTest
{
    private const int MAX_TURNS = 300;
    private const int SIM_SEED = 20260803;

    private sealed class SimViolations
    {
        public readonly List<string> Errors = new List<string>();
        public void Check(bool condition, string message)
        {
            if (!condition) Errors.Add(message);
        }
    }

    [Test]
    public void test_full_race_runs_consistently_with_invariants()
    {
        // ── 赛道：银石 ──
        TrackConfig trackCfg = TrackDataLoader.LoadConfig("silverstone_afternoon_tea");
        Assert.IsNotNull(trackCfg, "银石 JSON 应可加载");
        var nodes = TrackDataLoader.ConfigToNodes(trackCfg);
        TrackDataLoader.BuildCornerMaps(trackCfg, nodes, out var cornerLimits, out _);
        int totalNodes = nodes.Count;

        // ── 会话与玩家（全科技解锁，覆盖 L2/L3 修正） ──
        var session = new RaceSession(new SystemRandomSource(SIM_SEED));
        session.InitializeWeather(trackCfg.weatherPool, trackCfg.defaultWeather);
        var expectedSpeedCards = new Dictionary<PlayerState, int>();
        var expectedTrickCards = new Dictionary<PlayerState, int>();
        var expectedPermanentHeat = new Dictionary<PlayerState, int>();
        GameConfigSO simConfig = CreateSimConfig();

        TeamId[] teams = { TeamId.CN, TeamId.UK, TeamId.DE };
        var players = new List<PlayerState>();
        for (int i = 0; i < teams.Length; i++)
        {
            var p = new PlayerState($"P{i}", i > 0, trackCfg.cells[0].index, 1) { teamId = teams[i] };
            p.techState = TechTreeRules.CreateDemoState(teams[i]);
            UnlockEverything(p.techState);
            TechTreeRules.ResetPerRaceState(p.techState);
            TechTreeRules.ActivateAllUnlocked(p.techState);
            TechTreeRules.SelectBroth(p.techState, BrothType.Shio); // 汤底冷却生效
            int pool = session.EffectiveHeatPoolSize(p, 6);
            p.deck.InitializeDeck(simConfig, new HeatPool(pool), new SystemRandomSource(SIM_SEED + i));
            p.deck.AddTrickCardsToDrawPile(session.CreateInitialTrickCards(teams[i]));
            p.deck.DrawToHand(session.EffectiveHandSize(p, 7));
            p.trickState.ResetPerRace();
            players.Add(p);
            expectedSpeedCards[p] = simConfig.speedCardDistribution.Length;
            expectedTrickCards[p] = TrickCardRules.INITIAL_TRICK_CARDS_PER_TEAM;
            expectedPermanentHeat[p] = pool + simConfig.initialHeatCards;
        }
        session.Players.AddRange(players);

        // ── 比赛循环（纯层模拟） ──
        var violations = new SimViolations();
        int turn = 0;
        while (turn < MAX_TURNS && !session.IsRaceOver() && !RaceEndedForHuman(players))
        {
            turn++;
            foreach (var p in players)
                session.BeginTurn(p);

            foreach (var p in session.GetTurnOrder())
            {
                if (p.skipNextTurn) { p.skipNextTurn = false; p.gear = 1; continue; }
                if (p.isBlown || p.hasFinished) continue;

                // 档位（简单策略：热量高降档，低升档）
                p.gear = SimPolicyGear(p);

                // 抽牌
                p.deck.DrawToHand(session.EffectiveHandSize(p, 7));

                // 选牌：最大 N 张速度牌
                var chosen = p.deck.GetTopNSpeedCards(p.gear);
                int missing = RaceRules.GetMissingSpeedCardCount(p.gear, chosen.Count);
                if (missing > 0 && p.deck.DrawHeatFromPool(missing) < missing)
                {
                    SimSpin(p, p.position, session.EffectiveSpinMax(p));
                    continue;
                }
                foreach (CardData card in chosen)
                {
                    SpeedCardCommitResult commit = CardPlayRules.CommitSpeedCard(p, card, p.gear);
                    violations.Check(commit == SpeedCardCommitResult.Success,
                        $"{p.name} 速度牌提交失败: {commit}");
                }

                // 移动计算（与管理器同构的纯层版本）
                p.cornerTotalThisTurn = RaceRules.SumCardValues(p.playedSpeedCardsThisTurn);
                int rawEnd = p.position + p.cornerTotalThisTurn;
                var crossed = TrackRules.GetUniqueApexCornersCrossed(nodes, p.position, rawEnd);
                int bonus = session.ComputeMovementBonus(p, crossed.Count > 0);
                bonus += session.ComputeSlipstreamBonus(p, players, totalNodes);
                if (p.techState != null && TechTreeRules.ShouldTriggerWurstplatte(p.techState, session.TechDb, crossed.Count > 0))
                    bonus += 1;
                p.totalMovementThisTurn = p.cornerTotalThisTurn + bonus;

                // 移动 + 圈数
                int oldPos = p.position;
                int newPos = p.position + p.totalMovementThisTurn;
                for (int i = oldPos + 1; i <= newPos; i++)
                {
                    if (nodes[i % totalNodes].isStartFinish)
                    {
                        p.lap++;
                        session.OnNewLap(p);
                        if (p.lap >= trackCfg.laps)
                        {
                            p.hasFinished = true;
                            session.AssignFinish(p);
                        }
                    }
                }
                p.position = newPos % totalNodes;
                violations.Check(p.position >= 0 && p.position < totalNodes,
                    $"{p.name} 位置越界: {p.position}");

                // 弯道判定（科技+天气限速）
                if (p.cornerTotalThisTurn > 0 && !p.isBlown)
                {
                    foreach (var cid in crossed)
                    {
                        int limit = session.EffectiveCornerLimit(p, cornerLimits.TryGetValue(cid, out var l) ? l : 99);
                        if (p.cornerTotalThisTurn > limit)
                        {
                            int heat = Mathf.Max(1, p.cornerTotalThisTurn - limit - session.ConsumeHeatReduction(p));
                            int drawn = p.deck.DrawHeatFromPool(heat);
                            if (drawn < heat)
                            {
                                SimSpin(p, oldPos, session.EffectiveSpinMax(p));
                                break;
                            }
                        }
                    }
                }
                violations.Check(p.deck.heatPool.remaining >= 0,
                    $"{p.name} 热量池为负: {p.deck.heatPool.remaining}");

                // 维修区（热量高自动进站）
                if (PitLaneRules.CrossedPitEntry(oldPos, p.position, nodes) && p.HeatRatio >= 0.6f)
                {
                    var pit = PitLaneRules.EnterPit(p, nodes);
                    if (pit.success)
                    {
                        p.deck.RecoverAllHeatToPool();
                        violations.Check(p.position >= 0 && p.position < totalNodes,
                            $"{p.name} 进站后位置越界: {p.position}");
                    }
                }
            }

            // 圈内换天（每圈一次，由最先过线者触发）
            foreach (var p in players)
                if (p.lap > sessionWeatherRolledLap)
                {
                    sessionWeatherRolledLap = p.lap;
                    session.RollWeatherForLap();
                    break;
                }

            // 与运行时 CleanupTurn 一致：打出区进入弃牌堆，限时牌销毁。
            foreach (var p in players)
            {
                p.deck.DiscardSpeedCards(p.playedSpeedCardsThisTurn);
                p.playedSpeedCardsThisTurn.Clear();
                p.deck.RemoveTempCardsFromHand();
                // 模拟玩家的可选弃牌：清走手中特技牌，避免不可用牌永久堵住手牌。
                p.deck.DiscardPlayableCardsFromHand(p.deck.GetTricksInHand());

                int speedTotal = p.deck.CountSpeedInDeck() + p.deck.CountSpeedInHand();
                int trickTotal = p.deck.CountTricksInDeck() + p.deck.GetTricksInHand().Count;
                int heatTotal = p.deck.heatPool.remaining +
                    p.deck.CountHeatInDeck() + p.deck.CountHeatInHand();
                violations.Check(speedTotal == expectedSpeedCards[p],
                    $"{p.name} 速度牌不守恒: {speedTotal}/{expectedSpeedCards[p]}");
                violations.Check(trickTotal == expectedTrickCards[p],
                    $"{p.name} 特技牌不守恒: {trickTotal}/{expectedTrickCards[p]}");
                violations.Check(heatTotal == expectedPermanentHeat[p],
                    $"{p.name} 永久热量不守恒: {heatTotal}/{expectedPermanentHeat[p]}");
            }
        }

        // ── 断言：不变量 ──
        Assert.AreEqual(0, violations.Errors.Count,
            "比赛模拟违反不变量:\n" + string.Join("\n", violations.Errors));

        // 至少 2 人完赛（300 回合内）
        int finished = players.FindAll(p => p.hasFinished).Count;
        Assert.IsTrue(finished >= 2, $"300 回合内完赛人数不足: {finished}/3");

        // 完赛顺位唯一且从 1 开始
        var orders = new HashSet<int>();
        foreach (var p in players)
            if (p.hasFinished)
                orders.Add(p.finishOrder);
        Assert.AreEqual(finished, orders.Count, "完赛顺位必须唯一");
        Assert.IsTrue(!orders.Contains(0), "完赛者必须有 finishOrder");

        // 排名可生成且无异常
        var rankings = session.GetRankings();
        Assert.AreEqual(players.Count, rankings.Count);
        foreach (var e in rankings)
            Assert.IsTrue(e.rank >= 1 && e.rank <= players.Count);

        // 爆缸者最多 1 人（3 人赛）
        int blown = players.FindAll(p => p.isBlown).Count;
        Assert.IsTrue(blown <= 1, $"爆缸人数异常: {blown}");
        Object.DestroyImmediate(simConfig);
    }

    // ===== 模拟辅助 =====

    private int sessionWeatherRolledLap;

    private static bool RaceEndedForHuman(List<PlayerState> players)
    {
        // 人类（P0）完赛或爆缸即结束（与 CheckGameEnd 一致）
        return players[0].hasFinished || players[0].isBlown;
    }

    private static int SimPolicyGear(PlayerState p)
    {
        if (p.HeatRatio >= 0.6f) return Mathf.Max(1, p.gear - 1);
        if (p.HeatRatio <= 0.25f && p.gear < 4) return p.gear + 1;
        return p.gear;
    }

    private static void SimSpin(PlayerState p, int rewindPos, int spinMax)
    {
        p.spinCounter++;
        p.deck.RecoverAllHeatToPool();
        p.position = rewindPos;
        p.gear = 1;
        p.skipNextTurn = true;
        if (p.spinCounter >= spinMax)
            p.isBlown = true;
    }

    private static void UnlockEverything(TechTreeState state)
    {
        state.rpBalance = 999999;
        var db = TechTreeDatabaseFactory.CreateDefault();
        // 逐层解锁（CanUnlock 会校验层级门槛）
        bool progress = true;
        while (progress)
        {
            progress = false;
            foreach (var node in db.nodes.Values)
            {
                if (!state.IsUnlocked(node.id) && TechTreeRules.CanUnlock(state, node.id, db))
                {
                    TechTreeRules.UnlockNode(state, node.id, db);
                    progress = true;
                }
            }
        }
    }

    private static GameConfigSO CreateSimConfig()
    {
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.speedCardDistribution = new[] { 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4 };
        config.initialHeatCards = 3;
        config.heatPoolPerPlayer = 6;
        config.handSize = 7;
        config.totalLaps = 3;
        return config;
    }
}
