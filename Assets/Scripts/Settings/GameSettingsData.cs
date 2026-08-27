using System;
using UnityEngine;

/// <summary>
/// Serializable player preferences. Audio fields are intentionally data-only
/// until the project gains an AudioMixer/audio service.
/// </summary>
[Serializable]
public sealed class GameSettingsData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public float masterVolume = 1f;
    public float musicVolume = 0.8f;
    public float soundEffectsVolume = 1f;
    public bool fullscreen = true;
    public int resolutionWidth = 1920;
    public int resolutionHeight = 1080;
    public float animationSpeed = 1f;
    public bool reduceMotion;
    public bool tutorialCompleted;

    public static GameSettingsData CreateDefault(int width = 1920, int height = 1080)
    {
        return new GameSettingsData
        {
            resolutionWidth = Mathf.Max(640, width),
            resolutionHeight = Mathf.Max(360, height)
        };
    }

    public GameSettingsData Clone()
    {
        return new GameSettingsData
        {
            version = version,
            masterVolume = masterVolume,
            musicVolume = musicVolume,
            soundEffectsVolume = soundEffectsVolume,
            fullscreen = fullscreen,
            resolutionWidth = resolutionWidth,
            resolutionHeight = resolutionHeight,
            animationSpeed = animationSpeed,
            reduceMotion = reduceMotion,
            tutorialCompleted = tutorialCompleted
        };
    }

    public void Normalize(int fallbackWidth = 1920, int fallbackHeight = 1080)
    {
        version = CurrentVersion;
        masterVolume = Mathf.Clamp01(masterVolume);
        musicVolume = Mathf.Clamp01(musicVolume);
        soundEffectsVolume = Mathf.Clamp01(soundEffectsVolume);
        resolutionWidth = resolutionWidth >= 640
            ? resolutionWidth
            : Mathf.Max(640, fallbackWidth);
        resolutionHeight = resolutionHeight >= 360
            ? resolutionHeight
            : Mathf.Max(360, fallbackHeight);
        animationSpeed = NormalizeAnimationSpeed(animationSpeed);
    }

    public static float NormalizeAnimationSpeed(float value)
    {
        float[] allowed = { 0.5f, 1f, 1.5f, 2f };
        float best = allowed[0];
        float bestDistance = Mathf.Abs(value - best);
        for (int i = 1; i < allowed.Length; i++)
        {
            float distance = Mathf.Abs(value - allowed[i]);
            if (distance < bestDistance)
            {
                best = allowed[i];
                bestDistance = distance;
            }
        }
        return best;
    }

    public static float ScaleAnimationDuration(GameSettingsData data, float baseDuration)
    {
        if (data != null && data.reduceMotion)
            return 0f;
        float speed = data != null ? NormalizeAnimationSpeed(data.animationSpeed) : 1f;
        return Mathf.Max(0f, baseDuration) / speed;
    }
}
