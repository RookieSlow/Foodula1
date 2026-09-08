using UnityEngine;

/// <summary>Versioned, per-driver XP persistence for normal races.</summary>
public static class DriverProgressStore
{
    private const string KeyPrefix = "Foodula1.DriverXp.v1.";

    public static int Load(string driverId)
    {
        if (!DriverCatalog.TryGet(driverId, out _)) return 0;
        return Mathf.Max(0, PlayerPrefs.GetInt(BuildKey(driverId), 0));
    }

    public static void Save(string driverId, int xp)
    {
        if (!DriverCatalog.TryGet(driverId, out _)) return;
        PlayerPrefs.SetInt(BuildKey(driverId), Mathf.Max(0, xp));
        PlayerPrefs.Save();
    }

    public static string BuildKey(string driverId) => KeyPrefix + (driverId ?? string.Empty);
}
