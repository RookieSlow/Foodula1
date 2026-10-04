/// <summary>Pure eligibility policy for an AI's ordered trick-card hand.</summary>
public static class AITrickSelectionRules
{
    public static bool ShouldAttempt(
        TrickCardDef definition, float heatRatio, float heatWarningThreshold, bool isLastPlace)
    {
        if (definition == null)
            return false;

        if (definition.IsDefense && heatRatio >= heatWarningThreshold)
            return true;
        if (definition.IsAttack && heatRatio <= 0.35f)
            return true;
        return definition.effectType == TrickEffectType.KantoOden && isLastPlace;
    }
}
