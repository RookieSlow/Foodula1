using NUnit.Framework;

/// <summary>Checks the old team-rule dispatch order without scene, UI, or log side effects.</summary>
public class TutorialSpeedSelectionGateTests
{
    [TestCase("tutorial_team_cn_v1", TutorialStepId.ChinaHotpot, 1, "火锅底料")]
    [TestCase("tutorial_team_cn_v1", TutorialStepId.ChinaIceJelly, 1, "冰糕")]
    [TestCase("tutorial_team_us_v1", TutorialStepId.UsStraight, 3, "速度 1")]
    [TestCase("tutorial_team_de_v1", TutorialStepId.DeStraight, 3, "德国直道")]
    [TestCase("tutorial_team_de_v1", TutorialStepId.DeSauerkraut, 1, "酸菜发酵")]
    [TestCase("tutorial_team_it_v1", TutorialStepId.ItCorner, 3, "速度 1")]
    [TestCase("tutorial_team_it_v1", TutorialStepId.ItParmigiano, 1, "帕尔马干酪")]
    [TestCase("tutorial_team_jp_v1", TutorialStepId.JpKantoSkip, 1, "关东慢煮")]
    [TestCase("tutorial_team_jp_v1", TutorialStepId.JpTorpedo, 3, "鱼雷天妇罗")]
    [TestCase("tutorial_le_mans_uk_v1", TutorialStepId.UkSpecialtyScone, 1, "司康")]
    public void RejectsAtTheExistingTeamRuleWithItsReason(
        string scenario, TutorialStepId step, int speed, string expectedReason)
    {
        var player = new PlayerState("lesson", false, 5, 1);
        var selected = new[] { new CardData(CardType.Speed, speed) };

        Assert.That(TutorialSpeedSelectionGate.Validate(
            scenario, step, player, selected, out string reason), Is.False);

        StringAssert.Contains(expectedReason, reason);
        Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
        Assert.That(selected[0].value, Is.EqualTo(speed));
    }

    [TestCase("tutorial_team_us_v1", TutorialStepId.UsStraight, 1)]
    [TestCase("tutorial_team_de_v1", TutorialStepId.DeStraight, 1)]
    [TestCase("tutorial_team_it_v1", TutorialStepId.ItCorner, 1)]
    [TestCase("tutorial_team_jp_v1", TutorialStepId.JpKantoRelease, 1)]
    [TestCase("tutorial_team_de_v1", TutorialStepId.UsStraight, 3)]
    [TestCase("tutorial_le_mans_uk_v1", TutorialStepId.SpeedCardsAndMovement, 3)]
    public void AllowsValidOrUnrelatedSelectionsWithoutMutatingCards(
        string scenario, TutorialStepId step, int speed)
    {
        var player = new PlayerState("lesson", false, 5, 1);
        var card = new CardData(CardType.Speed, speed);

        Assert.That(TutorialSpeedSelectionGate.Validate(
            scenario, step, player, new[] { card }, out string reason), Is.True, reason);

        Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
        Assert.That(card.value, Is.EqualTo(speed));
    }

    [Test]
    public void ArmedGermanLessonStillRejectsWrongSpeedAfterTrickCheck()
    {
        var player = new PlayerState("lesson", false, 5, 1);
        player.trickState.sauerkrautPlayed = true;

        Assert.That(TutorialSpeedSelectionGate.Validate(
            "tutorial_team_de_v1", TutorialStepId.DeSauerkraut, player,
            new[] { new CardData(CardType.Speed, 3) }, out string reason), Is.False);

        StringAssert.Contains("速度 1", reason);
    }

    [Test]
    public void NullPlayerUsesTheOriginalUnarmedTrickLessonFallback()
    {
        Assert.That(TutorialSpeedSelectionGate.Validate(
            "tutorial_team_cn_v1", TutorialStepId.ChinaHotpot, null,
            new[] { new CardData(CardType.Speed, 1) }, out string reason), Is.False);
        StringAssert.Contains("火锅底料", reason);
    }
}
