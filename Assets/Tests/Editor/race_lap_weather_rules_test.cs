using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RaceLapWeatherRulesTests
{
    [TestCase(1)]
    [TestCase(2)]
    public void SharedWeatherGateRejectsEarlierLapWithoutRollingBackState(int laggingLap)
    {
        var state = new RaceWeatherState();
        state.MarkLapRolled(3);

        Assert.That(RaceLapWeatherRules.Advance(laggingLap - 1, 3,
            state.LastRolledLap, true).ShouldRollWeather, Is.False);
        Assert.That(state.TryBeginLapRoll(laggingLap, true), Is.False);
        Assert.That(state.LastRolledLap, Is.EqualTo(3));
    }

    [Test]
    public void test_first_crossing_advances_lap_and_requests_weather_roll()
    {
        RaceLapWeatherTransition result = RaceLapWeatherRules.Advance(0, 3, 0, true);

        Assert.That(result.Lap, Is.EqualTo(1));
        Assert.That(result.HasFinished, Is.False);
        Assert.That(result.ShouldRollWeather, Is.True);
    }

    [Test]
    public void test_second_car_on_same_lap_does_not_request_second_roll()
    {
        RaceLapWeatherTransition result = RaceLapWeatherRules.Advance(0, 3, 1, true);

        Assert.That(result.Lap, Is.EqualTo(1));
        Assert.That(result.ShouldRollWeather, Is.False);
    }

    [Test]
    public void test_disabled_weather_does_not_consume_lap_gate()
    {
        RaceLapWeatherTransition result = RaceLapWeatherRules.Advance(0, 3, 0, false);

        Assert.That(result.Lap, Is.EqualTo(1));
        Assert.That(result.ShouldRollWeather, Is.False);
    }

    [Test]
    public void test_final_crossing_advances_and_marks_finish()
    {
        RaceLapWeatherTransition result = RaceLapWeatherRules.Advance(2, 3, 1, true);

        Assert.That(result.Lap, Is.EqualTo(3));
        Assert.That(result.HasFinished, Is.True);
        Assert.That(result.ShouldRollWeather, Is.True);
    }

    [Test]
    public void test_reset_weather_state_allows_first_lap_roll_again()
    {
        var state = new RaceWeatherState();
        state.MarkLapRolled(2);
        state.Reset();

        RaceLapWeatherTransition result = RaceLapWeatherRules.Advance(
            0,
            3,
            state.LastRolledLap,
            true);

        Assert.That(result.ShouldRollWeather, Is.True);
    }

    [Test]
    public void test_weather_gate_rejects_the_lap_that_was_already_rolled()
    {
        Assert.That(RaceLapWeatherRules.ShouldRollWeatherForLap(4, 4, true), Is.False);
    }

    [Test]
    public void test_weather_gate_accepts_a_new_lap_when_weather_is_enabled()
    {
        Assert.That(RaceLapWeatherRules.ShouldRollWeatherForLap(5, 4, true), Is.True);
    }

    [Test]
    public void test_weather_gate_does_not_consume_a_lap_when_weather_is_disabled()
    {
        Assert.That(RaceLapWeatherRules.ShouldRollWeatherForLap(5, 4, false), Is.False);
    }
}

