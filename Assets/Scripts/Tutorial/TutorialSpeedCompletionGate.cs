using System.Collections.Generic;

/// <summary>
/// Applies the existing team tutorial checks before the card phase may end.
/// This does not advance the lesson or write HUD and race-log feedback.
/// </summary>
public static class TutorialSpeedCompletionGate
{
    public static bool Validate(
        string scenarioId, TutorialStepId step, bool stepComplete,
        PlayerState player, out string reason)
    {
        IReadOnlyList<CardData> played = player != null
            ? player.playedSpeedCardsThisTurn : null;
        if (!TutorialSpecialtyCardRules.ValidateTrickLessonCompletion(
                step, stepComplete, out reason))
            return false;
        if (scenarioId == "tutorial_team_de_v1" &&
            !TutorialSpecialtyCardRules.ValidateDeStraightSpeedCompletion(
                step, played, out reason))
            return false;
        if (scenarioId == "tutorial_team_de_v1" &&
            !TutorialSpecialtyCardRules.ValidateDeEffectSpeedCompletion(
                step, player != null && player.trickState != null &&
                (step == TutorialStepId.DeSauerkraut
                    ? player.trickState.sauerkrautPlayed
                    : player.trickState.schwarzbrotActive),
                played, out reason))
            return false;
        if (scenarioId == "tutorial_team_it_v1" &&
            !TutorialSpecialtyCardRules.ValidateItCornerSpeedCompletion(
                step, played, out reason))
            return false;
        if (scenarioId == "tutorial_team_jp_v1" &&
            !TutorialSpecialtyCardRules.ValidateJpSpeedCompletion(
                step, player != null && player.trickState != null &&
                player.trickState.torpedoTempuraActive,
                player != null ? player.extraCardSlotsThisTurn : 0,
                played, out reason))
            return false;
        if (scenarioId == "tutorial_team_it_v1" &&
            !TutorialSpecialtyCardRules.ValidateItSlipstreamSpeedCompletion(
                step, player != null && player.trickState != null &&
                player.trickState.parmigianoActive,
                played, out reason))
            return false;
        if (scenarioId == "tutorial_team_us_v1")
            return TutorialSpecialtyCardRules.ValidateUsSpeedPhaseCompletion(
                step, played, out reason);
        return true;
    }
}
