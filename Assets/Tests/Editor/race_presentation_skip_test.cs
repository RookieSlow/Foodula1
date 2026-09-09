using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RacePresentationSkipTests
{
    [Test]
    public void SkipRequestOnlyAppliesInsideAnActivePresentationWindow()
    {
        var state = new RacePresentationSkipState();

        Assert.That(state.RequestSkip(), Is.False);
        Assert.That(state.IsSkipRequested, Is.False);

        state.Begin();
        Assert.That(state.RequestSkip(), Is.True);
        Assert.That(state.IsSkipRequested, Is.True);

        state.End();
        Assert.That(state.IsActive, Is.False);
        Assert.That(state.IsSkipRequested, Is.False);
    }

    [Test]
    public void CarMovementAnimatorSnapsToTargetWhenPresentationIsSkipped()
    {
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        var car = new GameObject("SkippedCarMovementTestCar");
        try
        {
            bool shouldSkip = true;
            var animator = new CarMovementAnimator(
                config,
                new CarOrientationController(config),
                () => 0.01f,
                () => shouldSkip);
            IEnumerator routine = animator.MoveToNode(car, new Vector3(4f, 2f, 0f));

            int yieldedFrames = 0;
            while (routine.MoveNext())
                yieldedFrames++;

            Assert.That(yieldedFrames, Is.EqualTo(0));
            Assert.That(car.transform.position, Is.EqualTo(new Vector3(4f, 2f, 0f)));
        }
        finally
        {
            Object.DestroyImmediate(car);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void TailwindVisualStopsAndRestoresStateWhenSkipped()
    {
        var canvasObject = new GameObject(
            "RacePresentationSkipCanvas",
            typeof(RectTransform),
            typeof(Canvas));
        var host = new GameObject("RacePresentationSkipFX");
        var follower = new GameObject("SkippedFollower");
        var leader = new GameObject("SkippedLeader");
        float previousTimeScale = Time.timeScale;
        bool shouldSkip = false;

        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RaceEventFX effect = host.AddComponent<RaceEventFX>();
            effect.Initialize(canvas, null);

            IEnumerator routine = effect.PlaySlipstreams(
                new List<RaceEventFX.SlipstreamVisualEvent>
                {
                    new RaceEventFX.SlipstreamVisualEvent(follower.transform, leader.transform, 2)
                },
                () => shouldSkip);

            Assert.That(routine.MoveNext(), Is.True);
            shouldSkip = true;

            int yieldedFrames = 0;
            while (routine.MoveNext())
            {
                yieldedFrames++;
                Assert.That(yieldedFrames, Is.LessThan(4));
            }

            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
            Assert.That(follower.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(leader.transform.localScale, Is.EqualTo(Vector3.one));

            FieldInfo effectBusy = typeof(RaceEventFX).GetField(
                "effectBusy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(effectBusy, Is.Not.Null);
            Assert.That((bool)effectBusy.GetValue(effect), Is.False);
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            Object.DestroyImmediate(follower);
            Object.DestroyImmediate(leader);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(canvasObject);
        }
    }
}