/// <summary>
/// Exercises the actual race coordinator without starting its scene or loop.
/// Pure transition tests above do not cover session ranking, technology reset,
/// or the shared weather-roll gate across multiple cars.
/// </summary>
public class RaceLapCrossingAdapterTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private GameConfigSO config;
    private MVPGameManager manager;
    private RaceSession session;
    private CountingRandom random;

    private sealed class CountingRandom : IRandomSource
    {
        public int WeatherRolls { get; private set; }

        public int NextInt(int minimumInclusive, int maximumExclusive) => minimumInclusive;

        public double NextDouble()
        {
            WeatherRolls++;
            return 1.0; // Leave weather unchanged; count the actual roll attempts.
        }
    }

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Lap crossing adapter regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.totalLaps = 3;
        config.enableWeather = false;
        manager.config = config;
        random = new CountingRandom();
        session = new RaceSession(random)
        {
            WeatherPool = new[] { "sunny", "rain" }
        };
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, session);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(config);
    }

    private PlayerState AddPlayer(string name, bool isAi = false)
    {
        var player = new PlayerState(name, isAi, 0, 1);
        session.Players.Add(player);
        return player;
    }

    private RaceWeatherState WeatherGate => (RaceWeatherState)typeof(MVPGameManager)
        .GetField("weatherState", Private).GetValue(manager);

    [Test]
    public void OrdinaryCrossingAdvancesLapAndResetsPerLapTechnologyUse()
    {
        var player = AddPlayer("US");
        player.techState = new TechTreeState(TeamId.US)
        {
            heatReductionUsedThisLap = true
        };

        manager.OnPlayerCrossedStartFinish(player);

        Assert.That(player.lap, Is.EqualTo(1));
        Assert.That(player.hasFinished, Is.False);
        Assert.That(player.techState.heatReductionUsedThisLap, Is.False);
        Assert.That(session.NextFinishOrder, Is.EqualTo(1));
    }

    [Test]
    public void LastLapAssignsFinishExactlyOnceEvenIfCrossingIsReportedAgain()
    {
        config.totalLaps = 2;
        var player = AddPlayer("Finisher");
        player.lap = 1;

        manager.OnPlayerCrossedStartFinish(player);
        manager.OnPlayerCrossedStartFinish(player);

        Assert.That(player.lap, Is.EqualTo(2));
        Assert.That(player.hasFinished, Is.True);
        Assert.That(player.finishOrder, Is.EqualTo(1));
        Assert.That(session.NextFinishOrder, Is.EqualTo(2));
    }

    [Test]
    public void TwoFinishersKeepTheirCrossingOrder()
    {
        config.totalLaps = 1;
        var ai = AddPlayer("AI", true);
        var human = AddPlayer("Human");

        manager.OnPlayerCrossedStartFinish(ai);
        manager.OnPlayerCrossedStartFinish(human);

        Assert.That(ai.finishOrder, Is.EqualTo(1));
        Assert.That(human.finishOrder, Is.EqualTo(2));
        Assert.That(session.NextFinishOrder, Is.EqualTo(3));
    }

    [Test]
    public void TwoCarsOnSameLapOnlyTriggerOneWeatherRoll()
    {
        config.enableWeather = true;
        var first = AddPlayer("First");
        var second = AddPlayer("Second", true);

        manager.OnPlayerCrossedStartFinish(first);
        manager.OnPlayerCrossedStartFinish(second);

        Assert.That(random.WeatherRolls, Is.EqualTo(1));
        Assert.That(WeatherGate.LastRolledLap, Is.EqualTo(1));
    }

    [Test]
    public void DisabledWeatherDoesNotConsumeTheSharedLapGate()
    {
        var first = AddPlayer("First");
        var second = AddPlayer("Second", true);

        manager.OnPlayerCrossedStartFinish(first);
        Assert.That(random.WeatherRolls, Is.Zero);
        Assert.That(WeatherGate.LastRolledLap, Is.Zero);

        config.enableWeather = true;
        manager.OnPlayerCrossedStartFinish(second);
        Assert.That(random.WeatherRolls, Is.EqualTo(1));
        Assert.That(WeatherGate.LastRolledLap, Is.EqualTo(1));
    }

    [Test]
    public void NewLapAllowsNextWeatherRollWhileSameLapDoesNot()
    {
        config.enableWeather = true;
        var first = AddPlayer("First");
        var second = AddPlayer("Second", true);

        manager.OnPlayerCrossedStartFinish(first);
        manager.OnPlayerCrossedStartFinish(second);
        manager.OnPlayerCrossedStartFinish(first);

        Assert.That(random.WeatherRolls, Is.EqualTo(2));
        Assert.That(WeatherGate.LastRolledLap, Is.EqualTo(2));
        Assert.That(first.lap, Is.EqualTo(2));
        Assert.That(second.lap, Is.EqualTo(1));
    }
}

public sealed class RaceLapCrossingExecutionTests
{
    private sealed class RollCounter : IRandomSource
    {
        public int Rolls { get; private set; }
        public int NextInt(int minimumInclusive, int maximumExclusive) => minimumInclusive;
        public double NextDouble()
        {
            Rolls++;
            return 1.0;
        }
    }

    private static RaceSession Session(IRandomSource random, params PlayerState[] players)
    {
        var session = new RaceSession(random) { WeatherPool = new[] { "sunny", "rain" } };
        foreach (PlayerState player in players)
            session.Players.Add(player);
        return session;
    }

