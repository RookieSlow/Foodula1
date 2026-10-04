using NUnit.Framework;

public class RaceStateLogFormatterCommittedHeatTests
{
    [TestCase(false, false, "PLAYER")]
    [TestCase(false, true, "AI")]
    [TestCase(true, false, "PLAYER")]
    [TestCase(true, true, "AI")]
    public void CommittedHeatKeepsItsInstanceAndLogsEffectiveSpeedTwo(
        bool temporary, bool isAI, string source)
    {
        var player = new PlayerState("BBQ", isAI, 0, 2) { teamId = TeamId.US };
        CardData heat = temporary ? CardData.CreateTempHeat() : new CardData(CardType.Heat, 0);
        player.playedSpeedCardsThisTurn.Add(heat);

        Assert.That(RaceStateLogFormatter.BuildPlayedCards(player, source), Is.EqualTo(
            $"[CARDS] BBQ source={source} count=1 values=[2] gear_limit=2 base_limit=2 " +
            "extra_slots=0 hotpot_attack=False hotpot_card_value=0 bbq_heat=1"));
        Assert.That(player.playedSpeedCardsThisTurn, Has.Count.EqualTo(1));
        Assert.That(player.playedSpeedCardsThisTurn[0], Is.SameAs(heat));
        Assert.That(heat.type, Is.EqualTo(CardType.Heat));
        Assert.That(heat.value, Is.Zero);
        Assert.That(heat.isTemp, Is.EqualTo(temporary));
    }

    [Test]
    public void MixedCardsRetainCommitOrderAndCountOnlyHeatAsBbqHeat()
    {
        var player = new PlayerState("Mixed", false, 0, 2)
        {
            teamId = TeamId.UK, extraCardSlotsThisTurn = 2
        };
        var first = new CardData(CardType.Speed, 4);
        var permanentHeat = new CardData(CardType.Heat, 0);
        var last = new CardData(CardType.Speed, 1);
        var temporaryHeat = CardData.CreateTempHeat();
        player.playedSpeedCardsThisTurn.Add(first);
        player.playedSpeedCardsThisTurn.Add(permanentHeat);
        player.playedSpeedCardsThisTurn.Add(last);
        player.playedSpeedCardsThisTurn.Add(temporaryHeat);

        Assert.That(RaceStateLogFormatter.BuildPlayedCards(player, "AI"), Is.EqualTo(
            "[CARDS] Mixed source=AI count=4 values=[4,2,1,2] gear_limit=4 base_limit=2 " +
            "extra_slots=2 hotpot_attack=False hotpot_card_value=0 bbq_heat=2"));
        Assert.That(player.playedSpeedCardsThisTurn,
            Is.EqualTo(new[] { first, permanentHeat, last, temporaryHeat }));
        Assert.That(permanentHeat.value, Is.Zero);
        Assert.That(temporaryHeat.value, Is.Zero);
    }

    [Test]
    public void OrdinarySpeedCardsDoNotAcquireBbqHeatMarker()
    {
        var player = new PlayerState("Normal", false, 0, 1) { teamId = TeamId.UK };
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 2));

        Assert.That(RaceStateLogFormatter.BuildPlayedCards(player, "PLAYER"), Is.EqualTo(
            "[CARDS] Normal source=PLAYER count=1 values=[2] gear_limit=1 base_limit=1 " +
            "extra_slots=0 hotpot_attack=False hotpot_card_value=0"));
    }
}
