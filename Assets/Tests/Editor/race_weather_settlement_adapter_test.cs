using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

// Shares inactive objects, actual movement and memory-only settlement fixtures.
// Weather draws use the authored weighted pool, not an animated race/GameLoop.
public sealed partial class RaceResultSettlementAdapterTests
{
    [TestCase(TeamId.UK, false)] [TestCase(TeamId.DE, false)]
    [TestCase(TeamId.IT, false)] [TestCase(TeamId.US, false)]
    [TestCase(TeamId.CN, false)] [TestCase(TeamId.JP, false)]
    [TestCase(TeamId.UK, true)] [TestCase(TeamId.DE, true)]
    [TestCase(TeamId.IT, true)] [TestCase(TeamId.US, true)]
    [TestCase(TeamId.CN, true)] [TestCase(TeamId.JP, true)]
    public void LeaderWeatherRollsAreNotRepeatedByLaggingCarsBeforeSettlement(TeamId team, bool career)
    {
        using (new SilentMovementAudio())
        {
            MemoryCareerStore store = null;
            CareerRepository repository = career ? CreateSeason(team, out store) : null;
            CareerRaceLaunchRequest launch = career
                ? CreateLaunch(repository.Load().State, "weather-" + team) : null;
            CreateDrivingParticipants(team, launch);
            TrackConfig track = manager.trackManager.LoadedTrackConfig;
            Assert.That(track.laps, Is.EqualTo(3));
            Assert.That(track.defaultWeather, Is.EqualTo("sunny"));
            // Preserve duplicate entries: the requested RNG upper bounds guard
            // the actual weighted candidate lists, not a deduplicated pool.
            string[] afterSunny = track.weatherPool.Where(id => id != "sunny").ToArray();
            string[] afterRain = track.weatherPool.Where(id => id != "light_rain").ToArray();
            var random = new SettlementWeatherRandom(new[] { 0.9, 0.1, 0.1 },
                new[] { Array.IndexOf(afterSunny, "light_rain"), Array.IndexOf(afterRain, "heavy_rain") },
                new[] { afterSunny.Length, afterRain.Length });
            EnableSettlementWeather(random, track.weatherPool, track.defaultWeather);
            var messages = new List<string>();
            hud.SetLogSink(messages.Add);
            int[] rp = session.Players.Select(p => p.techState.rpBalance).ToArray();
            int[] xp = session.Players.Select(p => p.driverXp).ToArray();
            string jsonBefore = store?.Json;

            // One leader crosses all three laps before the other cars. A
            // same-lap-only gate would roll laps 1/2/3 again for each follower.
            AdvanceInstantly(session.Players[2], 3 * manager.trackManager.TotalNodes);
            Assert.That(session.Players[2].finishOrder, Is.EqualTo(1));
            Assert.That(SettlementWeatherGate.LastRolledLap, Is.EqualTo(3));
            Assert.That(session.Weather, Is.EqualTo(WeatherType.HeavyRain));
            AssertDrivingRaceActive();
            DriveFieldToFinish(new[] { 1, 0, 3 }, 2);

            random.AssertConsumed(3, 2);
            Assert.That(SettlementWeatherGate.LastRolledLap, Is.EqualTo(3));
            Assert.That(session.Human.finishOrder, Is.EqualTo(3));
            Assert.That(messages.Count(m => m.Contains("天气变化:")), Is.EqualTo(2));
            Assert.That(messages.Count(m => m.EndsWith(" 圈！")), Is.EqualTo(12));
            Assert.That(messages.Count(m => m.Contains(" 完赛！")), Is.EqualTo(4));
            int lastChange = messages.FindLastIndex(m => m.Contains("天气变化:"));
            int firstFinish = messages.FindIndex(m => m.Contains(" 完赛！"));
            Assert.That(lastChange, Is.LessThan(firstFinish));
            Assert.That(store?.Json, Is.EqualTo(jsonBefore), "Movement does not save career JSON.");
            SettleWeatherRace(repository, store, team, rp, xp, false, 3);
            random.AssertConsumed(3, 2); // Settlement itself consumes no weather randomness.
            AssertPresented();
        }
    }

