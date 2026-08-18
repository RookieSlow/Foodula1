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
    public void AnimatorMovesToTargetUsingInjectedDeltaTime()
    {
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        var car = new GameObject("CarMovementAnimatorTestCar");
        try
        {
            config.moveAnimSpeed = 2f;
            var animator = new CarMovementAnimator(
                config,
                new CarOrientationController(config),
                () => 0.5f);
            IEnumerator routine = animator.MoveToNode(car, Vector3.right);

            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(car.transform.position.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(car.transform.position, Is.EqualTo(Vector3.right));
        }
        finally
        {
            Object.DestroyImmediate(car);
            Object.DestroyImmediate(config);
        }
    }
}
