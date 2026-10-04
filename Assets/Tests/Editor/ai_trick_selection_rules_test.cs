using NUnit.Framework;

public class AITrickSelectionRulesTests
{
    private static readonly TrickCardDef Defense = new TrickCardDef
    {
        cardType = TrickCardType.Defense,
        effectType = TrickEffectType.EnglishBreakfastTea
    };

    private static readonly TrickCardDef Attack = new TrickCardDef
    {
        cardType = TrickCardType.Attack,
        effectType = TrickEffectType.Scone
    };

    private static readonly TrickCardDef Kanto = new TrickCardDef
    {
        cardType = TrickCardType.Defense,
        effectType = TrickEffectType.KantoOden
    };

    [Test]
    public void UnknownDefinitionIsNotAttempted()
    {
        Assert.That(AITrickSelectionRules.ShouldAttempt(null, 1f, 0.7f, true), Is.False);
    }

    [TestCase(0.69f, false)]
    [TestCase(0.70f, true)]
    [TestCase(0.80f, true)]
    public void DefenseUsesConfiguredWarningThreshold(float heatRatio, bool expected)
    {
        Assert.That(AITrickSelectionRules.ShouldAttempt(Defense, heatRatio, 0.7f, true),
            Is.EqualTo(expected));
    }

    [TestCase(0.34f, true)]
    [TestCase(0.35f, true)]
    [TestCase(0.36f, false)]
    public void AttackUsesOriginalLowHeatCutoff(float heatRatio, bool expected)
    {
        Assert.That(AITrickSelectionRules.ShouldAttempt(Attack, heatRatio, 0.7f, true),
            Is.EqualTo(expected));
    }

    [Test]
    public void KantoIsAttemptedInLastPlaceAtModerateHeat()
    {
        Assert.That(AITrickSelectionRules.ShouldAttempt(Kanto, 0.5f, 0.7f, true), Is.True);
        Assert.That(AITrickSelectionRules.ShouldAttempt(Kanto, 0.5f, 0.7f, false), Is.False);
    }

    [Test]
    public void KantoDefenseStillTriggersAtWarningThresholdRegardlessOfRank()
    {
        Assert.That(AITrickSelectionRules.ShouldAttempt(Kanto, 0.7f, 0.7f, false), Is.True);
    }
}
