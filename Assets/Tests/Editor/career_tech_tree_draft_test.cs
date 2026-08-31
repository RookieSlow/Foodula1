using System;
using System.Collections.Generic;
using NUnit.Framework;

public sealed class CareerTechTreeDraftTests
{
    [Test]
    public void Draft_CopiesSourceAndTogglesWithoutMutatingSnapshot()
    {
        TechTreeDatabase database = BuildDatabase();
        CareerTechSnapshot source = CreateSnapshot(TeamId.UK, 100, new[] { "common" }, new[] { "common" });
        Assert.That(CareerTechTreeDraft.TryCreate(source, database, out CareerTechTreeDraft draft), Is.True);

        Assert.That(draft.TryToggleOrUnlock("common"), Is.True);
        Assert.That(draft.IsActive("common"), Is.False);
        Assert.That(source.ActiveNodeIds, Is.EqualTo(new[] { "common" }));
    }

    [Test]
    public void Draft_UnlocksWithCareerBudgetAndBuildsIndependentSnapshot()
    {
        TechTreeDatabase database = BuildDatabase();
        CareerTechSnapshot source = CreateSnapshot(TeamId.UK, 100, Array.Empty<string>(), Array.Empty<string>());
        Assert.That(CareerTechTreeDraft.TryCreate(source, database, out CareerTechTreeDraft draft), Is.True);

        Assert.That(draft.TryToggleOrUnlock("common"), Is.True);
        Assert.That(draft.RpBalance, Is.EqualTo(60));
        Assert.That(draft.IsUnlocked("common"), Is.True);
        Assert.That(draft.IsActive("common"), Is.True);
        Assert.That(draft.TryBuildSnapshot(out CareerTechSnapshot updated), Is.True);
        Assert.That(updated.RpBalance, Is.EqualTo(60));
        Assert.That(source.RpBalance, Is.EqualTo(100));
        Assert.That(source.UnlockedNodeIds, Is.Empty);
    }

    [Test]
    public void Draft_RejectsNodesOwnedByAnotherTeam()
    {
        CareerTechSnapshot source = CreateSnapshot(TeamId.UK, 100, Array.Empty<string>(), Array.Empty<string>());
        Assert.That(CareerTechTreeDraft.TryCreate(source, BuildDatabase(), out CareerTechTreeDraft draft), Is.True);
        Assert.That(draft.CanUnlock("de-only"), Is.False);
        Assert.That(draft.TryToggleOrUnlock("de-only"), Is.False);
        Assert.That(draft.RpBalance, Is.EqualTo(100));
    }

    [Test]
    public void Draft_PreservesUkSunNeverSetsTarget()
    {
        Assert.That(CareerTechSnapshot.TryCreate(
            TeamId.UK, 0, Array.Empty<string>(), Array.Empty<string>(), TeamId.JP,
            out CareerTechSnapshot source), Is.True);
        Assert.That(CareerTechTreeDraft.TryCreate(source, BuildDatabase(), out CareerTechTreeDraft draft), Is.True);
        Assert.That(draft.TryBuildSnapshot(out CareerTechSnapshot updated), Is.True);
        Assert.That(updated.SunNeverSetsTarget, Is.EqualTo(TeamId.JP));
    }

    [Test]
    public void Service_ConfirmsSummerDraftOnceAndUnlocksRaceFive()
    {
        var repository = new CareerRepository(new MemoryStore(), new MemorySerializer());
        CareerSeasonState state = StartAtSummerBreak();
        Assert.That(repository.Save(state), Is.True);
        var service = new CareerModeService(repository);
        Assert.That(CareerTechTreeDraft.TryCreate(
            service.CurrentState.ActiveTechSnapshot, BuildDatabase(), out CareerTechTreeDraft draft), Is.True);
        Assert.That(draft.TryToggleOrUnlock("common"), Is.True);
        Assert.That(draft.TryBuildSnapshot(out CareerTechSnapshot snapshot), Is.True);

        Assert.That(service.TryConfirmSummerBreak(snapshot), Is.True);
        Assert.That(service.CurrentState.Phase, Is.EqualTo(CareerPhase.Racing));
        Assert.That(service.CurrentState.NextTrackIndex, Is.EqualTo(4));
        Assert.That(service.CurrentState.ActiveTechSnapshot.UnlockedNodeIds, Contains.Item("common"));
        Assert.That(CareerModeRules.CanStartNextRace(service.CurrentState), Is.True);
        Assert.That(service.TryConfirmSummerBreak(snapshot), Is.False);
    }

