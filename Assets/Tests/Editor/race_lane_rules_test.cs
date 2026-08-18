using System.Collections.Generic;
using NUnit.Framework;

public class RaceLaneRulesTests
{
    [Test]
    public void EarlierActiveCarMakesSameCellOpponentTrailing()
    {
        var leader = new PlayerState("Leader", false, 8, 1) { lap = 1 };
        var trailing = new PlayerState("Trailing", true, 8, 1) { lap = 1 };
        var players = new List<PlayerState> { leader, trailing };

        Assert.That(RaceLaneRules.IsTrailingInParallel(trailing, players, 1), Is.True);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void DifferentLapOrCellDoesNotCreateParallelLane(bool differentLap, bool differentCell)
    {
        var leader = new PlayerState("Leader", false, 8, 1) { lap = 1 };
        var candidate = new PlayerState("Candidate", true, differentCell ? 9 : 8, 1)
        {
            lap = differentLap ? 2 : 1
        };

        Assert.That(RaceLaneRules.IsTrailingInParallel(
            candidate,
            new List<PlayerState> { leader, candidate },
            1), Is.False);
    }

    [Test]
    public void FinishedOrBlownCarsDoNotClaimParallelLane()
    {
        var finishedLeader = new PlayerState("Finished", false, 8, 1)
        {
            lap = 1,
            hasFinished = true
        };
        var candidate = new PlayerState("Candidate", true, 8, 1) { lap = 1 };

        Assert.That(RaceLaneRules.IsTrailingInParallel(
            candidate,
            new List<PlayerState> { finishedLeader, candidate },
            1), Is.False);
    }

    [Test]
    public void InvalidCandidateIndexIsSafe()
    {
        var candidate = new PlayerState("Candidate", true, 8, 1);

        Assert.That(RaceLaneRules.IsTrailingInParallel(
            candidate,
            new List<PlayerState> { candidate },
            -1), Is.False);
    }
}
