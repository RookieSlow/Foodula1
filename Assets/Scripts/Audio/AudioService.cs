using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent, scene-independent audio entry point. Clips are resolved lazily from
/// Resources/Audio and missing optional assets are intentionally silent.
/// </summary>
public sealed class AudioService : MonoBehaviour
{
    private const float MusicFadeDuration = 0.45f;
    private static AudioService instance;

    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
    private readonly AudioEventThrottle throttle = new AudioEventThrottle();
    private AudioSource[] musicSources;
    private AudioSource sfxSource;
    private AudioSource uiSource;
    private int activeMusicSource;
    private Coroutine musicFade;
    private float musicVolume = 1f;

    public static AudioService Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;
        new GameObject("AudioService").AddComponent<AudioService>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        musicSources = new[] { CreateSource("MusicA", true), CreateSource("MusicB", true) };
        sfxSource = CreateSource("SFX", false);
        uiSource = CreateSource("UI", false);
        ApplySettings(GameSettingsRuntime.Current);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        PlaySceneMusic(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    public static void ApplySettings(GameSettingsData settings)
    {
        if (instance == null || settings == null)
            return;

        float sfxVolume = AudioRuntimeRules.GetEffectiveLinearVolume(
            settings.masterVolume, settings.soundEffectsVolume);
        instance.musicVolume = AudioRuntimeRules.GetEffectiveLinearVolume(
            settings.masterVolume, settings.musicVolume);
        instance.sfxSource.volume = sfxVolume;
        instance.uiSource.volume = sfxVolume;

        if (instance.musicFade == null && instance.musicSources != null)
        {
            instance.musicSources[instance.activeMusicSource].volume = instance.musicVolume;
            instance.musicSources[1 - instance.activeMusicSource].volume = 0f;
        }
    }

    public static void PlaySfx(string eventName)
    {
        instance?.PlayOneShot(eventName, instance.sfxSource);
    }

    public static void PlayUi(string eventName)
    {
        instance?.PlayOneShot(eventName, instance.uiSource);
    }

    private AudioSource CreateSource(string sourceName, bool loop)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.pitch = 1f;
        return source;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlaySceneMusic(scene.name);
    }

    private void PlaySceneMusic(string sceneName)
    {
        string musicName = AudioRuntimeRules.GetMusicNameForScene(sceneName);
        if (musicName == null)
            return;

        AudioClip clip = LoadClip(AudioRuntimeRules.GetMusicPath(musicName));
        AudioSource current = musicSources[activeMusicSource];
        if (current.clip == clip && current.isPlaying)
            return;
        AudioSource incoming = musicSources[1 - activeMusicSource];
        if (incoming.clip == clip && incoming.isPlaying)
            return;

        if (musicFade != null)
            StopCoroutine(musicFade);
        musicFade = StartCoroutine(CrossFadeMusic(clip));
    }

    private IEnumerator CrossFadeMusic(AudioClip nextClip)
    {
        AudioSource from = musicSources[activeMusicSource];
        AudioSource to = musicSources[1 - activeMusicSource];
        to.Stop();
        to.clip = nextClip;
        to.volume = 0f;
        to.pitch = 1f;
        if (nextClip != null)
            to.Play();

        float startFromVolume = from.volume;
        float elapsed = 0f;
        while (elapsed < MusicFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / MusicFadeDuration);
            from.volume = Mathf.Lerp(startFromVolume, 0f, progress);
            to.volume = Mathf.Lerp(0f, musicVolume, progress);
            yield return null;
        }

        from.Stop();
        from.clip = null;
        from.volume = 0f;
        to.volume = musicVolume;
        activeMusicSource = 1 - activeMusicSource;
        musicFade = null;
    }

    private void PlayOneShot(string eventName, AudioSource source)
    {
        if (source == null || !throttle.ShouldPlay(
                eventName, Time.unscaledTime, AudioRuntimeRules.GetCooldown(eventName)))
        {
            return;
        }

        AudioClip clip = LoadClip(AudioRuntimeRules.GetSfxPath(eventName));
        if (clip != null)
        {
            source.pitch = 1f;
            source.PlayOneShot(clip);
        }
    }

    private AudioClip LoadClip(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return null;
        if (!clipCache.TryGetValue(resourcePath, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>(resourcePath);
            clipCache[resourcePath] = clip;
        }
        return clip;
    }
}