    [Test]
    public void Service_SaveFailureKeepsSummerBreakOpen()
    {
        var store = new MemoryStore();
        var repository = new CareerRepository(store, new MemorySerializer());
        CareerSeasonState state = StartAtSummerBreak();
        Assert.That(repository.Save(state), Is.True);
        var service = new CareerModeService(repository);
        store.FailWrites = true;

        Assert.That(service.TryConfirmSummerBreak(state.ActiveTechSnapshot), Is.False);
        Assert.That(service.CurrentState.Phase, Is.EqualTo(CareerPhase.SummerBreak));
        Assert.That(service.CurrentState.SummerBreakUsed, Is.False);
        Assert.That(CareerModeRules.CanAdjustTechTree(service.CurrentState), Is.True);
    }

    private static TechTreeDatabase BuildDatabase()
    {
        var database = new TechTreeDatabase();
        database.Add(new TechNodeDef(
            "common", "通用", "Common", TechTreeTier.L1, null, 1, 40,
            Array.Empty<string>(), string.Empty, Array.Empty<TechEffect>(), "测试通用节点"));
        database.Add(new TechNodeDef(
            "de-only", "德国", "Germany", TechTreeTier.L1, TeamId.DE, 1, 20,
            Array.Empty<string>(), string.Empty, Array.Empty<TechEffect>(), "测试德国节点"));
        return database;
    }

    private static CareerTechSnapshot CreateSnapshot(
        TeamId team, int rp, IEnumerable<string> unlocked, IEnumerable<string> active)
    {
        Assert.That(CareerTechSnapshot.TryCreate(team, rp, unlocked, active, null, out CareerTechSnapshot snapshot), Is.True);
        return snapshot;
    }

    private static CareerSeasonState StartAtSummerBreak()
    {
        var state = new CareerSeasonState();
        CareerTechSnapshot initial = CreateSnapshot(TeamId.UK, 100, Array.Empty<string>(), Array.Empty<string>());
        Assert.That(CareerModeRules.TryStartSeason(
            state, TeamId.UK, new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US }, initial), Is.True);
        for (int race = 0; race < 4; race++)
        {
            Assert.That(CareerModeRules.TryRecordRace(state, new CareerRaceResult(
                $"summer-{race}", CareerModeRules.GetNextTrackId(state), new[]
                {
                    new CareerCompetitorResult(TeamId.UK, 1),
                    new CareerCompetitorResult(TeamId.DE, 2),
                    new CareerCompetitorResult(TeamId.IT, 3),
                    new CareerCompetitorResult(TeamId.US, 4)
                })), Is.True);
        }
        return state;
    }

    private sealed class MemoryStore : ICareerKeyValueStore
    {
        private string value;
        public bool FailWrites { get; set; }
        public bool HasKey(string key) => value != null;
        public string GetString(string key) => value ?? string.Empty;
        public bool TrySetAndSave(string key, string serialized)
        {
            if (FailWrites) return false;
            value = serialized;
            return true;
        }
        public bool TryDeleteAndSave(string key) { value = null; return true; }
    }

    private sealed class MemorySerializer : ICareerSerializer
    {
        private readonly Dictionary<string, CareerSaveData> values = new Dictionary<string, CareerSaveData>();
        private int nextId;
        public string Serialize(CareerSaveData data)
        {
            string id = (++nextId).ToString();
            values[id] = data;
            return id;
        }
        public CareerSaveData Deserialize(string serialized) => values[serialized];
    }
}
