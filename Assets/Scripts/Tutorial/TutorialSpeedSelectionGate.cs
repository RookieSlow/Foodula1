using System.Collections.Generic;

/// <summary>
/// Orders the existing team tutorial speed-selection checks. The race manager
/// still decides when a guided step is active and presents a rejection.
/// </summary>
public static class TutorialSpeedSelectionGate
{
    public static bool Validate(
        string scenarioId,
        TutorialStepId step,
        PlayerState player,
        IReadOnlyList<CardData> selected,
        out string reason)
    {
        IReadOnlyList<CardData> played = player != null ? player.playedSpeedCardsThisTurn : null;
        if (!TutorialSpecialtyCardRules.ValidateTrickLessonSpeedSelection(
                step, player != null && TrickCardRules.HasHotpotAttack(player.trickState),
                selected, out reason))
            return false;

        if (scenarioId == "tutorial_team_de_v1" &&
            !TutorialSpecialtyCardRules.ValidateDeStraightSpeedSelection(
                step, played, selected, out reason))
            return false;
        if (scenarioId == "tutorial_team_it_v1" &&
            !TutorialSpecialtyCardRules.ValidateItCornerSpeedSelection(
                step, played, selected, out reason))
            return false;
        if (scenarioId == "tutorial_team_jp_v1" &&
            !TutorialSpecialtyCardRules.ValidateJpSpeedSelection(
                step, player != null && player.trickState != null &&
                player.trickState.torpedoTempuraActive,
                played, selected, out reason))
            return false;
        if (scenarioId == "tutorial_team_it_v1" &&
            !TutorialSpecialtyCardRules.ValidateItSlipstreamSpeedSelection(
                step, player != null && player.trickState != null &&
                player.trickState.parmigianoActive,
                played, selected, out reason))
            return false;
        if (scenarioId == "tutorial_team_de_v1" &&
            !TutorialSpecialtyCardRules.ValidateDeEffectSpeedSelection(
                step, player != null && player.trickState != null &&
                (step == TutorialStepId.DeSauerkraut
                    ? player.trickState.sauerkrautPlayed
                    : player.trickState.schwarzbrotActive),
                played, selected, out reason))
            return false;
        if (scenarioId == "tutorial_team_us_v1")
            return TutorialSpecialtyCardRules.ValidateUsSpeedSelection(
                step, played, selected, out reason);
        return true;
    }
}
