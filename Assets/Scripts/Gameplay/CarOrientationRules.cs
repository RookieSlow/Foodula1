using UnityEngine;

/// <summary>Pure 2D car-facing calculations shared by runtime movement and EditMode tests.</summary>
public static class CarOrientationRules
{
    /// <summary>
    /// Returns the world-space Z rotation for a track direction after removing the
    /// sprite's authored facing angle. A zero direction preserves the supplied fallback.
    /// </summary>
    public static float GetFacingAngle(
        Vector2 direction,
        float spriteFacingAngle,
        float fallbackAngle = 0f)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return fallbackAngle;

        float trackAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return trackAngle - spriteFacingAngle;
    }
}
