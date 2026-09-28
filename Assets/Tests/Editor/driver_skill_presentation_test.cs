using NUnit.Framework;

public class DriverSkillRaceContextRulesTests
{
    private static PlayerState Racer(int position, int lap = 0)
    {
        return new PlayerState("Racer", false, position, 1) { position = position, lap = lap };
    }

    [Test]
    public void BehindCountUsesCircularDistanceAndExcludesIneligibleRacers()
    {
        PlayerState player = Racer(1);
        PlayerState finished = Racer(0);
        finished.hasFinished = true;
        PlayerState blown = Racer(0);
        blown.isBlown = true;
        var racers = new[] { player, null, Racer(0), Racer(59), Racer(58),
            Racer(57), Racer(2), Racer(1), Racer(0, 1), finished, blown };
        Assert.AreEqual(3, DriverSkillRaceContextRules.CountNearbyOpponentsBehind(player, racers, 60, 3));
        Assert.AreEqual(1, DriverSkillRaceContextRules.CountNearbyOpponentsBehind(player, racers, 60, 1));
        Assert.AreEqual(1, player.position);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void InvalidTrackHasNoNearbyOpponents(int totalNodes)
    {
        Assert.AreEqual(0, DriverSkillRaceContextRules.CountNearbyOpponentsBehind(
            Racer(5), new[] { Racer(4) }, totalNodes, 3));
    }

    [Test]
    public void MissingParticipantsOrPlayerReturnsZero()
    {
        Assert.AreEqual(0, DriverSkillRaceContextRules.CountNearbyOpponentsBehind(Racer(5), null, 60, 3));
        Assert.AreEqual(0, DriverSkillRaceContextRules.CountNearbyOpponentsBehind(null, new[] { Racer(4) }, 60, 3));
    }

    [Test]
    public void BuildReadsHeatAndLapWithoutConsumingResources()
    {
        PlayerState player = Racer(5, 2);
        player.deck = new CardDeck();
        player.deck.InitializeExactOrder(new CardData[0], new HeatPool(10));
        player.deck.DrawHeatFromPool(4);
        DriverSkillActivationContext context = DriverSkillRaceContextRules.Build(
            player, true, 3, new[] { player, Racer(4, 2) }, 60);
        Assert.IsTrue(context.CanAcceptInput);
        Assert.AreEqual(2, context.Lap);
        Assert.AreEqual(3, context.TotalLaps);
        Assert.AreEqual(6, context.EngineRemaining);
        Assert.AreEqual(10, context.EngineCapacity);
        Assert.AreEqual(1, context.NearbyOpponentsBehind);
        Assert.AreEqual(6, HeatGaugeRules.Evaluate(player.deck).EngineRemaining);
    }

    [Test]
    public void BuildMissingPlayerUsesSafeFactsAndPreservesInputGate()
    {
        DriverSkillActivationContext context = DriverSkillRaceContextRules.Build(null, false, 3, null, 0);
        Assert.IsFalse(context.CanAcceptInput);
        Assert.AreEqual(0, context.Lap);
        Assert.AreEqual(0, context.EngineCapacity);
        Assert.AreEqual(0, context.NearbyOpponentsBehind);
    }

    [Test]
    public void BuiltContextHonorsKyleBehindCondition()
    {
        DriverCatalog.TryGet("us_kyle_busch", out DriverProfile profile);
        var state = new DriverSkillRuntimeState();
        state.Initialize(profile, 3, true);
        PlayerState player = Racer(5);
        Assert.IsTrue(DriverSkillRules.CanActivate(profile, state,
            DriverSkillRaceContextRules.Build(player, true, 3, new[] { player, Racer(2) }, 60), out _));
        Assert.IsFalse(DriverSkillRules.CanActivate(profile, state,
            DriverSkillRaceContextRules.Build(player, true, 3, new[] { player, Racer(1) }, 60), out _));
    }
}

public class DriverSkillPresentationRulesTests
{
    private static DriverProfile Tony()
    {
        Assert.IsTrue(DriverCatalog.TryGet("us_tony_stewart", out DriverProfile profile));
        return profile;
    }

    [Test]
    public void MissingProfileOrRuntimeUsesDisabledFallback()
    {
        Assert.AreEqual("车手技能", DriverSkillPresentationRules.GetButtonLabel(null,
            new DriverSkillRuntimeState(), default, out bool enabled));
        Assert.IsFalse(enabled);
        Assert.AreEqual("车手技能", DriverSkillPresentationRules.GetButtonLabel(Tony(), null, default, out enabled));
        Assert.IsFalse(enabled);
    }

    [TestCase(false, 3, "当前模式禁用车手技能")]
    [TestCase(true, 1, "Lv3 解锁主动技能")]
    [TestCase(true, 3, "仅可在档位确认前发动")]
    public void UnavailableButtonShowsOriginalReason(bool modeEnabled, int level, string reason)
    {
        DriverProfile profile = Tony();
        var state = new DriverSkillRuntimeState();
        state.Initialize(profile, level, modeEnabled);
        Assert.AreEqual(profile.ActiveName + "  " + reason,
            DriverSkillPresentationRules.GetButtonLabel(profile, state, default, out bool enabled));
        Assert.IsFalse(enabled);
    }

    [Test]
    public void ReadyButtonIsReadOnlyAndShowsRemainingUses()
    {
        DriverProfile profile = Tony();
        var state = new DriverSkillRuntimeState();
        state.Initialize(profile, 5, true);
        int uses = state.UsesRemaining;
        var context = new DriverSkillActivationContext(true, 0, 3, 10, 10, 0);
        Assert.AreEqual(profile.ActiveName + "  ×" + uses,
            DriverSkillPresentationRules.GetButtonLabel(profile, state, context, out bool enabled));
        Assert.IsTrue(enabled);
        Assert.AreEqual(uses, state.UsesRemaining);
        Assert.AreEqual(0, state.ActiveTurnsRemaining);
    }

    [TestCase("us_tony_stewart", 5, "3 回合")]
    [TestCase("cn_ma_qinghua", 7, "本场有效")]
    public void ActiveDurationHasPriorityOverInputGate(string id, int level, string duration)
    {
        Assert.IsTrue(DriverCatalog.TryGet(id, out DriverProfile profile));
        var state = new DriverSkillRuntimeState();
        state.Initialize(profile, level, true);
        Assert.IsTrue(state.TryActivate(profile, new DriverSkillActivationContext(true, 0, 3, 10, 10, 0), out _));
        int turns = state.ActiveTurnsRemaining;
        Assert.AreEqual(profile.ActiveName + "  " + duration,
            DriverSkillPresentationRules.GetButtonLabel(profile, state, default, out bool enabled));
        Assert.IsFalse(enabled);
        Assert.AreEqual(turns, state.ActiveTurnsRemaining);
    }
}
