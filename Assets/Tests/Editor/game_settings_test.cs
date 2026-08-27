using System.Collections.Generic;
using NUnit.Framework;

public class GameSettingsTests
{
    [Test]
    public void DefaultsCoverRequiredPersistentSettingsHonestly()
    {
        GameSettingsData data = GameSettingsData.CreateDefault(2560, 1440);

        Assert.That(data.masterVolume, Is.EqualTo(1f));
        Assert.That(data.musicVolume, Is.EqualTo(0.8f));
        Assert.That(data.soundEffectsVolume, Is.EqualTo(1f));
        Assert.That(data.fullscreen, Is.True);
        Assert.That(data.resolutionWidth, Is.EqualTo(2560));
        Assert.That(data.resolutionHeight, Is.EqualTo(1440));
        Assert.That(data.animationSpeed, Is.EqualTo(1f));
        Assert.That(data.reduceMotion, Is.False);
        Assert.That(data.tutorialCompleted, Is.False);
    }

    [Test]
    public void NormalizeClampsVolumesResolutionAndAnimationSpeed()
    {
        var data = new GameSettingsData
        {
            masterVolume = -2f,
            musicVolume = 2f,
            soundEffectsVolume = 0.45f,
            resolutionWidth = 0,
            resolutionHeight = 0,
            animationSpeed = 1.7f
        };

        data.Normalize(1600, 900);

        Assert.That(data.masterVolume, Is.EqualTo(0f));
        Assert.That(data.musicVolume, Is.EqualTo(1f));
        Assert.That(data.soundEffectsVolume, Is.EqualTo(0.45f));
        Assert.That(data.resolutionWidth, Is.EqualTo(1600));
        Assert.That(data.resolutionHeight, Is.EqualTo(900));
        Assert.That(data.animationSpeed, Is.EqualTo(1.5f));
    }

    [Test]
    public void RepositoryRoundTripPersistsEverySetting()
    {
        var store = new MemoryStore();
        var repository = new GameSettingsRepository(store, () => 1920, () => 1080);
        var saved = new GameSettingsData
        {
            masterVolume = 0.6f,
            musicVolume = 0.4f,
            soundEffectsVolume = 0.2f,
            fullscreen = false,
            resolutionWidth = 1600,
            resolutionHeight = 900,
            animationSpeed = 2f,
            reduceMotion = true,
            tutorialCompleted = true
        };

        repository.Save(saved);
        GameSettingsData loaded = repository.Load();

        Assert.That(store.SaveCount, Is.EqualTo(1));
        Assert.That(loaded.masterVolume, Is.EqualTo(0.6f));
        Assert.That(loaded.musicVolume, Is.EqualTo(0.4f));
        Assert.That(loaded.soundEffectsVolume, Is.EqualTo(0.2f));
        Assert.That(loaded.fullscreen, Is.False);
        Assert.That(loaded.resolutionWidth, Is.EqualTo(1600));
        Assert.That(loaded.resolutionHeight, Is.EqualTo(900));
        Assert.That(loaded.animationSpeed, Is.EqualTo(2f));
        Assert.That(loaded.reduceMotion, Is.True);
        Assert.That(loaded.tutorialCompleted, Is.True);
    }

    [Test]
    public void MissingCorruptOrUnknownVersionSettingsFallBackWithoutThrowing()
    {
        var store = new MemoryStore();
        var repository = new GameSettingsRepository(store, () => 1366, () => 768);

        GameSettingsData missing = repository.Load();
        store.SetString(GameSettingsRepository.SettingsKey, "{not-json");
        GameSettingsData corrupt = repository.Load();
        store.SetString(GameSettingsRepository.SettingsKey, "{\"version\":99,\"resolutionWidth\":800}");
        GameSettingsData unknownVersion = repository.Load();

        Assert.That(missing.resolutionWidth, Is.EqualTo(1366));
        Assert.That(missing.resolutionHeight, Is.EqualTo(768));
        Assert.That(corrupt.resolutionWidth, Is.EqualTo(1366));
        Assert.That(corrupt.resolutionHeight, Is.EqualTo(768));
        Assert.That(unknownVersion.resolutionWidth, Is.EqualTo(1366));
        Assert.That(unknownVersion.resolutionHeight, Is.EqualTo(768));
    }

    [Test]
    public void DisplayApplicationUsesPersistedModeAndResolution()
    {
        var data = new GameSettingsData
        {
            fullscreen = false,
            resolutionWidth = 1280,
            resolutionHeight = 720
        };
        var target = new RecordingDisplayTarget();

        GameSettingsRuntime.ApplyDisplay(data, target);

        Assert.That(target.Width, Is.EqualTo(1280));
        Assert.That(target.Height, Is.EqualTo(720));
        Assert.That(target.Fullscreen, Is.False);
        Assert.That(target.ApplyCount, Is.EqualTo(1));
    }

    [Test]
    public void AnimationSpeedAndReducedMotionScaleOnlyPresentationTime()
    {
        var fast = new GameSettingsData { animationSpeed = 2f };
        var slow = new GameSettingsData { animationSpeed = 0.5f };
        var reduced = new GameSettingsData { animationSpeed = 1f, reduceMotion = true };

        Assert.That(GameSettingsData.ScaleAnimationDuration(fast, 0.2f), Is.EqualTo(0.1f));
        Assert.That(GameSettingsData.ScaleAnimationDuration(slow, 0.2f), Is.EqualTo(0.4f));
        Assert.That(GameSettingsData.ScaleAnimationDuration(reduced, 0.2f), Is.EqualTo(0f));
        Assert.That(GameSettingsData.ScaleAnimationDuration(fast, -1f), Is.EqualTo(0f));
    }

    private sealed class MemoryStore : IGameSettingsKeyValueStore
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();
        public int SaveCount { get; private set; }
        public bool HasKey(string key) => values.ContainsKey(key);
        public string GetString(string key) => values[key];
        public void SetString(string key, string value) => values[key] = value;
        public void Save() => SaveCount++;
    }

    private sealed class RecordingDisplayTarget : IDisplaySettingsTarget
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public bool Fullscreen { get; private set; }
        public int ApplyCount { get; private set; }

        public void Apply(int width, int height, bool fullscreen)
        {
            Width = width;
            Height = height;
            Fullscreen = fullscreen;
            ApplyCount++;
        }
    }
}
