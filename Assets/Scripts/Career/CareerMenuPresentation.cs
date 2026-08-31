using System;
using System.Collections.Generic;
using System.Text;

/// <summary>Player-facing labels shared by the authored and runtime menu paths.</summary>
public static class MainMenuLabels
{
    public const string QuickRace = "自由赛事";
    public const string QuickRaceTrackTitle = "自由赛事 · 选择赛道";
    public const string Career = "生涯模式";
}

/// <summary>Pure view data for the runtime-built career overlay.</summary>
public sealed class CareerMenuViewModel
{
    public string Header { get; internal set; }
    public string Status { get; internal set; }
    public string Calendar { get; internal set; }
    public string Standings { get; internal set; }
    public string PlayerSummary { get; internal set; }
    public string PrimaryAction { get; internal set; }
    public bool HasCareer { get; internal set; }
    public bool RequiresRecovery { get; internal set; }
    public bool CanLaunchRace { get; internal set; }
    public bool CanAdjustTech { get; internal set; }
    public bool CanStartNewSeason { get; internal set; }
}

/// <summary>Pure formatting and deterministic field selection for career UI.</summary>
public static class CareerMenuPresentation
{
    private static readonly TeamId[] Teams =
    {
        TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP
    };

    public static IReadOnlyList<TeamId> AvailableTeams => Teams;

    /// <summary>
    /// Builds a stable four-car field with the player first, followed by the
    /// first three other teams in catalog order.
    /// </summary>
    public static TeamId[] BuildCompetitorField(TeamId playerTeam)
    {
        if (!Enum.IsDefined(typeof(TeamId), playerTeam))
            return Array.Empty<TeamId>();

        var field = new List<TeamId> { playerTeam };
        for (int i = 0; i < Teams.Length && field.Count < 4; i++)
        {
            if (Teams[i] != playerTeam)
                field.Add(Teams[i]);
        }
        return field.ToArray();
    }

    public static CareerMenuViewModel Build(
        CareerSeasonState state,
        CareerLoadStatus loadStatus,
        bool raceLaunchAvailable)
    {
        var view = new CareerMenuViewModel
        {
            Header = MainMenuLabels.Career,
            HasCareer = state != null && state.HasLockedTeam &&
                        state.Phase != CareerPhase.NotStarted,
            RequiresRecovery = loadStatus == CareerLoadStatus.Invalid
        };

        if (!view.HasCareer)
        {
            view.Status = view.RequiresRecovery
                ? "检测到无法读取的生涯存档。请选择车队，并明确确认后覆盖或放弃旧存档。"
                : "选择任意车队开始八站生涯；确认后本轮不可更换。";
            view.Calendar = BuildEmptyCalendar();
            view.Standings = "完成首站后生成积分榜。";
            view.PlayerSummary = "尚未开始生涯";
            view.PrimaryAction = view.RequiresRecovery ? "确认覆盖并新建" : "确认车队并新建";
            return view;
        }

        List<CareerStanding> standings = CareerModeRules.GetStandings(state);
        view.Calendar = BuildCalendar(state);
        view.Standings = BuildStandings(standings);
        CareerStanding player = standings.Find(item => item.TeamId == state.LockedTeam);
        view.PlayerSummary = player == null
            ? $"锁定车队：{GetTeamLabel(state.LockedTeam)}"
            : $"锁定车队：{GetTeamLabel(state.LockedTeam)}　总分 {player.Points}　当前第 {player.Rank} 名";

        switch (state.Phase)
        {
            case CareerPhase.SummerBreak:
                view.Status = "夏休：已完成 4 站。确认一次科技树调整后才能进入第 5 站。";
                view.PrimaryAction = "调整生涯科技树";
                view.CanAdjustTech = true;
                break;
            case CareerPhase.Completed:
                CareerStanding champion = standings.Count > 0 ? standings[0] : null;
                if (player != null)
                    view.PlayerSummary = $"锁定车队：{GetTeamLabel(state.LockedTeam)}　总分 {player.Points}　最终第 {player.Rank} 名";
                view.Status = champion == null
                    ? "八站生涯已完成。最终积分榜不可用。"
                    : $"八站生涯已完成。总冠军：{GetTeamLabel(champion.TeamId)}（{champion.Points} 分）；" +
                      $"你的最终排名：第 {(player == null ? 0 : player.Rank)} 名。";
                view.PrimaryAction = "开启新一轮生涯";
                view.CanStartNewSeason = true;
                break;
            default:
                string trackName = GetTrackDisplayName(CareerModeRules.GetNextTrackId(state));
                view.Status = $"下一站：第 {state.NextTrackIndex + 1}/8 站　{trackName}";
                view.PrimaryAction = raceLaunchAvailable
                    ? $"开始第 {state.NextTrackIndex + 1} 站"
                    : "比赛接入将在下一阶段启用";
                view.CanLaunchRace = raceLaunchAvailable && CareerModeRules.CanStartNextRace(state);
                break;
        }

        return view;
    }

    public static string GetTeamLabel(TeamId team)
    {
        switch (team)
        {
            case TeamId.UK: return "英国 UK";
            case TeamId.DE: return "德国 DE";
            case TeamId.IT: return "意大利 IT";
            case TeamId.US: return "美国 US";
            case TeamId.CN: return "中国 CN";
            case TeamId.JP: return "日本 JP";
            default: return "未知车队";
        }
    }

    private static string BuildEmptyCalendar()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < CareerModeRules.TrackSchedule.Count; i++)
        {
            if (i > 0) builder.AppendLine();
            builder.Append($"○ {i + 1}. {GetTrackDisplayName(CareerModeRules.TrackSchedule[i])}");
        }
        return builder.ToString();
    }

    private static string BuildCalendar(CareerSeasonState state)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < CareerModeRules.TrackSchedule.Count; i++)
        {
            if (i > 0) builder.AppendLine();
            string marker = i < state.NextTrackIndex ? "✓" :
                (i == state.NextTrackIndex && state.Phase != CareerPhase.Completed ? "▶" : "○");
            builder.Append($"{marker} {i + 1}. {GetTrackDisplayName(CareerModeRules.TrackSchedule[i])}");
        }
        return builder.ToString();
    }

    private static string BuildStandings(List<CareerStanding> standings)
    {
        if (standings == null || standings.Count == 0)
            return "暂无积分。";

        var builder = new StringBuilder();
        for (int i = 0; i < standings.Count; i++)
        {
            if (i > 0) builder.AppendLine();
            CareerStanding entry = standings[i];
            builder.Append($"{entry.Rank}. {GetTeamLabel(entry.TeamId),-8}  {entry.Points,2} 分  {entry.Wins} 胜");
        }
        return builder.ToString();
    }

    private static string GetTrackDisplayName(string trackId)
    {
        IReadOnlyList<TrackSelectionOption> tracks = TrackSelectionState.AvailableTracks;
        for (int i = 0; i < tracks.Count; i++)
        {
            if (string.Equals(tracks[i].TrackId, trackId, StringComparison.Ordinal))
                return tracks[i].DisplayName;
        }
        return string.IsNullOrEmpty(trackId) ? "—" : trackId;
    }
}
