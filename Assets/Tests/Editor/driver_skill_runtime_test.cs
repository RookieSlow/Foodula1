using NUnit.Framework;

public class DriverSkillRuntimeTests
{
    [TestCase("uk_hunter_hart", 3)]
    [TestCase("uk_hunter_hart", 5)]
    [TestCase("uk_hunter_hart", 7)]
    [TestCase("uk_nigel_mansell", 3)]
    [TestCase("uk_nigel_mansell", 5)]
    [TestCase("uk_nigel_mansell", 7)]
    public void UkWindowExpiresAtNextOwnerInputAndCannotTickTwice(string id, int level)
    {
        DriverCatalog.TryGet(id, out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, level, true);
        state.BeginGlobalTurn();
        Assert.That(state.BeginDrivingInput(), Is.True);
        int uses = state.UsesRemaining;
        Assert.That(state.TryActivate(driver,
            new DriverSkillActivationContext(true, 2, 3, 6, 6, 0), out _), Is.True);

        state.BeginGlobalTurn();
        Assert.That(state.IsActive, Is.True, "A global round is not the owner's next input.");
        Assert.That(state.DrivingInputsStarted, Is.EqualTo(1));
        Assert.That(state.BeginDrivingInput(), Is.True);
        Assert.That(state.IsActive, Is.False);
        Assert.That(state.BeginDrivingInput(), Is.False);
        Assert.That(state.DrivingInputsStarted, Is.EqualTo(2));
        Assert.That(state.UsesRemaining, Is.EqualTo(uses - 1));
    }

    [TestCase("uk_hunter_hart")]
    [TestCase("uk_nigel_mansell")]
    public void InterruptedUkInputDoesNotRefundOrReopenUntilANewRound(string id)
    {
        DriverCatalog.TryGet(id, out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 7, true);
        state.BeginGlobalTurn();
        state.BeginDrivingInput();
        Assert.That(state.TryActivate(driver,
            new DriverSkillActivationContext(true, 2, 3, 6, 6, 0), out _), Is.True);
        int remaining = state.UsesRemaining;

        state.InterruptDrivingInput();
        state.InterruptDrivingInput();
        Assert.That(state.IsActive, Is.False);
        Assert.That(state.ActivatedThisTurn, Is.False);
        Assert.That(state.BeginDrivingInput(), Is.False);
        Assert.That(state.UsesRemaining, Is.EqualTo(remaining));
        state.BeginGlobalTurn();
        Assert.That(state.BeginDrivingInput(), Is.True);
        Assert.That(state.IsActive, Is.False);
        Assert.That(state.DrivingInputsStarted, Is.EqualTo(2));
    }

    [TestCase("uk_hunter_hart")]
    [TestCase("uk_nigel_mansell")]
    public void UkClockCopyAdvancesWithoutChangingRealDurationOrInputCount(string id)
    {
        DriverCatalog.TryGet(id, out DriverProfile driver);
        var real = new DriverSkillRuntimeState();
        real.Initialize(driver, 7, true);
        real.BeginGlobalTurn();
        real.BeginDrivingInput();
        Assert.That(real.TryActivate(driver,
            new DriverSkillActivationContext(true, 2, 3, 6, 6, 0), out _), Is.True);
        real.BeginGlobalTurn();
        DriverSkillRuntimeState preview = real.CreatePresentationCopy();

        Assert.That(preview.BeginDrivingInput(), Is.True);
        Assert.That(preview.IsActive, Is.False);
        Assert.That(real.IsActive, Is.True);
        Assert.That(real.DrivingInputsStarted, Is.EqualTo(1));
        Assert.That(preview.DrivingInputsStarted, Is.EqualTo(2));
        Assert.That(preview.UsesRemaining, Is.EqualTo(real.UsesRemaining));
    }

    [TestCase("uk_hunter_hart")]
    [TestCase("uk_nigel_mansell")]
    public void DisabledUkSkillDoesNotOpenAnInputClock(string id)
    {
        DriverCatalog.TryGet(id, out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 7, false);
        state.BeginGlobalTurn();
        Assert.That(state.BeginDrivingInput(), Is.False);
        Assert.That(state.DrivingInputsStarted, Is.Zero);
        Assert.That(state.IsActive, Is.False);
    }

