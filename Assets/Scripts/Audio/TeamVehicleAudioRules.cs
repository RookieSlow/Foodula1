using System;
using UnityEngine;

/// <summary>Authored audio identity; it reads gear state and never changes drivetrain rules.</summary>
[Serializable]
public sealed class TeamVehicleAudioProfile
{
    public string team;
    public string engineResourcePath;
    public string midEngineResourcePath;
    public string highEngineResourcePath;
    public string upShiftResourcePath;
    public string downShiftResourcePath;
    public float lowPitch = 0.92f;
    public float highPitch = 1.44f;
    public float cellsToMax = 10f;
    public float engineGain = 0.75f;
    public float shiftGain = 0.65f;
    public float pitchResponse = 7f;
    public float fadeOutSeconds = 0.09f;
}

[Serializable]
public sealed class TeamVehicleAudioConfiguration
{
    public int schemaVersion = 1;
    public TeamVehicleAudioProfile[] profiles;
}

/// <summary>Pure rev/identity helpers plus the one-time Resources configuration boundary.</summary>
public static class TeamVehicleAudioRules
{
    public const string ConfigurationPath = "Audio/team_vehicle_profiles";
    private static TeamVehicleAudioConfiguration configuration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetConfiguration() => configuration = null;

    /// <summary>Absolute traversed cells, so a short move cannot reach a long move's peak.</summary>
    public static float EvaluateIntensity(float completedCells, float cellsToMax = 12f)
    {
        if (float.IsNaN(completedCells) || completedCells <= 0f)
            return 0f;
        float threshold = IsFinite(cellsToMax) && cellsToMax > 0f ? cellsToMax : 12f;
        return Mathf.Clamp01(completedCells / threshold);
    }

    public static float GetEnginePitch(TeamVehicleAudioProfile profile, float completedCells)
    {
        if (profile == null)
            return 1f;
        // Keep the recorded layers, with an audible continuous rev rise across normal travel.
        float low = IsFinite(profile.lowPitch) ? Mathf.Clamp(profile.lowPitch, 0.75f, 1.8f) : 0.92f;
        float high = IsFinite(profile.highPitch) ? Mathf.Clamp(profile.highPitch, low, 1.8f) : Mathf.Max(low, 1.44f);
        return Mathf.Lerp(low, high, EvaluateIntensity(completedCells, profile.cellsToMax));
    }

    /// <summary>Equal-power low/mid/high blend; adjacent recordings overlap without a gain dip.</summary>
    public static Vector3 GetEngineLayerWeights(float intensity)
    {
        float value = IsFinite(intensity) ? Mathf.Clamp01(intensity) : 0f;
        if (value <= 0f)
            return new Vector3(1f, 0f, 0f);
        if (value >= 1f)
            return new Vector3(0f, 0f, 1f);
        float angle = (value < 0.5f ? value * 2f : (value - 0.5f) * 2f) * Mathf.PI * 0.5f;
        return value < 0.5f
            ? new Vector3(Mathf.Max(0f, Mathf.Cos(angle)), Mathf.Sin(angle), 0f)
            : new Vector3(0f, Mathf.Max(0f, Mathf.Cos(angle)), Mathf.Sin(angle));
    }

    public static string GetShiftResourcePath(TeamVehicleAudioProfile profile, int previousGear, int targetGear)
    {
        if (profile == null || previousGear == targetGear)
            return null;
        return ValidateResourcePath(targetGear > previousGear
            ? profile.upShiftResourcePath : profile.downShiftResourcePath);
    }

    public static string ValidateResourcePath(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;
        string trimmed = resourcePath.Trim();
        if (!trimmed.StartsWith("Audio/", StringComparison.Ordinal) ||
            trimmed.Contains("..") || trimmed.Contains("\\") || trimmed.EndsWith("/"))
            return null;
        return trimmed;
    }

    public static TeamVehicleAudioProfile GetProfile(TeamId team)
    {
        if (configuration == null)
            configuration = LoadConfiguration();
        if (configuration.profiles != null)
        {
            foreach (TeamVehicleAudioProfile profile in configuration.profiles)
            {
                if (profile != null && Enum.TryParse(profile.team, true, out TeamId candidate) && candidate == team)
                    return profile;
            }
        }
        return null;
    }

    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static TeamVehicleAudioConfiguration LoadConfiguration()
    {
        TextAsset data = Resources.Load<TextAsset>(ConfigurationPath);
        if (data != null)
        {
            try
            {
                TeamVehicleAudioConfiguration loaded = JsonUtility.FromJson<TeamVehicleAudioConfiguration>(data.text);
                if (loaded != null && (loaded.schemaVersion == 1 || loaded.schemaVersion == 2) && loaded.profiles != null)
                    return loaded;
            }
            catch (ArgumentException) { /* Optional malformed audio must not interrupt a race. */ }
        }
        return new TeamVehicleAudioConfiguration { profiles = Array.Empty<TeamVehicleAudioProfile>() };
    }
}
