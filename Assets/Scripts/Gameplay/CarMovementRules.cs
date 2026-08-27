using UnityEngine;

/// <summary>Pure movement thresholds shared by the runtime animation adapter and tests.</summary>
public static class CarMovementRules
{
    /// <summary>Compatibility threshold used to decide that a node animation has arrived.</summary>
    public const float DefaultArrivalThreshold = 0.02f;

    /// <summary>Returns true when the car is close enough to the target node to snap.</summary>
    public static bool HasReachedTarget(
        Vector3 currentPosition,
        Vector3 targetPosition,
        float arrivalThreshold = DefaultArrivalThreshold)
    {
        float safeThreshold = Mathf.Max(0f, arrivalThreshold);
        return (targetPosition - currentPosition).sqrMagnitude <= safeThreshold * safeThreshold;
    }

    /// <summary>Calculates a frame-independent movement step, clamping invalid inputs to zero.</summary>
    public static float GetStepDistance(float moveSpeed, float deltaTime)
    {
        return Mathf.Max(0f, moveSpeed) * Mathf.Max(0f, deltaTime);
    }

    /// <summary>
    /// Returns the presentation-only hop height for a car moving between two
    /// adjacent nodes. The offset is zero at both nodes and peaks at the
    /// midpoint, keeping the gameplay position and target unchanged.
    /// </summary>
    public static float GetBounceOffset(float progress, float height)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        return Mathf.Sin(clampedProgress * Mathf.PI) * Mathf.Max(0f, height);
    }
}
