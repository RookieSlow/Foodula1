using NUnit.Framework;

public class RaceStateLogFormatterTests
{
    [TestCase(false, false, false, "PLAYER", "False", "False")]
    [TestCase(true, true, false, "AI", "True", "False")]
    [TestCase(true, false, true, "AI", "False", "True")]
    public void SnapshotPreservesFieldsAndDoesNotMutateDeck(
        bool ai, bool blown, bool finished, string role, string blownText, string finishedText)
    {
        var player = new PlayerState("车手", ai, 7, 2)
        {
            lap = 1, position = 7, isBlown = blown, hasFinished = finished
        };
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 3) }, new HeatPool(6));
        player.deck.DrawToHand(1);
        player.deck.DrawHeatFromPoolToHand(2);
        Assert.AreEqual($"[STATE] 车手 role={role} lap=1 position=7 gear=2 engine_heat=4 hand_speed=1 " +
            $"blown={blownText} finished={finishedText}", RaceStateLogFormatter.BuildPlayerSnapshot(player));
        Assert.AreEqual(4, player.deck.heatPool.remaining);
        Assert.AreEqual(3, player.deck.HandCount);
        Assert.AreEqual(7, player.position);
    }

    [Test]
    public void EmptyPlayedAreaPreservesEmptyListAndOptionalSlotFields()
    {
        var player = new PlayerState("Player", false, 0, 2)
        {
            teamId = TeamId.US, extraCardSlotsThisTurn = 1
        };
        Assert.AreEqual("[CARDS] Player source=PLAYER count=0 values=[] gear_limit=3 base_limit=2 " +
            "extra_slots=1 hotpot_attack=False hotpot_card_value=0",
            RaceStateLogFormatter.BuildPlayedCards(player, "PLAYER"));
    }

    [TestCase(1, 3)]
    [TestCase(2, 4)]
    public void ChinaCardsPreserveCommitOrderAndAttackMetadata(int consecutive, int baseCount)
    {
        var player = new PlayerState("CN", false, 0, 2)
        {
            teamId = TeamId.CN, chinaConsecutiveGearCount = consecutive,
            extraCardSlotsThisTurn = 1, hotpotAttackAppliedThisTurn = true,
            hotpotAttackCardValueThisTurn = 3
        };
        var first = new CardData(CardType.Speed, 3);
        player.playedSpeedCardsThisTurn.Add(first);
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 1));
        Assert.AreEqual($"[CARDS] CN source=AI count=2 values=[3,1] gear_limit={baseCount + 1} " +
            $"base_limit={baseCount} extra_slots=1 hotpot_attack=True hotpot_card_value=3",
            RaceStateLogFormatter.BuildPlayedCards(player, "AI"));
        Assert.AreSame(first, player.playedSpeedCardsThisTurn[0]);
        Assert.AreEqual(2, player.playedSpeedCardsThisTurn.Count);
        Assert.IsTrue(player.hotpotAttackAppliedThisTurn);
        Assert.AreEqual(1, player.extraCardSlotsThisTurn);
    }

    [Test]
    public void NullSourceRetainsOriginalEmptySourceValue()
    {
        var player = new PlayerState("Player", false, 0, 1) { teamId = TeamId.UK };
        StringAssert.Contains("source= count=0 values=[]", RaceStateLogFormatter.BuildPlayedCards(player, null));
    }
}
