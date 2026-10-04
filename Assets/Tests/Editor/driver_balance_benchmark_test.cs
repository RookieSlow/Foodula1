using System.Collections.Generic;
using NUnit.Framework;

public class DriverBalanceBenchmarkTests
{
    [TestCase(TeamId.US, false)]
    [TestCase(TeamId.UK, true)]
    public void BenchmarkPlanningCountsRegionalHeatOnlyWhileEligible(TeamId team, bool borrowed)
    {
        var session = new RaceSession(new SystemRandomSource(17));
        var player = new PlayerState("Planning", true, 5, 1)
        {
            teamId = team,
            techState = new TechTreeState(team)
        };
        player.techState.activeNodeIds.Add(borrowed
            ? "uk-l3-sun-never-sets" : "us-l2-smoked-bbq");
        if (borrowed) player.techState.sunNeverSetsTarget = TeamId.US;
        player.deck.heatPool = new HeatPool(6);
        player.deck.DrawHeatFromPoolToHand(1);
        player.deck.AddCardsToHand(new[] { new CardData(CardType.Speed, 3) });

        Assert.AreEqual(2, DriverBalanceBenchmark.CountPlayableSpeedCards(session, player, 60));
        Assert.AreEqual(5, DriverBalanceBenchmark.EstimatePlayableMovement(session, player, 2, 60, false));
        Assert.AreEqual(5, DriverBalanceBenchmark.EstimatePlayableMovement(session, player, 2, 60, true));

        player.position = 6;
        Assert.AreEqual(1, DriverBalanceBenchmark.CountPlayableSpeedCards(session, player, 60));
        Assert.AreEqual(3, DriverBalanceBenchmark.EstimatePlayableMovement(session, player, 2, 60, false));
        player.position = 5;
        player.techState.activeNodeIds.Clear();
        Assert.AreEqual(1, DriverBalanceBenchmark.CountPlayableSpeedCards(session, player, 60));
        Assert.AreEqual(3, DriverBalanceBenchmark.EstimatePlayableMovement(session, player, 2, 60, true));
    }

    [TestCase(TeamId.US, false)]
    [TestCase(TeamId.UK, true)]
    public void BenchmarkCommitsBBQHeatAtSpeedTwoAndRetiresOriginalCard(TeamId team, bool borrowed)
    {
        var session = new RaceSession(new SystemRandomSource(17));
        var player = new PlayerState("BBQ benchmark", true, 5, 1)
        {
            teamId = team,
            techState = new TechTreeState(team)
        };
        player.techState.activeNodeIds.Add(borrowed
            ? "uk-l3-sun-never-sets" : "us-l2-smoked-bbq");
        if (borrowed) player.techState.sunNeverSetsTarget = TeamId.US;
        player.deck.heatPool = new HeatPool(6);
        Assert.AreEqual(1, player.deck.DrawHeatFromPoolToHand(1));
        CardData heat = player.deck.Hand[0];
        var speed = new CardData(CardType.Speed, 3);
        player.deck.AddCardsToHand(new[] { speed });

        var chosen = AIPlanner.ChooseSpeedCards(player.deck, 2, 0f, false,
            0.7f, 0.5f, 0f, session.Random, true);
        CollectionAssert.AreEquivalent(new[] { heat, speed }, chosen);
        Assert.AreEqual(SpeedCardCommitResult.Success,
            DriverBalanceBenchmark.CommitBenchmarkSpeedCards(session, player, chosen, 2, 60));
        Assert.AreEqual(5, CardPlayRules.SumCommittedSpeedCardValues(player.playedSpeedCardsThisTurn));
        Assert.IsFalse(player.deck.ContainsInHand(heat));
        Assert.AreEqual(0, player.deck.DiscardPlayedSpeedCards(player.playedSpeedCardsThisTurn));
        Assert.Contains(heat, new List<CardData>(player.deck.DiscardPile));
        Assert.AreEqual(5, player.deck.heatPool.remaining);
    }

