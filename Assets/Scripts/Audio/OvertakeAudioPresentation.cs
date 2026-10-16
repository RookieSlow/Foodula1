using System.Collections;
using UnityEngine;

/// <summary>A single recorded pass-by voice, scoped to its presentation owner.</summary>
public sealed class OvertakeAudioPresentation : MonoBehaviour
{
    private const float RecordingGain = .85f;
    private AudioSource voice;
    private MonoBehaviour owner;
    private Coroutine playback;
    private int version;

    internal int Begin(MonoBehaviour presentationOwner, AudioClip clip, float volume)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || presentationOwner == null ||
            !presentationOwner.isActiveAndEnabled || clip == null || owner != null)
            return 0;
        Stop();
        if (voice == null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 0f;
            voice.ignoreListenerPause = true;
        }
        owner = presentationOwner;
        version++;
        if (version == 0) version++;
        voice.clip = clip;
        voice.pitch = 1f;
        SetVolume(volume);
        voice.Play();
        playback = StartCoroutine(WatchPlayback(version));
        return version;
    }

    internal void SetVolume(float volume)
    {
        if (voice != null)
            voice.volume = float.IsNaN(volume) || float.IsInfinity(volume) ? 0f : Mathf.Clamp01(volume) * RecordingGain;
    }

    internal void End(MonoBehaviour presentationOwner, int token)
    {
        if (token != 0 && token == version && ReferenceEquals(owner, presentationOwner))
            Stop();
    }

    internal void Stop()
    {
        if (playback != null) StopCoroutine(playback);
        playback = null;
        owner = null;
        if (voice == null) return;
        voice.Stop();
        voice.clip = null;
        voice.pitch = 1f;
        voice.volume = 0f;
    }

    private IEnumerator WatchPlayback(int token)
    {
        float elapsed = 0f;
        while (token == version && owner != null && owner.isActiveAndEnabled &&
               voice != null && voice.isPlaying && elapsed < 0.78f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (token == version)
        {
            playback = null;
            Stop();
        }
    }

    private void OnDisable() => Stop();
    private void OnDestroy() => Stop();
}
