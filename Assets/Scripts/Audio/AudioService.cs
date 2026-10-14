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
    private AudioSource engineSource;
    private AudioSource[] engineSources;
    private MonoBehaviour engineOwner;
    private TeamId engineTeam;
    private TeamVehicleAudioProfile engineProfile;
    private Coroutine engineFade;
    private Coroutine engineCue;
    private int engineVersion;
    private float engineIntensity;
    private float engineBlendIntensity;
    private float engineTargetPitch = 1f;
    private float engineEnvelope;
    private float sfxVolume = 1f;
    private bool engineEnding;
    private int activeMusicSource;
    private Coroutine musicFade;
    private float musicVolume = 1f;

    public static AudioService Instance => instance;
    public MonoBehaviour EngineOwner => engineOwner;
    public TeamId? EngineTeam => engineOwner != null ? engineTeam : (TeamId?)null;
    public float EngineIntensity => engineIntensity;
    public float EngineVolume => engineSources != null
        ? Mathf.Sqrt(engineSources[0].volume * engineSources[0].volume +
                     engineSources[1].volume * engineSources[1].volume +
                     engineSources[2].volume * engineSources[2].volume) : 0f;
    public float EnginePitch => engineSource != null ? engineSource.pitch : 1f;
    public Vector3 EngineLayerVolumes => engineSources != null
        ? new Vector3(engineSources[0].volume, engineSources[1].volume, engineSources[2].volume) : Vector3.zero;

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
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
        musicSources = new[] { CreateSource("MusicA", true), CreateSource("MusicB", true) };
        sfxSource = CreateSource("SFX", false);
        uiSource = CreateSource("UI", false);
        engineSources = new[] { CreateSource("EngineLow", true), CreateSource("EngineMid", true), CreateSource("EngineHigh", true) };
        engineSource = engineSources[0];
        foreach (AudioSource source in engineSources)
            source.volume = 0f;
        ApplySettings(GameSettingsRuntime.Current);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void Start()
    {
        PlaySceneMusic(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        UnsubscribeSceneEvents();
        if (instance != this)
            return;
        StopVehicleEngine();
        instance = null;
    }

    private void UnsubscribeSceneEvents()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnDisable() => StopVehicleEngine();

    private void LateUpdate()
    {
        if (engineProfile == null)
            return;
        if (engineOwner == null || !engineOwner.isActiveAndEnabled)
        {
            StopVehicleEngine();
            return;
        }

        // Slow-motion changes the traversal pace, never the soundtrack clock.
        float delta = Mathf.Max(0f, Time.unscaledDeltaTime);
        float response = TeamVehicleAudioRules.IsFinite(engineProfile.pitchResponse)
            ? Mathf.Max(0f, engineProfile.pitchResponse) : 7f;
        engineBlendIntensity = Mathf.MoveTowards(engineBlendIntensity, engineIntensity, response * delta);
        foreach (AudioSource source in engineSources)
            source.pitch = Mathf.MoveTowards(source.pitch, engineTargetPitch, response * delta);
        if (!engineEnding)
            engineEnvelope = Mathf.MoveTowards(engineEnvelope, 1f, delta / 0.025f);
        ApplyEngineVolume();
    }

    public static void ApplySettings(GameSettingsData settings)
    {
        if (instance == null || settings == null)
            return;

        float sfxVolume = AudioRuntimeRules.GetEffectiveLinearVolume(
            settings.masterVolume, settings.soundEffectsVolume);
        instance.sfxVolume = sfxVolume;
        instance.musicVolume = AudioRuntimeRules.GetEffectiveLinearVolume(
            settings.masterVolume, settings.musicVolume);
        instance.sfxSource.volume = sfxVolume;
        instance.uiSource.volume = sfxVolume;
        instance.ApplyEngineVolume();

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

    public static void BeginVehicleEngine(MonoBehaviour owner, TeamId team, int gear, float completedCells = 0f)
    {
        if (instance == null || owner == null || !owner.isActiveAndEnabled)
            return;
        instance.StartVehicleEngine(owner, team, completedCells);
    }

    public static void UpdateVehicleEngine(MonoBehaviour owner, float completedCells)
    {
        if (instance == null || instance.engineOwner != owner || instance.engineProfile == null)
            return;
        if (owner == null || !owner.isActiveAndEnabled)
        {
            instance.StopVehicleEngine();
            return;
        }
        instance.engineIntensity = TeamVehicleAudioRules.EvaluateIntensity(
            completedCells, instance.engineProfile.cellsToMax);
        instance.engineTargetPitch = TeamVehicleAudioRules.GetEnginePitch(instance.engineProfile, completedCells);
    }

    public static void EndVehicleEngine(MonoBehaviour owner, bool immediate = false)
    {
        if (instance == null || instance.engineOwner != owner || instance.engineProfile == null)
            return;
        if (immediate || !Application.isPlaying || owner == null || !owner.isActiveAndEnabled)
        {
            instance.StopVehicleEngine();
            return;
        }
        if (instance.engineEnding)
            return;
        if (instance.engineCue != null)
        {
            instance.StopCoroutine(instance.engineCue);
            instance.engineCue = null;
        }
        instance.engineEnding = true;
        instance.engineFade = instance.StartCoroutine(instance.FadeOutVehicleEngine(instance.engineVersion));
    }

    /// <summary>Call only after the race rule has successfully committed an actual change.</summary>
    public static void PlayTeamGearShift(TeamId team, int previousGear, int targetGear)
    {
        if (instance == null || !Application.isPlaying || previousGear == targetGear)
            return;
        TeamVehicleAudioProfile profile = TeamVehicleAudioRules.GetProfile(team);
        string path = TeamVehicleAudioRules.GetShiftResourcePath(profile, previousGear, targetGear);
        AudioClip clip = instance.LoadClip(path);
        if (clip == null)
            clip = instance.LoadClip(AudioRuntimeRules.GetSfxPath(AudioEventNames.GearShift));
        if (clip == null || !instance.throttle.ShouldPlay(path ?? AudioEventNames.GearShift, Time.unscaledTime, 0.04f))
            return;
        instance.sfxSource.pitch = 1f;
        instance.sfxSource.PlayOneShot(clip, profile != null ? SafeGain(profile.shiftGain) : 0.65f);
    }

    /// <summary>Short rev for a committed non-animated bonus, never for a visual teleport.</summary>
    public static void PlayTeamAccelerationCue(MonoBehaviour owner, TeamId team, int gear, int cells)
    {
        if (cells <= 0 || instance == null || !Application.isPlaying || owner == null || !owner.isActiveAndEnabled)
            return;
        BeginVehicleEngine(owner, team, gear);
        if (instance.engineOwner == owner && instance.engineProfile != null)
            instance.engineCue = instance.StartCoroutine(instance.PlayAccelerationCue(owner, instance.engineVersion, cells));
    }

    private void StartVehicleEngine(MonoBehaviour owner, TeamId team, float completedCells)
    {
        StopVehicleEngine();
        TeamVehicleAudioProfile profile = TeamVehicleAudioRules.GetProfile(team);
        AudioClip clip = LoadClip(TeamVehicleAudioRules.ValidateResourcePath(profile?.engineResourcePath));
        if (clip == null)
            return;
        AudioClip midClip = LoadClip(TeamVehicleAudioRules.ValidateResourcePath(profile.midEngineResourcePath)) ?? clip;
        AudioClip highClip = LoadClip(TeamVehicleAudioRules.ValidateResourcePath(profile.highEngineResourcePath)) ?? midClip;
        engineOwner = owner;
        engineTeam = team;
        engineProfile = profile;
        engineIntensity = TeamVehicleAudioRules.EvaluateIntensity(completedCells, profile.cellsToMax);
        engineBlendIntensity = engineIntensity;
        engineTargetPitch = TeamVehicleAudioRules.GetEnginePitch(profile, completedCells);
        engineEnvelope = 0f;
        engineSources[0].clip = clip;
        engineSources[1].clip = midClip;
        engineSources[2].clip = highClip;
        // Editor rules/adapter tests may inspect state without starting a real voice.
        foreach (AudioSource source in engineSources)
        {
            source.pitch = engineTargetPitch;
            source.volume = 0f;
            if (Application.isPlaying)
                source.Play();
        }
    }

    private IEnumerator PlayAccelerationCue(MonoBehaviour owner, int version, int cells)
    {
        float duration = Mathf.Clamp(0.14f + cells * 0.025f, 0.16f, 0.4f);
        float elapsed = 0f;
        while (elapsed < duration && version == engineVersion && owner != null && owner.isActiveAndEnabled)
        {
            elapsed += Time.unscaledDeltaTime;
            UpdateVehicleEngine(owner, cells * Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        if (version != engineVersion)
            yield break;
        engineCue = null;
        EndVehicleEngine(owner);
    }

    private IEnumerator FadeOutVehicleEngine(int version)
    {
        float duration = engineProfile != null && TeamVehicleAudioRules.IsFinite(engineProfile.fadeOutSeconds)
            ? Mathf.Clamp(engineProfile.fadeOutSeconds, 0.01f, 0.4f) : 0.09f;
        float from = engineEnvelope;
        float elapsed = 0f;
        while (elapsed < duration && version == engineVersion)
        {
            elapsed += Time.unscaledDeltaTime;
            engineEnvelope = Mathf.Lerp(from, 0f, Mathf.Clamp01(elapsed / duration));
            ApplyEngineVolume();
            yield return null;
        }
        if (version != engineVersion)
            yield break;
        engineFade = null;
        StopVehicleEngine();
    }

    private void StopVehicleEngine()
    {
        engineVersion++;
        if (engineFade != null)
            StopCoroutine(engineFade);
        if (engineCue != null)
            StopCoroutine(engineCue);
        engineFade = null;
        engineCue = null;
        engineOwner = null;
        engineProfile = null;
        engineIntensity = 0f;
        engineBlendIntensity = 0f;
        engineEnvelope = 0f;
        engineEnding = false;
        engineTargetPitch = 1f;
        if (engineSources != null)
        {
            foreach (AudioSource source in engineSources)
            {
                if (source == null) continue;
                source.Stop();
                source.clip = null;
                source.pitch = 1f;
                source.volume = 0f;
            }
        }
    }

    private void ApplyEngineVolume()
    {
        if (engineSources == null)
            return;
        float gain = sfxVolume * (engineProfile != null ? SafeGain(engineProfile.engineGain) : 0f) * engineEnvelope;
        Vector3 weights = TeamVehicleAudioRules.GetEngineLayerWeights(engineBlendIntensity);
        engineSources[0].volume = gain * weights.x;
        engineSources[1].volume = gain * weights.y;
        engineSources[2].volume = gain * weights.z;
    }

    private static float SafeGain(float value) => TeamVehicleAudioRules.IsFinite(value) ? Mathf.Clamp01(value) : 0f;

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
        if (this == null)
        {
            UnsubscribeSceneEvents();
            return;
        }
        StopVehicleEngine();
        PlaySceneMusic(scene.name);
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (this == null)
        {
            UnsubscribeSceneEvents();
            return;
        }
        if (engineOwner == null || engineOwner.gameObject.scene == scene)
            StopVehicleEngine();
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
