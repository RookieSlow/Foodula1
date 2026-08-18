using NUnit.Framework;
using UnityEngine;

public class TeamCarPresentationTests
{
    [TestCase(TeamId.UK, 0)]
    [TestCase(TeamId.DE, 1)]
    [TestCase(TeamId.IT, 2)]
    [TestCase(TeamId.US, 3)]
    [TestCase(TeamId.CN, 4)]
    [TestCase(TeamId.JP, 5)]
    public void EveryTeamUsesItsStableSpriteSlot(TeamId teamId, int expectedIndex)
    {
        Assert.That(TeamCarPresentationRules.GetSpriteIndex(teamId), Is.EqualTo(expectedIndex));
    }

    [Test]
    public void MissingSpriteFallsBackWithoutThrowing()
    {
        var sprites = new Sprite[TeamCarPresentationRules.TeamCount];

        Sprite resolved;
        bool found = TeamCarPresentationRules.TryGetSprite(sprites, TeamId.CN, out resolved);

        Assert.That(found, Is.False);
        Assert.That(resolved, Is.Null);
    }

    [Test]
    public void ShortSpriteArrayFallsBackWithoutThrowing()
    {
        Sprite resolved;
        bool found = TeamCarPresentationRules.TryGetSprite(new Sprite[1], TeamId.CN, out resolved);

        Assert.That(found, Is.False);
        Assert.That(resolved, Is.Null);
    }

    [Test]
    public void ConfiguredSpriteResolvesFromTeamSlot()
    {
        var sprites = new Sprite[TeamCarPresentationRules.TeamCount];
        var chinaSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
        sprites[(int)TeamId.CN] = chinaSprite;

        try
        {
            Sprite resolved;
            bool found = TeamCarPresentationRules.TryGetSprite(sprites, TeamId.CN, out resolved);

            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(chinaSprite));
        }
        finally
        {
            Object.DestroyImmediate(chinaSprite);
        }
    }

    [Test]
    public void UnknownTeamUsesNeutralFallbackColor()
    {
        Color color = TeamCarPresentationRules.GetFallbackColor((TeamId)99);

        Assert.That(color, Is.EqualTo(Color.white));
    }
}
