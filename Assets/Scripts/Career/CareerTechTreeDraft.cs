using System;
using System.Collections.Generic;

/// <summary>
/// Isolated, mutable draft used only during the career summer break. It is
/// created from a career-owned snapshot and never reads or writes the normal
/// TechTreeProfileStore.
/// </summary>
public sealed class CareerTechTreeDraft
{
    private readonly TechTreeDatabase database;
    private readonly TechTreeState workingState;

    public TeamId TeamId => workingState.teamId;
    public int RpBalance => workingState.rpBalance;

    private CareerTechTreeDraft(TechTreeDatabase database, TechTreeState workingState)
    {
        this.database = database;
        this.workingState = workingState;
    }

    public static bool TryCreate(
        CareerTechSnapshot source,
        TechTreeDatabase database,
        out CareerTechTreeDraft draft)
    {
        draft = null;
        if (source == null || database == null)
            return false;

        var state = new TechTreeState(source.TeamId, source.RpBalance);
        for (int i = 0; i < source.UnlockedNodeIds.Count; i++)
        {
            string nodeId = source.UnlockedNodeIds[i];
            TechNodeDef node = database.Get(nodeId);
            if (!IsNodeAvailableToTeam(node, source.TeamId))
                return false;
            state.unlockedNodeIds.Add(nodeId);
        }

        for (int i = 0; i < source.ActiveNodeIds.Count; i++)
        {
            string nodeId = source.ActiveNodeIds[i];
            if (!state.unlockedNodeIds.Contains(nodeId))
                return false;
            state.activeNodeIds.Add(nodeId);
        }

        state.sunNeverSetsTarget = source.SunNeverSetsTarget;
        draft = new CareerTechTreeDraft(database, state);
        return true;
    }

    public IReadOnlyList<TechNodeDef> GetNodes(TechTreeTier tier)
    {
        return database.GetAllForTeamInTier(TeamId, tier, TeamId == global::TeamId.CN);
    }

    public bool IsUnlocked(string nodeId) => workingState.IsUnlocked(nodeId);
    public bool IsActive(string nodeId) => workingState.IsActive(nodeId);

    public bool CanUnlock(string nodeId)
    {
        return IsNodeAvailableToTeam(database.Get(nodeId), TeamId) &&
               TechTreeRules.CanUnlock(workingState, nodeId, database);
    }

    /// <summary>
    /// Unlocks and activates a currently available node, or toggles an already
    /// unlocked node. Failed actions leave the draft unchanged.
    /// </summary>
    public bool TryToggleOrUnlock(string nodeId)
    {
        TechNodeDef node = database.Get(nodeId);
        if (!IsNodeAvailableToTeam(node, TeamId))
            return false;

        if (!workingState.IsUnlocked(nodeId))
        {
            if (!TechTreeRules.UnlockNode(workingState, nodeId, database))
                return false;
            workingState.activeNodeIds.Add(nodeId);
            return true;
        }

        if (!workingState.activeNodeIds.Remove(nodeId))
            workingState.activeNodeIds.Add(nodeId);
        return true;
    }

    public bool TryBuildSnapshot(out CareerTechSnapshot snapshot)
    {
        return CareerTechSnapshot.TryCreate(
            workingState.teamId,
            workingState.rpBalance,
            workingState.unlockedNodeIds,
            workingState.activeNodeIds,
            workingState.sunNeverSetsTarget,
            out snapshot);
    }

    private static bool IsNodeAvailableToTeam(TechNodeDef node, TeamId team)
    {
        return node != null && (!node.teamId.HasValue || node.teamId.Value == team);
    }
}
