using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RaceRankingTests
{
    private List<PlayerState> CreateField()
    {
        return new List<PlayerState>
        {
            new PlayerState("Alice", false, 0, 1) { teamId = TeamId.UK },
            new PlayerState("Bob", false, 0, 1) { teamId = TeamId.DE },
            new PlayerState("Charlie", false, 0, 1) { teamId = TeamId.IT },
            new PlayerState("Diana", false, 0, 1) { teamId = TeamId.US },
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sort Tests
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_further_position_ranks_higher()
    {
        var field = CreateField();
        field[0].position = 20; // Alice ahead
        field[1].position = 10; // Bob behind

        var sorted = RaceRanking.SortByPosition(field);
        Assert.That(sorted[0].name, Is.EqualTo("Alice"));
        Assert.That(sorted[1].name, Is.EqualTo("Bob"));
    }

    [Test]
    public void test_more_laps_ranks_higher()
    {
        var field = CreateField();
        field[0].lap = 2; field[0].position = 5;  // Alice: 2 laps, pos 5
        field[1].lap = 1; field[1].position = 30; // Bob: 1 lap, pos 30 (but fewer laps)

        var sorted = RaceRanking.SortByPosition(field);
        Assert.That(sorted[0].name, Is.EqualTo("Alice"), "More laps beats further position");
    }

    [Test]
    public void test_finished_players_rank_above_active()
    {
        var field = CreateField();
        field[0].hasFinished = true; field[0].finishOrder = 1;
        field[1].lap = 2; field[1].position = 50; // Almost finished but not yet

        var sorted = RaceRanking.SortByPosition(field);
        Assert.That(sorted[0].name, Is.EqualTo("Alice"), "Finished beats active");
    }

    [Test]
    public void test_finish_order_breaks_ties()
    {
        var field = CreateField();
        field[0].hasFinished = true; field[0].finishOrder = 2; // Finished second
        field[1].hasFinished = true; field[1].finishOrder = 1; // Finished first

        var sorted = RaceRanking.SortByPosition(field);
        Assert.That(sorted[0].name, Is.EqualTo("Bob"), "Earlier finish = higher rank");
        Assert.That(sorted[1].name, Is.EqualTo("Alice"));
    }

    [Test]
    public void test_blown_players_sink_to_bottom()
    {
        var field = CreateField();
        field[0].lap = 0; field[0].position = 3;
        field[1].isBlown = true; field[1].lap = 2; field[1].position = 10; // Was ahead but blew up

        var sorted = RaceRanking.SortByPosition(field);
        Assert.That(sorted[0].name, Is.EqualTo("Alice"), "Active beats blown regardless of position");
        // Bob is blown — should be last (after Alice, Charlie, Diana)
        Assert.That(sorted[3].name, Is.EqualTo("Bob"), "Blown player sinks to bottom");
    }

    [Test]
    public void test_sort_preserves_input_list()
    {
        var field = CreateField();
        var original = new List<string> { field[0].name, field[1].name, field[2].name, field[3].name };

        RaceRanking.SortByPosition(field);

        // Original list should be unchanged
        for (int i = 0; i < field.Count; i++)
            Assert.That(field[i].name, Is.EqualTo(original[i]));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Current Rank
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_get_current_rank()
    {
        var field = CreateField();
        field[0].position = 30; // 1st
        field[1].position = 20; // 2nd
        field[2].position = 10; // 3rd
        field[3].position = 5;  // 4th

        Assert.That(RaceRanking.GetCurrentRank(field[0], field), Is.EqualTo(1));
        Assert.That(RaceRanking.GetCurrentRank(field[3], field), Is.EqualTo(4));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Turn Order
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_turn_order_trailing_first()
    {
        var field = CreateField();
        field[0].position = 30; // 1st
        field[1].position = 20;
        field[2].position = 10;
        field[3].position = 5;  // Last

        var order = RaceRanking.GetTurnOrder(field);
        Assert.That(order[0].name, Is.EqualTo("Diana"), "Last place goes first");
        Assert.That(order[3].name, Is.EqualTo("Alice"), "First place goes last");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Finish
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_assign_finish_order()
    {
        var field = CreateField();
        int nextOrder = 1;

        Assert.That(RaceRanking.AssignFinishOrder(field[0], ref nextOrder), Is.EqualTo(1));
        Assert.That(field[0].finishOrder, Is.EqualTo(1));
        Assert.That(nextOrder, Is.EqualTo(2));

        Assert.That(RaceRanking.AssignFinishOrder(field[1], ref nextOrder), Is.EqualTo(2));
        Assert.That(field[1].finishOrder, Is.EqualTo(2));
        Assert.That(nextOrder, Is.EqualTo(3));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Race State
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_race_over_when_all_finished()
    {
        var field = CreateField();
        foreach (var p in field) p.hasFinished = true;
        Assert.That(RaceRanking.IsRaceOver(field), Is.True);
    }

    [Test]
    public void test_race_over_when_all_blown_or_finished()
    {
        var field = CreateField();
        field[0].hasFinished = true;
        field[1].isBlown = true;
        field[2].hasFinished = true;
        field[3].isBlown = true;
        Assert.That(RaceRanking.IsRaceOver(field), Is.True);
    }

    [Test]
    public void test_race_not_over_with_active_players()
    {
        var field = CreateField();
        field[0].hasFinished = true;
        // Bob, Charlie, Diana still racing
        Assert.That(RaceRanking.IsRaceOver(field), Is.False);
    }

    [Test]
    public void test_race_not_over_when_blown_player_leaves_active_racer()
    {
        var field = CreateField();
        field[0].isBlown = true;
        field[1].lap = 2;
        field[1].position = 40;

        Assert.That(RaceRanking.IsRaceOver(field), Is.False,
            "A DNF player must not terminate the race while another racer is active");
    }

    [Test]
    public void test_count_active()
    {
        var field = CreateField();
        field[0].hasFinished = true;
        field[1].isBlown = true;
        Assert.That(RaceRanking.CountActive(field), Is.EqualTo(2));
    }

    [Test]
    public void test_format_results_returns_string()
    {
        var field = CreateField();
        field[0].hasFinished = true; field[0].finishOrder = 1;
        field[1].isBlown = true;

        var result = RaceRanking.FormatResults(field);
        Assert.That(result, Does.Contain("比赛结果"));
        Assert.That(result, Does.Contain("Alice"));
        Assert.That(result, Does.Contain("Bob"));
        Assert.That(result, Does.Contain("[UK]"));
        Assert.That(result, Does.Contain("[完赛]"));
        Assert.That(result, Does.Contain("[爆缸]"));
        Assert.That(result, Does.Not.Contain("🇬"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Empty / Edge Cases
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_empty_field()
    {
        var sorted = RaceRanking.SortByPosition(new List<PlayerState>());
        Assert.That(sorted.Count, Is.Zero);
    }

    [Test]
    public void test_single_player()
    {
        var field = new List<PlayerState>
        {
            new PlayerState("Solo", false, 10, 2) { teamId = TeamId.JP }
        };
        var sorted = RaceRanking.SortByPosition(field);
        Assert.That(sorted.Count, Is.EqualTo(1));
        Assert.That(sorted[0].name, Is.EqualTo("Solo"));
    }
}

public class RaceSessionRemainingFinishTests
{
    [TestCase(1)]
    [TestCase(3)]
    public void RemainingActivePlayersReceiveCurrentOrderByLapThenPosition(int nextOrder)
    {
        var session = new RaceSession { NextFinishOrder = nextOrder };
        var lowerLap = new PlayerState("lower", false, 40, 1) { lap = 1 };
        var farther = new PlayerState("farther", false, 20, 1) { lap = 2 };
        var nearer = new PlayerState("nearer", false, 10, 1) { lap = 2 };
        session.Players.Add(lowerLap);
        session.Players.Add(nearer);
        session.Players.Add(farther);

        session.AssignRemainingFinishers();

        Assert.That(farther.finishOrder, Is.EqualTo(nextOrder));
        Assert.That(nearer.finishOrder, Is.EqualTo(nextOrder + 1));
        Assert.That(lowerLap.finishOrder, Is.EqualTo(nextOrder + 2));
        Assert.That(session.NextFinishOrder, Is.EqualTo(nextOrder + 3));
        Assert.That(session.Players, Is.EqualTo(new[] { lowerLap, nearer, farther }),
            "Ranking must not reorder the session field.");
        Assert.That(lowerLap.hasFinished, Is.False);
        Assert.That(nearer.hasFinished, Is.False);
        Assert.That(farther.hasFinished, Is.False);
    }

    [Test]
    public void ExistingFinisherAndBlownPlayerKeepTheirResults()
    {
        var session = new RaceSession { NextFinishOrder = 2 };
        var finished = new PlayerState("finished", false, 0, 1)
            { hasFinished = true, finishOrder = 1 };
        var blown = new PlayerState("blown", true, 30, 1) { isBlown = true };
        var active = new PlayerState("active", true, 5, 1);
        session.Players.AddRange(new[] { blown, active, finished });

        session.AssignRemainingFinishers();

        Assert.That(finished.finishOrder, Is.EqualTo(1));
        Assert.That(blown.finishOrder, Is.Zero);
        Assert.That(active.finishOrder, Is.EqualTo(2));
        Assert.That(active.hasFinished, Is.False);
        Assert.That(session.NextFinishOrder, Is.EqualTo(3));
    }

    [Test]
    public void NoEligibleParticipantLeavesFinishCounterUntouched()
    {
        var session = new RaceSession { NextFinishOrder = 4 };
        session.AssignRemainingFinishers();
        Assert.That(session.NextFinishOrder, Is.EqualTo(4));

        session.Players.Add(new PlayerState("blown", true, 0, 1) { isBlown = true });
        session.Players.Add(new PlayerState("finished", false, 0, 1)
            { hasFinished = true, finishOrder = 3 });
        session.AssignRemainingFinishers();
        Assert.That(session.NextFinishOrder, Is.EqualTo(4));
    }
}

public class RaceEndCoordinatorTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private MVPGameManager manager;
    private RaceSession session;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("RaceEndCoordinatorTests");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        session = new RaceSession(new SystemRandomSource(31));
        SetField("session", session);
    }

    [TearDown]
    public void TearDown()
    {
        if (host != null) Object.DestroyImmediate(host);
    }

    [Test]
    public void MissingSessionEndsWithoutReadingModeState()
    {
        SetField("session", null);
        Assert.That(CheckGameEnd(), Is.True);
    }

    [Test]
    public void FreeRaceWaitsForEveryActiveOpponentAfterHumanDnf()
    {
        session.Players.Add(new PlayerState("human", false, 0, 1) { isBlown = true });
        var opponent = new PlayerState("opponent", true, 0, 1);
        session.Players.Add(opponent);

        Assert.That(CheckGameEnd(), Is.False);
        opponent.hasFinished = true;
        Assert.That(CheckGameEnd(), Is.True);
    }

    [Test]
    public void CareerRaceKeepsTheSameAllParticipantsEndGate()
    {
        var season = new CareerSeasonState();
        Assert.That(CareerModeRules.TryStartSeason(season, TeamId.UK,
            new[] { TeamId.UK, TeamId.US, TeamId.IT, TeamId.DE }), Is.True);
        Assert.That(CareerRaceLaunchRequest.TryCreate(season, "race-end-boundary", out var launch), Is.True);
        SetField("careerRaceLaunch", launch);
        var human = new PlayerState("human", false, 0, 1) { hasFinished = true };
        var opponent = new PlayerState("opponent", true, 0, 1);
        session.Players.Add(human);
        session.Players.Add(opponent);

        Assert.That(CheckGameEnd(), Is.False);
        opponent.isBlown = true;
        Assert.That(CheckGameEnd(), Is.True);
    }

    [TestCase(TutorialRunPhase.Guided, false)]
    [TestCase(TutorialRunPhase.Practice, false)]
    [TestCase(TutorialRunPhase.Completed, true)]
    [TestCase(TutorialRunPhase.Exited, true)]
    public void TutorialPhaseOverridesOnlyAfterCompletionOrExit(TutorialRunPhase phase, bool ends)
    {
        var director = new TutorialRuntimeDirector(
            TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.UK),
            phase != TutorialRunPhase.Guided);
        if (phase == TutorialRunPhase.Completed)
            Assert.That(director.CompletePracticeLap(out _), Is.True);
        if (phase == TutorialRunPhase.Exited)
            director.ExitTutorial();
        SetField("tutorialDirector", director);
        session.Players.Add(new PlayerState("active", false, 0, 1));

        Assert.That(director.Phase, Is.EqualTo(phase));
        Assert.That(CheckGameEnd(), Is.EqualTo(ends));
    }

    private bool CheckGameEnd()
    {
        var method = typeof(MVPGameManager).GetMethod("CheckGameEnd", PrivateInstance);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(manager, null);
    }

    private void SetField(string name, object value)
    {
        var field = typeof(MVPGameManager).GetField(name, PrivateInstance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(manager, value);
    }
}