    [Test]
    public void BenchmarkCannotCommitHeatOutsideBBQZone()
    {
        var session = new RaceSession(new SystemRandomSource(17));
        var player = new PlayerState("Outside", true, 6, 1)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US)
        };
        player.techState.activeNodeIds.Add("us-l2-smoked-bbq");
        player.deck.heatPool = new HeatPool(6);
        player.deck.DrawHeatFromPoolToHand(1);
        CardData heat = player.deck.Hand[0];

        Assert.AreEqual(SpeedCardCommitResult.InvalidCard,
            DriverBalanceBenchmark.CommitBenchmarkSpeedCards(session, player, new[] { heat }, 1, 60));
        Assert.IsTrue(player.deck.ContainsInHand(heat));
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
    }

    [Test]
    public void USHomeRaceBenchmarkReplaysDeterministicallyWithBBQHeatCards()
    {
        DriverBalanceBenchmark.RaceSnapshot first = DriverBalanceBenchmark.SimulateForTests(
            "indianapolis_burger", "us_tony_stewart", 3, true,
            DriverBalanceBenchmark.BaseSeed, 0);
        DriverBalanceBenchmark.RaceSnapshot replay = DriverBalanceBenchmark.SimulateForTests(
            "indianapolis_burger", "us_tony_stewart", 3, true,
            DriverBalanceBenchmark.BaseSeed, 0);

        Assert.AreEqual(first.Rank, replay.Rank);
        Assert.AreEqual(first.Finished, replay.Finished);
        Assert.AreEqual(first.Blown, replay.Blown);
        Assert.AreEqual(first.Turns, replay.Turns);
        Assert.AreEqual(first.HeatPaid, replay.HeatPaid);
    }

    [TestCase(TeamId.US, false)]
    [TestCase(TeamId.UK, true)]
    public void RegionalEngineCapacityTracksBenchmarkPositionWithoutStacking(TeamId team, bool borrowed)
    {
        var session = new RaceSession(new SystemRandomSource(17));
        var player = new PlayerState("BBQ benchmark", true, 5, 1)
        {
            teamId = team,
            techState = new TechTreeState(team)
        };
        player.techState.activeNodeIds.Add(borrowed
            ? "uk-l3-sun-never-sets" : "us-l2-smoked-bbq");
        session.PrepareTechnologyForRace(player, borrowed ? "US" : "GB");
        player.deck.heatPool = new HeatPool(6);

        DriverBalanceBenchmark.SyncRegionalHeatCapacity(session, player, 60);
        DriverBalanceBenchmark.SyncRegionalHeatCapacity(session, player, 60);
        Assert.AreEqual(8, player.deck.heatPool.remaining);
        player.position = 6;
        DriverBalanceBenchmark.SyncRegionalHeatCapacity(session, player, 60);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        player.position = 25;
        DriverBalanceBenchmark.SyncRegionalHeatCapacity(session, player, 60);
        Assert.AreEqual(8, player.deck.heatPool.remaining);
        Assert.AreEqual(0, session.GetSmokedBBQEngineCapacityBonusAtPosition(player, 25, 0));
    }

    [Test]
    public void BenchmarkExposesEveryCatalogDriverAtBothComparisonLevels()
    {
        var ids = new HashSet<string>(DriverBalanceBenchmark.GetDriverIdsForTests());

        Assert.That(ids.Count, Is.EqualTo(12));
        foreach (DriverProfile driver in DriverCatalog.All)
            Assert.That(ids.Contains(driver.Id), Is.True, driver.Id);
        Assert.That(DriverBalanceBenchmark.GetLevelsForTests(), Is.EqualTo(new[] { 3, 7 }));
    }

    [Test]
    public void PairedRaceIsDeterministicAndKeepsTheControlSeparate()
    {
        DriverBalanceBenchmark.RaceSnapshot first = DriverBalanceBenchmark.SimulateForTests(
            "silverstone_afternoon_tea",
            "de_michael_schumacher",
            3,
            true,
            DriverBalanceBenchmark.BaseSeed,
            0);
        DriverBalanceBenchmark.RaceSnapshot replay = DriverBalanceBenchmark.SimulateForTests(
            "silverstone_afternoon_tea",
            "de_michael_schumacher",
            3,
            true,
            DriverBalanceBenchmark.BaseSeed,
            0);
        DriverBalanceBenchmark.RaceSnapshot control = DriverBalanceBenchmark.SimulateForTests(
            "silverstone_afternoon_tea",
            "de_michael_schumacher",
            3,
            false,
            DriverBalanceBenchmark.BaseSeed,
            0);

        Assert.That(first.Enabled, Is.True);
        Assert.That(control.Enabled, Is.False);
        Assert.That(first.DriverId, Is.EqualTo("de_michael_schumacher"));
        Assert.That(first.Rank, Is.EqualTo(replay.Rank));
        Assert.That(first.Finished, Is.EqualTo(replay.Finished));
        Assert.That(first.Blown, Is.EqualTo(replay.Blown));
        Assert.That(first.Turns, Is.EqualTo(replay.Turns));
        Assert.That(first.HeatPaid, Is.EqualTo(replay.HeatPaid));
        Assert.That(first.Activations, Is.EqualTo(replay.Activations));
        Assert.That(first.Rank, Is.GreaterThanOrEqualTo(1));
        Assert.That(control.Rank, Is.GreaterThanOrEqualTo(1));
    }
}

public class TrackTeamBalanceCornerHeatTests
{
    [Test]
    public void BenchmarkConsumesCommonHeatReductionOncePerLap()
    {
        var session = new RaceSession(new SystemRandomSource(17));
        var player = new PlayerState("US corner", true, 0, 1)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US)
        };
        player.techState.activeNodeIds.Add("common-l1-heat-coating");

        Assert.AreEqual(3, TrackTeamBalanceBenchmark.ResolveCornerHeatCost(session, player, 3));
        Assert.AreEqual(4, TrackTeamBalanceBenchmark.ResolveCornerHeatCost(session, player, 3));
        session.OnNewLap(player);
        Assert.AreEqual(3, TrackTeamBalanceBenchmark.ResolveCornerHeatCost(session, player, 3));
    }

    [Test]
    public void BenchmarkRespectsVehicleToggleAfterMinimumHeatFloor()
    {
        var session = new RaceSession(new SystemRandomSource(17));
        var player = new PlayerState("US corner", true, 0, 1)
        {
            teamId = TeamId.US,
            techState = new TechTreeState(TeamId.US)
        };
        player.techState.activeNodeIds.Add("common-l1-heat-coating");

        Assert.AreEqual(2, TrackTeamBalanceBenchmark.ResolveCornerHeatCost(session, player, 1));
        session.OnNewLap(player);
        session.TeamVehicleBonusesEnabled = false;
        Assert.AreEqual(1, TrackTeamBalanceBenchmark.ResolveCornerHeatCost(session, player, 1));
    }

    [Test]
    public void BenchmarkAppliesDriverPassiveAfterVehicleAndTechnology()
    {
        var session = new RaceSession(new SystemRandomSource(17));
        var player = new PlayerState("Ma corner", true, 0, 1) { teamId = TeamId.CN };
        DriverCatalog.TryGet("cn_ma_qinghua", out DriverProfile driver);
        player.driverSkill.Initialize(driver, 4, true);
        player.driverSkill.BeginTurn();

        Assert.AreEqual(0, TrackTeamBalanceBenchmark.ResolveCornerHeatCost(session, player, 1));
    }
}
