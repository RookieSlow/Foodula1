using NUnit.Framework;

public class GearRequirementFeedbackTests
{
    [Test]
    public void MissingCardsShowFailureAndHandShortageWarning()
    {
        GearRequirementFeedback feedback = GearRequirementFeedbackRules.Evaluate(
            requiredCards: 4,
            confirmedCards: 1,
            pendingCards: 0,
            speedCardsInHand: 1,
            engineHeat: 3);

        Assert.That(feedback.MissingCards, Is.EqualTo(3));
        Assert.That(feedback.HandCannotSatisfy, Is.True);
        Assert.That(feedback.MayTriggerSpin, Is.False);

        string status = GearRequirementFeedbackRules.FormatStatus("G4", feedback);
        StringAssert.Contains("要求 4 张", status);
        StringAssert.Contains("本回合 1/4", status);
        StringAssert.Contains("缺 3 张", status);
        StringAssert.Contains("当前手牌速度牌不足", status);
    }

    [Test]
    public void PendingSpeedCardsCountTowardEffectiveRequirement()
    {
        GearRequirementFeedback feedback = GearRequirementFeedbackRules.Evaluate(
            requiredCards: 3,
            confirmedCards: 1,
            pendingCards: 2,
            speedCardsInHand: 2,
            engineHeat: 0);

        Assert.That(feedback.DisplayedCards, Is.EqualTo(3));
        Assert.That(feedback.MissingCards, Is.Zero);
        Assert.That(feedback.WillTriggerEngineFailure, Is.False);
        StringAssert.Contains("已满足要求", GearRequirementFeedbackRules.FormatStatus("G3", feedback));
        Assert.That(GearRequirementFeedbackRules.FormatEndButtonLabel(feedback), Is.EqualTo("结束出牌"));
    }

    [Test]
    public void InsufficientEngineHeatWarnsAboutPossibleSpin()
    {
        GearRequirementFeedback feedback = GearRequirementFeedbackRules.Evaluate(
            requiredCards: 3,
            confirmedCards: 1,
            pendingCards: 0,
            speedCardsInHand: 0,
            engineHeat: 1);

        Assert.That(feedback.MissingCards, Is.EqualTo(2));
        Assert.That(feedback.MayTriggerSpin, Is.True);
        StringAssert.Contains("可能触发失控", GearRequirementFeedbackRules.FormatStatus("G3", feedback));
        StringAssert.Contains("可能失控", GearRequirementFeedbackRules.FormatEndButtonLabel(feedback));
    }

    [Test]
    public void PendingSpeedStatusUsesCommittedHeatValueWithoutChangingCard()
    {
        var heat = CardData.CreateTempHeat();
        string status = GearRequirementFeedbackRules.FormatPendingSpeedStatus(
            "G2（要求 2 张）", new[] { new CardData(CardType.Speed, 3), heat });

        Assert.AreEqual("G2（要求 2 张）\n待确认：2 张速度牌（速度总和 5）", status);
        Assert.IsTrue(heat.IsHeat);
        Assert.AreEqual(0, heat.value);
    }

    [TestCase(SpeedCardCommitResult.SpeedLimitReached, 4, "速度牌已达上限：4 张（本次选择未提交）")]
    [TestCase(SpeedCardCommitResult.CardNotInHand, 3, "出牌失败：手牌状态已变化")]
    [TestCase(SpeedCardCommitResult.InvalidCard, 2, "出牌失败：手牌状态已变化")]
    public void ConfirmedSpeedFailureCopyPreservesExistingOutcomeMapping(
        SpeedCardCommitResult result, int maxCards, string expected)
    {
        Assert.AreEqual(expected, GearRequirementFeedbackRules.FormatSpeedCommitFailure(result, maxCards));
    }

    [Test]
    public void ConfirmedSpeedLogUsesTheSameHeatValueAsPendingSelection()
    {
        CardData heat = CardData.CreateTempHeat();
        CardData[] cards = { new CardData(CardType.Speed, 3), heat };

        Assert.AreEqual("车手 确认 2 张速度牌（速度总和 5）。",
            GearRequirementFeedbackRules.FormatCommittedSpeedLog("车手", cards));
        Assert.AreEqual("G2\n待确认：2 张速度牌（速度总和 5）",
            GearRequirementFeedbackRules.FormatPendingSpeedStatus("G2", cards));
        Assert.IsTrue(heat.IsHeat);
        Assert.AreEqual(0, heat.value);
    }

    [Test]
    public void ConfirmedSpeedLogPreservesOrdinaryCardTotal()
    {
        CardData[] cards = { new CardData(CardType.Speed, 1), new CardData(CardType.Speed, 4) };
        Assert.AreEqual("Racer 确认 2 张速度牌（速度总和 5）。",
            GearRequirementFeedbackRules.FormatCommittedSpeedLog("Racer", cards));
    }

    [TestCase(1, 3, "已打出 1/3 张速度牌；可继续多选或结束出牌")]
    [TestCase(4, 4, "已打出 4/4 张速度牌；可继续多选或结束出牌")]
    public void ConfirmedSpeedStatusPreservesProgressCopy(int confirmed, int maxCards, string expected)
    {
        Assert.AreEqual(expected,
            GearRequirementFeedbackRules.FormatCommittedSpeedStatus(confirmed, maxCards));
    }

    [TestCase("特技牌")]
    [TestCase("热汤")]
    public void PendingTrickStatusKeepsImmediateActivationCopy(string name)
    {
        Assert.AreEqual($"G1\n待确认：{name}（确认后立即发动）",
            GearRequirementFeedbackRules.FormatPendingTrickStatus("G1", name));
    }

    [TestCase(true, 0, false, "弃置所选牌  [空格]")]
    [TestCase(true, 2, false, "弃置所选牌  [空格]")]
    [TestCase(false, 1, false, "打出速度牌  [空格]")]
    [TestCase(false, 3, false, "打出速度牌 (3)  [空格]")]
    [TestCase(false, 1, true, "打出特技牌  [空格]")]
    public void SelectedActionLabelsPreserveModeAndShortcut(
        bool discard, int selectedCount, bool trick, string expected)
    {
        var feedback = GearRequirementFeedbackRules.Evaluate(3, 0, 0, 0, 0);
        Assert.AreEqual(expected, GearRequirementFeedbackRules.FormatActionButtonLabel(
            discard, selectedCount, trick, feedback));
    }

    [Test]
    public void EmptySelectionUsesEndWarningWithoutSpaceShortcut()
    {
        var feedback = GearRequirementFeedbackRules.Evaluate(3, 1, 0, 0, 1);
        Assert.AreEqual("结束出牌 · 缺2，可能失控",
            GearRequirementFeedbackRules.FormatActionButtonLabel(false, 0, false, feedback));
        var noManager = GearRequirementFeedbackRules.Evaluate(0, 0, 0, 0, 0);
        Assert.AreEqual("结束出牌",
            GearRequirementFeedbackRules.FormatActionButtonLabel(false, 0, false, noManager));
    }
}
