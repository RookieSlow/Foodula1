using NUnit.Framework;
using UnityEngine;

public class BrandArtResourcesTests
{
    [TestCase(TeamId.UK, "Brand/team_uk")]
    [TestCase(TeamId.DE, "Brand/team_de")]
    [TestCase(TeamId.IT, "Brand/team_it")]
    [TestCase(TeamId.US, "Brand/team_us")]
    [TestCase(TeamId.CN, "Brand/team_cn")]
    [TestCase(TeamId.JP, "Brand/team_jp")]
    public void EveryTeamUsesStableResourcePath(TeamId team, string expectedPath)
    {
        Assert.That(BrandArtResources.TryGetTeamLogoPath(team, out string path), Is.True);
        Assert.That(path, Is.EqualTo(expectedPath));
    }

    [Test]
    public void UnknownTeamHasNoBrandPath()
    {
        Assert.That(BrandArtResources.TryGetTeamLogoPath((TeamId)99, out string path), Is.False);
        Assert.That(path, Is.Empty);
        Assert.That(BrandArtResources.LoadTeamLogo((TeamId)99), Is.Null);
    }

    [Test]
    public void MenuBrandSpritesLoadFromResources()
    {
        Assert.That(BrandArtResources.LoadMainMenuBackground(), Is.Not.Null);
        Assert.That(BrandArtResources.LoadMainMenuLogo(), Is.Not.Null);
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void EveryTeamLogoLoadsFromResources(TeamId team)
    {
        Sprite logo = BrandArtResources.LoadTeamLogo(team);
        Assert.That(logo, Is.Not.Null, $"Missing brand logo for {team}");
    }
}
