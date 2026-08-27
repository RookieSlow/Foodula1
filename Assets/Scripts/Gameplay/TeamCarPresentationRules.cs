using UnityEngine;

/// <summary>
/// Presentation-only mapping between a team identity and its car artwork.
/// Gameplay stats remain owned by <see cref="TeamVehicleRules"/>; this class
/// keeps sprite lookup and fallback colors out of the race orchestrator.
/// </summary>
public static class TeamCarPresentationRules
{
    /// <summary>Number of supported team presentation slots in the car artwork array.</summary>
    public const int TeamCount = 6;

    /// <summary>Returns the authored car-sprite slot for a team, or -1 for an unknown value.</summary>
    public static int GetSpriteIndex(TeamId teamId)
    {
        int index = (int)teamId;
        return index >= 0 && index < TeamCount ? index : -1;
    }

    /// <summary>
    /// Resolves a configured team sprite without throwing for missing arrays,
    /// short arrays, or an unrecognised team value.
    /// </summary>
    public static bool TryGetSprite(Sprite[] sprites, TeamId teamId, out Sprite sprite)
    {
        int index = GetSpriteIndex(teamId);
        if (sprites != null && index >= 0 && index < sprites.Length && sprites[index] != null)
        {
            sprite = sprites[index];
            return true;
        }

        sprite = null;
        return false;
    }

    /// <summary>Returns the stable fallback color used when team art is absent.</summary>
    public static Color GetFallbackColor(TeamId teamId)
    {
        switch (teamId)
        {
            case TeamId.UK:
                return new Color(0.85f, 0.2f, 0.2f);
            case TeamId.DE:
                return new Color(0.2f, 0.35f, 0.85f);
            case TeamId.IT:
                return new Color(0.2f, 0.7f, 0.35f);
            case TeamId.US:
                return new Color(0.9f, 0.7f, 0.15f);
            case TeamId.CN:
                return new Color(0.95f, 0.4f, 0.1f);
            case TeamId.JP:
                return new Color(0.3f, 0.8f, 0.85f);
            default:
                return Color.white;
        }
    }

    /// <summary>
    /// Returns the compact, stable team code used by runtime race badges.
    /// The code remains readable even before authored flag/avatar art exists.
    /// </summary>
    public static string GetBadgeCode(TeamId teamId)
    {
        switch (teamId)
        {
            case TeamId.UK:
                return "UK";
            case TeamId.DE:
                return "DE";
            case TeamId.IT:
                return "IT";
            case TeamId.US:
                return "US";
            case TeamId.CN:
                return "CN";
            case TeamId.JP:
                return "JP";
            default:
                return "NA";
        }
    }

    /// <summary>
    /// Returns a darker badge fill derived from the team color so it reads as a
    /// deliberate overlay rather than a second car sprite.
    /// </summary>
    public static Color GetBadgeColor(TeamId teamId)
    {
        Color baseColor = GetFallbackColor(teamId);
        if (GetSpriteIndex(teamId) < 0)
            return new Color(0.16f, 0.2f, 0.27f, 0.96f);

        return new Color(
            Mathf.Clamp01(baseColor.r * 0.72f),
            Mathf.Clamp01(baseColor.g * 0.72f),
            Mathf.Clamp01(baseColor.b * 0.72f),
            0.96f);
    }

    /// <summary>Chooses a high-contrast label color for the badge fill.</summary>
    public static Color GetBadgeTextColor(TeamId teamId)
    {
        Color color = GetBadgeColor(teamId);
        float luminance = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
        return luminance >= 0.44f ? Color.black : Color.white;
    }
}
