using System.Collections.Generic;
using NUnit.Framework;

public class PitLaneRulesTests
{
    private List<TrackNode> BuildTrackWithPit()
    {
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 40; i++)
        {
            if (i == 10)
                nodes.Add(new TrackNode(i, 99, "Pit Entry", isPitEntry: true));
            else if (i == 15)
                nodes.Add(new TrackNode(i, 99, "Pit Exit", isPitExit: true));
            else
                nodes.Add(new TrackNode(i, 99, $"Node {i}"));
        }
        return nodes;
    }

    private List<TrackNode> BuildTrackWithoutPit()
    {
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 20; i++)
            nodes.Add(new TrackNode(i, 99, $"Node {i}"));
        return nodes;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Detection
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_has_pit_lane_returns_true()
    {
        var nodes = BuildTrackWithPit();
        Assert.That(PitLaneRules.HasPitLane(nodes), Is.True);
    }

    [Test]
    public void test_no_pit_lane_returns_false()
    {
        var nodes = BuildTrackWithoutPit();
        Assert.That(PitLaneRules.HasPitLane(nodes), Is.False);
    }

    [Test]
    public void test_find_pit_entry()
    {
        var nodes = BuildTrackWithPit();
        Assert.That(PitLaneRules.FindPitEntry(nodes), Is.EqualTo(10));
    }

    [Test]
    public void test_find_pit_exit()
    {
        var nodes = BuildTrackWithPit();
        Assert.That(PitLaneRules.FindPitExit(nodes), Is.EqualTo(15));
    }

    [Test]
    public void test_find_pit_entry_returns_minus_one_when_missing()
    {
        var nodes = BuildTrackWithoutPit();
        Assert.That(PitLaneRules.FindPitEntry(nodes), Is.EqualTo(-1));
    }

    [Test]
    public void test_is_at_pit_entry()
    {
        var nodes = BuildTrackWithPit();
        Assert.That(PitLaneRules.IsAtPitEntry(10, nodes), Is.True);
        Assert.That(PitLaneRules.IsAtPitEntry(9, nodes), Is.False);
    }

    [Test]
    public void test_crossed_pit_entry()
    {
        var nodes = BuildTrackWithPit();
        Assert.That(PitLaneRules.CrossedPitEntry(9, 11, nodes), Is.True);
        Assert.That(PitLaneRules.CrossedPitEntry(5, 8, nodes), Is.False);
    }

    [Test]
    public void test_crossed_pit_entry_after_start_finish_wrap()
    {
        var nodes = BuildTrackWithPit();

        Assert.That(PitLaneRules.CrossedPitEntry(39, 51, nodes), Is.True);
    }

    [Test]
    public void test_did_not_cross_pit_entry_before_next_lap_entry()
    {
        var nodes = BuildTrackWithPit();

        Assert.That(PitLaneRules.CrossedPitEntry(39, 49, nodes), Is.False);
    }

    [Test]
    public void test_crossed_pit_entry_handles_missing_track()
    {
        Assert.That(PitLaneRules.CrossedPitEntry(0, 10, null), Is.False);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Pit Stop
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_can_enter_pit()
    {
        var nodes = BuildTrackWithPit();
        var player = new PlayerState("Test", false, 0, 1);
        Assert.That(PitLaneRules.CanEnterPit(player, nodes), Is.True);
    }

    [Test]
    public void test_cannot_enter_pit_when_blown()
    {
        var nodes = BuildTrackWithPit();
        var player = new PlayerState("Test", false, 0, 1);
        player.isBlown = true;
        Assert.That(PitLaneRules.CanEnterPit(player, nodes), Is.False);
    }

    [Test]
    public void test_cannot_enter_pit_when_finished()
    {
        var nodes = BuildTrackWithPit();
        var player = new PlayerState("Test", false, 0, 1);
        player.hasFinished = true;
        Assert.That(PitLaneRules.CanEnterPit(player, nodes), Is.False);
    }

    [Test]
    public void test_cannot_enter_pit_without_pit_lane()
    {
        var nodes = BuildTrackWithoutPit();
        var player = new PlayerState("Test", false, 0, 1);
        Assert.That(PitLaneRules.CanEnterPit(player, nodes), Is.False);
    }

    [Test]
    public void test_enter_pit_moves_to_exit()
    {
        var nodes = BuildTrackWithPit();
        var player = new PlayerState("Test", false, 5, 1);
        var result = PitLaneRules.EnterPit(player, nodes);

        Assert.That(result.success, Is.True);
        Assert.That(result.exitPosition, Is.EqualTo(15));
        Assert.That(player.position, Is.EqualTo(15));
        Assert.That(player.skipNextTurn, Is.True);
    }

    [Test]
    public void test_pit_stop_cools_all_heat()
    {
        var nodes = BuildTrackWithPit();
        var player = new PlayerState("Test", false, 5, 1);
        var result = PitLaneRules.EnterPit(player, nodes);

        Assert.That(result.success, Is.True);
        Assert.That(result.heatCooled, Is.EqualTo(999)); // All heat
        Assert.That(result.turnsSkipped, Is.EqualTo(1));
    }

    // ═══════════════════════════════════════════════════════════════════
    // China Team Unique
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void test_china_can_skip_pit()
    {
        Assert.That(PitLaneRules.CanChinaSkipPit(TeamId.CN), Is.True);
    }

    [Test]
    public void test_non_china_cannot_skip_pit()
    {
        Assert.That(PitLaneRules.CanChinaSkipPit(TeamId.UK), Is.False);
        Assert.That(PitLaneRules.CanChinaSkipPit(TeamId.DE), Is.False);
    }
}
