using UnityEngine;

/// <summary>
/// Single runtime boundary for optional Foodula1 brand art.
/// Callers retain their existing text/colour fallback when a sprite is absent.
/// </summary>
public static class BrandArtResources
{
    public const string MainMenuBackgroundPath = "Brand/main_menu_background";
    public const string MainMenuLogoPath = "Brand/foodula1_logo";

    public static Sprite LoadMainMenuBackground() => Resources.Load<Sprite>(MainMenuBackgroundPath);

    public static Sprite LoadMainMenuLogo() => Resources.Load<Sprite>(MainMenuLogoPath);

    public static Sprite LoadTeamLogo(TeamId team) =>
        TryGetTeamLogoPath(team, out string path) ? Resources.Load<Sprite>(path) : null;

    public static bool TryGetTeamLogoPath(TeamId team, out string path)
    {
        switch (team)
        {
            case TeamId.UK: path = "Brand/team_uk"; return true;
            case TeamId.DE: path = "Brand/team_de"; return true;
            case TeamId.IT: path = "Brand/team_it"; return true;
            case TeamId.US: path = "Brand/team_us"; return true;
            case TeamId.CN: path = "Brand/team_cn"; return true;
            case TeamId.JP: path = "Brand/team_jp"; return true;
            default: path = string.Empty; return false;
        }
    }
}
