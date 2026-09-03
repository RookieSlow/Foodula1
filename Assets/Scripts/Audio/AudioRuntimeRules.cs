using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Pure naming, volume and throttling rules for the runtime audio adapter.</summary>
public static class AudioRuntimeRules
{
    public const string MusicRoot = "Audio/Music/";
    public const string SfxRoot = "Audio/SFX/";
    public const float SilenceDecibels = -80f;

    public static float GetEffectiveLinearVolume(float master, float channel)
    {
        return Mathf.Clamp01(master) * Mathf.Clamp01(channel);
    }

    public static float LinearToDecibels(float linear)
    {
        float clamped = Mathf.Clamp01(linear);
        return clamped <= 0.0001f ? SilenceDecibels : Mathf.Log10(clamped) * 20f;
    }

    public static string GetMusicPath(string musicName)
    {
        return BuildResourcePath(MusicRoot, musicName);
    }

    public static string GetSfxPath(string eventName)
    {
        return BuildResourcePath(SfxRoot, eventName);
    }

    public static string GetMusicNameForScene(string sceneName)
    {
        if (string.Equals(sceneName, "Race", StringComparison.OrdinalIgnoreCase))
            return AudioEventNames.RaceMusic;
        if (string.Equals(sceneName, "MainMenu", StringComparison.OrdinalIgnoreCase))
            return AudioEventNames.MenuMusic;
        return null;
    }

    public static float GetCooldown(string eventName)
    {
        switch (eventName)
        {
            case AudioEventNames.CarHop:
                return 0.09f;
            case AudioEventNames.CardDraw:
            case AudioEventNames.CardDiscard:
            case AudioEventNames.CardPlay:
                return 0.08f;
            case AudioEventNames.HeatPay:
            case AudioEventNames.HeatCool:
                return 0.12f;
            default:
                return 0.04f;
        }
    }

    private static string BuildResourcePath(string root, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        string trimmed = name.Trim();
        if (trimmed.Contains("/") || trimmed.Contains("\\") || trimmed.Contains(".."))
            return null;
        return root + trimmed;
    }
}

/// <summary>Clock-independent gate used to keep repeated board events from becoming a noise wall.</summary>
public sealed class AudioEventThrottle
{
    private readonly Dictionary<string, float> lastPlayedAt = new Dictionary<string, float>();

    public bool ShouldPlay(string eventName, float now, float cooldown)
    {
        if (string.IsNullOrEmpty(eventName))
            return false;

        float safeNow = Mathf.Max(0f, now);
        float safeCooldown = Mathf.Max(0f, cooldown);
        if (lastPlayedAt.TryGetValue(eventName, out float previous) &&
            safeNow >= previous && safeNow - previous < safeCooldown)
        {
            return false;
        }

        lastPlayedAt[eventName] = safeNow;
        return true;
    }

    public void Clear()
    {
        lastPlayedAt.Clear();
    }
}
