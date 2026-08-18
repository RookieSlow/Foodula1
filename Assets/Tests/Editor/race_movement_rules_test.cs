using System.Collections.Generic;
using NUnit.Framework;

public class RaceMovementRulesTests
{
    [Test]
    public void CountsAnOpponentPassedDuringMovement()
    {
        var player = new PlayerState("Player", false, 0, 1)
        {
            cornerTotalThisTurn = 3
        };
        var opponent = new PlayerState("Opponent", true, 2, 1);
        var turnOrder = new List<PlayerState> { player, opponent };

        int overtakes = RaceMovementRules.CountOvertakes(
            player, turnOrder, 10, false, candidate => candidate.skipNextTurn);

        Assert.That(overtakes, Is.EqualTo(1));
    }

    [Test]
    public void SkippedOpponentDoesNotCountAsOvertake()
    {
        var player = new PlayerState("Player", false, 0, 1)
        {
            totalMovementThisTurn = 3
        };
        var opponent = new PlayerState("Opponent", true, 2, 1)
        {
            totalMovementThisTurn = 0,
            skipNextTurn = true
        };

        int overtakes = RaceMovementRules.CountOvertakes(
            player,
            new List<PlayerState> { player, opponent },
            10,
            true,
            candidate => candidate.skipNextTurn);

        Assert.That(overtakes, Is.EqualTo(0));
    }

    [Test]
    public void InvalidTrackSizeProducesNoOvertakes()
    {
        var player = new PlayerState("Player", false, 0, 1);

        Assert.That(RaceMovementRules.CountOvertakes(
            player,
            new List<PlayerState> { player },
            0,
            true,
            null), Is.EqualTo(0));
        Assert.That(RaceMovementRules.IsAhead(1, 0, 0), Is.False);
    }
}
