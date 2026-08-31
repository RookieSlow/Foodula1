using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerPrefsCareerKeyValueStore : ICareerKeyValueStore
{
    public bool HasKey(string key) => PlayerPrefs.HasKey(key);
    public string GetString(string key) => PlayerPrefs.GetString(key);

    public bool TrySetAndSave(string key, string value)
    {
        bool hadPrevious = PlayerPrefs.HasKey(key);
        string previous = hadPrevious ? PlayerPrefs.GetString(key) : string.Empty;
        try
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
            return true;
        }
        catch (Exception)
        {
            if (hadPrevious) PlayerPrefs.SetString(key, previous);
            else PlayerPrefs.DeleteKey(key);
            return false;
        }
    }

    public bool TryDeleteAndSave(string key)
    {
        bool hadPrevious = PlayerPrefs.HasKey(key);
        string previous = hadPrevious ? PlayerPrefs.GetString(key) : string.Empty;
        try
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return true;
        }
        catch (Exception)
        {
            if (hadPrevious) PlayerPrefs.SetString(key, previous);
            return false;
        }
    }
}

public sealed class JsonUtilityCareerSerializer : ICareerSerializer
{
    public string Serialize(CareerSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        return JsonUtility.ToJson(data);
    }

    public CareerSaveData Deserialize(string serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
            throw new FormatException("Career save is empty.");
        return JsonUtility.FromJson<CareerSaveData>(serialized);
    }
}

/// <summary>Boundary between career-owned tech snapshots and race TechTreeState.</summary>
public static class CareerTechSnapshotMapper
{
    public static bool TryCapture(TechTreeState state, out CareerTechSnapshot snapshot)
    {
        snapshot = null;
        return state != null && CareerTechSnapshot.TryCreate(
            state.teamId,
            state.rpBalance,
            state.unlockedNodeIds,
            state.activeNodeIds,
            state.sunNeverSetsTarget,
            out snapshot);
    }

    public static bool TryCreateRuntimeState(
        CareerTechSnapshot snapshot,
        TechTreeDatabase database,
        out TechTreeState state)
    {
        state = null;
        if (!IsValidForDatabase(snapshot, database))
            return false;

        var restored = new TechTreeState(snapshot.TeamId, snapshot.RpBalance);
        for (int i = 0; i < snapshot.UnlockedNodeIds.Count; i++)
            restored.unlockedNodeIds.Add(snapshot.UnlockedNodeIds[i]);
        for (int i = 0; i < snapshot.ActiveNodeIds.Count; i++)
            restored.activeNodeIds.Add(snapshot.ActiveNodeIds[i]);
        restored.sunNeverSetsTarget = snapshot.SunNeverSetsTarget;
        state = restored;
        return true;
    }

    public static bool IsSeasonValidForDatabase(
        CareerSeasonState state,
        TechTreeDatabase database)
    {
        return state != null &&
               IsValidForDatabase(state.InitialTechSnapshot, database) &&
               IsValidForDatabase(state.ActiveTechSnapshot, database) &&
               (!state.SummerBreakUsed ||
                IsValidForDatabase(state.SummerBreakTechSnapshot, database));
    }

    private static bool IsValidForDatabase(
        CareerTechSnapshot snapshot,
        TechTreeDatabase database)
    {
        if (snapshot == null || database == null)
            return false;

        var unlocked = new HashSet<string>(snapshot.UnlockedNodeIds, StringComparer.Ordinal);
        for (int i = 0; i < snapshot.UnlockedNodeIds.Count; i++)
        {
            TechNodeDef node = database.Get(snapshot.UnlockedNodeIds[i]);
            if (node == null || (node.teamId.HasValue && node.teamId.Value != snapshot.TeamId))
                return false;
        }

        for (int i = 0; i < snapshot.ActiveNodeIds.Count; i++)
        {
            if (!unlocked.Contains(snapshot.ActiveNodeIds[i]))
                return false;
        }

        return true;
    }
}

public static class CareerRuntimeRepository
{
    public static CareerRepository CreateDefault()
    {
        TechTreeDatabase database = TechTreeDatabaseFactory.CreateDefault();
        return new CareerRepository(
            new PlayerPrefsCareerKeyValueStore(),
            new JsonUtilityCareerSerializer(),
            state => CareerTechSnapshotMapper.IsSeasonValidForDatabase(state, database));
    }
}
