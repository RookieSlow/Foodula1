using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Stops an isolated, never-enabled service without touching the live singleton or settings.</summary>
public sealed class RacingTechAudioTeardownTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(false)]
    [TestCase(true)]
    public void SceneCallbackFromDestroyedServiceDetachesBothEventsWithoutTouchingSingleton(bool loadedEvent)
    {
        AudioService original = AudioService.Instance;
        int loadedCount = SceneCallbackCount("sceneLoaded");
        int unloadedCount = SceneCallbackCount("sceneUnloaded");
        var root = new GameObject("Never activated stale audio listener");
        root.SetActive(false);
        AudioService service = root.AddComponent<AudioService>();
        var loaded = (UnityAction<Scene, LoadSceneMode>)Delegate.CreateDelegate(
            typeof(UnityAction<Scene, LoadSceneMode>), service, typeof(AudioService).GetMethod("OnSceneLoaded", Private));
        var unloaded = (UnityAction<Scene>)Delegate.CreateDelegate(
            typeof(UnityAction<Scene>), service, typeof(AudioService).GetMethod("OnSceneUnloaded", Private));
        UnityAction<Scene, LoadSceneMode> sentinelLoaded = (scene, mode) => { };
        UnityAction<Scene> sentinelUnloaded = scene => { };
        try
        {
            Assert.That(AudioService.Instance, Is.SameAs(original));
            SceneManager.sceneLoaded += loaded;
            SceneManager.sceneUnloaded += unloaded;
            SceneManager.sceneLoaded += sentinelLoaded;
            SceneManager.sceneUnloaded += sentinelUnloaded;
            Object.DestroyImmediate(root);
            Assert.That(service == null, Is.True);
            Assert.That(ReferenceEquals(service, null), Is.False);
            // Unity may skip OnDestroy for never-activated fixtures. Explicitly
            // restore a missing subscription to reproduce the observed stale delegate.
            if (!ContainsSceneCallback("sceneLoaded", loaded)) SceneManager.sceneLoaded += loaded;
            if (!ContainsSceneCallback("sceneUnloaded", unloaded)) SceneManager.sceneUnloaded += unloaded;
            Assert.That(ContainsSceneCallback("sceneLoaded", loaded), Is.True);
            Assert.That(ContainsSceneCallback("sceneUnloaded", unloaded), Is.True);
            Scene active = SceneManager.GetActiveScene();
            Assert.DoesNotThrow(() =>
            {
                if (loadedEvent) loaded(active, LoadSceneMode.Single);
                else unloaded(active);
            });
            Assert.That(ContainsSceneCallback("sceneLoaded", loaded), Is.False);
            Assert.That(ContainsSceneCallback("sceneUnloaded", unloaded), Is.False);
            Assert.That(ContainsSceneCallback("sceneLoaded", sentinelLoaded), Is.True);
            Assert.That(ContainsSceneCallback("sceneUnloaded", sentinelUnloaded), Is.True);
            Assert.That(SceneCallbackCount("sceneLoaded"), Is.EqualTo(loadedCount + 1));
            Assert.That(SceneCallbackCount("sceneUnloaded"), Is.EqualTo(unloadedCount + 1));
            Assert.That(AudioService.Instance, Is.SameAs(original));
        }
        finally
        {
            SceneManager.sceneLoaded -= loaded;
            SceneManager.sceneUnloaded -= unloaded;
            SceneManager.sceneLoaded -= sentinelLoaded;
            SceneManager.sceneUnloaded -= sentinelUnloaded;
            if (root != null) Object.DestroyImmediate(root);
        }
        Assert.That(SceneCallbackCount("sceneLoaded"), Is.EqualTo(loadedCount));
        Assert.That(SceneCallbackCount("sceneUnloaded"), Is.EqualTo(unloadedCount));
        Assert.That(AudioService.Instance, Is.SameAs(original));
    }

    [Test]
    public void DestroyedNonSingletonServiceUnsubscribesOnlyItsOwnSceneCallbacks()
    {
        AudioService original = AudioService.Instance;
        var root = new GameObject("Inactive retired audio scene listener");
        root.SetActive(false);
        AudioService service = root.AddComponent<AudioService>();
        var loaded = (UnityAction<Scene, LoadSceneMode>)Delegate.CreateDelegate(
            typeof(UnityAction<Scene, LoadSceneMode>), service, typeof(AudioService).GetMethod("OnSceneLoaded", Private));
        var unloaded = (UnityAction<Scene>)Delegate.CreateDelegate(
            typeof(UnityAction<Scene>), service, typeof(AudioService).GetMethod("OnSceneUnloaded", Private));
        UnityAction<Scene, LoadSceneMode> sentinelLoaded = (scene, mode) => { };
        UnityAction<Scene> sentinelUnloaded = scene => { };
        int loadedCount = SceneCallbackCount("sceneLoaded");
        int unloadedCount = SceneCallbackCount("sceneUnloaded");
        try
        {
            Assert.That(AudioService.Instance, Is.SameAs(original));
            Assert.That(service, Is.Not.SameAs(original));
            SceneManager.sceneLoaded += loaded;
            SceneManager.sceneUnloaded += unloaded;
            SceneManager.sceneLoaded += sentinelLoaded;
            SceneManager.sceneUnloaded += sentinelUnloaded;
            Assert.That(ContainsSceneCallback("sceneLoaded", loaded), Is.True);
            Assert.That(ContainsSceneCallback("sceneUnloaded", unloaded), Is.True);
            typeof(AudioService).GetMethod("OnDestroy", Private).Invoke(service, null);
            Assert.That(ContainsSceneCallback("sceneLoaded", loaded), Is.False);
            Assert.That(ContainsSceneCallback("sceneUnloaded", unloaded), Is.False);
            Assert.That(ContainsSceneCallback("sceneLoaded", sentinelLoaded), Is.True);
            Assert.That(ContainsSceneCallback("sceneUnloaded", sentinelUnloaded), Is.True);
            Assert.That(SceneCallbackCount("sceneLoaded"), Is.EqualTo(loadedCount + 1));
            Assert.That(SceneCallbackCount("sceneUnloaded"), Is.EqualTo(unloadedCount + 1));
            Assert.That(AudioService.Instance, Is.SameAs(original));
        }
        finally
        {
            SceneManager.sceneLoaded -= loaded;
            SceneManager.sceneUnloaded -= unloaded;
            SceneManager.sceneLoaded -= sentinelLoaded;
            SceneManager.sceneUnloaded -= sentinelUnloaded;
            Object.DestroyImmediate(root);
        }
        Assert.That(AudioService.Instance, Is.SameAs(original));
        Assert.That(SceneCallbackCount("sceneLoaded"), Is.EqualTo(loadedCount));
        Assert.That(SceneCallbackCount("sceneUnloaded"), Is.EqualTo(unloadedCount));
    }

    private static int SceneCallbackCount(string eventName)
    {
        FieldInfo field = typeof(SceneManager).GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(field, Is.Not.Null);
        return (field.GetValue(null) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    private static bool ContainsSceneCallback(string eventName, Delegate callback)
    {
        FieldInfo field = typeof(SceneManager).GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, "Unity scene event backing field must exist for inspection.");
        var listeners = field.GetValue(null) as Delegate;
        if (listeners == null) return false;
        foreach (Delegate listener in listeners.GetInvocationList())
            if (listener.Equals(callback)) return true;
        return false;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StopEngineSkipsNullOrDestroyedLayerAndStillClearsLiveLayerRepeatedly(bool destroyed)
    {
        AudioService original = AudioService.Instance;
        var root = new GameObject("Inactive isolated audio teardown");
        root.SetActive(false);
        AudioClip clip = null;
        try
        {
            AudioService service = root.AddComponent<AudioService>();
            Assert.That(AudioService.Instance, Is.SameAs(original), "Inactive fixture must not bootstrap or replace the singleton.");
            AudioSource dead = null;
            if (destroyed)
            {
                var deadHost = new GameObject("Destroyed engine layer");
                deadHost.transform.SetParent(root.transform, false);
                dead = deadHost.AddComponent<AudioSource>();
                Object.DestroyImmediate(deadHost);
                Assert.That(ReferenceEquals(dead, null), Is.False, "Exercise a destroyed Unity object, not just C# null.");
                Assert.That(dead == null, Is.True);
            }
            AudioSource live = root.AddComponent<AudioSource>();
            clip = AudioClip.Create("Owned teardown clip", 64, 1, 44100, false);
            live.clip = clip;
            live.pitch = 1.7f;
            live.volume = 0.65f;
            var sources = new[] { dead, live };
            FieldInfo field = typeof(AudioService).GetField("engineSources", Private);
            field.SetValue(service, sources);
            MethodInfo stop = typeof(AudioService).GetMethod("StopVehicleEngine", Private);
            for (int i = 0; i < 2; i++)
            {
                Assert.DoesNotThrow(() => stop.Invoke(service, null), "A dead earlier layer must not prevent remaining voice cleanup.");
                Assert.That(live.isPlaying, Is.False);
                Assert.That(live.clip, Is.Null);
                Assert.That(live.pitch, Is.EqualTo(1f));
                Assert.That(live.volume, Is.Zero);
                Assert.That(field.GetValue(service), Is.SameAs(sources));
                Assert.That(ReferenceEquals(sources[0], dead), Is.True);
                Assert.That(sources[1], Is.SameAs(live));
                Assert.That(AudioService.Instance, Is.SameAs(original));
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
            if (clip != null) Object.DestroyImmediate(clip);
        }
        Assert.That(AudioService.Instance, Is.SameAs(original));
    }
}
