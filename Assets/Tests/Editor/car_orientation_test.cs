using NUnit.Framework;
using UnityEngine;
using UnityEditor;

public class CarOrientationTests
{
    [Test]
    public void DefaultCarSpriteFacingAngleMatchesRightFacingSprites()
    {
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        try
        {
            Assert.That(config.carSpriteFacingAngle, Is.EqualTo(0f),
                "Car sprites are authored with the nose pointing right (+X). Their default offset must be zero.");
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [TestCase(1f, 0f, 0f)]
    [TestCase(0f, 1f, 90f)]
    [TestCase(-1f, 0f, 180f)]
    [TestCase(0f, -1f, -90f)]
    public void TrackTangentAnglesUseUnity2DForwardConvention(float x, float y, float expected)
    {
        float angle = CarOrientationRules.GetFacingAngle(new Vector2(x, y), 0f);

        Assert.That(Mathf.DeltaAngle(angle, expected), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void SpriteFacingOffsetIsRemovedFromTrackAngle()
    {
        float angle = CarOrientationRules.GetFacingAngle(Vector2.right, 90f);

        Assert.That(Mathf.DeltaAngle(angle, -90f), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ZeroDirectionPreservesFallbackAngle()
    {
        Assert.That(CarOrientationRules.GetFacingAngle(Vector2.zero, 45f, 37f), Is.EqualTo(37f));
    }

    [TestCase("Assets/Scripts/Config/MVPGameConfig.asset")]
    [TestCase("Assets/Scripts/Config/SilverstoneGameConfig.asset")]
    public void SerializedConfigsMatchRightFacingCarSprites(string path)
    {
        var config = AssetDatabase.LoadAssetAtPath<GameConfigSO>(path);

        Assert.That(config, Is.Not.Null, $"Missing game config at {path}");
        Assert.That(config.carSpriteFacingAngle, Is.EqualTo(0f));
    }
}
