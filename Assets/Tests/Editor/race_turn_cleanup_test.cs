using System;
using System.Collections.Generic;
using NUnit.Framework;

public class RaceTurnCleanupTests
{
    private static PlayerState CreatePlayer(bool ai = false)
    {
        var player = new PlayerState("driver", ai, 5, 2)
        {
            techState = new TechTreeState(TeamId.UK, 0),
            italyCornerExitBoostReady = true
        };
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 3) }, new HeatPool(6));
        player.deck.DrawToHand(1);
        player.playedSpeedCardsThisTurn.AddRange(player.deck.RemoveFromHand(new List<CardData>(player.deck.Hand)));
        player.deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
        return player;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CleanupPreservesExactOwnershipAndEffectOrder(bool ai)
    {
        var player = CreatePlayer(ai);
        CardData played = player.playedSpeedCardsThisTurn[0];
        var events = new List<string>();
        RaceTurnCleanup.Execute(player, p =>
        {
            events.Add("skills");
            Assert.AreSame(played, p.deck.DiscardPile[0]);
            Assert.AreEqual(1, p.deck.CountTemporaryHeatOutsideEngine());
        }, (p, count) =>
        {
            events.Add("temporary");
            Assert.AreEqual(1, count);
            Assert.AreEqual(0, p.deck.CountTemporaryHeatOutsideEngine());
        }, p =>
        {
            events.Add("technology");
            Assert.AreSame(played, p.playedSpeedCardsThisTurn[0]);
        });
        CollectionAssert.AreEqual(new[] { "skills", "temporary", "technology" }, events);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.AreSame(played, player.deck.DiscardPile[0]);
        Assert.AreEqual(1, player.deck.DiscardPileCount);
        Assert.IsTrue(player.italyCornerExitBoostReady);
        Assert.AreEqual(5, player.position);
    }

    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(false, false, false)]
    public void TerminalOrDisabledTechStillCleansCardsAndSkills(bool finished, bool blown, bool tech)
    {
        var player = CreatePlayer();
        player.hasFinished = finished;
        player.isBlown = blown;
        if (!tech) player.techState = null;
        int skills = 0, temporary = 0, technology = 0;
        RaceTurnCleanup.Execute(player, p => skills++, (p, n) => temporary += n, p => technology++);
        Assert.AreEqual(1, skills);
        Assert.AreEqual(1, temporary);
        Assert.AreEqual(0, technology);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.AreEqual(1, player.deck.DiscardPileCount);
    }

    [Test]
    public void PermanentHeatIsNotDestroyedOrRefunded()
    {
        var player = CreatePlayer();
        player.deck.DrawHeatFromPoolToHand(2);
        RaceTurnCleanup.Execute(player, null, null, null);
        Assert.AreEqual(2, player.deck.CountHeatInHand());
        Assert.AreEqual(4, player.deck.heatPool.remaining);
        Assert.AreEqual(6, player.deck.CountPermanentHeatOutsideEngine() + player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountTemporaryHeatOutsideEngine());
    }

    [Test]
    public void TerminalGateReadsStateAfterSkillResolution()
    {
        var player = CreatePlayer();
        int techCalls = 0;
        RaceTurnCleanup.Execute(player, p => p.hasFinished = true, null, p => techCalls++);
        Assert.AreEqual(0, techCalls);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
    }

    [Test]
    public void EmptyCleanupDoesNotReportTemporaryRemoval()
    {
        var player = new PlayerState("driver", false, 0, 1);
        RaceTurnCleanup.Execute(player, null, (p, n) => Assert.Fail("No cards removed"), null);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.AreEqual(0, player.deck.DiscardPileCount);
    }

    [Test]
    public void EffectFailurePropagatesWithoutRunningLaterCleanupStages()
    {
        var player = CreatePlayer();
        Assert.Throws<InvalidOperationException>(() => RaceTurnCleanup.Execute(player,
            p => throw new InvalidOperationException("effect failure"),
            (p, n) => Assert.Fail("Later stage"), p => Assert.Fail("Later stage")));
        Assert.AreEqual(1, player.deck.DiscardPileCount);
        Assert.AreEqual(1, player.playedSpeedCardsThisTurn.Count);
        Assert.AreEqual(1, player.deck.CountTemporaryHeatOutsideEngine());
    }
}