    [Test]
    public void OrdinaryCrossingAdvancesLapBeforeSoundAndMessage()
    {
        var player = new PlayerState("Driver", false, 0, 1);
        var session = Session(new SystemRandomSource(1), player);
        var events = new List<string>();

        RaceLapWeatherTransition transition = RaceLapCrossingExecution.Execute(
            player, session, new RaceWeatherState(), 3, false,
            finished => events.Add($"sound:{finished}:lap={player.lap}"),
            events.Add);

        Assert.That(transition.Lap, Is.EqualTo(1));
        Assert.That(transition.HasFinished, Is.False);
        Assert.That(events, Is.EqualTo(new[]
        {
            "sound:False:lap=1", "Driver 完成第 1 圈！"
        }));
        Assert.That(player.hasFinished, Is.False);
    }

    [Test]
    public void FinalCrossingAssignsFinishAfterSoundAndLapMessage()
    {
        var player = new PlayerState("Winner", false, 0, 1);
        var session = Session(new SystemRandomSource(1), player);
        var events = new List<string>();

        RaceLapWeatherTransition transition = RaceLapCrossingExecution.Execute(
            player, session, new RaceWeatherState(), 1, false,
            finished => events.Add($"sound:{finished}:finished={player.hasFinished}"),
            message => events.Add($"message:{message}:order={player.finishOrder}"));

        Assert.That(transition.HasFinished, Is.True);
        Assert.That(player.hasFinished, Is.True);
        Assert.That(player.finishOrder, Is.EqualTo(1));
        Assert.That(events, Is.EqualTo(new[]
        {
            "sound:True:finished=False",
            "message:Winner 完成第 1 圈！:order=0",
            "message:<color=green><b>Winner 完赛！</b></color>:order=1"
        }));
    }

    [Test]
    public void SameLapCarsShareOneWeatherRoll()
    {
        var first = new PlayerState("First", false, 0, 1);
        var second = new PlayerState("Second", true, 0, 1);
        var random = new RollCounter();
        var session = Session(random, first, second);
        var weatherState = new RaceWeatherState();

        RaceLapCrossingExecution.Execute(first, session, weatherState, 3, true,
            _ => { }, null);
        RaceLapCrossingExecution.Execute(second, session, weatherState, 3, true,
            _ => { }, null);

        Assert.That(random.Rolls, Is.EqualTo(1));
        Assert.That(weatherState.LastRolledLap, Is.EqualTo(1));
        Assert.That(first.lap, Is.EqualTo(1));
        Assert.That(second.lap, Is.EqualTo(1));
    }

    [Test]
    public void DisabledWeatherDoesNotConsumeLaterCarsGate()
    {
        var first = new PlayerState("First", false, 0, 1);
        var second = new PlayerState("Second", true, 0, 1);
        var random = new RollCounter();
        var session = Session(random, first, second);
        var weatherState = new RaceWeatherState();

        RaceLapCrossingExecution.Execute(first, session, weatherState, 3, false,
            _ => { }, null);
        Assert.That(weatherState.LastRolledLap, Is.Zero);
        RaceLapCrossingExecution.Execute(second, session, weatherState, 3, true,
            _ => { }, null);

        Assert.That(random.Rolls, Is.EqualTo(1));
        Assert.That(weatherState.LastRolledLap, Is.EqualTo(1));
    }

    [Test]
    public void SoundFailureStopsWeatherAndFinishAsBefore()
    {
        var player = new PlayerState("Driver", false, 0, 1);
        var random = new RollCounter();
        var session = Session(random, player);
        var weatherState = new RaceWeatherState();

        Assert.Throws<System.InvalidOperationException>(() =>
            RaceLapCrossingExecution.Execute(player, session, weatherState, 1, true,
                _ => throw new System.InvalidOperationException("sound failed"),
                _ => Assert.Fail("message must not run")));

        Assert.That(player.lap, Is.EqualTo(1));
        Assert.That(player.hasFinished, Is.False);
        Assert.That(player.finishOrder, Is.Zero);
        Assert.That(weatherState.LastRolledLap, Is.Zero);
        Assert.That(random.Rolls, Is.Zero);
    }
}
