using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Session/campaign boundary for team technology. The rules layer remains
/// pure; this adapter owns persistence and provides one profile per team.
/// Profiles are saved as small JSON DTOs in PlayerPrefs so scene transitions
/// and application restarts preserve RP, unlocked nodes, and active choices.
/// </summary>
public static class TechTreeProfileStore
{
    private const string KeyPrefix = "Foodula1.TechTree.";
    private const string LegacyKeyPrefix = "Foodular1.TechTree.";
    private static readonly Dictionary<TeamId, TechTreeState> Cache =
        new Dictionary<TeamId, TechTreeState>();

    public static TechTreeState GetOrCreate(TeamId teamId, TechTreeDatabase db)
    {
        if (Cache.TryGetValue(teamId, out TechTreeState cached))
            return cached;

        TechTreeState state = RestoreOrCreate(teamId, db,
            PlayerPrefs.HasKey, key => PlayerPrefs.GetString(key), Save);
        Cache[teamId] = state;
        return state;
    }

    private static TechTreeState RestoreOrCreate(
        TeamId teamId,
        TechTreeDatabase db,
        Func<string, bool> hasKey,
        Func<string, string> read,
        Action<TechTreeState> save)
    {
        string key = KeyPrefix + teamId;
        string legacyKey = LegacyKeyPrefix + teamId;
        string savedKey = hasKey(key)
            ? key
            : (hasKey(legacyKey) ? legacyKey : null);
        TechTreeState state = null;
        if (savedKey != null)
        {
            try
            {
                state = TechTreeProfileCodec.Decode(read(savedKey), teamId, db);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TechTreeProfileStore] Ignoring invalid save for {teamId}: {ex.Message}");
            }
        }

        if (state == null)
        {
            state = CreateDemoProfile(teamId, db);
            save(state);
        }
        else if (savedKey == legacyKey)
        {
            // Migrate the legacy spelling without invalidating existing player progress.
            save(state);
        }

        return state;
    }

    public static bool TryUnlock(TechTreeState state, string nodeId, TechTreeDatabase db)
    {
        if (!TechTreeRules.UnlockNode(state, nodeId, db)) return false;
        // Newly unlocked techs are immediately available for the next race;
        // the UI can toggle them off without changing permanent ownership.
        state.activeNodeIds.Add(nodeId);
        Save(state);
        return true;
    }

    public static void SetActiveNodes(TechTreeState state, IEnumerable<string> nodeIds,
        TechTreeDatabase db)
    {
        TechTreeRules.SelectActiveNodes(state, nodeIds, db);
        Save(state);
    }

    public static void Save(TechTreeState state)
    {
        if (state == null) return;
        Cache[state.teamId] = state;
        PlayerPrefs.SetString(KeyPrefix + state.teamId, TechTreeProfileCodec.Encode(state));
        PlayerPrefs.Save();
    }

    public static void SaveAll()
    {
        SaveAllFrom(Cache, Save);
    }

    private static void SaveAllFrom(
        Dictionary<TeamId, TechTreeState> profiles,
        Action<TechTreeState> save)
    {
        // Save updates Cache, which invalidates Dictionary.Values enumeration
        // on Unity's runtime even when the existing key is assigned again.
        var snapshot = new List<TechTreeState>(profiles.Values);
        foreach (TechTreeState state in snapshot)
            save(state);
    }

    /// <summary>Development helper used by tests and reset controls.</summary>
    public static void Clear(TeamId teamId)
    {
        Cache.Remove(teamId);
        PlayerPrefs.DeleteKey(KeyPrefix + teamId);
        PlayerPrefs.DeleteKey(LegacyKeyPrefix + teamId);
        PlayerPrefs.Save();
    }

    private static TechTreeState CreateDemoProfile(TeamId teamId, TechTreeDatabase db)
    {
        return TechTreeRules.CreateDemoProfile(teamId, db);
    }
}
