using UnityEngine;

/// <summary>
/// Runtime adapter for vehicle-facing presentation.
/// Pure angle calculations stay in <see cref="CarOrientationRules"/>; this
/// class owns only Unity transforms and the configured rotation speed.
/// </summary>
public sealed class CarOrientationController
{
    private readonly float spriteFacingAngle;
    private readonly float rotateSpeed;

    public CarOrientationController(GameConfigSO config)
    {
        spriteFacingAngle = config != null ? config.carSpriteFacingAngle : 0f;
        rotateSpeed = config != null ? Mathf.Max(0f, config.carRotateSpeed) : 540f;
    }

    /// <summary>Returns the initial rotation for a car facing a track tangent.</summary>
    public Quaternion GetFacingRotation(Vector2 direction)
    {
        return Quaternion.Euler(0f, 0f,
            CarOrientationRules.GetFacingAngle(direction, spriteFacingAngle));
    }

    /// <summary>Snaps a teleported or stationary car toward the next node.</summary>
    public void FaceImmediately(GameObject car, Vector3 target)
    {
        if (car == null)
            return;

        Vector3 direction = target - car.transform.position;
        float currentAngle = car.transform.rotation.eulerAngles.z;
        float targetAngle = CarOrientationRules.GetFacingAngle(
            direction, spriteFacingAngle, currentAngle);
        car.transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
    }

    /// <summary>Rotates a moving car toward its current target node.</summary>
    public void RotateTowards(GameObject car, Vector3 target, float deltaTime)
    {
        if (car == null)
            return;

        Vector3 direction = target - car.transform.position;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        float currentAngle = car.transform.rotation.eulerAngles.z;
        float targetAngle = CarOrientationRules.GetFacingAngle(
            direction, spriteFacingAngle, currentAngle);
        float nextAngle = Mathf.MoveTowardsAngle(
            currentAngle, targetAngle, rotateSpeed * Mathf.Max(0f, deltaTime));
        car.transform.rotation = Quaternion.Euler(0f, 0f, nextAngle);
    }
}
