using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Native player-loop verification of real audio voices without race or save mutations.</summary>
public class TeamVehicleRuntimeAudioTests
{
    [UnityTest]
    public IEnumerator EngineVoicePlaysRampsUnderSlowMotionRespectsMuteAndSwitchesTeam()
    {
        yield return new EnterPlayMode();
        AudioService service = AudioService.Instance;
        Assert.That(service, Is.Not.Null);
        float previousScale = Time.timeScale;
        GameSettingsData previousSettings = GameSettingsRuntime.Current;
        AudioSource engine = (AudioSource)typeof(AudioService).GetField("engineSource",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
        AudioSource[] layers = (AudioSource[])typeof(AudioService).GetField("engineSources",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
        try
        {
            AudioService.ApplySettings(new GameSettingsData { masterVolume = 0f, soundEffectsVolume = 1f });
            AudioService.BeginVehicleEngine(service, TeamId.US, 4, 2f);
            yield return new WaitForSecondsRealtime(0.07f);
            Assert.That(engine.isPlaying, Is.True);
            Assert.That(engine.loop, Is.True);
            Assert.That(layers.Length, Is.EqualTo(3));
            foreach (AudioSource layer in layers)
            {
                Assert.That(layer.isPlaying, Is.True);
                Assert.That(layer.loop, Is.True);
                Assert.That(layer.clip, Is.Not.Null);
                Assert.That(layer.volume, Is.Zero);
            }
            float shortPitch = engine.pitch;
            Time.timeScale = 0.25f;
            AudioService.UpdateVehicleEngine(service, 6f);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(engine.pitch / shortPitch, Is.GreaterThanOrEqualTo(1.15f),
                "Normal travel must audibly rise under the native player loop, even during slow motion.");
            AudioService.UpdateVehicleEngine(service, 16f);
            yield return new WaitForSecondsRealtime(0.18f);
            Assert.That(engine.pitch, Is.GreaterThan(shortPitch));
            Assert.That(engine.pitch, Is.EqualTo(TeamVehicleAudioRules.GetProfile(TeamId.US).highPitch).Within(0.05f));
            Assert.That(engine.volume, Is.Zero, "Mute must apply to a playing voice.");
            Assert.That(service.EngineLayerVolumes, Is.EqualTo(Vector3.zero));
            AudioSource[] music = (AudioSource[])typeof(AudioService).GetField("musicSources",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            Assert.That(music[0].pitch, Is.EqualTo(1f));
            Assert.That(music[1].pitch, Is.EqualTo(1f));
            AudioService.EndVehicleEngine(service);
            AudioService.BeginVehicleEngine(service, TeamId.CN, ChinaGearShiftRules.GoGear, 4f);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(service.EngineTeam, Is.EqualTo(TeamId.CN));
            Assert.That(engine.isPlaying, Is.True, "The old fade cannot stop the replacement electric voice.");
            Assert.That(engine.clip.name, Is.EqualTo("team_engine_cn"));
            Assert.That(layers[1].clip.name, Is.EqualTo("team_engine_cn_mid"));
            Assert.That(layers[2].clip.name, Is.EqualTo("team_engine_cn_high"));
            foreach (AudioSource layer in layers)
                Assert.That(layer.isPlaying, Is.True, "Stale fades must leave every replacement layer running.");
            AudioService.EndVehicleEngine(service, true);
            Assert.That(engine.isPlaying, Is.False);
            Assert.That(engine.clip, Is.Null);
            Assert.That(service.EngineOwner, Is.Null);
            foreach (AudioSource layer in layers)
            {
                Assert.That(layer.isPlaying, Is.False);
                Assert.That(layer.clip, Is.Null);
            }
        }
        finally
        {
            Time.timeScale = previousScale;
            AudioService.EndVehicleEngine(service, true);
            AudioService.ApplySettings(previousSettings);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RecordedLayersLoadCrossfadeFadeOutAndStopOnInactiveOwnerAndSceneExit()
    {
        yield return new EnterPlayMode();
        AudioService service = AudioService.Instance;
        Assert.That(service, Is.Not.Null);
        GameSettingsData previousSettings = GameSettingsRuntime.Current;
        Scene ownerScene = SceneManager.CreateScene("RecordedVehicleAudioOwnerScope");
        GameObject ownerObject = new GameObject("RecordedVehicleAudioOwner", typeof(RectTransform));
        var owner = ownerObject.AddComponent<UnityEngine.UI.LayoutElement>();
        SceneManager.MoveGameObjectToScene(ownerObject, ownerScene);
        AudioSource[] layers = (AudioSource[])typeof(AudioService).GetField("engineSources",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
        try
        {
            AudioService.ApplySettings(new GameSettingsData { masterVolume = .5f, soundEffectsVolume = .4f });
            TeamVehicleAudioProfile profile = TeamVehicleAudioRules.GetProfile(TeamId.IT);
            AudioService.BeginVehicleEngine(owner, TeamId.IT, 3);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(layers[0].clip.name, Is.EqualTo("team_engine_it"));
            Assert.That(layers[1].clip.name, Is.EqualTo("team_engine_it_mid"));
            Assert.That(layers[2].clip.name, Is.EqualTo("team_engine_it_high"));
            foreach (AudioSource layer in layers)
                Assert.That(layer.isPlaying, Is.True);
            Assert.That(service.EngineLayerVolumes.x, Is.GreaterThan(0f));
            Assert.That(service.EngineLayerVolumes.z, Is.Zero);

            AudioService.UpdateVehicleEngine(owner, profile.cellsToMax * .5f);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(service.EngineLayerVolumes.y, Is.EqualTo(.2f * profile.engineGain).Within(.001f));
            Assert.That(service.EngineLayerVolumes.x, Is.Zero.Within(.001f));
            AudioService.UpdateVehicleEngine(owner, profile.cellsToMax);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(service.EngineLayerVolumes.z, Is.EqualTo(.2f * profile.engineGain).Within(.001f));

            AudioService.ApplySettings(new GameSettingsData { masterVolume = 0f, soundEffectsVolume = 1f });
            foreach (AudioSource layer in layers)
                Assert.That(layer.volume, Is.Zero);
            AudioService.EndVehicleEngine(owner);
            yield return new WaitForSecondsRealtime(.15f);
            foreach (AudioSource layer in layers)
            {
                Assert.That(layer.isPlaying, Is.False);
                Assert.That(layer.clip, Is.Null);
            }
            AudioService.BeginVehicleEngine(owner, TeamId.CN, ChinaGearShiftRules.GoGear, 4f);
            owner.enabled = false;
            yield return null;
            yield return null;
            Assert.That(service.EngineOwner, Is.Null);
            foreach (AudioSource layer in layers)
                Assert.That(layer.clip, Is.Null);

            owner.enabled = true;
            AudioService.BeginVehicleEngine(owner, TeamId.UK, 2, 6f);
            yield return new WaitForSecondsRealtime(.04f);
            yield return SceneManager.UnloadSceneAsync(ownerScene);
            Assert.That(service.EngineOwner, Is.Null);
            Assert.That(service.EngineLayerVolumes, Is.EqualTo(Vector3.zero));
            foreach (AudioSource layer in layers)
            {
                Assert.That(layer.isPlaying, Is.False);
                Assert.That(layer.clip, Is.Null);
            }
        }
        finally
        {
            AudioService.EndVehicleEngine(service.EngineOwner, true);
            AudioService.ApplySettings(previousSettings);
            if (ownerObject != null)
                Object.Destroy(ownerObject);
            if (ownerScene.IsValid() && ownerScene.isLoaded)
                SceneManager.UnloadSceneAsync(ownerScene);
        }
        yield return new ExitPlayMode();
    }


    [UnityTest]
    public IEnumerator DestroyedHudDoesNotInterruptNativeDisableDestroyOrPlayModeExit()
    {
        yield return new EnterPlayMode();
        AudioService service = AudioService.Instance;
        Assert.That(service, Is.Not.Null);
        GameSettingsData previousSettings = GameSettingsRuntime.Current;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        AudioSource[] layers = (AudioSource[])typeof(AudioService).GetField("engineSources", flags).GetValue(service);
        Scene fixtureScene = SceneManager.CreateScene("VehicleHudTeardownScope");
        var hosts = new System.Collections.Generic.List<GameObject>();
        bool readyForNativeExit = false;
        try
        {
            AudioService.ApplySettings(new GameSettingsData { masterVolume = 0f, soundEffectsVolume = 1f });
            foreach (bool destroyManager in new[] { false, true })
            {
                var host = new GameObject("HUD teardown native coordinator");
                hosts.Add(host);
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, fixtureScene);
                var manager = host.AddComponent<MVPGameManager>();
                manager.enabled = false;
                var hudHost = new GameObject("HUD destroyed before coordinator");
                hosts.Add(hudHost);
                hudHost.SetActive(false);
                SceneManager.MoveGameObjectToScene(hudHost, fixtureScene);
                manager.hudUI = hudHost.AddComponent<HUDUI>();
                var player = new PlayerState("Teardown driver", false, 4, 3) { teamId = TeamId.DE };
                var session = new RaceSession();
                session.Players.Add(player);
                typeof(MVPGameManager).GetField("session", flags).SetValue(manager, session);
                typeof(MVPGameManager).GetField("movementAudioPlayer", flags).SetValue(manager, player);
                typeof(MVPGameManager).GetField("instantAudioPlayer", flags).SetValue(manager, player);
                typeof(MVPGameManager).GetField("movementAudioCompletedCells", flags).SetValue(manager, 7);
                host.SetActive(true);
                manager.enabled = true;
                // No frame elapses before teardown, so the real race Start never runs.
                AudioService.BeginVehicleEngine(manager, TeamId.DE, 3, 7f);
                Assert.That(service.EngineOwner, Is.SameAs(manager));
                foreach (AudioSource layer in layers) Assert.That(layer.clip, Is.Not.Null);
                Object.DestroyImmediate(hudHost);
                Assert.That(manager.hudUI == null, Is.True);
                Assert.DoesNotThrow(() => typeof(MVPGameManager).GetMethod("UpdateMovementAudio", flags)
                    .Invoke(manager, new object[] { 8.25f }));
                Assert.That(service.EngineIntensity, Is.GreaterThan(0f));
                // Invoke Unity's native lifecycle rather than reflecting these callbacks.
                if (destroyManager) Object.DestroyImmediate(host);
                else host.SetActive(false);
                Assert.That(service.EngineOwner, Is.Null);
                Assert.That(service.EngineLayerVolumes, Is.EqualTo(Vector3.zero));
                foreach (AudioSource layer in layers)
                {
                    Assert.That(layer.isPlaying, Is.False);
                    Assert.That(layer.clip, Is.Null);
                }
                Assert.That(typeof(MVPGameManager).GetField("movementAudioPlayer", flags).GetValue(manager), Is.Null);
                Assert.That(typeof(MVPGameManager).GetField("instantAudioPlayer", flags).GetValue(manager), Is.Null);
                Assert.That(typeof(MVPGameManager).GetField("movementAudioCompletedCells", flags).GetValue(manager), Is.EqualTo(0));
                Assert.That(player.position, Is.EqualTo(4));
                Assert.That(player.gear, Is.EqualTo(3));
            }
            readyForNativeExit = true;
        }
        finally
        {
            AudioService.EndVehicleEngine(service.EngineOwner, true);
            AudioService.ApplySettings(previousSettings);
            if (!readyForNativeExit)
                foreach (GameObject host in hosts)
                    if (host != null) Object.DestroyImmediate(host);
        }
        // The disabled, previously active manager still holds a destroyed HUD.
        // Exiting Play Mode must dispatch its native OnDestroy without an exception.
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditorAfterFailure()
    {
        if (EditorApplication.isPlaying)
            yield return new ExitPlayMode();
    }
}
