using System;
using System.Collections.Generic;

/// <summary>One team/driver slot in a custom free-race field.</summary>
public readonly struct FreeRaceRosterEntry
{
    public FreeRaceRosterEntry(TeamId teamId, string driverId)
    {
        TeamId = teamId;
        DriverId = driverId ?? string.Empty;
    }

    public TeamId TeamId { get; }
    public string DriverId { get; }
}

/// <summary>
/// Pure validation and catalog helpers for the free-race field editor.
/// A field contains one car per selected team and one unique driver per car.
/// </summary>
public static class FreeRaceRosterRules
{
    public const int MinParticipants = 2;
    public const int MaxParticipants = 6;
    public const int ThunderstormParticipants = 12;

    private static readonly TeamId[] Teams =
    {
        TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP
    };

    public static IReadOnlyList<TeamId> AvailableTeams => Teams;

    public static bool IsKnownTeam(TeamId teamId)
    {
        return Enum.IsDefined(typeof(TeamId), teamId);
    }

    /// <summary>Validates a proposed field before it is handed to the race scene.</summary>
    public static bool TryValidate(
        IReadOnlyList<TeamId> selectedTeams,
        IReadOnlyDictionary<TeamId, string> driverByTeam,
        out string error)
    {
        error = string.Empty;
        if (selectedTeams == null)
        {
            error = "尚未选择车队。";
            return false;
        }

        if (selectedTeams.Count < MinParticipants || selectedTeams.Count > MaxParticipants)
        {
            error = $"自由赛事需要选择 {MinParticipants}-{MaxParticipants} 支车队。";
            return false;
        }

        if (driverByTeam == null)
        {
            error = "每支车队都需要安排车手。";
            return false;
        }

        var teams = new HashSet<TeamId>();
        var drivers = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < selectedTeams.Count; i++)
        {
            TeamId team = selectedTeams[i];
            if (!IsKnownTeam(team) || !teams.Add(team))
            {
                error = "参赛车队不能重复。";
                return false;
            }

            if (!driverByTeam.TryGetValue(team, out string driverId) ||
                !DriverCatalog.TryGet(driverId, out DriverProfile driver) ||
                driver.Team != team)
            {
                error = $"{team} 还没有安排有效车手。";
                return false;
            }

            if (!drivers.Add(driver.Id))
            {
                error = "同一名车手不能同时驾驶两支车队。";
                return false;
            }
        }

        return true;
    }

    /// <summary>Copies a validated field to an immutable value array for the race scene.</summary>
    public static FreeRaceRosterEntry[] BuildEntries(
        IReadOnlyList<TeamId> selectedTeams,
        IReadOnlyDictionary<TeamId, string> driverByTeam)
    {
        if (!TryValidate(selectedTeams, driverByTeam, out _))
            return Array.Empty<FreeRaceRosterEntry>();

        var result = new FreeRaceRosterEntry[selectedTeams.Count];
        for (int i = 0; i < selectedTeams.Count; i++)
            result[i] = new FreeRaceRosterEntry(selectedTeams[i], driverByTeam[selectedTeams[i]]);
        return result;
    }

    /// <summary>
    /// Builds the dedicated full-field experiment: both catalogued drivers from
    /// all six teams enter the same race. The selected garage driver is moved
    /// to the first slot so the player remains in control of their current car.
    /// </summary>
    public static bool TryBuildThunderstormEntries(
        string primaryDriverId,
        out FreeRaceRosterEntry[] entries,
        out string error)
    {
        entries = Array.Empty<FreeRaceRosterEntry>();
        error = string.Empty;
        if (!DriverCatalog.TryGet(primaryDriverId, out DriverProfile primary))
        {
            error = "雷霆大混战缺少有效的玩家车手。";
            return false;
        }

        IReadOnlyList<DriverProfile> drivers = DriverCatalog.All;
        if (drivers == null || drivers.Count != ThunderstormParticipants)
        {
            error = $"雷霆大混战需要 {ThunderstormParticipants} 名车手，当前目录不完整。";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var teams = new Dictionary<TeamId, int>();
        for (int i = 0; i < drivers.Count; i++)
        {
            DriverProfile driver = drivers[i];
            if (driver == null || !ids.Add(driver.Id))
            {
                error = "雷霆大混战的车手目录存在重复或空车手。";
                return false;
            }

            teams.TryGetValue(driver.Team, out int count);
            teams[driver.Team] = count + 1;
        }

        if (teams.Count != Teams.Length)
        {
            error = "雷霆大混战需要六支正式车队。";
            return false;
        }

        for (int i = 0; i < Teams.Length; i++)
        {
            if (!teams.TryGetValue(Teams[i], out int count) || count != 2)
            {
                error = $"{Teams[i]} 必须恰好有两名车手参加雷霆大混战。";
                return false;
            }
        }

        var result = new List<FreeRaceRosterEntry>(drivers.Count)
        {
            new FreeRaceRosterEntry(primary.Team, primary.Id)
        };
        for (int i = 0; i < drivers.Count; i++)
        {
            if (drivers[i].Id == primary.Id) continue;
            result.Add(new FreeRaceRosterEntry(drivers[i].Team, drivers[i].Id));
        }

        entries = result.ToArray();
        return entries.Length == ThunderstormParticipants;
    }
}

