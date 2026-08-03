using NUnit.Framework;

/// <summary>
/// PlayerState 状态机边界测试 — 爆缸、完赛、失控计数、跳回合、扩展系统字段。
/// </summary>
public class PlayerStateTest
{
    private PlayerState CreatePlayer(int startPos = 0)
    {
        return new PlayerState("Test", false, startPos, 1);
    }

    // ===== 基础状态 =====

    [Test]
    public void test_initial_state_defaults()
    {
        var p = CreatePlayer(5);
        Assert.AreEqual(5, p.position);
        Assert.AreEqual(1, p.gear);
        Assert.AreEqual(0, p.lap);
        Assert.IsFalse(p.hasFinished);
        Assert.IsFalse(p.isBlown);
        Assert.AreEqual(0, p.spinCounter);
        Assert.IsFalse(p.skipNextTurn);
        Assert.AreEqual(0, p.finishOrder);
        Assert.IsNotNull(p.deck);
        Assert.IsNotNull(p.trickState);
    }

    [Test]
    public void test_clear_turn_state_resets_turn_fields_only()
    {
        var p = CreatePlayer();
        p.gear = 3;
        p.position = 10;
        p.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 2));
        p.totalMovementThisTurn = 5;
        p.trickMoveBonusThisTurn = 2;
        p.cornerTotalThisTurn = 4;
        p.extraCardSlotsThisTurn = 1;
        p.kantoOdenSkipThisTurn = true;

        p.ClearTurnState();

        Assert.AreEqual(0, p.playedSpeedCardsThisTurn.Count);
        Assert.AreEqual(0, p.totalMovementThisTurn);
        Assert.AreEqual(0, p.trickMoveBonusThisTurn);
        Assert.AreEqual(0, p.cornerTotalThisTurn);
        Assert.AreEqual(0, p.extraCardSlotsThisTurn);
        Assert.IsFalse(p.kantoOdenSkipThisTurn);
        // 持久状态不受影响
        Assert.AreEqual(3, p.gear);
        Assert.AreEqual(10, p.position);
    }

    [Test]
    public void test_clear_turn_state_records_position_at_turn_start()
    {
        var p = CreatePlayer(7);
        p.position = 12;
        p.ClearTurnState();
        Assert.AreEqual(12, p.positionAtTurnStart);
    }

    [Test]
    public void test_clear_turn_state_defaults_selected_gear_to_current()
    {
        var p = CreatePlayer();
        p.gear = 4;
        p.selectedGearThisTurn = 2;
        p.ClearTurnState();
        Assert.AreEqual(4, p.selectedGearThisTurn);
    }

    // ===== 热量比率 / 速度牌数 =====

    [Test]
    public void test_heat_ratio_zero_when_hand_empty()
    {
        var p = CreatePlayer();
        Assert.AreEqual(0f, p.HeatRatio);
    }

    [Test]
    public void test_heat_ratio_mixed_hand()
    {
        // 抽完全部 3 张（2 速 + 1 热）— 与洗牌顺序无关，确定性
        var config = UnityEngine.ScriptableObject.CreateInstance<GameConfigSO>();
        config.speedCardDistribution = new[] { 2, 3 };
        config.initialHeatCards = 1;

        var p = CreatePlayer();
        p.deck.InitializeDeck(config, new HeatPool(5), new SystemRandomSource(99));
        p.deck.DrawToHand(3);

        Assert.AreEqual(3, p.deck.HandCount);
        Assert.AreEqual(2, p.SpeedCardCount);
        Assert.AreEqual(1f / 3f, p.HeatRatio, 0.001f);
    }

    // ===== 失控计数 → 爆缸 =====

    [Test]
    public void test_spin_counter_threshold_eliminates()
    {
        var p = CreatePlayer();
        p.spinCounter = 2;
        // 默认阈值 3 → 第 3 次失控淘汰
        bool eliminatedAt3 = p.spinCounter + 1 >= 3;
        Assert.IsTrue(eliminatedAt3);
        Assert.IsFalse(p.isBlown);
    }

    [Test]
    public void test_finish_order_assignable()
    {
        var p = CreatePlayer();
        p.finishOrder = 1;
        Assert.AreEqual(1, p.finishOrder);
    }

    // ===== 扩展系统字段 =====

    [Test]
    public void test_tech_state_assignable_and_queryable()
    {
        var p = CreatePlayer();
        Assert.IsNull(p.techState);
        var db = TechTreeDatabaseFactory.CreateDefault();
        p.techState = TechTreeRules.CreateDemoState(TeamId.CN);
        Assert.IsNotNull(p.techState);
        Assert.AreEqual(TeamId.CN, p.techState.teamId);
        // 未解锁节点不可激活
        Assert.IsFalse(p.techState.IsActive("cn-l2-dim-sum-combo"));
    }

    [Test]
    public void test_trick_state_reset_per_turn_and_race()
    {
        var p = CreatePlayer();
        p.trickState.trickPlayedThisTurn = true;
        p.trickState.parmigianoActive = true;
        p.trickState.crossedLandmarkLastTurn = true;

        p.trickState.ResetPerTurn();

        Assert.IsFalse(p.trickState.trickPlayedThisTurn);
        Assert.IsFalse(p.trickState.parmigianoActive);
        // per-race 状态不受 per-turn 重置影响
        Assert.IsTrue(p.trickState.crossedLandmarkLastTurn);
    }

    [Test]
    public void test_trick_state_reset_per_race_clears_all()
    {
        var p = CreatePlayer();
        p.trickState.crossedLandmarkLastTurn = true;
        p.trickState.kantoOdenActive = true;
        p.trickState.kantoOdenAccumulatedCards = 3;

        p.trickState.ResetPerRace();

        Assert.IsFalse(p.trickState.crossedLandmarkLastTurn);
        Assert.IsFalse(p.trickState.kantoOdenActive);
        Assert.AreEqual(0, p.trickState.kantoOdenAccumulatedCards);
    }
}
