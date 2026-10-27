using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Explicit fixture ownership. Selection never changes or disables a test.</summary>
public static class ModularRegressionCatalog
{
    public const string AssetPath = "Assets/Tests/Editor/RegressionModules.json";
    [Serializable] public sealed class Catalog { public int version; public Module[] modules; }
    [Serializable] public sealed class Module
    {
        public string id, label;
        public bool longRunning;
        public string[] dependencies, fixtures;
    }
    [Serializable] public sealed class Plan
    {
        public string[] modules, testNames, missingFixtures, staleFixtures;
        public int discovered, selected, excluded, longSelected;
        public bool full;
    }

    public static Catalog Load()
    {
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
        if (asset == null) throw new InvalidOperationException("Missing module manifest: " + AssetPath);
        var catalog = JsonUtility.FromJson<Catalog>(asset.text);
        Validate(catalog);
        return catalog;
    }

    public static void Validate(Catalog catalog)
    {
        if (catalog == null || catalog.version != 1 || catalog.modules == null || catalog.modules.Length == 0)
            throw new ArgumentException("Invalid regression module manifest.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var owners = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in catalog.modules)
        {
            if (module == null || string.IsNullOrWhiteSpace(module.id) ||
                module.id == "full" || module.id == "fast-all" || !ids.Add(module.id))
                throw new ArgumentException("Empty, reserved or duplicate module id.");
            if (module.fixtures == null || module.fixtures.Length == 0)
                throw new ArgumentException("Module has no fixtures: " + module.id);
            foreach (string fixture in module.fixtures)
                if (string.IsNullOrWhiteSpace(fixture) || !owners.Add(fixture))
                    throw new ArgumentException("Empty or multiply owned fixture: " + fixture);
        }
        foreach (var module in catalog.modules)
            foreach (string dependency in module.dependencies ?? Array.Empty<string>())
                if (!ids.Contains(dependency)) throw new ArgumentException("Unknown dependency: " + dependency);
    }

    // Longest match also supports future namespaces/nested fixtures without parsing test arguments.
    public static string OwnerFixture(string testName, IEnumerable<string> fixtures)
    {
        string best = null;
        foreach (string fixture in fixtures)
            if ((best == null || fixture.Length > best.Length) && testName.StartsWith(fixture + ".", StringComparison.Ordinal))
                best = fixture;
        return best;
    }

    public static Plan Select(Catalog catalog, IEnumerable<string> discoveredNames, IEnumerable<string> requested)
    {
        Validate(catalog);
        var names = discoveredNames.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var wanted = new HashSet<string>(requested ?? Array.Empty<string>(), StringComparer.Ordinal);
        if (wanted.Count == 0) throw new ArgumentException("Select at least one module.");
        bool full = wanted.Contains("full"), fast = wanted.Contains("fast-all");
        if ((full || fast) && wanted.Count != 1) throw new ArgumentException("Do not mix aggregate and individual modules.");
        var byId = catalog.modules.ToDictionary(m => m.id, StringComparer.Ordinal);
        if (full || fast)
            wanted = new HashSet<string>(catalog.modules.Where(m => full || !m.longRunning).Select(m => m.id), StringComparer.Ordinal);
        foreach (string id in wanted.ToArray())
            if (!byId.ContainsKey(id)) throw new ArgumentException("Unknown regression module: " + id);
        // Iterative closure terminates even if future module dependencies contain cycles.
        var queue = new Queue<string>(wanted);
        while (queue.Count > 0)
            foreach (string dependency in byId[queue.Dequeue()].dependencies ?? Array.Empty<string>())
                if (wanted.Add(dependency)) queue.Enqueue(dependency);
        if (fast && wanted.Any(id => byId[id].longRunning))
            throw new ArgumentException("A fast module dependency includes a long-running module.");
        var fixtureOwners = catalog.modules.SelectMany(m => m.fixtures.Select(f => new { fixture = f, module = m }))
            .ToDictionary(x => x.fixture, x => x.module, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var missing = new HashSet<string>(StringComparer.Ordinal);
        var selected = new List<string>();
        int longCount = 0;
        foreach (string name in names)
        {
            string fixture = OwnerFixture(name, fixtureOwners.Keys);
            if (fixture == null) { missing.Add(name); if (full) selected.Add(name); continue; }
            seen.Add(fixture);
            if (!wanted.Contains(fixtureOwners[fixture].id)) continue;
            selected.Add(name);
            if (fixtureOwners[fixture].longRunning) longCount++;
        }
        var stale = fixtureOwners.Keys.Where(f => !seen.Contains(f)).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        var plan = new Plan { full = full, modules = wanted.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            discovered = names.Length, selected = selected.Count, excluded = names.Length - selected.Count,
            longSelected = longCount, testNames = selected.ToArray(), missingFixtures = missing.ToArray(), staleFixtures = stale };
        // Full remains available to run newly added tests before ownership has been assigned.
        if (!full && (missing.Count > 0 || stale.Length > 0))
            throw new InvalidOperationException("Module inventory drift. Unmapped cases: " + missing.Count +
                "; stale fixtures: " + string.Join(", ", stale) + ". Update " + AssetPath + "; full remains available.");
        if (selected.Count == 0) throw new InvalidOperationException("No tests selected; refusing an empty success.");
        return plan;
    }
}
