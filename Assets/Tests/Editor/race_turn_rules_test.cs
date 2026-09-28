using System.Collections.Generic;
using NUnit.Framework;

public class RaceTurnRulesTests
{
    public static IEnumerable<TestCaseData> StartActionCases()
    {
        for (int flags = 0; flags < 32; flags++)
        {
            bool finished = (flags & 1) != 0;
            bool blown = (flags & 2) != 0;
            bool pit = (flags & 4) != 0;
            bool skip = (flags & 8) != 0;
            bool oden = (flags & 16) != 0;
            RaceTurnStartAction expected = finished || blown
                ? RaceTurnStartAction.ExcludeTerminal
                : pit ? RaceTurnStartAction.ExecuteScheduledPitStop
                : skip || oden ? RaceTurnStartAction.ResolveSkip
                : RaceTurnStartAction.SelectGear;
            yield return new TestCaseData(finished, blown, pit, skip, oden, expected);
        }
    }

    [TestCaseSource(nameof(StartActionCases))]
    public void StartActionPreservesPriorityAndDoesNotConsumeFlags(
        bool finished, bool blown, bool pit, bool skip, bool oden, RaceTurnStartAction expected)
    {
        var player = new PlayerState("driver", false, 9, 2)
        {
            hasFinished = finished, isBlown = blown, pitStopScheduled = pit,
            skipNextTurn = skip, kantoOdenSkipThisTurn = oden
        };
        Assert.AreEqual(expected, RaceTurnRules.GetStartAction(player));
        Assert.AreEqual(finished, player.hasFinished);
        Assert.AreEqual(blown, player.isBlown);
        Assert.AreEqual(pit, player.pitStopScheduled);
        Assert.AreEqual(skip, player.skipNextTurn);
        Assert.AreEqual(oden, player.kantoOdenSkipThisTurn);
        Assert.AreEqual(9, player.position);
        Assert.AreEqual(2, player.gear);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RequestedPitEntryDoesNotExecuteBeforeItIsScheduled(bool ai)
    {
        var player = new PlayerState("driver", ai, 0, 1) { pitStopRequested = true };
        Assert.AreEqual(RaceTurnStartAction.SelectGear, RaceTurnRules.GetStartAction(player));
        Assert.IsTrue(player.pitStopRequested);
    }

    [Test]
    public void MissingParticipantIsExcluded()
    {
        Assert.AreEqual(RaceTurnStartAction.ExcludeTerminal, RaceTurnRules.GetStartAction(null));
    }

    [Test]
    public void SkipFlagsAreRecognized()
    {
        var player = new PlayerState("driver", false, 0, 1);

        Assert.That(RaceTurnRules.ShouldSkip(player), Is.False);
        player.skipNextTurn = true;
        Assert.That(RaceTurnRules.ShouldSkip(player), Is.True);

        player.skipNextTurn = false;
        player.kantoOdenSkipThisTurn = true;
        Assert.That(RaceTurnRules.ShouldSkip(player), Is.True);
    }

    [Test]
    public void InactiveIncludesConsumedSkipFlagsAndTerminalStates()
    {
        var player = new PlayerState("driver", false, 0, 1);
        var skipped = new HashSet<PlayerState>();

        Assert.That(RaceTurnRules.IsInactive(player, skipped), Is.False);

        skipped.Add(player);
        Assert.That(RaceTurnRules.IsInactive(player, skipped), Is.True);

        skipped.Clear();
        player.isBlown = true;
        Assert.That(RaceTurnRules.IsInactive(player, skipped), Is.True);

        player.isBlown = false;
        player.hasFinished = true;
        Assert.That(RaceTurnRules.IsInactive(player, skipped), Is.True);
    }

    [Test]
    public void NullParticipantsAreInactiveButDoNotRequestSkip()
    {
        Assert.That(RaceTurnRules.ShouldSkip(null), Is.False);
        Assert.That(RaceTurnRules.IsInactive(null, null), Is.True);
        Assert.That(RaceTurnRules.IsTerminal(null), Is.True);
    }

    [Test]
    public void TerminalStateLocksFinishedAndBlownParticipants()
    {
        var player = new PlayerState("driver", false, 0, 1);

        Assert.That(RaceTurnRules.IsTerminal(player), Is.False);
        player.hasFinished = true;
        Assert.That(RaceTurnRules.IsTerminal(player), Is.True);

        player.hasFinished = false;
        player.isBlown = true;
        Assert.That(RaceTurnRules.IsTerminal(player), Is.True);
    }
}
