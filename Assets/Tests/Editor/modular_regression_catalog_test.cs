using System;
using System.Linq;
using NUnit.Framework;

public sealed class ModularRegressionCatalogTests
{
    private static ModularRegressionCatalog.Catalog Fixture()
    {
        return new ModularRegressionCatalog.Catalog { version = 1, modules = new[] {
            new ModularRegressionCatalog.Module { id = "core", fixtures = new[] { "CoreTests" } },
            new ModularRegressionCatalog.Module { id = "tech", dependencies = new[] { "core" }, fixtures = new[] { "TechTests" } },
            new ModularRegressionCatalog.Module { id = "drivers", fixtures = new[] { "DriverTests" } },
            new ModularRegressionCatalog.Module { id = "long-races", longRunning = true, fixtures = new[] { "LongTests" } }
        }};
    }
    private static readonly string[] Names = { "CoreTests.One", "TechTests.Value(1)", "TechTests.Value(2)", "DriverTests.Action", "LongTests.Race" };

    [Test] public void SelectedModulesIncludeDependenciesAndDeduplicateParameterizedCases()
    {
        var plan = ModularRegressionCatalog.Select(Fixture(), Names.Concat(Names), new[] { "tech", "drivers", "tech" });
        Assert.That(plan.selected, Is.EqualTo(4)); Assert.That(plan.discovered, Is.EqualTo(5));
        Assert.That(plan.testNames, Does.Contain("TechTests.Value(2)")); Assert.That(plan.longSelected, Is.Zero);
        Assert.That(plan.modules, Does.Contain("core")); Assert.That(plan.excluded, Is.EqualTo(1));
    }
    [Test] public void FastAndLongPartitionsHaveNoOverlapAndTheirUnionIsFull()
    {
        var catalog = Fixture();
        var fast = ModularRegressionCatalog.Select(catalog, Names, new[] { "fast-all" });
        var slow = ModularRegressionCatalog.Select(catalog, Names, new[] { "long-races" });
        var full = ModularRegressionCatalog.Select(catalog, Names, new[] { "full" });
        Assert.That(fast.testNames.Intersect(slow.testNames), Is.Empty);
        Assert.That(fast.testNames.Union(slow.testNames).OrderBy(n => n), Is.EqualTo(full.testNames));
        Assert.That(slow.longSelected, Is.EqualTo(1)); Assert.That(full.selected, Is.EqualTo(5));
    }
    [Test] public void NewUnmappedFixtureBlocksFocusedRunButFullStillIncludesIt()
    {
        var names = Names.Concat(new[] { "NewTests.Unassigned" });
        Assert.Throws<InvalidOperationException>(() => ModularRegressionCatalog.Select(Fixture(), names, new[] { "tech" }));
        var full = ModularRegressionCatalog.Select(Fixture(), names, new[] { "full" });
        Assert.That(full.selected, Is.EqualTo(6)); Assert.That(full.missingFixtures, Is.EqualTo(new[] { "NewTests.Unassigned" }));
    }
    [Test] public void StaleConfiguredFixtureCannotSilentlyShrinkAFocusedRun()
    {
        Assert.Throws<InvalidOperationException>(() => ModularRegressionCatalog.Select(Fixture(), Names.Where(n => !n.StartsWith("DriverTests.")), new[] { "tech" }));
    }
    [TestCase("unknown")]
    [TestCase("")]
    public void UnknownOrEmptySelectionNeverBecomesRunAll(string id)
    {
        Assert.Throws<ArgumentException>(() => ModularRegressionCatalog.Select(Fixture(), Names, id.Length == 0 ? new string[0] : new[] { id }));
    }
    [TestCase("full")]
    [TestCase("fast-all")]
    public void AggregateModesCannotBeMixedWithIndividualModules(string mode)
    {
        Assert.Throws<ArgumentException>(() => ModularRegressionCatalog.Select(Fixture(), Names, new[] { mode, "tech" }));
    }
    [Test] public void DuplicateFixtureOwnershipFailsValidation()
    {
        var catalog = Fixture(); catalog.modules[2].fixtures = new[] { "TechTests" };
        Assert.Throws<ArgumentException>(() => ModularRegressionCatalog.Validate(catalog));
    }
    [Test] public void UnknownDependenciesAndAccidentalLongFastDependencyFailClosed()
    {
        var catalog = Fixture(); catalog.modules[1].dependencies = new[] { "missing" };
        Assert.Throws<ArgumentException>(() => ModularRegressionCatalog.Validate(catalog));
        catalog.modules[1].dependencies = new[] { "long-races" };
        Assert.Throws<ArgumentException>(() => ModularRegressionCatalog.Select(catalog, Names, new[] { "fast-all" }));
    }
    [Test] public void FixtureMatchingUsesExactBoundaryAndSupportsNamespaces()
    {
        Assert.That(ModularRegressionCatalog.OwnerFixture("TechTestsExtra.One", new[] { "TechTests" }), Is.Null);
        Assert.That(ModularRegressionCatalog.OwnerFixture("Space.TechTests.Value(\"x.y\")", new[] { "Space.TechTests" }), Is.EqualTo("Space.TechTests"));
    }
    [Test] public void RealCatalogHasExclusiveLongRaceFixtureAndCriticalModules()
    {
        var catalog = ModularRegressionCatalog.Load();
        Assert.That(catalog.modules.Select(m => m.id), Does.Contain("tech").And.Contain("drivers").And.Contain("audio").And.Contain("ui"));
        var owner = catalog.modules.Single(m => m.fixtures.Contains("RaceGameLoopCoroutineTests"));
        Assert.That(owner.longRunning, Is.True);
        Assert.That(catalog.modules.Where(m => !m.longRunning).SelectMany(m => m.fixtures), Does.Not.Contain("RaceGameLoopCoroutineTests"));
    }
}
