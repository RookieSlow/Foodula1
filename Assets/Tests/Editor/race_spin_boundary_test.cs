using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RaceSpinRulesTests
{
    [TestCase(WeatherType.Sunny, 0, 3, 1, false)]
    [TestCase(WeatherType.Cloudy, 1, 3, 2, false)]
    [TestCase(WeatherType.LightRain, 1, 3, 3, true)]
    [TestCase(WeatherType.HeavyRain, 2, 3, 5, true)]
    [TestCase(WeatherType.Sunny, 2, 4, 3, false)]
    [TestCase(WeatherType.Sunny, 3, 4, 4, true)]
    [TestCase(WeatherType.HeavyRain, 3, 4, 6, true)]
    public void WeatherAndEffectiveCapPreserveUnclampedCounter(WeatherType weather, int counter, int cap, int next, bool eliminated)
    {
        RaceSpinOutcome result = RaceSpinRules.Evaluate(counter, weather, cap);
        Assert.AreEqual(next, result.Counter);
        Assert.AreEqual(eliminated, result.Eliminated);
    }

    [TestCase(TeamId.CN, 2, ChinaGearShiftRules.RecoverGear)]
    [TestCase(TeamId.US, 2, 2)]
    [TestCase(TeamId.UK, 2, 2)]
    [TestCase(TeamId.DE, 2, 2)]
    [TestCase(TeamId.IT, 2, 2)]
    [TestCase(TeamId.JP, 2, 2)]
    public void ChinaRecoverIsIndependentOfStandardMinimum(TeamId team, int minimum, int expected)
        => Assert.AreEqual(expected, RaceSpinRules.GetRecoveryGear(team, minimum));
}

/// <summary>Real failed-payment/spin adapter, with no scene startup, cars, UI, FX or persistence.</summary>
public class RaceSpinBoundaryIntegrationTests
{
    private GameObject host;
    private GameConfigSO config;
    private MVPGameManager manager;
    private RaceSession session;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Spin boundary integration");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.minGear = 2;
        manager.config = config;
        session = new RaceSession(new SystemRandomSource(1));
        typeof(MVPGameManager).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, session);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(config);
    }

    private PlayerState Player(TeamId team, bool ai)
    {
        var player = new PlayerState("driver", ai, 17, 4) { teamId = team, chinaConsecutiveGearCount = 3 };
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 3) }, new HeatPool(6));
        player.deck.DrawHeatFromPool(2, HeatPaymentDestination.Hand);
        player.deck.DrawHeatFromPool(3, HeatPaymentDestination.Discard);
        player.deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
        session.Players.Add(player);
        return player;
    }

    [TestCase(false, TeamId.CN, WeatherType.Sunny, 0, 1)]
    [TestCase(true, TeamId.CN, WeatherType.HeavyRain, 2, 5)]
    [TestCase(false, TeamId.US, WeatherType.LightRain, 1, 3)]
    [TestCase(true, TeamId.US, WeatherType.Sunny, 0, 1)]
    [TestCase(false, TeamId.DE, WeatherType.HeavyRain, 0, 3)]
    [TestCase(true, TeamId.JP, WeatherType.Cloudy, 1, 2)]
    public void PartialPaymentSpinsAndRecoversWithoutDuplicatingHeat(bool ai, TeamId team, WeatherType weather, int before, int expected)
    {
        var player = Player(team, ai);
        player.spinCounter = before;
        session.Weather = weather;
        Assert.IsFalse(manager.TryPayHeat(player, 2, 9, "engine failure"));
        Assert.AreEqual(expected, player.spinCounter);
        Assert.AreEqual(expected >= 3, player.isBlown);
        Assert.AreEqual(9, player.position);
        Assert.AreEqual(team == TeamId.CN ? ChinaGearShiftRules.RecoverGear : 2, player.gear);
        Assert.AreEqual(0, player.chinaConsecutiveGearCount);
        Assert.IsTrue(player.skipNextTurn);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountPermanentHeatOutsideEngine());
        Assert.AreEqual(0, player.deck.CountHeatInHand());
        Assert.AreEqual(1, player.heatPaidCardsThisTurn.Count, "Actual partial receipt is retained until turn reset.");
        session.BeginTurn(player);
        Assert.IsEmpty(player.heatPaidCardsThisTurn);
        Assert.AreEqual(expected >= 3 ? RaceTurnStartAction.ExcludeTerminal : RaceTurnStartAction.ResolveSkip,
            RaceTurnRules.GetStartAction(player));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AlreadyBlownSpinIsANoOp(bool ai)
    {
        var player = Player(TeamId.US, ai);
        player.isBlown = true;
        player.spinCounter = 3;
        manager.HandleSpin(player, 9, "late spin");
        Assert.AreEqual(3, player.spinCounter);
        Assert.AreEqual(17, player.position);
        Assert.AreEqual(4, player.gear);
        Assert.AreEqual(3, player.chinaConsecutiveGearCount);
        Assert.IsFalse(player.skipNextTurn);
        Assert.AreEqual(1, player.deck.heatPool.remaining);
        Assert.AreEqual(5, player.deck.CountPermanentHeatOutsideEngine());
    }

    [TestCase(2, false)]
    [TestCase(3, true)]
    public void ActualTechnologyCapIsUsedBeforeSpin(int before, bool eliminated)
    {
        var player = Player(TeamId.IT, false);
        session.TechDb = TechTreeDatabaseFactory.CreateDefault();
        player.techState = new TechTreeState(TeamId.IT, 0);
        player.techState.activeNodeIds.Add("it-l2-lasagna");
        Assert.AreEqual(4, session.EffectiveSpinMax(player));
        player.spinCounter = before;
        manager.HandleSpin(player, 9, "overspeed");
        Assert.AreEqual(before + 1, player.spinCounter);
        Assert.AreEqual(eliminated, player.isBlown);
    }
}
