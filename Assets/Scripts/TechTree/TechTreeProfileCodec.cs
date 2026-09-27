using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Converts persistent tech-tree profile data to and from the stable JSON
/// payload used by <see cref="TechTreeProfileStore"/>. Runtime race flags are
/// intentionally excluded from this campaign profile.
/// </summary>
public static class TechTreeProfileCodec
{
    [Serializable]
    private sealed class SaveData
    {
        public TeamId teamId;
        public int rpBalance;
        public List<string> unlocked = new List<string>();
        public List<string> active = new List<string>();
    }

    public static string Encode(TechTreeState state)
    {
        if (state == null) return null;

        var save = new SaveData
        {
            teamId = state.teamId,
            rpBalance = state.rpBalance,
            unlocked = new List<string>(state.unlockedNodeIds),
            active = new List<string>(state.activeNodeIds)
        };
        return JsonUtility.ToJson(save);
    }

    /// <summary>
    /// Decodes a profile under the team selected by its storage key. Unknown
    /// nodes are ignored and active nodes must also be unlocked, matching the
    /// legacy profile-store recovery behavior.
    /// </summary>
    public static TechTreeState Decode(string serialized, TeamId teamId, TechTreeDatabase database)
    {
        if (string.IsNullOrEmpty(serialized)) return null;

        SaveData save = JsonUtility.FromJson<SaveData>(serialized);
        if (save == null || save.unlocked == null || save.active == null) return null;

        var state = new TechTreeState(teamId, save.rpBalance);
        foreach (string id in save.unlocked)
            if (database.Get(id) != null) state.unlockedNodeIds.Add(id);

        foreach (string id in save.active)
            if (state.unlockedNodeIds.Contains(id)) state.activeNodeIds.Add(id);

        return state;
    }
}
