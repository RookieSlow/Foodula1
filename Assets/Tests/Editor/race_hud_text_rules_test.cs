using System.Collections.Generic;
using NUnit.Framework;

public class RaceHudTextRulesTests
{
    [Test]
    public void LapLabelKeepsTheCurrentAndRequiredLap()
    {
        var player = new PlayerState("Human", false, 0, 1) { lap = 2 };

        Assert.That(RaceHudTextRules.FormatLap(player, 3), Is.EqualTo("圈数: 2/3"));
    }

    [Test]
    public void PositionWithoutRosterKeepsTheTrackCellOnly()
    {
        var player = new PlayerState("Human", false, 43, 1);

        Assert.That(RaceHudTextRules.FormatPosition(player, null, 42),
            Is.EqualTo("格 2/42"));
        Assert.That(RaceHudTextRules.FormatPosition(player, new PlayerState[0], 42),
            Is.EqualTo("格 2/42"));
    }

    [Test]
    public void PositionWithRosterIncludesRankingWithoutChangingRosterOrder()
    {
        var human = new PlayerState("Human", false, 7, 1);
        var ai = new PlayerState("AI", true, 3, 1);
        var roster = new List<PlayerState> { ai, human };

        Assert.That(RaceHudTextRules.FormatPosition(human, roster, 42),
            Is.EqualTo("格 8/42 | 排名: 1/2"));
        Assert.That(roster[0], Is.SameAs(ai));
        Assert.That(roster[1], Is.SameAs(human));
    }

    [Test]
    public void BlownAiStatusTakesPriorityOverFinishAndResourceText()
    {
        var ai = new PlayerState("AI", true, 10, 2)
        { isBlown = true, hasFinished = true };

        Assert.That(RaceHudTextRules.FormatAiStatus(ai, 42),
            Is.EqualTo("<color=red>AI: 爆缸!</color>"));
    }

    [Test]
    public void FinishedAiUsesTheFinishedLabel()
    {
        var ai = new PlayerState("AI", true, 10, 2) { hasFinished = true };

        Assert.That(RaceHudTextRules.FormatAiStatus(ai, 42),
            Is.EqualTo("<color=green>AI: 完赛!</color>"));
    }

    [Test]
    public void ActiveAiShowsGearEngineLapAndWrappedCell()
    {
        var ai = new PlayerState("AI", true, 43, 2)
        { teamId = TeamId.DE, lap = 1 };
        ai.deck.heatPool = new HeatPool(6);

        Assert.That(RaceHudTextRules.FormatAiStatus(ai, 42),
            Is.EqualTo("AI: G2 | 引擎:6 | 圈1 | 格2"));
    }

    [Test]
    public void StandingsKeepRankOrderLapCellAndSelfMarker()
    {
        var human = new PlayerState("Human", false, 43, 1) { lap = 2 };
        var ai = new PlayerState("AI", true, 10, 1) { lap = 1 };
        var roster = new List<PlayerState> { ai, human };

        string result = RaceHudTextRules.FormatStandings(roster, human, 42);

        Assert.That(result, Does.StartWith("1. Human 圈2 格2 ←你"));
        Assert.That(result, Does.Contain("2. AI 圈1 格11"));
        Assert.That(result.Split(new[] { "←你" }, System.StringSplitOptions.None).Length,
            Is.EqualTo(2));
        Assert.That(roster[0], Is.SameAs(ai));
    }

    [Test]
    public void StandingsWithoutTrackSizeUseQuestionMarkAndNoSelfMarker()
    {
        var ai = new PlayerState("AI", true, 10, 1);

        string result = RaceHudTextRules.FormatStandings(new[] { ai }, null, 0);

        Assert.That(result, Does.Contain("1. AI 圈0 格?"));
        Assert.That(result, Does.Not.Contain("←你"));
    }
}
