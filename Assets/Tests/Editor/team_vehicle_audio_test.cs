using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Isolated rev rules and real audio adapter state; these do not claim listening acceptance.</summary>
public class TeamVehicleAudioTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void RevIntensityUsesAbsoluteCellsAndSaturatesWithoutMakingShortMovesRedline()
    {
        float previous = 0f;
        for (int cells = 0; cells <= 30; cells++)
        {
            float value = TeamVehicleAudioRules.EvaluateIntensity(cells, 12f);
            Assert.That(value, Is.GreaterThanOrEqualTo(previous));
            previous = value;
        }
        Assert.That(TeamVehicleAudioRules.EvaluateIntensity(1f), Is.LessThan(0.1f));
        Assert.That(TeamVehicleAudioRules.EvaluateIntensity(4f), Is.LessThan(TeamVehicleAudioRules.EvaluateIntensity(10f)));
        Assert.That(TeamVehicleAudioRules.EvaluateIntensity(12f), Is.EqualTo(1f));
        Assert.That(TeamVehicleAudioRules.EvaluateIntensity(100f), Is.EqualTo(1f));
        Assert.That(TeamVehicleAudioRules.EvaluateIntensity(-1f), Is.Zero);
        Assert.That(TeamVehicleAudioRules.EvaluateIntensity(float.NaN), Is.Zero);
        Assert.That(TeamVehicleAudioRules.EvaluateIntensity(6f, 0f), Is.EqualTo(0.5f));
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void AuthoredTeamAssetsHaveDistinctPathsAndLoopAtMatchingPcmSamples(TeamId team)
    {
        TeamVehicleAudioProfile profile = TeamVehicleAudioRules.GetProfile(team);
        Assert.That(profile, Is.Not.Null);
        string[] paths = { profile.engineResourcePath, profile.midEngineResourcePath, profile.highEngineResourcePath };
        Assert.That(paths, Is.Unique);
        foreach (string path in paths)
        {
            Assert.That(path, Is.Not.Null.And.Not.Empty);
            AudioClip clip = Resources.Load<AudioClip>(path);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.frequency, Is.EqualTo(48000));
            Assert.That(clip.channels, Is.EqualTo(1));
            float[] first = new float[1];
            float[] last = new float[1];
            Assert.That(clip.GetData(first, 0), Is.True);
            Assert.That(clip.GetData(last, clip.samples - 1), Is.True);
            Assert.That(last[0], Is.EqualTo(first[0]).Within(1f / 32768f));
        }
        Assert.That(profile.upShiftResourcePath, Is.Not.EqualTo(profile.downShiftResourcePath));
        Assert.That(Resources.Load<AudioClip>(profile.upShiftResourcePath), Is.Not.Null);
        Assert.That(Resources.Load<AudioClip>(profile.downShiftResourcePath), Is.Not.Null);
        Assert.That(TeamVehicleAudioRules.GetEnginePitch(profile, 1f),
            Is.LessThan(TeamVehicleAudioRules.GetEnginePitch(profile, profile.cellsToMax)));
    }

    [Test]
    public void ShiftMappingDoesNotTreatHoldOrChinaGoChainAsAnotherShift()
    {
        TeamVehicleAudioProfile china = TeamVehicleAudioRules.GetProfile(TeamId.CN);
        Assert.That(TeamVehicleAudioRules.GetShiftResourcePath(china, 2, 2), Is.Null);
        Assert.That(TeamVehicleAudioRules.GetShiftResourcePath(china, 1, 2), Is.EqualTo(china.upShiftResourcePath));
        Assert.That(TeamVehicleAudioRules.GetShiftResourcePath(china, 2, 1), Is.EqualTo(china.downShiftResourcePath));
        Assert.That(TeamVehicleAudioRules.GetProfile((TeamId)999), Is.Null);
        Assert.That(TeamVehicleAudioRules.ValidateResourcePath("Audio/../outside"), Is.Null);
    }

    [Test]
    public void RevAndVolumeAreIsolatedFromMusicUiAndOtherSfxAndRespectMute()
    {
        using (var scope = new AudioScope())
        {
            TeamVehicleAudioProfile profile = TeamVehicleAudioRules.GetProfile(TeamId.US);
            AudioService.BeginVehicleEngine(scope.OwnerA, TeamId.US, 4, profile.cellsToMax);
            Set(scope.Service, "engineEnvelope", 1f);
            AudioService.ApplySettings(new GameSettingsData { masterVolume = .5f, soundEffectsVolume = .4f, musicVolume = .8f });
            AudioSource engine = Get<AudioSource>(scope.Service, "engineSource");
            AudioSource[] layers = Get<AudioSource[]>(scope.Service, "engineSources");
            AudioSource sfx = Get<AudioSource>(scope.Service, "sfxSource");
            AudioSource ui = Get<AudioSource>(scope.Service, "uiSource");
            AudioSource[] music = Get<AudioSource[]>(scope.Service, "musicSources");
            Assert.That(engine, Is.Not.SameAs(sfx));
            Assert.That(layers.Length, Is.EqualTo(3));
            Assert.That(layers, Is.Unique);
            foreach (AudioSource layer in layers)
                Assert.That(layer, Is.Not.SameAs(ui).And.Not.SameAs(sfx).And.Not.SameAs(music[0]));
            Assert.That(scope.Service.EnginePitch, Is.EqualTo(profile.highPitch));
            Assert.That(scope.Service.EngineVolume, Is.EqualTo(.2f * profile.engineGain).Within(.0001f));
            Assert.That(sfx.volume, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(ui.volume, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(music[0].volume, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(sfx.pitch, Is.EqualTo(1f));
            Assert.That(ui.pitch, Is.EqualTo(1f));
            Assert.That(music[0].pitch, Is.EqualTo(1f));
            AudioService.ApplySettings(new GameSettingsData { masterVolume = 0f, soundEffectsVolume = 1f });
            Assert.That(scope.Service.EngineVolume, Is.Zero);
            Assert.That(scope.Service.EngineLayerVolumes, Is.EqualTo(Vector3.zero));
            Assert.That(sfx.volume, Is.Zero);
            Assert.That(ui.volume, Is.Zero);
        }
    }

    [Test]
    public void OwnerInactiveOrOwnerSceneUnloadStopsTheLoopAndClearsItsClip()
    {
        using (var scope = new AudioScope())
        {
            AudioService.BeginVehicleEngine(scope.OwnerA, TeamId.UK, 2, 5f);
            scope.OwnerA.gameObject.SetActive(false);
            Invoke(scope.Service, "LateUpdate");
            Assert.That(scope.Service.EngineOwner, Is.Null);
            Assert.That(Get<AudioSource>(scope.Service, "engineSource").clip, Is.Null);
            foreach (AudioSource layer in Get<AudioSource[]>(scope.Service, "engineSources"))
                Assert.That(layer.clip, Is.Null);
            AudioService.BeginVehicleEngine(scope.OwnerB, TeamId.CN, 2);
            Invoke(scope.Service, "OnSceneUnloaded", scope.OwnerB.gameObject.scene);
            Assert.That(scope.Service.EngineOwner, Is.Null);
            Assert.That(scope.Service.EngineIntensity, Is.Zero);
            Assert.That(scope.Service.EngineVolume, Is.Zero);
        }
    }

    [Test]
    public void LateFadeCannotStopTheReplacementVoiceAndUnrelatedOwnerCannotEndIt()
    {
        using (var scope = new AudioScope())
        {
            AudioService.BeginVehicleEngine(scope.OwnerA, TeamId.UK, 2, 3f);
            int previousVersion = Get<int>(scope.Service, "engineVersion");
            IEnumerator oldFade = (IEnumerator)Invoke(scope.Service, "FadeOutVehicleEngine", previousVersion);
            Assert.That(oldFade.MoveNext(), Is.True);
            AudioService.BeginVehicleEngine(scope.OwnerB, TeamId.DE, 3, 7f);
            Assert.That(oldFade.MoveNext(), Is.False);
            AudioService.EndVehicleEngine(scope.OwnerA, true);
            Assert.That(scope.Service.EngineOwner, Is.SameAs(scope.OwnerB));
            Assert.That(scope.Service.EngineTeam, Is.EqualTo(TeamId.DE));
            Assert.That(Get<AudioSource>(scope.Service, "engineSource").clip.name, Is.EqualTo("team_engine_de"));
            AudioService.EndVehicleEngine(scope.OwnerB, true);
            Assert.That(scope.Service.EngineOwner, Is.Null);
        }
    }

    [Test]
    public void EmptyInstantMovementAndMissingProfileNeverStartAnEngine()
    {
        using (var scope = new AudioScope())
        {
            AudioService.PlayTeamAccelerationCue(scope.OwnerA, TeamId.JP, 1, 0);
            Assert.That(scope.Service.EngineOwner, Is.Null);
            AudioService.BeginVehicleEngine(scope.OwnerA, (TeamId)999, 1);
            Assert.That(scope.Service.EngineOwner, Is.Null);
            AudioService.UpdateVehicleEngine(scope.OwnerA, 5f);
            AudioService.EndVehicleEngine(scope.OwnerA, true);
            Assert.That(scope.Service.EngineIntensity, Is.Zero);
        }
    }

    [Test]
    public void CallsBeforeBootstrapAreSilentAndDoNotCreateAnAudioSingleton()
    {
        FieldInfo instance = typeof(AudioService).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object original = instance.GetValue(null);
        instance.SetValue(null, null);
        try
        {
            Assert.DoesNotThrow(() =>
            {
                AudioService.BeginVehicleEngine(null, TeamId.UK, 1);
                AudioService.UpdateVehicleEngine(null, 4f);
                AudioService.EndVehicleEngine(null, true);
                AudioService.PlayTeamGearShift(TeamId.CN, 1, 2);
                AudioService.PlayTeamAccelerationCue(null, TeamId.JP, 2, 3);
            });
            Assert.That(AudioService.Instance, Is.Null);
        }
        finally { instance.SetValue(null, original); }
    }

    [Test]
    public void RecordedRpmLayersCrossfadeWithConstantPowerAndBoundedPitch()
    {
        Assert.That(TeamVehicleAudioRules.GetEngineLayerWeights(0f), Is.EqualTo(new Vector3(1f, 0f, 0f)));
        Assert.That(TeamVehicleAudioRules.GetEngineLayerWeights(.5f), Is.EqualTo(new Vector3(0f, 1f, 0f)));
        Assert.That(TeamVehicleAudioRules.GetEngineLayerWeights(1f), Is.EqualTo(new Vector3(0f, 0f, 1f)));
        for (int step = 0; step <= 100; step++)
        {
            Vector3 weights = TeamVehicleAudioRules.GetEngineLayerWeights(step / 100f);
            Assert.That(weights.sqrMagnitude, Is.EqualTo(1f).Within(.00001f));
            Assert.That(weights.x, Is.GreaterThanOrEqualTo(0f));
            Assert.That(weights.y, Is.GreaterThanOrEqualTo(0f));
            Assert.That(weights.z, Is.GreaterThanOrEqualTo(0f));
        }
        Assert.That(TeamVehicleAudioRules.GetEngineLayerWeights(float.NaN), Is.EqualTo(new Vector3(1f, 0f, 0f)));
        var exaggerated = new TeamVehicleAudioProfile { lowPitch = .1f, highPitch = 4f };
        Assert.That(TeamVehicleAudioRules.GetEnginePitch(exaggerated, 0f), Is.EqualTo(.75f));
        Assert.That(TeamVehicleAudioRules.GetEnginePitch(exaggerated, 100f), Is.EqualTo(1.8f));

        using (var scope = new AudioScope())
        {
            AudioService.BeginVehicleEngine(scope.OwnerA, TeamId.IT, 3, 3.25f);
            Set(scope.Service, "engineEnvelope", 1f);
            AudioService.ApplySettings(new GameSettingsData { masterVolume = .5f, soundEffectsVolume = .4f });
            Vector3 volumes = scope.Service.EngineLayerVolumes;
            Assert.That(volumes.x, Is.GreaterThan(0f));
            Assert.That(volumes.y, Is.GreaterThan(0f));
            Assert.That(volumes.z, Is.Zero);
            Assert.That(volumes.magnitude, Is.EqualTo(.2f * TeamVehicleAudioRules.GetProfile(TeamId.IT).engineGain).Within(.00001f));
        }
    }


    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void NormalTravelHasAnAudibleRevRiseWithoutMakingOneCellRedline(TeamId team)
    {
        TeamVehicleAudioProfile profile = TeamVehicleAudioRules.GetProfile(team);
        float start = TeamVehicleAudioRules.GetEnginePitch(profile, 0f);
        float sixCells = TeamVehicleAudioRules.GetEnginePitch(profile, 6f);
        float peak = TeamVehicleAudioRules.GetEnginePitch(profile, profile.cellsToMax);
        Assert.That(sixCells / start, Is.GreaterThanOrEqualTo(Mathf.Pow(2f, 4f / 12f)),
            "Six cells must produce at least a four-semitone pitch glide, in addition to recorded RPM layers.");
        Assert.That(peak / start, Is.GreaterThanOrEqualTo(1.45f));
        Assert.That(TeamVehicleAudioRules.EvaluateIntensity(1f, profile.cellsToMax), Is.LessThan(0.15f));
        Assert.That(TeamVehicleAudioRules.GetEnginePitch(profile, 1f), Is.LessThan(sixCells));
        Assert.That(profile.engineGain, Is.InRange(0.65f, 0.9f),
            "The engine must be present in the mix while retaining gain headroom.");
        Assert.That(peak, Is.LessThanOrEqualTo(1.6f));
        Assert.That(TeamVehicleAudioRules.GetEnginePitch(profile, 100f), Is.EqualTo(peak));
    }

    private static object Invoke(object target, string method, params object[] arguments) =>
        typeof(AudioService).GetMethod(method, Private).Invoke(target, arguments);
    private static T Get<T>(object target, string field) => (T)typeof(AudioService).GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => typeof(AudioService).GetField(field, Private).SetValue(target, value);

    private sealed class AudioScope : IDisposable
    {
        private static readonly FieldInfo Instance = typeof(AudioService).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly AudioService original;
        public readonly AudioService Service;
        public readonly MVPGameManager OwnerA;
        public readonly MVPGameManager OwnerB;

        public AudioScope()
        {
            original = (AudioService)Instance.GetValue(null);
            Instance.SetValue(null, null);
            GameObject root = new GameObject("IsolatedTeamVehicleAudio");
            Service = root.AddComponent<AudioService>();
            if (AudioService.Instance != Service)
                Invoke(Service, "Awake");
            OwnerA = new GameObject("AudioOwnerA").AddComponent<MVPGameManager>();
            OwnerB = new GameObject("AudioOwnerB").AddComponent<MVPGameManager>();
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(OwnerA.gameObject);
            UnityEngine.Object.DestroyImmediate(OwnerB.gameObject);
            UnityEngine.Object.DestroyImmediate(Service.gameObject);
            Instance.SetValue(null, original);
        }
    }
}
