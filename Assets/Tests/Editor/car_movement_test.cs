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
    public void AnimatorMovesLinearlyToTargetUsingInjectedDeltaTime()
    {
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        var car = new GameObject("CarMovementAnimatorTestCar");
        try
        {
            config.moveAnimSpeed = 2f;
            config.nodeMoveDuration = 0.15f;
            var animator = new CarMovementAnimator(
                config,
                new CarOrientationController(config),
                () => 0.05f);
            IEnumerator routine = animator.MoveToNode(car, Vector3.right);

            bool leftTrackLine = false;
            while (routine.MoveNext())
            {
                Vector3 position = car.transform.position;
                leftTrackLine |= Mathf.Abs(position.y) > 0.0001f || Mathf.Abs(position.z) > 0.0001f;
                Assert.That(position.x, Is.InRange(0f, 1f));
            }

            Assert.That(leftTrackLine, Is.False);
            Assert.That(car.transform.position, Is.EqualTo(Vector3.right));
        }
        finally
        {
            Object.DestroyImmediate(car);
            Object.DestroyImmediate(config);
        }
    }
}
