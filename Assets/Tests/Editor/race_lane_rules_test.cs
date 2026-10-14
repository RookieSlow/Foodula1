using System.Collections.Generic;
using NUnit.Framework;

public class RaceLaneRulesTests
{
    [TestCase(2)]
    [TestCase(4)]
    [TestCase(6)]
    [TestCase(12)]
    public void SeededRostersMatchActivePrefixOccupancyWithoutMutatingPlayers(int count)
    {
        var random = new System.Random(7331 + count);
        for (int trial = 0; trial < 64; trial++)
        {
            var players = new List<PlayerState>();
            for (int index = 0; index < count; index++)
            {
                int status = random.Next(4);
                players.Add(random.Next(7) == 0 ? null : new PlayerState(
                    "Car" + index, index != 0, random.Next(4), 1)
                {
                    lap = random.Next(1, 4),
                    hasFinished = status == 2,
                    isBlown = status == 3
                });
            }

            var references = players.ToArray();
            var snapshots = players.ConvertAll(player => player == null
                ? (0, 0, false, false)
                : (player.lap, player.position, player.hasFinished, player.isBlown));
            // Independent oracle: accumulate cells claimed by the active prefix,
            // rather than repeating the production method's per-candidate scan.
            var occupied = new HashSet<(int, int)>();
            for (int index = 0; index < count; index++)
            {
                PlayerState player = players[index];
                bool active = player != null && !player.hasFinished && !player.isBlown;
                bool expected = active && occupied.Contains((player.lap, player.position));
                Assert.That(RaceLaneRules.IsTrailingInParallel(player, players, index),
                    Is.EqualTo(expected), "trial=" + trial + ", index=" + index);
                if (active) occupied.Add((player.lap, player.position));
            }

            for (int index = 0; index < count; index++)
            {
                Assert.That(players[index], Is.SameAs(references[index]));
                if (players[index] == null) continue;
                PlayerState player = players[index];
                Assert.That((player.lap, player.position, player.hasFinished, player.isBlown),
                    Is.EqualTo(snapshots[index]));
            }
        }
    }

    [Test]
    public void RepeatedCandidateReferenceDoesNotClaimItsOwnCell()
    {
        var candidate = new PlayerState("Candidate", true, 8, 1) { lap = 1 };
        var players = new List<PlayerState> { candidate, null, candidate };
        Assert.That(RaceLaneRules.IsTrailingInParallel(candidate, players, 2), Is.False);

        players.Insert(1, new PlayerState("Distinct", false, 8, 1) { lap = 1 });
        Assert.That(RaceLaneRules.IsTrailingInParallel(candidate, players, 3), Is.True);
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void TerminalFlagsExcludeBothClaimantAndCandidate(bool finished, bool blown)
    {
        var earlier = new PlayerState("Earlier", false, 8, 1) { lap = 1 };
        var candidate = new PlayerState("Candidate", true, 8, 1) { lap = 1 };
        var players = new List<PlayerState> { earlier, candidate };
        earlier.hasFinished = finished;
        earlier.isBlown = blown;
        Assert.That(RaceLaneRules.IsTrailingInParallel(candidate, players, 1),
            Is.EqualTo(!finished && !blown));

        earlier.hasFinished = false;
        earlier.isBlown = false;
        candidate.hasFinished = finished;
        candidate.isBlown = blown;
        Assert.That(RaceLaneRules.IsTrailingInParallel(candidate, players, 1),
            Is.EqualTo(!finished && !blown));
    }

    [Test]
    public void LaterMatchingCarDoesNotChangeEarlierCandidate()
    {
        var candidate = new PlayerState("Candidate", true, 8, 1) { lap = 1 };
        var later = new PlayerState("Later", false, 8, 1) { lap = 1 };
        Assert.That(RaceLaneRules.IsTrailingInParallel(candidate,
            new List<PlayerState> { null, candidate, later }, 1), Is.False);
    }

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
