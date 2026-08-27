using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RaceEventFXTests
{
    [Test]
    public void SpinOutDefaultsMatchVisualDesign()
    {
        GameObject host = new GameObject("RaceEventFXTest");
        try
        {
            RaceEventFX effect = host.AddComponent<RaceEventFX>();

            Assert.That(effect.spinOutDuration, Is.EqualTo(1f));
            Assert.That(effect.spinOutRotationDegrees, Is.EqualTo(360f));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void SpinProgressClampsToEffectWindow()
    {
        Assert.That(RaceEventPresentationRules.GetSpinProgress(-1f, 1f), Is.EqualTo(0f));
        Assert.That(RaceEventPresentationRules.GetSpinProgress(0.5f, 1f), Is.EqualTo(0.5f));
        Assert.That(RaceEventPresentationRules.GetSpinProgress(2f, 1f), Is.EqualTo(1f));
        Assert.That(RaceEventPresentationRules.GetSpinProgress(0f, 0f), Is.EqualTo(1f));
    }

    [Test]
    public void SpinRotationStartsAtZeroAndCompletesConfiguredAngle()
    {
        Assert.That(RaceEventPresentationRules.GetSpinRotation(0f, 360f), Is.EqualTo(0f));
        Assert.That(RaceEventPresentationRules.GetSpinRotation(0.5f, 360f), Is.EqualTo(315f));
        Assert.That(RaceEventPresentationRules.GetSpinRotation(1f, 360f), Is.EqualTo(360f));
        Assert.That(RaceEventPresentationRules.GetSpinRotation(0.5f, -10f), Is.EqualTo(0f));
    }

    [Test]
    public void SlowMotionScopeRestoresNormalGameplaySpeed()
    {
        GameObject host = new GameObject("RaceEventFXTimeScaleTest");
        float previousTimeScale = Time.timeScale;
        try
        {
            RaceEventFX effect = host.AddComponent<RaceEventFX>();
            MethodInfo beginSlowMotion = typeof(RaceEventFX).GetMethod(
                "BeginSlowMotion",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo endSlowMotion = typeof(RaceEventFX).GetMethod(
                "EndSlowMotion",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(beginSlowMotion, Is.Not.Null);
            Assert.That(endSlowMotion, Is.Not.Null);

            Time.timeScale = 1f;
            beginSlowMotion.Invoke(effect, new object[] { 0.28f });
            Assert.That(Time.timeScale, Is.EqualTo(0.28f).Within(0.001f));

            endSlowMotion.Invoke(effect, null);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void TailwindBonusMovementUsesItsOwnScopedSlowMotion()
    {
        GameObject host = new GameObject("RaceEventFXTailwindMovementTimeScaleTest");
        float previousTimeScale = Time.timeScale;
        try
        {
            RaceEventFX effect = host.AddComponent<RaceEventFX>();
            Time.timeScale = 1f;

            effect.BeginSlipstreamBonusMovementSlowMotion();
            Assert.That(Time.timeScale, Is.EqualTo(effect.slipstreamTimeScale).Within(0.001f));

            effect.EndSlipstreamBonusMovementSlowMotion();
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            Object.DestroyImmediate(host);
        }
    }
}