/// <summary>
/// Session-only custom free-race roster. It is intentionally separate from
/// career persistence: a free-race experiment must never alter the career save.
/// The first selected team is the human-controlled car; remaining cars are AI.
/// </summary>
public static class FreeRaceRosterState
{
    private static readonly List<TeamId> selectedTeams = new List<TeamId>();
    private static readonly Dictionary<TeamId, string> driverByTeam =
        new Dictionary<TeamId, string>();
    private static bool configured;
    private static bool thunderstorm;
    private static string thunderstormPrimaryDriverId = string.Empty;

    public static IReadOnlyList<TeamId> SelectedTeams => selectedTeams;
    public static IReadOnlyList<TeamId> AvailableTeams => FreeRaceRosterRules.AvailableTeams;
    public static bool IsConfigured => configured;
    public static bool IsThunderstorm => thunderstorm;
    public static int SelectedCount => selectedTeams.Count;
    public static int RosterCount => thunderstorm
        ? FreeRaceRosterRules.ThunderstormParticipants
        : selectedTeams.Count;

    public static bool IsSelected(TeamId teamId)
    {
        return selectedTeams.Contains(teamId);
    }

    public static string GetDriverId(TeamId teamId)
    {
        if (driverByTeam.TryGetValue(teamId, out string driverId) &&
            DriverCatalog.TryGet(driverId, out DriverProfile driver) &&
            driver.Team == teamId)
        {
            return driver.Id;
        }

        return DriverCatalog.GetDefaultForTeam(teamId).Id;
    }

    public static DriverProfile GetDriver(TeamId teamId)
    {
        return DriverCatalog.TryGet(GetDriverId(teamId), out DriverProfile driver)
            ? driver
            : DriverCatalog.GetDefaultForTeam(teamId);
    }

