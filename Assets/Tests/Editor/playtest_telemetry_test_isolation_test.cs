using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class PlaytestTelemetryTestIsolationTests
{
    internal const string OverrideKey = "Foodula1.Tests.PlaytestLogRoot";
    private string saved;

    [SetUp]
    public void SetUp() => saved = SessionState.GetString(OverrideKey, string.Empty);

    [TearDown]
    public void TearDown() => SessionState.SetString(OverrideKey, saved);

    internal static string AllowedRoot => Path.GetFullPath(Path.Combine(
        Application.dataPath, "..", "Temp", "Foodula1Regression"));

    private static string Resolve() => (string)typeof(PlaytestTelemetryService)
        .GetMethod("ResolveSessionLogRoot", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, null);

    [Test]
    public void AbsentOverridePreservesProductionDefault()
    {
        SessionState.EraseString(OverrideKey);
        Assert.That(Resolve(), Is.EqualTo(PlaytestTelemetryService.DefaultLogRoot));
    }

    [Test]
    public void ScopedAbsoluteOverrideDoesNotChangePublicDefault()
    {
        string production = PlaytestTelemetryService.DefaultLogRoot;
        string scoped = Path.Combine(AllowedRoot, "isolation", "session");
        SessionState.SetString(OverrideKey, scoped);
        Assert.That(Resolve(), Is.EqualTo(scoped));
        Assert.That(PlaytestTelemetryService.DefaultLogRoot, Is.EqualTo(production));
    }

    [TestCase("root")]
    [TestCase("parent")]
    [TestCase("sibling")]
    [TestCase("relative")]
    [TestCase("player")]
    public void UnsafeOverridesAreRejectedWithoutFallingBackToPlayerStorage(string kind)
    {
        string path = kind == "root" ? AllowedRoot
            : kind == "parent" ? Path.Combine(AllowedRoot, "..", "outside")
            : kind == "sibling" ? AllowedRoot + "-outside"
            : kind == "relative" ? Path.Combine("Temp", "Foodula1Regression", "relative")
            : PlaytestTelemetryService.DefaultLogRoot;
        SessionState.SetString(OverrideKey, path);
        var failure = Assert.Throws<TargetInvocationException>(() => Resolve());
        Assert.That(failure.InnerException, Is.TypeOf<InvalidOperationException>());
    }
}
