using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>Demo construction only: no storage calls, scene startup or profile reset.</summary>
public class DemoTechnologyProfileTests
{
    private static readonly string[] Standard =
    {
        "common-l1-heat-coating", "common-l1-lightweight-chassis",
        "common-l1-track-memory", "common-l1-expanded-tank"
    };
    private static readonly string[] Ev =
    {
        "cn-ev-l1-heat-pump", "cn-ev-l1-pmsm",
        "cn-ev-l1-torque-vector", "cn-ev-l1-solid-state"
    };

    private static string Unique(TeamId team)
    {
        switch (team)
        {
            case TeamId.UK: return "uk-l1-fish-and-chips";
            case TeamId.DE: return "de-l1-schwarzbier-fuel";
            case TeamId.IT: return "it-l1-pizza-sottile";
            case TeamId.US: return "us-l1-drive-thru";
            case TeamId.CN: return "cn-l1-yin-yang-tea";
            case TeamId.JP: return "jp-l1-nigiri";
            default: throw new ArgumentOutOfRangeException(nameof(team));
        }
    }

    private static List<string> Expected(TeamId team)
    {
        var result = new List<string>(team == TeamId.CN ? Ev : Standard);
        result.Add(Unique(team));
        return result;
    }

    private static void AssertLoadout(TechTreeState state, TeamId team,
        IEnumerable<string> expected, int rp)
    {
        Assert.AreEqual(team, state.teamId);
        Assert.AreEqual(rp, state.rpBalance);
        CollectionAssert.AreEquivalent(expected, state.unlockedNodeIds);
        CollectionAssert.AreEquivalent(expected, state.activeNodeIds);
        Assert.AreNotSame(state.unlockedNodeIds, state.activeNodeIds);
        Assert.IsFalse(state.fishAndChipsUsed);
        Assert.IsFalse(state.grillSpezialUsed);
        Assert.IsFalse(state.heatReductionUsedThisLap);
        Assert.AreEqual(-1, state.schwarzbierFuelLastLap);
        Assert.AreEqual(BrothType.None, state.brothSelection);
        Assert.IsNull(state.sunNeverSetsTarget);
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void SixTeamsRetainExactIdsAndNineThousandRp(TeamId team)
    {
        var db = TechTreeDatabaseFactory.CreateDefault();
        string catalogueBefore = CatalogueSnapshot(db);
        var state = TechTreeRules.CreateDemoProfile(team, db);
        AssertLoadout(state, team, Expected(team), 9000);
        Assert.AreEqual(catalogueBefore, CatalogueSnapshot(db));
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void RepeatedCreationDoesNotReuseMutatedProfileOrSets(TeamId team)
    {
        var db = TechTreeDatabaseFactory.CreateDefault();
        var first = TechTreeRules.CreateDemoProfile(team, db);
        first.rpBalance = -1;
        first.unlockedNodeIds.Clear();
        first.activeNodeIds.Add("unexpected");
        first.fishAndChipsUsed = true;
        first.sunNeverSetsTarget = TeamId.US;
        var second = TechTreeRules.CreateDemoProfile(team, db);
        Assert.AreNotSame(first, second);
        Assert.AreNotSame(first.unlockedNodeIds, second.unlockedNodeIds);
        Assert.AreNotSame(first.activeNodeIds, second.activeNodeIds);
        AssertLoadout(second, team, Expected(team), 9000);
        Assert.AreEqual(-1, first.rpBalance);
        Assert.IsTrue(first.activeNodeIds.Contains("unexpected"));
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void SessionAndNewHumanFactoryShareOutputWithoutTouchingCachedProfiles(TeamId team)
    {
        var session = new RaceSession(new SystemRandomSource(4));
        var field = typeof(TechTreeProfileStore).GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic);
        var cache = (Dictionary<TeamId, TechTreeState>)field.GetValue(null);
        var oldReferences = new Dictionary<TeamId, TechTreeState>(cache);
        var oldPayloads = new Dictionary<TeamId, string>();
        foreach (var entry in cache) oldPayloads.Add(entry.Key, TechTreeProfileCodec.Encode(entry.Value));

        var method = typeof(TechTreeProfileStore).GetMethod("CreateDemoProfile",
            BindingFlags.Static | BindingFlags.NonPublic);
        var human = (TechTreeState)method.Invoke(null, new object[] { team, session.TechDb });
        var ai = session.CreateDemoTechState(team);
        AssertLoadout(human, team, Expected(team), 9000);
        AssertLoadout(ai, team, Expected(team), 9000);
        Assert.AreNotSame(human, ai);
        human.activeNodeIds.Clear();
        Assert.AreEqual(5, ai.activeNodeIds.Count);
        Assert.AreEqual(oldReferences.Count, cache.Count);
        foreach (var entry in oldReferences)
        {
            Assert.AreSame(entry.Value, cache[entry.Key]);
            Assert.AreEqual(oldPayloads[entry.Key], TechTreeProfileCodec.Encode(entry.Value));
        }
    }

    [TestCase(TeamId.UK)] [TestCase(TeamId.DE)] [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)] [TestCase(TeamId.CN)] [TestCase(TeamId.JP)]
    public void MissingCommonStillAllowsThreeCommonGateAndDoesNotChargeMissingId(TeamId team)
    {
        var db = TechTreeDatabaseFactory.CreateDefault();
        string missing = team == TeamId.CN ? Ev[0] : Standard[0];
        db.nodes.Remove(missing);
        var expected = Expected(team);
        expected.Remove(missing);
        AssertLoadout(TechTreeRules.CreateDemoProfile(team, db), team, expected, 11500);
    }

    [Test]
    public void EmptyBudgetFactoryRemainsEmptyAndFunded()
    {
        var state = TechTreeRules.CreateDemoState(TeamId.CN);
        AssertLoadout(state, TeamId.CN, Array.Empty<string>(), 25000);
    }

    [Test]
    public void EmptyDatabaseLeavesBudgetUntouched()
    {
        AssertLoadout(TechTreeRules.CreateDemoProfile(TeamId.UK, new TechTreeDatabase()),
            TeamId.UK, Array.Empty<string>(), 25000);
    }

    [Test]
    public void PurchasesKeepAuthoredOrderWhenPricesExhaustBudget()
    {
        var db = TechTreeDatabaseFactory.CreateDefault();
        db.Get(Standard[0]).rpCost = 23000;
        AssertLoadout(TechTreeRules.CreateDemoProfile(TeamId.US, db), TeamId.US,
            new[] { Standard[0], Standard[1] }, 0);
    }

    [Test]
    public void FailedFirstUniqueDoesNotFallBackToSecondChinaUnique()
    {
        var db = TechTreeDatabaseFactory.CreateDefault();
        db.Get(Unique(TeamId.CN)).rpCost = 15000;
        AssertLoadout(TechTreeRules.CreateDemoProfile(TeamId.CN, db), TeamId.CN, Ev, 14000);
    }

    [Test]
    public void FirstUniqueFollowsDatabaseIndexRatherThanHardcodedTeamEffect()
    {
        var db = TechTreeDatabaseFactory.CreateDefault();
        db.nodesByTeam[TeamId.CN].Reverse();
        var expected = new List<string>(Ev) { "cn-l1-fast-charge" };
        AssertLoadout(TechTreeRules.CreateDemoProfile(TeamId.CN, db), TeamId.CN, expected, 9000);
    }

    [Test]
    public void NoTeamUniqueOnlyActivatesSuccessfulCommonPurchases()
    {
        var db = TechTreeDatabaseFactory.CreateDefault();
        db.nodesByTeam[TeamId.IT].Clear();
        AssertLoadout(TechTreeRules.CreateDemoProfile(TeamId.IT, db), TeamId.IT, Standard, 14000);
    }

    [Test]
    public void FutureCommonEntriesAreNotAutomaticallyPurchased()
    {
        var db = TechTreeDatabaseFactory.CreateDefault();
        db.Add(new TechNodeDef { id = "future-l1", tier = TechTreeTier.L1, rpCost = 1 });
        AssertLoadout(TechTreeRules.CreateDemoProfile(TeamId.UK, db), TeamId.UK, Expected(TeamId.UK), 9000);
    }

    [Test]
    public void NullDatabaseRetainsExistingExceptionContract()
    {
        Assert.Throws<NullReferenceException>(() => TechTreeRules.CreateDemoProfile(TeamId.UK, null));
    }

    private static string CatalogueSnapshot(TechTreeDatabase db)
    {
        var rows = new List<string>();
        foreach (var node in db.nodes.Values)
            rows.Add(node.id + ":" + node.rpCost + ":" + node.tier + ":" + node.teamId);
        rows.Sort(StringComparer.Ordinal);
        return string.Join("|", rows) + ";" + string.Join(",", db.commonNodeIds) + ";" + string.Join(",", db.cnEvNodeIds);
    }
}
