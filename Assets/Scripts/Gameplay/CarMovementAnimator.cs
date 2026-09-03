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
    private readonly float fallbackMoveSpeed;
    private readonly float nodeMoveDuration;
    private readonly float arrivalThreshold;
    private readonly CarOrientationController orientationController;
    private readonly Func<float> deltaTimeProvider;

    /// <summary>Creates a movement adapter with runtime configuration and an optional clock seam.</summary>
    public CarMovementAnimator(
        GameConfigSO config,
        CarOrientationController orientationController,
        Func<float> deltaTimeProvider = null)
    {
        fallbackMoveSpeed = config != null ? Mathf.Max(0f, config.moveAnimSpeed) : 12f;
        float fallbackDuration = fallbackMoveSpeed > 0f ? 1f / fallbackMoveSpeed : 0.15f;
        nodeMoveDuration = config != null
            ? Mathf.Max(0.01f, config.nodeMoveDuration > 0f ? config.nodeMoveDuration : fallbackDuration)
            : 0.15f;
        arrivalThreshold = CarMovementRules.DefaultArrivalThreshold;
        this.orientationController = orientationController ?? new CarOrientationController(config);
        this.deltaTimeProvider = deltaTimeProvider ?? (() => Time.deltaTime);
    }

    /// <summary>Animates one car to a target node and preserves its tangent-facing rotation.</summary>
    public IEnumerator MoveToNode(GameObject car, Vector3 targetPosition)
    {
        if (car == null)
            yield break;

        if (nodeMoveDuration <= 0f)
        {
            car.transform.position = targetPosition;
            orientationController.RotateTowards(car, targetPosition, 0f);
            yield break;
        }

        Vector3 startPosition = car.transform.position;
        if (CarMovementRules.HasReachedTarget(startPosition, targetPosition, arrivalThreshold))
        {
            car.transform.position = targetPosition;
            orientationController.RotateTowards(car, targetPosition, 0f);
            yield break;
        }

        // Each node is a discrete board-space step: interpolate along the
        // track plane, then snap to the exact target.
        float elapsed = 0f;
        while (elapsed < nodeMoveDuration)
        {
            float deltaTime = Mathf.Max(0f, deltaTimeProvider());
            elapsed += deltaTime;
            float progress = Mathf.Clamp01(elapsed / nodeMoveDuration);
            car.transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, progress);
            orientationController.RotateTowards(car, targetPosition, deltaTime);
            yield return null;
        }

        car.transform.position = targetPosition;
        orientationController.RotateTowards(car, targetPosition, Mathf.Max(0f, deltaTimeProvider()));
        AudioService.PlaySfx(AudioEventNames.CarHop);
    }
}
