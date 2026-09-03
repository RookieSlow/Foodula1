using UnityEngine;

public interface IDisplaySettingsTarget
{
    void Apply(int width, int height, bool fullscreen);
}

public sealed class UnityDisplaySettingsTarget : IDisplaySettingsTarget
{
    public void Apply(int width, int height, bool fullscreen)
    {
        FullScreenMode mode = fullscreen
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;
        Screen.SetResolution(width, height, mode);
    }
}

/// <summary>
/// Shared runtime view of persisted settings and their presentation adapters.
/// </summary>
public static class GameSettingsRuntime
{
    private static GameSettingsRepository repository;
    private static GameSettingsData current;

    public static GameSettingsData Current
    {
        get
        {
            EnsureLoaded();
            return current;
        }
    }

    public static void EnsureLoadedAndApplyDisplay()
    {
        EnsureLoaded();
        AudioService.ApplySettings(current);
        if (Application.isPlaying)
            ApplyDisplay(current, new UnityDisplaySettingsTarget());
    }

    public static void SaveAndApply(GameSettingsData data)
    {
        EnsureRepository();
        current = data != null ? data.Clone() : CreateRuntimeDefault();
        current.Normalize(Screen.width, Screen.height);
        repository.Save(current);
        AudioService.ApplySettings(current);
        if (Application.isPlaying)
            ApplyDisplay(current, new UnityDisplaySettingsTarget());
    }

    public static void MarkTutorialCompleted()
    {
        EnsureLoaded();
        if (current.tutorialCompleted)
            return;
        current.tutorialCompleted = true;
        repository.Save(current);
    }

    public static void ResetTutorialProgress()
    {
        EnsureLoaded();
        current.tutorialCompleted = false;
        repository.Save(current);
    }

    public static float ScaleAnimationDuration(float baseDuration)
    {
        EnsureLoaded();
        return GameSettingsData.ScaleAnimationDuration(current, baseDuration);
    }

    public static void ApplyDisplay(GameSettingsData data, IDisplaySettingsTarget target)
    {
        if (data == null || target == null)
            return;
        GameSettingsData normalized = data.Clone();
        normalized.Normalize();
        target.Apply(
            normalized.resolutionWidth,
            normalized.resolutionHeight,
            normalized.fullscreen);
    }

    private static void EnsureLoaded()
    {
        EnsureRepository();
        if (current == null)
            current = repository.Load();
    }

    private static void EnsureRepository()
    {
        if (repository == null)
        {
            repository = new GameSettingsRepository(
                new PlayerPrefsGameSettingsStore(),
                () => Mathf.Max(640, Screen.width),
                () => Mathf.Max(360, Screen.height));
        }
    }

    private static GameSettingsData CreateRuntimeDefault()
    {
        return GameSettingsData.CreateDefault(Screen.width, Screen.height);
    }
}