    [TestCase(TeamId.UK, false)] [TestCase(TeamId.IT, false)]
    [TestCase(TeamId.UK, true)] [TestCase(TeamId.IT, true)]
    public void HeavyRainRetirementDoesNotEndRemainingRacersOrLeakRewards(TeamId team, bool career)
    {
        using (new SilentMovementAudio())
        {
            MemoryCareerStore store = null;
            CareerRepository repository = career ? CreateSeason(team, out store) : null;
            CareerRaceLaunchRequest launch = career
                ? CreateLaunch(repository.Load().State, "wet-dnf-" + team) : null;
            CreateDrivingParticipants(team, launch);
            var random = new SettlementWeatherRandom(new[] { 0.9, 0.9, 0.9 });
            EnableSettlementWeather(random, manager.trackManager.LoadedTrackConfig.weatherPool, "heavy_rain");
            int[] rp = session.Players.Select(p => p.techState.rpBalance).ToArray();
            int[] xp = session.Players.Select(p => p.driverXp).ToArray();
            PlayerState human = session.Human;
            CrossWeatherLap(human, 1, 0);
            int spinMax = session.EffectiveSpinMax(human);
            int spins = (spinMax + 2) / 3; // GDD: heavy rain adds two to the base one.
            for (int i = 0; i < spins; i++)
            {
                manager.HandleSpin(human, human.position, "heavy rain settlement fixture");
                Assert.That(human.spinCounter, Is.EqualTo(3 * (i + 1)));
                Assert.That(human.isBlown, Is.EqualTo(i == spins - 1));
            }
            Assert.That(human.lap, Is.EqualTo(1));
            Assert.That(human.hasFinished, Is.False);
            Assert.That(human.finishOrder, Is.Zero);
            Assert.That(human.skipNextTurn, Is.True);
            AssertDrivingRaceActive();
            DriveFieldToFinish(new[] { 3, 1, 2 });
            Assert.That(session.NextFinishOrder, Is.EqualTo(4));
            random.AssertConsumed(3, 0);
            SettleWeatherRace(repository, store, team, rp, xp, true, 4);
            Assert.That(hud.gameOverText.text, Does.Contain("[爆缸]"));
            random.AssertConsumed(3, 0);
            AssertPresented();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EmptyOrSingleWeatherPoolKeepsGateMonotonicWithoutDrawingRandomness(bool singleton)
    {
        using (new SilentMovementAudio())
        {
            CreateDrivingParticipants(TeamId.UK);
            var random = new SettlementWeatherRandom(Array.Empty<double>());
            EnableSettlementWeather(random, singleton ? new[] { "sunny" } : Array.Empty<string>(), "sunny");
            AdvanceInstantly(session.Players[2], 3 * manager.trackManager.TotalNodes);
            foreach (int index in new[] { 1, 0, 3 })
            {
                PlayerState player = session.Players[index];
                for (int lap = 1; lap <= 3; lap++)
                {
                    CrossWeatherLap(player, lap, lap == 3 ? session.NextFinishOrder : 0);
                    Assert.That(SettlementWeatherGate.LastRolledLap, Is.EqualTo(3));
                    Assert.That(session.Weather, Is.EqualTo(WeatherType.Sunny));
                }
            }
            Assert.That(IsDrivingRaceOver(), Is.True);
            int writes = 0;
            Complete(_ => writes++, (_, __) => writes++,
                () => throw new InvalidOperationException("Ordinary race must not open career storage."));
            Assert.That(writes, Is.EqualTo(2));
            random.AssertConsumed(0, 0);
            AssertPresented();
        }
    }

    private RaceWeatherState SettlementWeatherGate => GetField<RaceWeatherState>("weatherState");

    private void EnableSettlementWeather(SettlementWeatherRandom random, string[] pool, string initial)
    {
        config.enableWeather = true;
        SettlementWeatherGate.Reset();
        session.Random = random;
        session.InitializeWeather(pool, initial);
        Assert.That(session.WeatherPool, Is.SameAs(pool));
        Assert.That(SettlementWeatherGate.LastRolledLap, Is.Zero);
    }

    private void CrossWeatherLap(PlayerState player, int lap, int finishOrder)
    {
        AdvanceInstantly(player, manager.trackManager.TotalNodes - 1);
        Assert.That(player.lap, Is.EqualTo(lap - 1));
        Assert.That(player.hasFinished, Is.False);
        AdvanceInstantly(player, 1);
        Assert.That(player.lap, Is.EqualTo(lap));
        Assert.That(player.finishOrder, Is.EqualTo(finishOrder));
    }

    private void SettleWeatherRace(CareerRepository repository, MemoryCareerStore store,
        TeamId team, int[] rp, int[] xp, bool dnf, int rank)
    {
        if (repository != null)
        {
            int before = store.SuccessfulWrites;
            CompleteCareer(repository);
            CareerSeasonState saved = repository.Load().State;
            var result = saved.RaceResults.Single().Standings.Single(p => p.TeamId == team);
            Assert.That(saved.NextTrackIndex, Is.EqualTo(1));
            Assert.That(result.DidNotFinish, Is.EqualTo(dnf));
            Assert.That(result.FinishPosition, Is.EqualTo(dnf ? 0 : rank));
            Assert.That(CareerModeRules.GetStandings(saved).Single(p => p.TeamId == team).Points,
                Is.EqualTo(dnf ? 0 : 4));
            Assert.That(store.SuccessfulWrites, Is.EqualTo(before + 1));
            Assert.That(store.KeysWritten, Has.All.EqualTo(CareerRepository.SaveKey));
            Assert.That(session.Players.Select(p => p.techState.rpBalance), Is.EqualTo(rp));
            Assert.That(session.Players.Select(p => p.driverXp), Is.EqualTo(xp));
            Assert.That(cache[team].rpBalance, Is.EqualTo(77));
        }
        else
        {
            var calls = new List<string>();
            Complete(state =>
            {
                Assert.That(state, Is.SameAs(session.Human.techState));
                Assert.That(state.rpBalance, Is.EqualTo(rp[0] + TechTreeRules.CalculateRaceRP(rank)));
                Assert.That(hud.gameOverPanel.activeSelf, Is.False);
                calls.Add("RP");
            }, (id, total) =>
            {
                Assert.That(id, Is.EqualTo(session.Human.driverId));
                Assert.That(total, Is.EqualTo(xp[0] + (dnf ? 0 : DriverProgression.CalculateRaceXp(
                    rank, session.Human.DriverProfile.TalentMultiplier, team))));
                Assert.That(calls, Is.EqualTo(new[] { "RP" }));
                calls.Add("XP");
            }, () => throw new InvalidOperationException("Ordinary weather must not open career storage."));
            Assert.That(calls, Is.EqualTo(new[] { "RP", "XP" }));
        }
    }

    private sealed class SettlementWeatherRandom : IRandomSource
    {
        private readonly Queue<double> doubles;
        private readonly Queue<int> integers;
        private readonly Queue<int> expectedUpperBounds;
        private int doubleCalls;
        private int integerCalls;

        public SettlementWeatherRandom(double[] rolls, int[] choices = null, int[] upperBounds = null)
        {
            doubles = new Queue<double>(rolls);
            integers = new Queue<int>(choices ?? Array.Empty<int>());
            expectedUpperBounds = new Queue<int>(upperBounds ?? Array.Empty<int>());
        }

        public double NextDouble()
        {
            Assert.That(doubles.Count, Is.GreaterThan(0), "Unexpected duplicate weather roll.");
            doubleCalls++;
            return doubles.Dequeue();
        }

        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            Assert.That(integers.Count, Is.GreaterThan(0), "Unexpected weather candidate selection.");
            Assert.That(minimumInclusive, Is.Zero);
            Assert.That(maximumExclusive, Is.EqualTo(expectedUpperBounds.Dequeue()));
            int choice = integers.Dequeue();
            Assert.That(choice, Is.InRange(0, maximumExclusive - 1));
            integerCalls++;
            return choice;
        }

        public void AssertConsumed(int expectedDoubles, int expectedIntegers)
        {
            Assert.That(doubleCalls, Is.EqualTo(expectedDoubles));
            Assert.That(integerCalls, Is.EqualTo(expectedIntegers));
            Assert.That(doubles, Is.Empty);
            Assert.That(integers, Is.Empty);
            Assert.That(expectedUpperBounds, Is.Empty);
        }
    }
}
