using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class OvertakeAudioPresentationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void RecordedOvertakeEventHasExplicitUnscaledBurstBoundary()
    {
        Assert.That(AudioRuntimeRules.GetSfxPath(AudioEventNames.OvertakePassby),
            Is.EqualTo("Audio/SFX/overtake_passby"));
        float cooldown = AudioRuntimeRules.GetCooldown(AudioEventNames.OvertakePassby);
        Assert.That(cooldown, Is.EqualTo(.12f));
        var throttle = new AudioEventThrottle();
        Assert.That(throttle.ShouldPlay(AudioEventNames.OvertakePassby, 1f, cooldown), Is.True);
        Assert.That(throttle.ShouldPlay(AudioEventNames.OvertakePassby, 1f, cooldown), Is.False);
        Assert.That(throttle.ShouldPlay(AudioEventNames.OvertakePassby, 1.06f, cooldown), Is.False);
        Assert.That(throttle.ShouldPlay(AudioEventNames.OvertakePassby, 1.13f, cooldown), Is.True);
    }

    [Test]
    public void DistributedRecordingLoadsWithNonSilentPcmAndMatchesPresentationWindow()
    {
        AudioClip clip = Resources.Load<AudioClip>(AudioRuntimeRules.GetSfxPath(AudioEventNames.OvertakePassby));
        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.length, Is.InRange(.70f, .80f));
        var data = new float[clip.samples * clip.channels];
        Assert.That(clip.GetData(data, 0), Is.True);
        double energy = 0;
        float peak = 0;
        foreach (float sample in data) { energy += sample * sample; peak = Mathf.Max(peak, Mathf.Abs(sample)); }
        Assert.That(System.Math.Sqrt(energy / data.Length), Is.GreaterThan(.001));
        Assert.That(peak, Is.GreaterThan(.02f).And.LessThanOrEqualTo(1f));
    }

    [UnityTest]
    public IEnumerator RealOvertakeEntryPlaysOneVoiceKeepsPitchAndStopsOnSkip()
    {
        yield return new EnterPlayMode();
        AudioService service = AudioService.Instance;
        Assert.That(service, Is.Not.Null);
        GameSettingsData previousSettings = GameSettingsRuntime.Current;
        float previousScale = Time.timeScale;
        Scene scene = SceneManager.CreateScene("Owned overtake presentation audio");
        GameObject root = null;
        TMP_FontAsset font = null;
        Material material = null;
        RaceEventFX effect = null;
        try
        {
            TMP_FontAsset original = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");
            bool dirty = EditorUtility.IsDirty(original);
            font = Object.Instantiate(original);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            material = Object.Instantiate(original.material); font.material = material;
            root = new GameObject("Owned audio event canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.GetComponent<Canvas>().enabled = false;
            effect = root.AddComponent<RaceEventFX>();
            effect.Initialize(root.GetComponent<Canvas>(), font);
            var car = new GameObject("Owned overtake car"); car.transform.SetParent(root.transform, false);
            Vector3 originalScale = car.transform.localScale;
            Quaternion originalRotation = car.transform.rotation;
            AudioService.ApplySettings(new GameSettingsData { masterVolume = .5f, soundEffectsVolume = .4f });
            AudioService.BeginVehicleEngine(effect, TeamId.UK, 2);
            AudioSource engine = (AudioSource)typeof(AudioService).GetField("engineSource", Private).GetValue(service);
            bool skip = false;
            effect.StartCoroutine(effect.PlayOvertake(car.transform, 4, () => skip));
            yield return null;
            yield return null;
            OvertakeAudioPresentation presentation = Presentation(service);
            Assert.That(presentation, Is.Not.Null);
            AudioSource voice = Voice(presentation);
            Assert.That(voice.isPlaying, Is.True, "The real committed-event coroutine must start the recording.");
            Assert.That(voice.clip.name, Is.EqualTo("overtake_passby"));
            Assert.That(voice.volume, Is.EqualTo(.17f).Within(.001f));
            Assert.That(Time.timeScale, Is.EqualTo(.28f).Within(.001f));
            Assert.That(voice.pitch, Is.EqualTo(1f));
            Assert.That(voice.loop, Is.False);
            Assert.That(voice.spatialBlend, Is.Zero);
            Assert.That(presentation.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(1));
            int firstToken = (int)typeof(OvertakeAudioPresentation).GetField("version", Private).GetValue(presentation);
            Assert.That(AudioService.BeginOvertake(effect, 7), Is.Zero, "Merged or simultaneous events cannot stack voices.");
            Assert.That((int)typeof(OvertakeAudioPresentation).GetField("version", Private).GetValue(presentation), Is.EqualTo(firstToken));
            AudioService.ApplySettings(new GameSettingsData { masterVolume = 0f, soundEffectsVolume = 1f });
            Assert.That(voice.volume, Is.Zero, "Mute applies immediately to the active dedicated voice.");
            skip = true;
            yield return null;
            yield return null;
            AssertStopped(voice);
            Assert.That(engine.isPlaying, Is.True, "Ending the pass-by must leave the independent engine voice running.");
            Assert.That(car.transform.localScale, Is.EqualTo(originalScale));
            Assert.That(car.transform.rotation, Is.EqualTo(originalRotation));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return new WaitForSecondsRealtime(.13f);
            effect.StartCoroutine(effect.PlayOvertake(car.transform, 0));
            effect.StartCoroutine(effect.PlayOvertake(car.transform, 2, () => true));
            yield return null;
            AssertStopped(voice);
            Assert.That(AudioService.BeginOvertake(effect, -1), Is.Zero);
            int secondToken = AudioService.BeginOvertake(effect, 1);
            Assert.That(secondToken, Is.Not.Zero.And.Not.EqualTo(firstToken));
            AudioService.EndOvertake(effect, firstToken);
            Assert.That(voice.isPlaying, Is.True, "Stale completion cannot stop a later event.");
            AudioService.EndOvertake(effect, secondToken);
            AssertStopped(voice);
            yield return new WaitForSecondsRealtime(.13f);
            AudioService.ApplySettings(new GameSettingsData { masterVolume = .5f, soundEffectsVolume = .4f });
            Assert.That(AudioService.BeginOvertake(effect, 1), Is.Not.Zero);
            Assert.That(voice.isPlaying, Is.True);
            Assert.That(voice.volume, Is.EqualTo(.17f).Within(.001f));
            AudioService.ApplySettings(new GameSettingsData { masterVolume = 1f, soundEffectsVolume = 0f });
            Assert.That(voice.volume, Is.Zero, "SFX-only mute must silence the active pass-by independently of master.");
            AudioService.ApplySettings(new GameSettingsData { masterVolume = .5f, soundEffectsVolume = .4f });
            Assert.That(voice.volume, Is.EqualTo(.17f).Within(.001f));
            yield return new WaitForSecondsRealtime(.9f);
            AssertStopped(voice);
            Assert.That(EditorUtility.IsDirty(original), Is.EqualTo(dirty));
        }
        finally
        {
            AudioService.EndVehicleEngine(effect, true);
            if (effect != null) effect.StopAllCoroutines();
            if (service != null && Presentation(service) != null)
                typeof(OvertakeAudioPresentation).GetMethod("Stop", Private | BindingFlags.Public).Invoke(Presentation(service), null);
            AudioService.ApplySettings(previousSettings);
            Time.timeScale = previousScale;
            if (root != null) Object.DestroyImmediate(root);
            if (font != null) Object.DestroyImmediate(font);
            if (material != null) Object.DestroyImmediate(material);
        }
        yield return SceneManager.UnloadSceneAsync(scene);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator NativeOwnerDisableDestroyAndSceneExitClearDedicatedVoice()
    {
        yield return new EnterPlayMode();
        AudioService service = AudioService.Instance;
        GameSettingsData settings = GameSettingsRuntime.Current;
        Scene scene = SceneManager.CreateScene("Owned overtake audio lifecycle");
        GameObject host = new GameObject("Overtake owner", typeof(RectTransform), typeof(LayoutElement));
        SceneManager.MoveGameObjectToScene(host, scene);
        LayoutElement owner = host.GetComponent<LayoutElement>();
        try
        {
            AudioService.ApplySettings(new GameSettingsData { masterVolume = 0f, soundEffectsVolume = 1f });
            int token = AudioService.BeginOvertake(owner, 1);
            Assert.That(token, Is.Not.Zero);
            AudioSource voice = Voice(Presentation(service));
            Assert.That(voice.isPlaying, Is.True);
            owner.enabled = false;
            yield return null; yield return null;
            AssertStopped(voice);
            owner.enabled = true;
            yield return new WaitForSecondsRealtime(.13f);
            Assert.That(AudioService.BeginOvertake(owner, 1), Is.Not.Zero);
            service.enabled = false;
            AssertStopped(voice);
            Assert.That(AudioService.BeginOvertake(owner, 1), Is.Zero);
            service.enabled = true;
            yield return new WaitForSecondsRealtime(.13f);
            Assert.That(AudioService.BeginOvertake(owner, 1), Is.Not.Zero);
            Object.DestroyImmediate(owner);
            yield return null; yield return null;
            AssertStopped(voice);
            yield return new WaitForSecondsRealtime(.13f);
            owner = host.AddComponent<LayoutElement>();
            Assert.That(AudioService.BeginOvertake(owner, 1), Is.Not.Zero);
            yield return SceneManager.UnloadSceneAsync(scene);
            AssertStopped(voice);
            Assert.That(service, Is.SameAs(AudioService.Instance));
        }
        finally
        {
            if (service != null && Presentation(service) != null)
                typeof(OvertakeAudioPresentation).GetMethod("Stop", Private | BindingFlags.Public).Invoke(Presentation(service), null);
            if (service != null) service.enabled = true;
            AudioService.ApplySettings(settings);
            if (host != null) Object.DestroyImmediate(host);
        }
        if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator EnsureEditModeAfterAssertionFailure()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    private static OvertakeAudioPresentation Presentation(AudioService service) =>
        (OvertakeAudioPresentation)typeof(AudioService).GetField("overtakePresentation", Private).GetValue(service);
    private static AudioSource Voice(OvertakeAudioPresentation presentation) =>
        (AudioSource)typeof(OvertakeAudioPresentation).GetField("voice", Private).GetValue(presentation);
    private static void AssertStopped(AudioSource voice)
    {
        Assert.That(voice.isPlaying, Is.False);
        Assert.That(voice.clip, Is.Null);
        Assert.That(voice.volume, Is.Zero);
        Assert.That(voice.pitch, Is.EqualTo(1f));
    }
}
