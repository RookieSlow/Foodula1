using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Native source playback of the twelve CC0 cues; does not claim a full-match listening pass.</summary>
public sealed class TeamShiftCc0RuntimeTests
{
    [UnityTest]
    public IEnumerator AllTwelveConfiguredCc0CuesContainPcmAndPlayOnAnOwnedAudioSource()
    {
        yield return new EnterPlayMode();
        Scene scene = SceneManager.CreateScene("Owned CC0 shift playback");
        GameObject host = null;
        GameSettingsData settings = GameSettingsRuntime.Current;
        var paths = new HashSet<string>();
        try
        {
            host = new GameObject("CC0 shift playback source", typeof(AudioSource));
            SceneManager.MoveGameObjectToScene(host, scene);
            AudioSource voice = host.GetComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.loop = false;
            voice.volume = .05f;
            // Exercise the owned source regardless of the user's global listener preference.
            // No listener, service or persisted settings are mutated by the test.
            voice.ignoreListenerPause = true;
            voice.ignoreListenerVolume = true;
            foreach (TeamId team in new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP })
            {
                TeamVehicleAudioProfile profile = TeamVehicleAudioRules.GetProfile(team);
                foreach (string path in new[] { profile.upShiftResourcePath, profile.downShiftResourcePath })
                {
                    Assert.That(path, Does.StartWith("Audio/SFX/team_shift_cc0_"));
                    Assert.That(paths.Add(path), Is.True, "Each direction must use its own resource.");
                    AudioClip clip = Resources.Load<AudioClip>(path);
                    Assert.That(clip, Is.Not.Null, path);
                    Assert.That(clip.frequency, Is.EqualTo(48000));
                    Assert.That(clip.channels, Is.EqualTo(1));
                    Assert.That(clip.length, Is.InRange(.159f, .241f));
                    var pcm = new float[clip.samples * clip.channels];
                    Assert.That(clip.GetData(pcm, 0), Is.True, path);
                    double energy = 0;
                    foreach (float sample in pcm) energy += sample * sample;
                    Assert.That(energy / pcm.Length, Is.GreaterThan(.0001), "Cue must contain audible PCM: " + path);
                    Assert.That(System.Math.Abs(pcm[0]), Is.LessThan(.0001f));
                    Assert.That(System.Math.Abs(pcm[pcm.Length - 1]), Is.LessThan(.0001f));
                    voice.clip = clip;
                    voice.Play();
                    yield return new WaitForSecondsRealtime(.03f);
                    Assert.That(voice.isPlaying, Is.True, "Native AudioSource must be playing: " + path);
                    Assert.That(voice.mute, Is.False);
                    Assert.That(voice.volume, Is.GreaterThan(0f));
                    voice.Stop();
                    Assert.That(voice.isPlaying, Is.False);
                    voice.clip = null;
                }
            }
            Assert.That(paths.Count, Is.EqualTo(12));
            Assert.That(GameSettingsRuntime.Current, Is.SameAs(settings), "Player settings must remain untouched.");
        }
        finally
        {
            if (host != null)
            {
                AudioSource voice = host.GetComponent<AudioSource>();
                if (voice != null) { voice.Stop(); voice.clip = null; }
                Object.DestroyImmediate(host);
            }
        }
        yield return SceneManager.UnloadSceneAsync(scene);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditMode()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
}