    [TestCase("uk_hunter_hart")]
    [TestCase("uk_nigel_mansell")]
    public void ReinitializingInterruptedUkRuntimeStartsANewRaceClock(string id)
    {
        DriverCatalog.TryGet(id, out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 7, true);
        state.BeginGlobalTurn();
        state.BeginDrivingInput();
        Assert.That(state.TryActivate(driver,
            new DriverSkillActivationContext(true, 2, 3, 6, 6, 0), out _), Is.True);
        state.InterruptDrivingInput();

        state.Initialize(driver, 7, true);

        Assert.That(state.DrivingInputsStarted, Is.Zero);
        Assert.That(state.IsActive, Is.False);
        Assert.That(state.UsesRemaining, Is.EqualTo(3));
        Assert.That(state.BeginDrivingInput(), Is.True);
        Assert.That(state.DrivingInputsStarted, Is.EqualTo(1));
    }

    [TestCase("de_michael_schumacher")]
    [TestCase("it_tazio_nuvolari")]
    [TestCase("jp_keiichi_tsuchiya")]
    [TestCase("jp_takumi_fujiwara")]
    public void UnmigratedDriverKeepsLegacyDurationAndIgnoresUkInputHooks(string id)
    {
        DriverCatalog.TryGet(id, out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 7, true);
        Assert.That(state.TryActivate(driver,
            new DriverSkillActivationContext(true, 2, 3, 0, 10, 1), out _), Is.True);
        int duration = state.ActiveTurnsRemaining;
        state.BeginGlobalTurn();
        int expected = duration == int.MaxValue ? duration : duration - 1;

        Assert.That(state.ActiveTurnsRemaining, Is.EqualTo(expected));
        Assert.That(state.BeginDrivingInput(), Is.False);
        state.InterruptDrivingInput();
        Assert.That(state.ActiveTurnsRemaining, Is.EqualTo(expected));
        Assert.That(state.DrivingInputsStarted, Is.Zero);
    }

    [TestCase(3)] [TestCase(5)] [TestCase(7)]
    public void VettelAlwaysRejectsLegacyFreeActivationWithoutSpendingRaceUses(int level)
    {
        DriverCatalog.TryGet("de_sebastian_vettel", out DriverProfile driver);
        var state = new DriverSkillRuntimeState(); state.Initialize(driver, level, true);
        int uses = state.UsesRemaining;
        Assert.That(state.TryActivate(driver, new DriverSkillActivationContext(true, 0, 3, 6, 6, 0), out string reason), Is.False);
        Assert.That(reason, Does.Contain("费用计划")); Assert.That(state.UsesRemaining, Is.EqualTo(uses));
        Assert.That(state.IsActive, Is.False); Assert.That(DriverSkillRules.ApplyGearHeatCost(state, 2, false), Is.EqualTo(2));
    }

    [Test]
    public void ActivationConsumesOneUseAndExpiresAfterDocumentedTurns()
    {
        DriverCatalog.TryGet("us_tony_stewart", out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 5, true);

        Assert.That(state.TryActivate(driver,
            new DriverSkillActivationContext(true, 0, 3, 10, 10, 0), out _), Is.True);
        Assert.That(state.UsesRemaining, Is.Zero);
        Assert.That(state.ActiveTurnsRemaining, Is.EqualTo(3));

        state.BeginTurn();
        Assert.That(state.ActiveTurnsRemaining, Is.EqualTo(2));
        state.BeginTurn();
        Assert.That(state.ActiveTurnsRemaining, Is.EqualTo(1));
        state.BeginTurn();
        Assert.That(state.IsActive, Is.False);
    }

