using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// Uses the shared inactive participant/HUD and memory-storage fixture.
// Advances the real instant-movement adapter, not animated movement or GameLoop.
// No test writes lap, hasFinished, finishOrder or isBlown to construct a result.
public sealed partial class RaceResultSettlementAdapterTests
{
    [TestCase(TeamId.UK, false)]
    [TestCase(TeamId.DE, false)]
    [TestCase(TeamId.IT, false)]
    [TestCase(TeamId.US, false)]
    [TestCase(TeamId.CN, false)]
    [TestCase(TeamId.JP, false)]
    [TestCase(TeamId.UK, true)]
    [TestCase(TeamId.DE, true)]
    [TestCase(TeamId.IT, true)]
    [TestCase(TeamId.US, true)]
    [TestCase(TeamId.CN, true)]
    [TestCase(TeamId.JP, true)]
    public void NormalRaceMovementAndRetirementReachSettlementWithoutPrematureEnd(
        TeamId team, bool humanDnf)
    {
        using (new SilentMovementAudio())
        {
            CreateDrivingParticipants(team);
            PlayerState human = session.Human;
            int rpBefore = human.techState.rpBalance;
            int[] xpBefore = session.Players.Select(p => p.driverXp).ToArray();
            AssertDrivingRaceActive();
            if (humanDnf)
            {
                AdvanceInstantly(human, manager.trackManager.TotalNodes - 1);
                RetireThroughRealSpins(human);
                Assert.That(human.lap, Is.Zero);
                Assert.That(human.hasFinished, Is.False);
                Assert.That(human.finishOrder, Is.Zero);
                AssertDrivingRaceActive(); // Human retirement does not end the AI race.
            }

            int[] order = humanDnf ? new[] { 3, 1, 2 } : new[] { 3, 0, 1, 2 };
            DriveFieldToFinish(order);
            Assert.That(human.finishOrder, Is.EqualTo(humanDnf ? 0 : 2));
            Assert.That(session.Players.Select(p => p.driverXp), Is.EqualTo(xpBefore));
            int humanRank = humanDnf ? 4 : 2;
            var calls = new List<string>();
            Complete(state =>
            {
                Assert.That(state, Is.SameAs(human.techState));
                // Normal DNF still receives placement RP; only its XP is zero.
                Assert.That(state.rpBalance, Is.EqualTo(rpBefore + TechTreeRules.CalculateRaceRP(humanRank)));
                Assert.That(IsDrivingRaceOver(), Is.True);
                Assert.That(hud.gameOverPanel.activeSelf, Is.False);
                calls.Add("RP");
            }, (id, xp) =>
            {
                Assert.That(id, Is.EqualTo(human.DriverProfile.Id));
                Assert.That(xp, Is.EqualTo(xpBefore[0] + (humanDnf ? 0 :
                    DriverProgression.CalculateRaceXp(humanRank,
                        human.DriverProfile.TalentMultiplier, human.DriverProfile.Team))));
                Assert.That(calls, Is.EqualTo(new[] { "RP" }));
                calls.Add("XP");
            }, () => throw new InvalidOperationException("Normal movement must not open career storage."));

            Assert.That(calls, Is.EqualTo(new[] { "RP", "XP" }));
            Assert.That(session.NextFinishOrder, Is.EqualTo(order.Length + 1));
            Assert.That(hud.gameOverText.text, Does.Contain(humanDnf ? "[爆缸]" : "[完赛]"));
            Assert.That(hud.gameOverText.text, Does.Contain("RP 奖励:"));
            AssertPresented();
        }
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void EightCareerTracksAdvanceActualMovementBeforeJsonAndHudSettlement(TeamId team)
    {
        using (new SilentMovementAudio())
        {
            CareerRepository repository = CreateSeason(team, out MemoryCareerStore store);
            int expectedPoints = 0;
            int[] classifiedPoints = { 10, 6, 4, 2 };
            for (int race = 0; race < CareerModeRules.RaceCount; race++)
            {
                CareerSeasonState before = repository.Load().State;
                if (before.Phase == CareerPhase.SummerBreak)
                {
                    Assert.That(new CareerModeService(repository)
                        .TryConfirmSummerBreak(before.ActiveTechSnapshot), Is.True);
                    before = repository.Load().State;
                }
                CareerRaceLaunchRequest launch = CreateLaunch(before, "driven-" + team + "-" + race);
                CreateDrivingParticipants(team, launch);
                Assert.That(manager.trackManager.LoadedTrackConfig.trackId, Is.EqualTo(launch.TrackId));
                Assert.That(manager.trackManager.TotalNodes, Is.EqualTo(
                    manager.trackManager.LoadedTrackConfig.cells.Length));
                Assert.That(config.totalLaps, Is.EqualTo(manager.trackManager.LoadedTrackConfig.laps));
                string snapshot = SnapshotFingerprint(launch.TechSnapshot);
                int[] rp = session.Players.Select(p => p.techState.rpBalance).ToArray();
                int[] xp = session.Players.Select(p => p.driverXp).ToArray();
                string jsonBeforeDriving = store.Json;
                AssertDrivingRaceActive();
                bool humanDnf = race == 4;
                if (humanDnf) RetireThroughRealSpins(session.Human);
                int[] order = Enumerable.Range(0, 4).Select(i => (i + race % 4) % 4)
                    .Where(i => !session.Players[i].isBlown).ToArray();

                DriveFieldToFinish(order);

                Assert.That(store.Json, Is.EqualTo(jsonBeforeDriving), "Movement alone never settles career storage.");
                int humanPlace = session.Human.finishOrder;
                Assert.That(humanPlace, humanDnf ? Is.Zero : Is.InRange(1, 4));
                if (!humanDnf) expectedPoints += classifiedPoints[humanPlace - 1];
                int writes = store.SuccessfulWrites;
                CompleteCareer(repository);

                CareerSeasonState after = repository.Load().State;
                CareerCompetitorResult recordedHuman = after.RaceResults[race].Standings
                    .Single(entry => entry.TeamId == team);
                Assert.That(after.NextTrackIndex, Is.EqualTo(race + 1));
                Assert.That(after.RaceResults[race].TrackId, Is.EqualTo(launch.TrackId));
                Assert.That(after.RaceResults[race].ResultId, Is.EqualTo(launch.ResultId));
                Assert.That(recordedHuman.FinishPosition, Is.EqualTo(humanPlace));
                Assert.That(recordedHuman.DidNotFinish, Is.EqualTo(humanDnf));
                Assert.That(CareerModeRules.GetStandings(after).Single(entry => entry.TeamId == team).Points,
                    Is.EqualTo(expectedPoints));
                Assert.That(store.SuccessfulWrites, Is.EqualTo(writes + 1));
                Assert.That(session.Players.Select(p => p.techState.rpBalance), Is.EqualTo(rp));
                Assert.That(session.Players.Select(p => p.driverXp), Is.EqualTo(xp));
                Assert.That(SnapshotFingerprint(launch.TechSnapshot), Is.EqualTo(snapshot));
                Assert.That(hud.gameOverText.text, Does.Contain("生涯赛果已保存："));
                Assert.That(hud.gameOverText.text, Does.Not.Contain("RP 奖励:"));
                if (race == 3) Assert.That(hud.gameOverText.text, Does.Contain("已进入夏休"));
                if (race == 7) Assert.That(hud.gameOverText.text, Does.Contain("八站生涯已完成"));
                AssertPresented();

                string settledJson = store.Json;
                Complete(ForbiddenTechSave, ForbiddenXpSave,
                    () => throw new InvalidOperationException("Driven result must not be saved twice."));
                Assert.That(store.Json, Is.EqualTo(settledJson));
                Assert.That(store.SuccessfulWrites, Is.EqualTo(writes + 1));
            }
            Assert.That(repository.Load().State.Phase, Is.EqualTo(CareerPhase.Completed));
            Assert.That(store.SuccessfulWrites, Is.EqualTo(10));
            Assert.That(store.KeysWritten, Has.All.EqualTo(CareerRepository.SaveKey));
            Assert.That(cache[team].rpBalance, Is.EqualTo(77));
        }
    }

    [TestCase(0)]
    [TestCase(3)]
    public void MultiWrapInstantMovementStopsLapRegistrationAtRaceDistanceBeforeSettlement(int startFinish)
    {
        using (new SilentMovementAudio())
        {
            config.enableTechTree = false;
            CreateParticipants(TeamId.UK);
            var nodes = Enumerable.Range(0, 9)
                .Select(i => new TrackNode(i, 0, "fixture", 0, i == startFinish)).ToList();
            SetTrack(new TrackConfig { trackId = "nonzero-start-fixture", country = "UK", laps = 3 });
            InstallDrivingTrack(nodes, 3);
            PositionFieldAtStart();
            PlayerState human = session.Human;
            int xpBefore = human.driverXp;

            AdvanceInstantly(human, 5 * nodes.Count + 2);

            Assert.That(human.position, Is.EqualTo((startFinish + 2) % nodes.Count));
            Assert.That(human.lap, Is.EqualTo(3), "Five crossings must not register beyond the finish boundary.");
            Assert.That(human.hasFinished, Is.True);
            Assert.That(human.finishOrder, Is.EqualTo(1));
            Assert.That(session.NextFinishOrder, Is.EqualTo(2));
            AssertDrivingRaceActive();
            DriveFieldToFinish(new[] { 2, 3, 1 }, 2);
            int xpWrites = 0;
            Complete(ForbiddenTechSave, (id, xp) =>
            {
                xpWrites++;
                Assert.That(id, Is.EqualTo(human.driverId));
                Assert.That(xp, Is.EqualTo(xpBefore + DriverProgression.CalculateRaceXp(
                    1, human.DriverProfile.TalentMultiplier, human.DriverProfile.Team)));
            }, () => throw new InvalidOperationException("Synthetic track uses the ordinary result branch."));
            Assert.That(xpWrites, Is.EqualTo(1));
            Assert.That(session.NextFinishOrder, Is.EqualTo(5));
            AssertPresented();
        }
    }

    [TestCase(3)]
    [TestCase(7)]
    public void FinalSprintCleanupRetirementFlowsThroughActualNormalResult(int level)
    {
        using (new SilentMovementAudio())
        {
            Assert.That(DriverCatalog.TryGet("it_tazio_nuvolari", out DriverProfile driver), Is.True);
            CreateDrivingParticipants(TeamId.IT, selectedDriver: driver);
            PlayerState human = session.Human;
            Assert.That(human.driverId, Is.EqualTo(driver.Id));
            human.driverSkill.Initialize(driver, level, true);
            human.deck.DrawHeatFromPoolToHand(human.deck.heatPool.remaining);
            Assert.That(human.driverSkill.TryActivate(driver,
                new DriverSkillActivationContext(true, 0, config.totalLaps,
                    human.deck.heatPool.remaining, config.heatPoolPerPlayer, 0), out _), Is.True);
            for (int spin = 0; spin < session.EffectiveSpinMax(human) - 1; spin++)
                manager.HandleSpin(human, human.position, "Final Sprint setup");
            Assert.That(human.isBlown, Is.False);
            int xp = human.driverXp;
            int rp = human.techState.rpBalance;

            typeof(MVPGameManager).GetMethod("CleanupTurn", PrivateInstance)
                .Invoke(manager, new object[] { human });

            Assert.That(human.spinCounter, Is.EqualTo(session.EffectiveSpinMax(human)));
            Assert.That(human.isBlown, Is.True);
            Assert.That(human.hasFinished, Is.False);
            Assert.That(human.finishOrder, Is.Zero);
            Assert.That(RaceTurnRules.GetStartAction(human), Is.EqualTo(RaceTurnStartAction.ExcludeTerminal));
            AssertDrivingRaceActive();
            DriveFieldToFinish(new[] { 3, 1, 2 });
            var calls = new List<string>();
            Complete(state =>
            {
                Assert.That(state, Is.SameAs(human.techState));
                Assert.That(state.rpBalance, Is.EqualTo(rp + TechTreeRules.CalculateRaceRP(4)));
                Assert.That(hud.gameOverPanel.activeSelf, Is.False);
                calls.Add("RP");
            }, (id, savedXp) =>
            {
                Assert.That(id, Is.EqualTo(driver.Id));
                Assert.That(savedXp, Is.EqualTo(xp), "Final Sprint DNF grants no XP.");
                Assert.That(calls, Is.EqualTo(new[] { "RP" }));
                calls.Add("XP");
            }, () => throw new InvalidOperationException("Normal Final Sprint must not open career storage."));

            Assert.That(calls, Is.EqualTo(new[] { "RP", "XP" }));
            Assert.That(hud.gameOverText.text, Does.Contain("[爆缸]"));
            Assert.That(human.finishOrder, Is.Zero);
            Assert.That(session.NextFinishOrder, Is.EqualTo(4));
            AssertPresented();
        }
    }

    private void CreateDrivingParticipants(TeamId team, CareerRaceLaunchRequest launch = null,
        DriverProfile selectedDriver = null)
    {
        // Install authored nodes before participant setup so regional capacity
        // sees this race's track, not the previous race or an empty visual track.
        TrackConfig track = TrackDataLoader.LoadConfig(launch?.TrackId ?? "silverstone_afternoon_tea");
        InstallDrivingTrack(TrackDataLoader.ConfigToNodes(track), track.laps);
        CreateParticipants(team, launch, selectedDriver: selectedDriver);
        PositionFieldAtStart();
    }

    private void InstallDrivingTrack(List<TrackNode> nodes, int laps)
    {
        typeof(TrackManager).GetField("nodes", PrivateInstance).SetValue(manager.trackManager, nodes);
        manager.trackManager.config = config;
        config.totalLaps = laps;
        config.enableWeather = false; // Weather has separate regression coverage.
        config.enablePitLane = false; // No pit reservation or lane interaction in this slice.
        Assert.That(nodes.Count, Is.GreaterThan(1));
        Assert.That(laps, Is.GreaterThan(0));
    }

    private void PositionFieldAtStart()
    {
        foreach (PlayerState player in session.Players)
        {
            player.position = manager.trackManager.StartFinishNodeIndex;
            Assert.That(player.lap, Is.Zero);
            Assert.That(player.hasFinished, Is.False);
            Assert.That(player.finishOrder, Is.Zero);
            Assert.That(player.isBlown, Is.False);
        }
    }

    private void DriveFieldToFinish(int[] order, int firstFinishOrder = 1)
    {
        for (int i = 0; i < order.Length; i++)
        {
            DriveOneToFinish(session.Players[order[i]], firstFinishOrder + i);
            Assert.That(IsDrivingRaceOver(), Is.EqualTo(i == order.Length - 1));
        }
        Assert.That(session.IsRaceOver(), Is.True);
    }

    private void DriveOneToFinish(PlayerState player, int expectedFinishOrder)
    {
        int nodes = manager.trackManager.TotalNodes;
        int start = manager.trackManager.StartFinishNodeIndex;
        for (int lap = 1; lap <= config.totalLaps; lap++)
        {
            AdvanceInstantly(player, nodes - 1);
            Assert.That(player.position, Is.EqualTo((start + nodes - 1) % nodes));
            Assert.That(player.lap, Is.EqualTo(lap - 1));
            Assert.That(player.hasFinished, Is.False);
            Assert.That(player.finishOrder, Is.Zero);
            AdvanceInstantly(player, 1);
            Assert.That(player.position, Is.EqualTo(start));
            Assert.That(player.lap, Is.EqualTo(lap));
            Assert.That(player.hasFinished, Is.EqualTo(lap == config.totalLaps));
            Assert.That(player.finishOrder, Is.EqualTo(lap == config.totalLaps ? expectedFinishOrder : 0));
        }
        Assert.That(session.NextFinishOrder, Is.EqualTo(expectedFinishOrder + 1));
        // Repeated finish notifications are inert, even while other racers remain active.
        manager.OnPlayerCrossedStartFinish(player);
        Assert.That(player.lap, Is.EqualTo(config.totalLaps));
        Assert.That(player.finishOrder, Is.EqualTo(expectedFinishOrder));
        Assert.That(session.NextFinishOrder, Is.EqualTo(expectedFinishOrder + 1));
    }

    private void RetireThroughRealSpins(PlayerState player)
    {
        int requiredSpins = session.EffectiveSpinMax(player);
        for (int spin = 0; spin < requiredSpins; spin++)
            manager.HandleSpin(player, player.position, "movement settlement fixture");
        Assert.That(player.isBlown, Is.True);
        Assert.That(player.hasFinished, Is.False);
        Assert.That(player.finishOrder, Is.Zero);
    }

    private void AdvanceInstantly(PlayerState player, int cells)
        => typeof(MVPGameManager).GetMethod("AdvanceInstantTechnologyMovement", PrivateInstance)
            .Invoke(manager, new object[] { player, cells });

    private bool IsDrivingRaceOver()
        => (bool)typeof(MVPGameManager).GetMethod("CheckGameEnd", PrivateInstance).Invoke(manager, null);

    private void AssertDrivingRaceActive()
    {
        Assert.That(IsDrivingRaceOver(), Is.False);
        Assert.That(session.IsRaceOver(), Is.False);
        Assert.That(hud.gameOverPanel.activeSelf, Is.False);
    }

    private sealed class SilentMovementAudio : IDisposable
    {
        private static readonly FieldInfo Instance = typeof(AudioService)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly object previousInstance = Instance.GetValue(null);

        public SilentMovementAudio() => Instance.SetValue(null, null);
        public void Dispose() => Instance.SetValue(null, previousInstance);
    }
}
