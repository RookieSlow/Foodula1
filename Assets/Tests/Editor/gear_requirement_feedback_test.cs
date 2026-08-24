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
}
