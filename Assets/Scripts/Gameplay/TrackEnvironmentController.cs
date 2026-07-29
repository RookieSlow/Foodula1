using UnityEngine;

/// <summary>
/// Selects the race-scene background that matches the configured track.
/// All layout sprites use the same 16:9 normalized coordinate space as the
/// JSON track data, so the background and runtime nodes remain aligned.
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class TrackEnvironmentController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private GameConfigSO config;
    [SerializeField] private SpriteRenderer backgroundRenderer;

    [Header("Track Layout Sprites")]
    [SerializeField] private Sprite silverstone;
    [SerializeField] private Sprite nurburgringGrandPrix;
    [SerializeField] private Sprite monza;
    [SerializeField] private Sprite indianapolis;
    [SerializeField] private Sprite shanghai;
    [SerializeField] private Sprite suzuka;
    [SerializeField] private Sprite nurburgringEndurance;
    [SerializeField] private Sprite leMansOldMulsanne;

    private void Awake()
    {
        ApplyConfiguredBackground();
    }

    /// <summary>
    /// Applies the sprite associated with the current track ID and sizes it to
    /// the same world-space rectangle used by TrackManager.
    /// </summary>
    public void ApplyConfiguredBackground()
    {
        if (config == null || backgroundRenderer == null)
        {
            Debug.LogWarning(
                "[TrackEnvironmentController] Config or background renderer is missing.",
                this);
            return;
        }

        Sprite selectedSprite = ResolveSprite(config.trackId);
        if (selectedSprite == null)
        {
            Debug.LogWarning(
                $"[TrackEnvironmentController] No background is configured for track '{config.trackId}'.",
                this);
            return;
        }

        backgroundRenderer.sprite = selectedSprite;
        FitBackgroundToTrackWorld();
    }

    private Sprite ResolveSprite(string trackId)
    {
        switch (trackId)
        {
            case "silverstone_afternoon_tea":
                return silverstone;
            case "nurburgring_bier":
                return nurburgringGrandPrix;
            case "monza_pasta":
                return monza;
            case "indianapolis_burger":
                return indianapolis;
            case "shanghai_dim_sum":
                return shanghai;
            case "suzuka_sushi":
                return suzuka;
            case "nurburgring_24h_endurance":
                return nurburgringEndurance;
            case "le_mans_old_mulsanne":
                return leMansOldMulsanne;
            default:
                return null;
        }
    }

    private void FitBackgroundToTrackWorld()
    {
        Vector2 spriteSize = backgroundRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            return;
        }

        float worldWidth = config.trackWorldSize;
        float worldHeight = config.trackWorldHeight > 0f
            ? config.trackWorldHeight
            : worldWidth;

        backgroundRenderer.transform.localScale = new Vector3(
            worldWidth / spriteSize.x,
            worldHeight / spriteSize.y,
            1f);
    }
}
