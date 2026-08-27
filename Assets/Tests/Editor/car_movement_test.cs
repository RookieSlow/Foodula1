using System.Collections;
using NUnit.Framework;
using UnityEngine;

public class CarMovementTests
{
    [Test]
    public void StepDistanceClampsInvalidInputs()
    {
        Assert.That(CarMovementRules.GetStepDistance(-4f, 0.5f), Is.EqualTo(0f));
        Assert.That(CarMovementRules.GetStepDistance(4f, -0.5f), Is.EqualTo(0f));
        Assert.That(CarMovementRules.GetStepDistance(4f, 0.25f), Is.EqualTo(1f));
    }

    [Test]
    public void ArrivalThresholdUsesSquaredDistance()
    {
        Assert.That(CarMovementRules.HasReachedTarget(
            Vector3.zero,
            new Vector3(0.01f, 0f, 0f)), Is.True);
        Assert.That(CarMovementRules.HasReachedTarget(
            Vector3.zero,
            new Vector3(0.03f, 0f, 0f)), Is.False);
    }

    [Test]
    public void BounceOffsetPeaksBetweenNodesAndReturnsToTrack()
    {
        Assert.That(CarMovementRules.GetBounceOffset(0f, 0.08f), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(CarMovementRules.GetBounceOffset(0.5f, 0.08f), Is.EqualTo(0.08f).Within(0.0001f));
        Assert.That(CarMovementRules.GetBounceOffset(1f, 0.08f), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(CarMovementRules.GetBounceOffset(0.5f, -1f), Is.EqualTo(0f));
    }

    [Test]
    public void AnimatorMovesToTargetUsingInjectedDeltaTime()
    {
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        var car = new GameObject("CarMovementAnimatorTestCar");
        try
        {
            config.moveAnimSpeed = 2f;
            config.nodeMoveDuration = 0.15f;
            config.nodeBounceHeight = 0.08f;
            var animator = new CarMovementAnimator(
                config,
                new CarOrientationController(config),
                () => 0.05f);
            IEnumerator routine = animator.MoveToNode(car, Vector3.right);

            bool sawBounce = false;
            while (routine.MoveNext())
                sawBounce |= car.transform.position.y > 0.001f;

            Assert.That(sawBounce, Is.True);
            Assert.That(car.transform.position, Is.EqualTo(Vector3.right));
        }
        finally
        {
            Object.DestroyImmediate(car);
            Object.DestroyImmediate(config);
        }
    }
}
