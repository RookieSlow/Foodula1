using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

public sealed class TechTreeProfileStoreTests
{
    private static readonly MethodInfo SaveAllFrom = typeof(TechTreeProfileStore).GetMethod(
        "SaveAllFrom", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo RestoreOrCreate = typeof(TechTreeProfileStore).GetMethod(
        "RestoreOrCreate", BindingFlags.Static | BindingFlags.NonPublic);

    [Test]
    public void RestoreOrCreate_PrefersCurrentKeyWithoutRewritingOrReadingLegacy()
    {
        var database = TechTreeDatabaseFactory.CreateDefault();
        var original = new TechTreeState(TeamId.US, 1350);
        original.unlockedNodeIds.Add("us-l1-drive-thru");
        string payload = TechTreeProfileCodec.Encode(original);
        var reads = new List<string>();
        var saves = new List<TechTreeState>();

        TechTreeState restored = Restore(TeamId.US, database,
            key => key == "Foodula1.TechTree.US" || key == "Foodular1.TechTree.US",
            key => { reads.Add(key); return payload; }, saves.Add);

        Assert.That(reads, Is.EqualTo(new[] { "Foodula1.TechTree.US" }));
        Assert.That(saves, Is.Empty);
        Assert.That(restored.rpBalance, Is.EqualTo(1350));
        Assert.That(restored.unlockedNodeIds, Is.EquivalentTo(original.unlockedNodeIds));
    }

    [Test]
    public void RestoreOrCreate_MigratesLegacyProfileWithoutChangingProgress()
    {
        var database = TechTreeDatabaseFactory.CreateDefault();
        var original = new TechTreeState(TeamId.UK, 4200);
        original.unlockedNodeIds.Add("common-l1-heat-coating");
        original.activeNodeIds.Add("common-l1-heat-coating");
        string payload = TechTreeProfileCodec.Encode(original);
        var reads = new List<string>();
        var saves = new List<TechTreeState>();

        TechTreeState restored = Restore(TeamId.UK, database,
            key => key == "Foodular1.TechTree.UK",
            key => { reads.Add(key); return payload; }, saves.Add);

        Assert.That(reads, Is.EqualTo(new[] { "Foodular1.TechTree.UK" }));
        Assert.That(saves, Is.EqualTo(new[] { restored }));
        Assert.That(restored.rpBalance, Is.EqualTo(4200));
        Assert.That(restored.unlockedNodeIds, Is.EquivalentTo(original.unlockedNodeIds));
        Assert.That(restored.activeNodeIds, Is.EquivalentTo(original.activeNodeIds));
    }

    [Test]
    public void RestoreOrCreate_InvalidCurrentProfileKeepsCurrentKeyPrecedence()
    {
        var database = TechTreeDatabaseFactory.CreateDefault();
        var reads = new List<string>();
        var saves = new List<TechTreeState>();

        TechTreeState restored = Restore(TeamId.JP, database,
            key => key == "Foodula1.TechTree.JP" || key == "Foodular1.TechTree.JP",
            key => { reads.Add(key); return string.Empty; }, saves.Add);

        Assert.That(reads, Is.EqualTo(new[] { "Foodula1.TechTree.JP" }));
        Assert.That(saves, Is.EqualTo(new[] { restored }));
        Assert.That(restored.teamId, Is.EqualTo(TeamId.JP));
        Assert.That(restored.unlockedNodeIds, Is.Not.Empty);
    }

    [Test]
    public void RestoreOrCreate_NoSavedKeyBuildsAndSavesOneDemoProfile()
    {
        var database = TechTreeDatabaseFactory.CreateDefault();
        var saves = new List<TechTreeState>();
        int reads = 0;

        TechTreeState restored = Restore(TeamId.CN, database,
            _ => false, _ => { reads++; return string.Empty; }, saves.Add);

        Assert.That(reads, Is.Zero);
        Assert.That(saves, Is.EqualTo(new[] { restored }));
        Assert.That(restored.teamId, Is.EqualTo(TeamId.CN));
        Assert.That(restored.unlockedNodeIds, Is.Not.Empty);
    }

    [Test]
    public void SaveAllFrom_VisitsEveryOriginalProfileWhenSaveReassignsCachedKeys()
    {
        var uk = new TechTreeState(TeamId.UK, 100);
        var us = new TechTreeState(TeamId.US, 200);
        var profiles = new Dictionary<TeamId, TechTreeState>
        {
            { TeamId.UK, uk },
            { TeamId.US, us }
        };
        var visited = new List<TechTreeState>();

        Invoke(profiles, state =>
        {
            visited.Add(state);
            profiles[state.teamId] = state;
        });

        Assert.That(visited, Is.EquivalentTo(new[] { uk, us }));
        Assert.That(profiles[TeamId.UK], Is.SameAs(uk));
        Assert.That(profiles[TeamId.US], Is.SameAs(us));
    }

    [Test]
    public void SaveAllFrom_DoesNotPullNewProfilesIntoAnActiveSavePass()
    {
        var uk = new TechTreeState(TeamId.UK, 100);
        var us = new TechTreeState(TeamId.US, 200);
        var profiles = new Dictionary<TeamId, TechTreeState> { { TeamId.UK, uk } };
        var visited = new List<TechTreeState>();

        Invoke(profiles, state =>
        {
            visited.Add(state);
            profiles[TeamId.US] = us;
        });

        Assert.That(visited, Is.EqualTo(new[] { uk }));
        Assert.That(profiles[TeamId.US], Is.SameAs(us));
    }

    private static void Invoke(Dictionary<TeamId, TechTreeState> profiles, Action<TechTreeState> save)
    {
        Assert.That(SaveAllFrom, Is.Not.Null);
        SaveAllFrom.Invoke(null, new object[] { profiles, save });
    }

    private static TechTreeState Restore(
        TeamId teamId,
        TechTreeDatabase database,
        Func<string, bool> hasKey,
        Func<string, string> read,
        Action<TechTreeState> save)
    {
        Assert.That(RestoreOrCreate, Is.Not.Null);
        return (TechTreeState)RestoreOrCreate.Invoke(
            null, new object[] { teamId, database, hasKey, read, save });
    }
}
