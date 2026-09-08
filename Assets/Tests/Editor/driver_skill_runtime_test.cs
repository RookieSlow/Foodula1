using NUnit.Framework;

public class DriverSkillRuntimeTests
{
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
    public void SchumacherPassivePreparesAOneHeatDiscountOnItsCadence()
    {
        DriverCatalog.TryGet("de_michael_schumacher", out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 2, true);

        state.BeginTurn();
        Assert.That(state.ConsumePassiveHeatDiscount(), Is.Zero);
        state.BeginTurn();
        Assert.That(state.ConsumePassiveHeatDiscount(), Is.Zero);
        state.BeginTurn();
        Assert.That(state.ConsumePassiveHeatDiscount(), Is.EqualTo(1));
        Assert.That(state.ConsumePassiveHeatDiscount(), Is.Zero);
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
    public void MansellPassiveArmsNextTurnMovementAndHigherTierCoolingAfterOvertake()
    {
        DriverCatalog.TryGet("uk_nigel_mansell", out DriverProfile driver);
        var tierOne = new DriverSkillRuntimeState();
        tierOne.Initialize(driver, 2, true);
        tierOne.BeginTurn();
        tierOne.ResolvePassiveTurnEnd(1);
        tierOne.BeginTurn();
        Assert.That(tierOne.PassiveMovementBonusThisTurn, Is.EqualTo(1));
        Assert.That(tierOne.PassiveCoolingBonusThisTurn, Is.Zero);

        var tierTwo = new DriverSkillRuntimeState();
        tierTwo.Initialize(driver, 4, true);
        tierTwo.BeginTurn();
        tierTwo.ResolvePassiveTurnEnd(1);
        tierTwo.BeginTurn();
        Assert.That(tierTwo.PassiveMovementBonusThisTurn, Is.EqualTo(1));
        Assert.That(tierTwo.PassiveCoolingBonusThisTurn, Is.EqualTo(1));
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
        Assert.That(skillRect.anchorMax.x, Is.LessThanOrEqualTo(resetRect.anchorMin.x),
            "Driver skill and reset actions must occupy separate columns.");
        Assert.That(skillRect.anchorMin.y, Is.GreaterThanOrEqualTo(returnRect.anchorMax.y),
            "Driver skill action must stay above the return-to-menu action.");
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