    /// <summary>
    /// Restores the current four-car quick-race feel before the user edits it.
    /// The selected garage driver is kept as the first (human) slot.
    /// </summary>
    public static void InitializeDefault(DriverProfile primaryDriver, IReadOnlyList<TeamId> fallbackAiTeams)
    {
        Clear();
        configured = true;

        DriverProfile primary = primaryDriver ?? DriverCatalog.GetDefaultForTeam(TeamId.CN);
        AddTeam(primary.Team, primary.Id);

        if (fallbackAiTeams != null)
        {
            for (int i = 0; i < fallbackAiTeams.Count &&
                selectedTeams.Count < 4 && selectedTeams.Count < FreeRaceRosterRules.MaxParticipants; i++)
            {
                TeamId team = fallbackAiTeams[i];
                if (!FreeRaceRosterRules.IsKnownTeam(team) || IsSelected(team)) continue;
                AddTeam(team, DriverCatalog.GetDefaultForTeam(team).Id);
            }
        }

        // A garage driver can already belong to one of the fallback teams.
        // Fill the remaining default slots from catalog order so the legacy
        // quick-race field remains player + three AI in every case.
        for (int i = 0; i < FreeRaceRosterRules.AvailableTeams.Count && selectedTeams.Count < 4; i++)
        {
            TeamId team = FreeRaceRosterRules.AvailableTeams[i];
            if (!IsSelected(team)) AddTeam(team, DriverCatalog.GetDefaultForTeam(team).Id);
        }
    }

    /// <summary>
    /// Selects every driver in the catalog for the full 6-team/12-car
    /// experiment. This is a separate mode so the compact custom roster keeps
    /// its readable one-car-per-team workflow.
    /// </summary>
    public static void InitializeThunderstorm(DriverProfile primaryDriver)
    {
        Clear();
        configured = true;
        thunderstorm = true;

        DriverProfile primary = primaryDriver ?? DriverSelectionState.ResolveDriver(TeamId.CN);
        thunderstormPrimaryDriverId = primary.Id;
        AddTeam(primary.Team, primary.Id);

        for (int i = 0; i < FreeRaceRosterRules.AvailableTeams.Count; i++)
        {
            TeamId team = FreeRaceRosterRules.AvailableTeams[i];
            if (!IsSelected(team))
                AddTeam(team, DriverCatalog.GetDefaultForTeam(team).Id);
        }
    }

    public static bool TrySetTeamSelected(TeamId teamId, bool selected)
    {
        if (thunderstorm) return false;
        if (!FreeRaceRosterRules.IsKnownTeam(teamId)) return false;
        if (selected)
        {
            if (IsSelected(teamId)) return true;
            if (selectedTeams.Count >= FreeRaceRosterRules.MaxParticipants) return false;
            AddTeam(teamId, DriverCatalog.GetDefaultForTeam(teamId).Id);
            configured = true;
            return true;
        }

        selectedTeams.Remove(teamId);
        configured = true;
        return true;
    }

    public static bool TryAssignDriver(TeamId teamId, string driverId)
    {
        if (thunderstorm) return false;
        if (!IsSelected(teamId) || !DriverCatalog.TryGet(driverId, out DriverProfile driver) ||
            driver.Team != teamId)
        {
            return false;
        }

        driverByTeam[teamId] = driver.Id;
        configured = true;
        return true;
    }

    public static bool TryBuildRoster(out FreeRaceRosterEntry[] roster, out string error)
    {
        if (thunderstorm)
            return FreeRaceRosterRules.TryBuildThunderstormEntries(
                thunderstormPrimaryDriverId, out roster, out error);

        roster = null;
        if (!FreeRaceRosterRules.TryValidate(selectedTeams, driverByTeam, out error))
            return false;

        roster = FreeRaceRosterRules.BuildEntries(selectedTeams, driverByTeam);
        return roster.Length > 0;
    }

    /// <summary>Promotes the first roster driver to the normal garage selection.</summary>
    public static bool ApplyPrimaryDriverSelection()
    {
        if (!TryBuildRoster(out FreeRaceRosterEntry[] roster, out _)) return false;
        return DriverSelectionState.TrySelect(roster[0].DriverId);
    }

    public static void Clear()
    {
        selectedTeams.Clear();
        driverByTeam.Clear();
        configured = false;
        thunderstorm = false;
        thunderstormPrimaryDriverId = string.Empty;
    }

    private static void AddTeam(TeamId teamId, string driverId)
    {
        if (IsSelected(teamId)) return;
        selectedTeams.Add(teamId);
        driverByTeam[teamId] = driverId;
    }
}
