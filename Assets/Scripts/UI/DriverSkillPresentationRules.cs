/// <summary>Skill button copy; activation legality remains owned by DriverSkillRules.</summary>
public static class DriverSkillPresentationRules
{
    public static string GetButtonLabel(
        DriverProfile profile, DriverSkillRuntimeState state,
        DriverSkillActivationContext context, out bool interactable)
    {
        interactable = false;
        if (profile == null || state == null) return "车手技能";
        if (state.IsActive)
        {
            string duration = state.ActiveTurnsRemaining == int.MaxValue
                ? "本场有效" : $"{state.ActiveTurnsRemaining} 回合";
            return $"{profile.ActiveName}  {duration}";
        }

        interactable = DriverSkillRules.CanActivate(profile, state, context, out string reason);
        return interactable
            ? $"{profile.ActiveName}  ×{state.UsesRemaining}"
            : $"{profile.ActiveName}  {reason}";
    }
}
