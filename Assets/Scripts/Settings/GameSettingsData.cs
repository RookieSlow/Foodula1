using System;
using UnityEngine;

/// <summary>
/// Serializable player preferences. Audio fields drive the current AudioService;
/// AudioMixer routing and an independent UI volume remain future extensions.
/// </summary>
[Serializable]
public sealed class GameSettingsData
{
    public const int CurrentVersion = 2;
    public const int DefaultInRaceConfirmationMask =
        (int)(InRaceConfirmationAction.GearCommit |
              InRaceConfirmationAction.CardAction |
              InRaceConfirmationAction.DriverSkill |
              InRaceConfirmationAction.ResetRace |
              InRaceConfirmationAction.ReturnToMenu |
              InRaceConfirmationAction.PitDecision |
              InRaceConfirmationAction.LaneChange);

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
    /// <summary>
    /// Bit mask for in-race confirmation gates. Gear selection itself is off
    /// by default because the following lock-in button is already gated, but
    /// every action remains individually configurable in Settings.
    /// </summary>
    public int inRaceConfirmationMask = DefaultInRaceConfirmationMask;

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
            tutorialCompleted = tutorialCompleted,
            inRaceConfirmationMask = inRaceConfirmationMask
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
        inRaceConfirmationMask = InRaceConfirmationRules.NormalizeMask(inRaceConfirmationMask);
    }

    public bool IsInRaceConfirmationEnabled(InRaceConfirmationAction action)
    {
        return InRaceConfirmationRules.IsEnabled(inRaceConfirmationMask, action);
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
