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
    [TestCase("de_sebastian_vettel")]
    [TestCase("it_alberto_ascari")]
    [TestCase("it_tazio_nuvolari")]
    [TestCase("us_tony_stewart")]
    [TestCase("us_kyle_busch")]
    [TestCase("cn_zhou_guanyu")]
    [TestCase("cn_ma_qinghua")]
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
    public void VettelPassiveOffersTopThreeLookaheadOnItsCadence()
    {
        DriverCatalog.TryGet("de_sebastian_vettel", out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 2, true);

        for (int i = 0; i < 3; i++)
        {
            state.BeginTurn();
            Assert.That(state.TryConsumePassiveDeckLookahead(out _), Is.False);
        }

        state.BeginTurn();
        Assert.That(state.TryConsumePassiveDeckLookahead(out int lookahead), Is.True);
        Assert.That(lookahead, Is.EqualTo(3));
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
    public void ZhouAndMaPassivesAreSingleUsePerTurn()
    {
        DriverCatalog.TryGet("cn_zhou_guanyu", out DriverProfile zhouDriver);
        var zhou = new DriverSkillRuntimeState();
        zhou.Initialize(zhouDriver, 2, true);
        zhou.BeginTurn();
        Assert.That(zhou.TryConsumePassiveWeatherProtection(), Is.True);
        Assert.That(zhou.TryConsumePassiveWeatherProtection(), Is.False);

        DriverCatalog.TryGet("cn_ma_qinghua", out DriverProfile maDriver);
        var maTierOne = new DriverSkillRuntimeState();
        maTierOne.Initialize(maDriver, 2, true);
        maTierOne.BeginTurn();
        Assert.That(maTierOne.ConsumePassiveCornerLimitBonus(), Is.EqualTo(1));
        Assert.That(maTierOne.ConsumePassiveCornerLimitBonus(), Is.Zero);
        Assert.That(maTierOne.ConsumePassiveCornerHeatReduction(), Is.Zero);

        var maTierTwo = new DriverSkillRuntimeState();
        maTierTwo.Initialize(maDriver, 4, true);
        maTierTwo.BeginTurn();
        Assert.That(maTierTwo.ConsumePassiveCornerHeatReduction(), Is.EqualTo(1));
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
    public void ChinaSpeedBypassesWeatherSlipstreamSuppression()
    {
        PlayerState follower = Player("cn_zhou_guanyu", TeamId.CN, 0, 5);
        PlayerState leader = Player("de_michael_schumacher", TeamId.DE, 1, 1);
        Assert.That(follower.driverSkill.TryActivate(follower.DriverProfile,
            new DriverSkillActivationContext(true, 0, 3, 10, 10, 0), out _), Is.True);

        var session = new RaceSession { Weather = WeatherType.HeavyRain };
        session.Players.Add(follower);
        session.Players.Add(leader);
        Assert.That(session.ComputeSlipstreamChain(follower, session.Players, 20).Triggered, Is.True);
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
