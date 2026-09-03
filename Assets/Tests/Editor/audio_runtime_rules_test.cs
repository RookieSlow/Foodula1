using NUnit.Framework;

public class AudioRuntimeRulesTests
{
    [TestCase(1f, 1f, 1f)]
    [TestCase(0.5f, 0.8f, 0.4f)]
    [TestCase(-1f, 0.8f, 0f)]
    [TestCase(2f, 0.5f, 0.5f)]
    public void EffectiveVolumeClampsAndCombinesMasterWithChannel(
        float master, float channel, float expected)
    {
        Assert.That(AudioRuntimeRules.GetEffectiveLinearVolume(master, channel),
            Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void LinearVolumeConvertsToExpectedDecibelsAndSilenceFloor()
    {
        Assert.That(AudioRuntimeRules.LinearToDecibels(1f), Is.EqualTo(0f).Within(0.001f));
        Assert.That(AudioRuntimeRules.LinearToDecibels(0.5f), Is.EqualTo(-6.0206f).Within(0.001f));
        Assert.That(AudioRuntimeRules.LinearToDecibels(0f), Is.EqualTo(-80f));
    }

    [Test]
    public void ResourcePathsMatchTheRuntimeFolderContract()
    {
        Assert.That(AudioRuntimeRules.GetMusicPath(AudioEventNames.MenuMusic),
            Is.EqualTo("Audio/Music/menu"));
        Assert.That(AudioRuntimeRules.GetMusicPath(AudioEventNames.RaceMusic),
            Is.EqualTo("Audio/Music/race"));
        Assert.That(AudioRuntimeRules.GetSfxPath(AudioEventNames.HeatPay),
            Is.EqualTo("Audio/SFX/heat_pay"));
        Assert.That(AudioRuntimeRules.GetSfxPath("../outside"), Is.Null);
        Assert.That(AudioRuntimeRules.GetSfxPath("nested/event"), Is.Null);
    }

    [Test]
    public void SceneMusicMappingIsExplicitAndDoesNotGuessUnknownScenes()
    {
        Assert.That(AudioRuntimeRules.GetMusicNameForScene("MainMenu"), Is.EqualTo("menu"));
        Assert.That(AudioRuntimeRules.GetMusicNameForScene("Race"), Is.EqualTo("race"));
        Assert.That(AudioRuntimeRules.GetMusicNameForScene("Credits"), Is.Null);
    }

    [Test]
    public void EventThrottleAllowsFirstAndBoundaryButRejectsBurst()
    {
        var throttle = new AudioEventThrottle();

        Assert.That(throttle.ShouldPlay(AudioEventNames.CarHop, 10f, 0.1f), Is.True);
        Assert.That(throttle.ShouldPlay(AudioEventNames.CarHop, 10.05f, 0.1f), Is.False);
        Assert.That(throttle.ShouldPlay(AudioEventNames.CardDraw, 10.05f, 0.1f), Is.True);
        Assert.That(throttle.ShouldPlay(AudioEventNames.CarHop, 10.1f, 0.1f), Is.True);
    }

    [Test]
    public void EventThrottleRecoversWhenClockMovesBackwards()
    {
        var throttle = new AudioEventThrottle();
        Assert.That(throttle.ShouldPlay(AudioEventNames.HeatCool, 20f, 1f), Is.True);
        Assert.That(throttle.ShouldPlay(AudioEventNames.HeatCool, 0f, 1f), Is.True);
    }
}