    [Test]
    public void UkLevelSevenReceivesThreeRaceUses()
    {
        DriverProfile driver = DriverCatalog.GetDefaultForTeam(TeamId.UK);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 7, true);
        Assert.That(state.Tier, Is.EqualTo(3));
        Assert.That(state.UsesRemaining, Is.EqualTo(3));
    }

    [Test]
    public void GutterRunConsumesOnlyItsAllowedCornerCount()
    {
        DriverCatalog.TryGet("jp_takumi_fujiwara", out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 7, true);
        Assert.That(state.TryActivate(driver,
            new DriverSkillActivationContext(true, 0, 3, 10, 10, 0), out _), Is.True);
        Assert.That(state.TryConsumeCornerIgnore(), Is.True);
        Assert.That(state.TryConsumeCornerIgnore(), Is.True);
        Assert.That(state.TryConsumeCornerIgnore(), Is.False);
    }

    [Test]
    public void SchumacherCadenceCannotPrepareGenericHeatDiscountWithoutActualCornerPayment()
    {
        DriverCatalog.TryGet("de_michael_schumacher", out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 2, true);
        for (int i = 0; i < 8; i++)
        {
            state.BeginTurn();
            Assert.That(state.ConsumePassiveHeatDiscount(), Is.Zero);
            Assert.That(state.Tuning.HasPending, Is.False);
        }
    }

    [Test]
    public void VettelSourceFeedbackDoesNotRetainLegacyCadenceLookahead()
    {
        DriverCatalog.TryGet("de_sebastian_vettel", out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 2, true);

        for (int i = 0; i < 8; i++)
        {
            state.BeginTurn();
            Assert.That(state.TryConsumePassiveDeckLookahead(out _), Is.False);
        }

        Assert.That(DriverSkillRules.GetPassiveDeckLookahead(DriverPassiveSkillId.InformationSponge, 1), Is.Zero);
        Assert.That(state.TryConsumePassiveDeckLookahead(out _), Is.False);
    }

    [Test]
    public void MansellLegacyOvertakeHookNoLongerAwardsMovementOrCooling()
    {
        DriverCatalog.TryGet("uk_nigel_mansell", out DriverProfile driver);
        var tierOne = new DriverSkillRuntimeState();
        tierOne.Initialize(driver, 2, true);
        tierOne.BeginTurn();
        tierOne.ResolvePassiveTurnEnd(1);
        tierOne.BeginTurn();
        Assert.That(tierOne.PassiveMovementBonusThisTurn, Is.Zero);
        Assert.That(tierOne.PassiveCoolingBonusThisTurn, Is.Zero);

        var tierTwo = new DriverSkillRuntimeState();
        tierTwo.Initialize(driver, 4, true);
        tierTwo.BeginTurn();
        tierTwo.ResolvePassiveTurnEnd(1);
        tierTwo.BeginTurn();
        Assert.That(tierTwo.PassiveMovementBonusThisTurn, Is.Zero);
        Assert.That(tierTwo.PassiveCoolingBonusThisTurn, Is.Zero);
    }

    [Test]
    public void ZhouInputClockAndMaPassivesAreSingleUsePerTurn()
    {
        DriverCatalog.TryGet("cn_zhou_guanyu", out DriverProfile zhouDriver);
        var zhou = new DriverSkillRuntimeState();
        zhou.Initialize(zhouDriver, 2, true);
        zhou.BeginTurn();
        Assert.That(zhou.BeginDrivingInput(), Is.True);
        Assert.That(zhou.IsDrivingInputOpen, Is.True);
        Assert.That(zhou.BeginDrivingInput(), Is.False);

        DriverCatalog.TryGet("cn_ma_qinghua", out DriverProfile maDriver);
        var ma = new DriverSkillRuntimeState();
        ma.Initialize(maDriver, 4, true);
        ma.BeginGlobalTurn();
        Assert.That(ma.BeginDrivingInput(), Is.True);
        Assert.That(ma.ConsumePassiveCornerLimitBonus(), Is.Zero,
            "Ma's passive is gated by an actual Recover -> Go transition.");
        ma.ResolveMaSubmittedInput(isGo: false, completed: true);
        ma.BeginGlobalTurn();
        Assert.That(ma.BeginDrivingInput(), Is.True);
        ma.ResolveMaSubmittedInput(isGo: true, completed: true);
        Assert.That(ma.IsMaPassiveApexReady, Is.True);
        Assert.That(ma.ConsumeMaPassiveCornerLimitBonus(), Is.EqualTo(1));
        Assert.That(ma.ConsumeMaPassiveCornerLimitBonus(), Is.Zero);
        Assert.That(ma.ConsumeMaPassiveCornerHeatReduction(), Is.EqualTo(1));
    }

    [Test]
    public void AuthoredRaceCanvasExposesDriverSkillButtonAndLabel()
    {
        UnityEngine.GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
            "Assets/Prefabs/UI/RaceCanvas.prefab");
        Assert.That(prefab, Is.Not.Null);
        HUDUI hud = prefab.GetComponentInChildren<HUDUI>(true);
        Assert.That(hud, Is.Not.Null);
        Assert.That(hud.driverSkillButton, Is.Not.Null);
        Assert.That(hud.driverSkillLabel, Is.Not.Null);
        Assert.That(hud.driverSkillButton.name, Is.EqualTo("DriverSkillBtn"));

        var skillRect = (UnityEngine.RectTransform)hud.driverSkillButton.transform;
        var resetRect = (UnityEngine.RectTransform)hud.resetButton.transform;
        var returnRect = (UnityEngine.RectTransform)hud.returnToMenuButton.transform;
        Assert.That(skillRect.anchorMin.x,
            Is.EqualTo(RaceUILayoutController.DriverSkillAnchorMin.x).Within(0.001f));
        Assert.That(skillRect.anchorMax.x,
            Is.EqualTo(RaceUILayoutController.DriverSkillAnchorMax.x).Within(0.001f));
        Assert.That(skillRect.anchorMax.y, Is.LessThanOrEqualTo(resetRect.anchorMin.y),
            "Driver skill action must stay below the destructive top actions.");
        Assert.That(resetRect.anchorMax.y, Is.LessThanOrEqualTo(returnRect.anchorMin.y),
            "Reset must stay below return-to-menu in the top action area.");
    }

    [Test]
    public void ChinaSpeedRequiresTheNewEscortOverrideForBlockedSlipstream()
    {
        PlayerState follower = Player("cn_zhou_guanyu", TeamId.CN, 0, 5);
        PlayerState leader = Player("de_michael_schumacher", TeamId.DE, 1, 1);
        Assert.That(follower.driverSkill.TryActivate(follower.DriverProfile,
            new DriverSkillActivationContext(true, 0, 3, 10, 10, 0), out _), Is.True);

        var session = new RaceSession { Weather = WeatherType.HeavyRain };
        session.Players.Add(follower);
        session.Players.Add(leader);
        Assert.That(session.ComputeSlipstreamChain(follower, session.Players, 20).Triggered, Is.False,
            "The old weather-immunity shortcut is removed; escort is now a card/old-heat commitment.");
    }

    [Test]
    public void SmokeScreenBlocksARealTrailingSlipstream()
    {
        PlayerState follower = Player("de_michael_schumacher", TeamId.DE, 0, 1);
        PlayerState leader = Player("jp_keiichi_tsuchiya", TeamId.JP, 1, 5);
        Assert.That(leader.driverSkill.TryActivate(leader.DriverProfile,
            new DriverSkillActivationContext(true, 0, 3, 10, 10, 0), out _), Is.True);

        var session = new RaceSession { Weather = WeatherType.Sunny };
        session.Players.Add(follower);
        session.Players.Add(leader);
        Assert.That(session.ComputeSlipstreamChain(follower, session.Players, 20).Triggered, Is.False);
    }

    [Test]
    public void StewartTrackFuryArmsOnActualOvertakeAndConsumesOneOwnerInput()
    {
        DriverCatalog.TryGet("us_tony_stewart", out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 7, true);

        state.BeginGlobalTurn();
        Assert.That(state.BeginDrivingInput(), Is.True);
        state.ResolvePassiveTurnEnd(1, 0);

        state.BeginGlobalTurn();
        Assert.That(state.BeginDrivingInput(), Is.True);
        Assert.That(state.PassiveMovementBonusThisTurn, Is.EqualTo(2), "L3 Track Fury is +2 after a real overtake.");

        state.BeginGlobalTurn();
        Assert.That(state.BeginDrivingInput(), Is.True);
        Assert.That(state.PassiveMovementBonusThisTurn, Is.Zero, "The passive window is not deferred to a second input.");
    }

    [Test]
    public void StewartTrackFuryL3RecoveryIsSafeStraightOnlyAndOncePerInput()
    {
        DriverCatalog.TryGet("us_tony_stewart", out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 7, true);

        state.BeginGlobalTurn();
        state.BeginDrivingInput();
        state.ResolvePassiveTurnEnd(1, 0);
        state.BeginGlobalTurn();
        state.BeginDrivingInput();

        Assert.That(state.TryConsumeTrackFuryRecovery(true), Is.True);
        Assert.That(state.TryConsumeTrackFuryRecovery(true), Is.False);

        state.BeginGlobalTurn();
        state.BeginDrivingInput();
        state.ResolvePassiveTurnEnd(1, 1);
        state.BeginGlobalTurn();
        state.BeginDrivingInput();
        Assert.That(state.TryConsumeTrackFuryRecovery(false), Is.False);
        Assert.That(state.TryConsumeTrackFuryRecovery(true), Is.False, "A corner input consumes the L3 opportunity without deferring it.");
    }

    [Test]
    public void StewartTrueSelfFixedFeeResolvesAfterCardSelectionWithoutSpinningOnShortage()
    {
        DriverCatalog.TryGet("us_tony_stewart", out DriverProfile driver);
        var paid = new DriverSkillRuntimeState();
        paid.Initialize(driver, 3, true);
        paid.BeginGlobalTurn();
        paid.BeginDrivingInput();
        Assert.That(paid.TryActivate(driver,
            new DriverSkillActivationContext(true, 0, 3, 1, 6, 0), out _), Is.True);
        Assert.That(DriverSkillRules.GetMovementBonus(paid, false), Is.EqualTo(2));
        paid.ResolveTrueSelfInputPayment(true);
        Assert.That(DriverSkillRules.GetReactionHeatCost(paid), Is.Zero);

        var exhausted = new DriverSkillRuntimeState();
        exhausted.Initialize(driver, 3, true);
        exhausted.BeginGlobalTurn();
        exhausted.BeginDrivingInput();
        Assert.That(exhausted.TryActivate(driver,
            new DriverSkillActivationContext(true, 0, 3, 1, 6, 0), out _), Is.True);
        exhausted.ResolveTrueSelfInputPayment(false);
        Assert.That(exhausted.IsActive, Is.False);
        Assert.That(DriverSkillRules.GetMovementBonus(exhausted, false), Is.Zero);
    }

    [Test]
    public void BuschPressureUsesOneStableTargetAndRefusalBlocksOnlyThatFollow()
    {
        PlayerState owner = Player("us_kyle_busch", TeamId.US, 10, 7);
        PlayerState target = Player("de_michael_schumacher", TeamId.DE, 8, 7);
        var session = new RaceSession();
        session.Players.Add(owner);
        session.Players.Add(target);

        Assert.That(owner.driverSkill.Pressure.CommitActivePressure(owner, target, 0), Is.True);
        Assert.That(target.driverSkill.Pressure.BeginTargetInput(session, target), Is.True);
        Assert.That(target.driverSkill.Pressure.HasIncomingDecision, Is.True);
        Assert.That(target.driverSkill.Pressure.CommitIncomingDecision(false), Is.True);
        Assert.That(target.driverSkill.Pressure.AllowsFollow(owner), Is.False);
        Assert.That(owner.driverSkill.Pressure.CanArmOutgoing(0), Is.False);

        target.position = 7;
        Assert.That(owner.driverSkill.Pressure.CommitActivePressure(owner, target, 0), Is.False,
            "A second pressure cannot redirect to another target in the same lap.");
    }

    [Test]
    public void BuschVillainAuraArmsTheLockedFrontTargetOnlyAfterActualOvertake()
    {
        PlayerState owner = Player("us_kyle_busch", TeamId.US, 5, 7);
        PlayerState target = Player("de_michael_schumacher", TeamId.DE, 7, 7);
        var session = new RaceSession();
        session.Players.Add(owner);
        session.Players.Add(target);

        owner.driverSkill.Pressure.BeginOwnerInput(session, owner, 20);
        Assert.That(owner.driverSkill.Pressure.LockedFrontTarget, Is.SameAs(target));
        Assert.That(owner.driverSkill.Pressure.ResolvePassiveActualOvertake(owner, 0, false), Is.False);
        Assert.That(target.driverSkill.Pressure.HasIncomingMarker, Is.False);

        owner.driverSkill.Pressure.BeginOwnerInput(session, owner, 20);
        Assert.That(owner.driverSkill.Pressure.ResolvePassiveActualOvertake(owner, 0, true), Is.True);
        Assert.That(target.driverSkill.Pressure.BeginTargetInput(session, target), Is.True);
        Assert.That(target.driverSkill.Pressure.CommitIncomingDecision(true), Is.True);
        Assert.That(target.driverSkill.Pressure.AllowsFollow(owner), Is.True);
    }

    [Test]
    public void CardDeckTrackFuryRecoveryReturnsOnlyPermanentHandHeat()
    {
        var deck = new CardDeck();
        deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 2) }, new HeatPool(3));
        Assert.That(deck.DrawHeatFromPoolToHand(1), Is.EqualTo(1));
        int before = deck.heatPool.remaining;
        Assert.That(deck.RemoveOnePermanentHeatFromHand(), Is.EqualTo(1));
        Assert.That(deck.heatPool.remaining, Is.EqualTo(before + 1));

        var temporary = CardData.CreateTempHeat();
        deck.AddCardsToHand(new[] { temporary });
        Assert.That(deck.RemoveOnePermanentHeatFromHand(), Is.EqualTo(0));
        Assert.That(deck.ContainsInHand(temporary), Is.True);
    }

    private static PlayerState Player(string driverId, TeamId team, int position, int level)
    {
        DriverCatalog.TryGet(driverId, out DriverProfile driver);
        var player = new PlayerState(driver.ShortName, false, position, 1)
        {
            driverId = driverId,
            teamId = team
        };
        player.driverSkill.Initialize(driver, level, true);
        return player;
    }
}
