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

    [Serializable]
    private sealed class SaveData
    {
        public TeamId teamId;
        public int rpBalance;
        public List<string> unlocked = new List<string>();
        public List<string> active = new List<string>();
    }

    public static TechTreeState GetOrCreate(TeamId teamId, TechTreeDatabase db)
    {
        if (Cache.TryGetValue(teamId, out TechTreeState cached))
            return cached;

        string key = KeyPrefix + teamId;
        string legacyKey = LegacyKeyPrefix + teamId;
        string savedKey = PlayerPrefs.HasKey(key)
            ? key
            : (PlayerPrefs.HasKey(legacyKey) ? legacyKey : null);
        TechTreeState state = null;
        if (savedKey != null)
        {
            try
            {
                SaveData save = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(savedKey));
                if (save != null && save.unlocked != null && save.active != null)
                {
                    state = new TechTreeState(teamId, save.rpBalance);
                    foreach (string id in save.unlocked)
                        if (db.Get(id) != null) state.unlockedNodeIds.Add(id);
                    foreach (string id in save.active)
                        if (state.unlockedNodeIds.Contains(id)) state.activeNodeIds.Add(id);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TechTreeProfileStore] Ignoring invalid save for {teamId}: {ex.Message}");
            }
        }

        if (state == null)
        {
            state = CreateDemoProfile(teamId, db);
            Save(state);
        }
        else if (savedKey == legacyKey)
        {
            // Migrate the legacy spelling without invalidating existing player progress.
            Save(state);
        }

        Cache[teamId] = state;
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
        SaveData save = new SaveData
        {
            teamId = state.teamId,
            rpBalance = state.rpBalance,
            unlocked = new List<string>(state.unlockedNodeIds),
            active = new List<string>(state.activeNodeIds)
        };
        PlayerPrefs.SetString(KeyPrefix + state.teamId, JsonUtility.ToJson(save));
        PlayerPrefs.Save();
    }

    public static void SaveAll()
    {
        foreach (TechTreeState state in Cache.Values)
            Save(state);
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
        TechTreeState state = TechTreeRules.CreateDemoState(teamId);
        string[] standard =
        {
            "common-l1-heat-coating",
            "common-l1-lightweight-chassis",
            "common-l1-track-memory",
            "common-l1-expanded-tank"
        };
        string[] ev =
        {
            "cn-ev-l1-heat-pump",
            "cn-ev-l1-pmsm",
            "cn-ev-l1-torque-vector",
            "cn-ev-l1-solid-state"
        };
        string[] common = teamId == TeamId.CN ? ev : standard;
        foreach (string id in common)
            TechTreeRules.UnlockNode(state, id, db);

        List<TechNodeDef> uniques = db.GetUniqueInTier(teamId, TechTreeTier.L1);
        if (uniques.Count > 0)
            TechTreeRules.UnlockNode(state, uniques[0].id, db);

        TechTreeRules.ActivateAllUnlocked(state);
        return state;
    }
}
