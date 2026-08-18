using System;
using System.Collections;
using UnityEngine;

/// <summary>Boundary for frame-by-frame vehicle movement presentation.</summary>
public interface ICarMovementAnimator
{
    /// <summary>Animates a car from its current position to one track node.</summary>
    IEnumerator MoveToNode(GameObject car, Vector3 targetPosition);
}

/// <summary>
/// Unity adapter for moving a vehicle between adjacent track nodes. Race rules,
/// lap crossing and camera ownership remain in <see cref="MVPGameManager"/>;
/// this class owns only interpolation and orientation updates.
/// </summary>
public sealed class CarMovementAnimator : ICarMovementAnimator
{
    private readonly float moveSpeed;
    private readonly float arrivalThreshold;
    private readonly CarOrientationController orientationController;
    private readonly Func<float> deltaTimeProvider;

    /// <summary>Creates a movement adapter with runtime configuration and an optional clock seam.</summary>
    public CarMovementAnimator(
        GameConfigSO config,
        CarOrientationController orientationController,
        Func<float> deltaTimeProvider = null)
    {
        moveSpeed = config != null ? Mathf.Max(0f, config.moveAnimSpeed) : 12f;
        arrivalThreshold = CarMovementRules.DefaultArrivalThreshold;
        this.orientationController = orientationController ?? new CarOrientationController(config);
        this.deltaTimeProvider = deltaTimeProvider ?? (() => Time.deltaTime);
    }

    /// <summary>Animates one car to a target node and preserves its tangent-facing rotation.</summary>
    public IEnumerator MoveToNode(GameObject car, Vector3 targetPosition)
    {
        if (car == null)
            yield break;

        if (moveSpeed <= 0f)
        {
            car.transform.position = targetPosition;
            orientationController.RotateTowards(car, targetPosition, 0f);
            yield break;
        }

        while (!CarMovementRules.HasReachedTarget(
            car.transform.position, targetPosition, arrivalThreshold))
        {
            float deltaTime = Mathf.Max(0f, deltaTimeProvider());
            car.transform.position = Vector3.MoveTowards(
                car.transform.position,
                targetPosition,
                CarMovementRules.GetStepDistance(moveSpeed, deltaTime));
            orientationController.RotateTowards(car, targetPosition, deltaTime);
            yield return null;
        }

        car.transform.position = targetPosition;
        orientationController.RotateTowards(car, targetPosition, Mathf.Max(0f, deltaTimeProvider()));
    }
}
