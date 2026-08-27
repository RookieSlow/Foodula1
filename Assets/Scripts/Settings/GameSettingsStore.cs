using System;
using UnityEngine;

public interface IGameSettingsKeyValueStore
{
    bool HasKey(string key);
    string GetString(string key);
    void SetString(string key, string value);
    void Save();
}

public sealed class PlayerPrefsGameSettingsStore : IGameSettingsKeyValueStore
{
    public bool HasKey(string key) => PlayerPrefs.HasKey(key);
    public string GetString(string key) => PlayerPrefs.GetString(key);
    public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
    public void Save() => PlayerPrefs.Save();
}

/// <summary>Persistence adapter kept separate from runtime application.</summary>
public sealed class GameSettingsRepository
{
    public const string SettingsKey = "Foodula1.Settings.V1";

    private readonly IGameSettingsKeyValueStore store;
    private readonly Func<int> fallbackWidth;
    private readonly Func<int> fallbackHeight;

    public GameSettingsRepository(
        IGameSettingsKeyValueStore store,
        Func<int> fallbackWidth = null,
        Func<int> fallbackHeight = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.fallbackWidth = fallbackWidth ?? (() => 1920);
        this.fallbackHeight = fallbackHeight ?? (() => 1080);
    }

    public GameSettingsData Load()
    {
        int width = fallbackWidth();
        int height = fallbackHeight();
        if (!store.HasKey(SettingsKey))
            return GameSettingsData.CreateDefault(width, height);

        try
        {
            GameSettingsData data = JsonUtility.FromJson<GameSettingsData>(
                store.GetString(SettingsKey));
            if (data == null || data.version != GameSettingsData.CurrentVersion)
                return GameSettingsData.CreateDefault(width, height);
            data.Normalize(width, height);
            return data;
        }
        catch (Exception)
        {
            return GameSettingsData.CreateDefault(width, height);
        }
    }

    public void Save(GameSettingsData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        GameSettingsData normalized = data.Clone();
        normalized.Normalize(fallbackWidth(), fallbackHeight());
        store.SetString(SettingsKey, JsonUtility.ToJson(normalized));
        store.Save();
    }
}
