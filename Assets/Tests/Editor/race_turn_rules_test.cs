using System.Collections.Generic;
using NUnit.Framework;

public class RaceTurnRulesTests
{
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
    }
}
