using NUnit.Framework;

/// <summary>Preserves the team tutorial card-phase completion dispatch and reasons.</summary>
public class TutorialSpeedCompletionGateTests
{
    [TestCase("tutorial_team_cn_v1", TutorialStepId.ChinaHotpot, false, "火锅底料")]
    [TestCase("tutorial_team_cn_v1", TutorialStepId.ChinaIceJelly, false, "冰糕")]
    [TestCase("tutorial_team_us_v1", TutorialStepId.UsFries, false, "薯条")]
    [TestCase("tutorial_team_us_v1", TutorialStepId.UsCola, false, "可乐")]
    [TestCase("tutorial_team_it_v1", TutorialStepId.ItChianti, false, "基安蒂红酒")]
    [TestCase("tutorial_team_uk_v1", TutorialStepId.UkSpecialtyScone, false, "司康")]
    [TestCase("tutorial_team_uk_v1", TutorialStepId.UkSpecialtyTea, false, "英式红茶")]
    [TestCase("tutorial_team_de_v1", TutorialStepId.DeStraight, true, "速度 1")]
    [TestCase("tutorial_team_de_v1", TutorialStepId.DeSauerkraut, true, "酸菜发酵")]
    [TestCase("tutorial_team_de_v1", TutorialStepId.DeSchwarzbrot, true, "黑面包垫底")]
    [TestCase("tutorial_team_it_v1", TutorialStepId.ItCorner, true, "速度 1")]
    [TestCase("tutorial_team_it_v1", TutorialStepId.ItCornerExit, true, "速度 1")]
    [TestCase("tutorial_team_it_v1", TutorialStepId.ItParmigiano, true, "帕尔马干酪")]
    [TestCase("tutorial_team_jp_v1", TutorialStepId.JpKantoRelease, true, "3 张速度牌")]
    [TestCase("tutorial_team_jp_v1", TutorialStepId.JpTorpedo, true, "鱼雷天妇罗")]
    [TestCase("tutorial_team_us_v1", TutorialStepId.UsStraight, true, "速度 1")]
    [TestCase("tutorial_team_us_v1", TutorialStepId.UsCorner, true, "速度 3")]
    [TestCase("tutorial_team_us_v1", TutorialStepId.UsSlipstream, true, "速度 1")]
    public void RejectsAtFirstUnfinishedLessonWithoutChangingCards(
        string scenario, TutorialStepId step, bool stepComplete, string reasonFragment)
    {
        var player = new PlayerState("lesson", false, 5, 1);
        Assert.That(TutorialSpeedCompletionGate.Validate(
            scenario, step, stepComplete, player, out string reason), Is.False);
        StringAssert.Contains(reasonFragment, reason);
        Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
    }

    [Test]
    public void CompletedTrickLessonDoesNotRequireSpeedCards()
    {
        var player = new PlayerState("lesson", false, 5, 1);
        Assert.That(TutorialSpeedCompletionGate.Validate(
            "tutorial_team_cn_v1", TutorialStepId.ChinaHotpot, true,
            player, out string reason), Is.True, reason);
        Assert.That(reason, Is.Empty);
    }

    [Test]
    public void ArmedGermanEffectStillNeedsOneSpeedOne()
    {
        var player = new PlayerState("lesson", false, 5, 1);
        player.trickState.sauerkrautPlayed = true;
        Assert.That(TutorialSpeedCompletionGate.Validate(
            "tutorial_team_de_v1", TutorialStepId.DeSauerkraut, true,
            player, out string reason), Is.False);
        StringAssert.Contains("还需要确认 1 张速度 1", reason);
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 1));
        Assert.That(TutorialSpeedCompletionGate.Validate(
            "tutorial_team_de_v1", TutorialStepId.DeSauerkraut, true,
            player, out reason), Is.True, reason);
    }

    [Test]
    public void JapaneseCarrySlotsAndTorpedoArmingRemainRequired()
    {
        var player = new PlayerState("lesson", false, 5, 1);
        for (int i = 0; i < 3; i++)
            player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 1));
        Assert.That(TutorialSpeedCompletionGate.Validate(
            "tutorial_team_jp_v1", TutorialStepId.JpKantoRelease, true,
            player, out string reason), Is.False);
        player.extraCardSlotsThisTurn = 2;
        Assert.That(TutorialSpeedCompletionGate.Validate(
            "tutorial_team_jp_v1", TutorialStepId.JpKantoRelease, true,
            player, out reason), Is.True, reason);

        player.playedSpeedCardsThisTurn.Clear();
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 3));
        Assert.That(TutorialSpeedCompletionGate.Validate(
            "tutorial_team_jp_v1", TutorialStepId.JpTorpedo, true,
            player, out reason), Is.False);
        player.trickState.torpedoTempuraActive = true;
        Assert.That(TutorialSpeedCompletionGate.Validate(
            "tutorial_team_jp_v1", TutorialStepId.JpTorpedo, true,
            player, out reason), Is.True, reason);
    }

    [Test]
    public void ItalianAndAmericanCompletionUseTheirOriginalCardShape()
    {
        var player = new PlayerState("lesson", false, 5, 1);
        player.trickState.parmigianoActive = true;
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 1));
        Assert.That(TutorialSpeedCompletionGate.Validate(
            "tutorial_team_it_v1", TutorialStepId.ItParmigiano, true,
            player, out string reason), Is.True, reason);
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 2));
        Assert.That(TutorialSpeedCompletionGate.Validate(
            "tutorial_team_it_v1", TutorialStepId.ItParmigiano, true,
            player, out reason), Is.False);
        player.playedSpeedCardsThisTurn[0] = new CardData(CardType.Speed, 3);
        Assert.That(TutorialSpeedCompletionGate.Validate(
            "tutorial_team_us_v1", TutorialStepId.UsCorner, true,
            player, out reason), Is.True, reason);
    }

    [Test]
    public void UnrelatedScenarioDoesNotApplyAnotherTeamsSpeedGate()
    {
        var player = new PlayerState("lesson", false, 5, 1);
        Assert.That(TutorialSpeedCompletionGate.Validate(
            "tutorial_team_de_v1", TutorialStepId.UsCorner, true,
            player, out string reason), Is.True, reason);
        Assert.That(reason, Is.Empty);
    }

    [Test]
    public void NullPlayerPreservesSpeedLessonRejection()
    {
        Assert.That(TutorialSpeedCompletionGate.Validate(
            "tutorial_team_us_v1", TutorialStepId.UsStraight, true,
            null, out string reason), Is.False);
        StringAssert.Contains("速度 1", reason);
    }
}
