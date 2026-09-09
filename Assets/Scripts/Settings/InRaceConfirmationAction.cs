using System;

/// <summary>
/// Player-configurable confirmation gates for actions that can change the
/// current race state. The enum is serialized as a bit mask in settings so
/// adding a new gate does not require another preference field.
/// </summary>
[Flags]
public enum InRaceConfirmationAction
{
    None = 0,
    GearSelection = 1 << 0,
    GearCommit = 1 << 1,
    CardAction = 1 << 2,
    DriverSkill = 1 << 3,
    ResetRace = 1 << 4,
    ReturnToMenu = 1 << 5,
    PitDecision = 1 << 6,
    LaneChange = 1 << 7,
    All = GearSelection | GearCommit | CardAction | DriverSkill |
          ResetRace | ReturnToMenu | PitDecision | LaneChange
}

/// <summary>Pure helpers for the serialized confirmation preference.</summary>
public static class InRaceConfirmationRules
{
    public static int NormalizeMask(int mask)
    {
        return mask & (int)InRaceConfirmationAction.All;
    }

    public static bool IsEnabled(int mask, InRaceConfirmationAction action)
    {
        if (action == InRaceConfirmationAction.None)
            return false;
        int normalized = NormalizeMask(mask);
        int requested = (int)action;
        return (normalized & requested) == requested;
    }

    public static string GetDisplayName(InRaceConfirmationAction action)
    {
        switch (action)
        {
            case InRaceConfirmationAction.GearSelection: return "换挡选择";
            case InRaceConfirmationAction.GearCommit: return "确认换挡";
            case InRaceConfirmationAction.CardAction: return "出牌 / 弃牌";
            case InRaceConfirmationAction.DriverSkill: return "车手技能";
            case InRaceConfirmationAction.ResetRace: return "重新开始";
            case InRaceConfirmationAction.ReturnToMenu: return "返回主菜单";
            case InRaceConfirmationAction.PitDecision: return "维修区决定";
            case InRaceConfirmationAction.LaneChange: return "印地换道";
            default: return "局内操作";
        }
    }

    /// <summary>
    /// Returns the action-specific label for dismissing the confirmation.
    /// These labels deliberately describe the result of the button instead
    /// of reusing navigation wording such as "返回" for every action.
    /// </summary>
    public static string GetCancelButtonLabel(InRaceConfirmationAction action)
    {
        switch (action)
        {
            case InRaceConfirmationAction.GearSelection:
            case InRaceConfirmationAction.GearCommit: return "返回修改";
            case InRaceConfirmationAction.CardAction: return "返回选牌";
            case InRaceConfirmationAction.DriverSkill: return "暂不发动";
            case InRaceConfirmationAction.ResetRace: return "继续比赛";
            case InRaceConfirmationAction.ReturnToMenu: return "留在比赛";
            case InRaceConfirmationAction.PitDecision: return "返回决定";
            case InRaceConfirmationAction.LaneChange: return "返回选择";
            default: return "取消";
        }
    }

    /// <summary>Returns the action-specific label for committing the action.</summary>
    public static string GetConfirmButtonLabel(InRaceConfirmationAction action)
    {
        switch (action)
        {
            case InRaceConfirmationAction.GearSelection: return "选择档位";
            case InRaceConfirmationAction.GearCommit: return "锁定档位";
            case InRaceConfirmationAction.CardAction: return "确认提交";
            case InRaceConfirmationAction.DriverSkill: return "发动技能";
            case InRaceConfirmationAction.ResetRace: return "重新开始";
            case InRaceConfirmationAction.ReturnToMenu: return "返回主菜单";
            case InRaceConfirmationAction.PitDecision: return "确认选择";
            case InRaceConfirmationAction.LaneChange: return "确认换道";
            default: return "确认";
        }
    }
}
