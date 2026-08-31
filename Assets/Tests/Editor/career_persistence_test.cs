using System;
using System.Collections.Generic;
using NUnit.Framework;

public class CareerPersistenceTests
{
    private static readonly TeamId[] Field =
    {
        TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US
    };

    [Test]
    public void TechSnapshot_RejectsDuplicateNodesAndActiveNodesThatAreNotUnlocked()
    {
        Assert.That(
            CareerTechSnapshot.TryCreate(
                TeamId.UK,
                5,
                new[] { "node-a", "node-a" },
                new[] { "node-a" },
                null,
                out _),
            Is.False);
        Assert.That(
            CareerTechSnapshot.TryCreate(
                TeamId.UK,
                5,
                new[] { "node-a" },
                new[] { "node-b" },
                null,
                out _),
            Is.False);
    }

    [Test]
    public void Codec_RoundTripPreservesStartedStateAndInitialTechSnapshot()
    {
        CareerSeasonState original = StartSeason(CreateTech("node-a"));

        Assert.That(CareerSaveCodec.TryToData(original, out CareerSaveData data), Is.True);
        Assert.That(CareerSaveCodec.TryFromData(data, out CareerSeasonState loaded), Is.True);

        Assert.That(loaded.LockedTeam, Is.EqualTo(TeamId.UK));
        Assert.That(loaded.Phase, Is.EqualTo(CareerPhase.Racing));
        Assert.That(loaded.NextTrackIndex, Is.Zero);
        Assert.That(loaded.InitialTechSnapshot.ActiveNodeIds, Is.EqualTo(new[] { "node-a" }));
        Assert.That(loaded.ActiveTechSnapshot.ActiveNodeIds, Is.EqualTo(new[] { "node-a" }));
    }

    [Test]
    public void Codec_RoundTripPreservesSummerBreakSnapshotAndFifthRaceProgress()
    {
        CareerSeasonState original = StartSeason(CreateTech("node-a"));
        for (int race = 0; race < 4; race++)
            Assert.That(CareerModeRules.TryRecordRace(original, BuildResult(original, race)), Is.True);

        CareerTechSnapshot summerTech = CreateTech("node-a", "node-b");
        Assert.That(CareerModeRules.ConfirmSummerBreakTechTree(original, summerTech), Is.True);
        Assert.That(CareerModeRules.TryRecordRace(original, BuildResult(original, 4)), Is.True);

        Assert.That(CareerSaveCodec.TryToData(original, out CareerSaveData data), Is.True);
        Assert.That(CareerSaveCodec.TryFromData(data, out CareerSeasonState loaded), Is.True);
        Assert.That(loaded.NextTrackIndex, Is.EqualTo(5));
        Assert.That(loaded.SummerBreakUsed, Is.True);
        Assert.That(loaded.SummerBreakTechSnapshot.ActiveNodeIds, Is.EqualTo(new[] { "node-a", "node-b" }));
        Assert.That(loaded.ActiveTechSnapshot.ActiveNodeIds, Is.EqualTo(new[] { "node-a", "node-b" }));
    }

    [Test]
    public void Codec_RejectsWrongSchemaAndChangedTrackSchedule()
    {
        CareerSeasonState state = StartSeason(CreateTech("node-a"));
        Assert.That(CareerSaveCodec.TryToData(state, out CareerSaveData wrongVersion), Is.True);
        wrongVersion.schemaVersion++;
        Assert.That(CareerSaveCodec.TryFromData(wrongVersion, out _), Is.False);

        Assert.That(CareerSaveCodec.TryToData(state, out CareerSaveData wrongSchedule), Is.True);
        wrongSchedule.trackIds[0] = "fallback_42";
        Assert.That(CareerSaveCodec.TryFromData(wrongSchedule, out _), Is.False);
    }

    [Test]
    public void Codec_RejectsTamperedPointsTable()
    {
        CareerSeasonState state = StartSeason(CreateTech("node-a"));
        Assert.That(CareerModeRules.TryRecordRace(state, BuildResult(state, 0)), Is.True);
        Assert.That(CareerSaveCodec.TryToData(state, out CareerSaveData data), Is.True);
        data.standings[0].points += 100;

        Assert.That(CareerSaveCodec.TryFromData(data, out _), Is.False);
    }

    [Test]
    public void Repository_MissingAndMalformedSaveReturnSafeEmptyState()
    {
        var store = new MemoryStore();
        var serializer = new MemorySerializer();
        var repository = new CareerRepository(store, serializer);

        CareerLoadResult missing = repository.Load();
        Assert.That(missing.Status, Is.EqualTo(CareerLoadStatus.Missing));
        Assert.That(missing.State.Phase, Is.EqualTo(CareerPhase.NotStarted));

        store.Values[CareerRepository.SaveKey] = "malformed";
        CareerLoadResult invalid = repository.Load();
        Assert.That(invalid.Status, Is.EqualTo(CareerLoadStatus.Invalid));
        Assert.That(invalid.State.Phase, Is.EqualTo(CareerPhase.NotStarted));
        Assert.That(store.HasKey(CareerRepository.SaveKey), Is.True,
            "Invalid data is preserved until the player confirms replacement or abandonment.");
    }

    [Test]
    public void Repository_SaveThenLoadRebuildsValidatedSeason()
    {
        var store = new MemoryStore();
        var serializer = new MemorySerializer();
        var repository = new CareerRepository(store, serializer);
        CareerSeasonState state = StartSeason(CreateTech("node-a"));
        Assert.That(CareerModeRules.TryRecordRace(state, BuildResult(state, 0)), Is.True);

        Assert.That(repository.Save(state), Is.True);
        CareerLoadResult loaded = repository.Load();

        Assert.That(loaded.Status, Is.EqualTo(CareerLoadStatus.Loaded));
        Assert.That(loaded.State.NextTrackIndex, Is.EqualTo(1));
        Assert.That(loaded.State.RaceResults[0].ResultId, Is.EqualTo("race-1"));
        Assert.That(CareerModeRules.GetStandings(loaded.State)[0].Points, Is.EqualTo(10));
    }

    [Test]
    public void Service_RequiresConfirmationBeforeReplacingOrAbandoningCareer()
    {
        var store = new MemoryStore();
        var repository = new CareerRepository(store, new MemorySerializer());
        var service = new CareerModeService(repository);

        Assert.That(service.TryCreateNew(TeamId.UK, Field, CreateTech("node-a"), false), Is.True);
        Assert.That(service.TryCreateNew(TeamId.DE, NewField(TeamId.DE), CreateTechFor(TeamId.DE), false), Is.False);
        Assert.That(service.CurrentState.LockedTeam, Is.EqualTo(TeamId.UK));
        Assert.That(service.TryAbandon(false), Is.False);
        Assert.That(service.HasStoredCareer, Is.True);

        Assert.That(service.TryAbandon(true), Is.True);
        Assert.That(service.HasStoredCareer, Is.False);
        Assert.That(service.CurrentState.Phase, Is.EqualTo(CareerPhase.NotStarted));
    }

    [Test]
    public void Service_SaveFailureDoesNotAdvanceCurrentRuntimeState()
    {
        var store = new MemoryStore();
        var repository = new CareerRepository(store, new MemorySerializer());
        var service = new CareerModeService(repository);
        Assert.That(service.TryCreateNew(TeamId.UK, Field, CreateTech("node-a"), false), Is.True);

        string persistedBefore = store.GetString(CareerRepository.SaveKey);
        store.FailWrites = true;
        Assert.That(service.TryRecordRace(BuildResult(service.CurrentState, 0)), Is.False);

        Assert.That(service.CurrentState.NextTrackIndex, Is.Zero);
        Assert.That(service.CurrentState.RaceResults, Is.Empty);
        Assert.That(store.GetString(CareerRepository.SaveKey), Is.EqualTo(persistedBefore));
    }

    [Test]
    public void Service_InvalidSaveRequiresExplicitReplacementConfirmation()
    {
        var store = new MemoryStore();
        store.Values[CareerRepository.SaveKey] = "malformed";
        var service = new CareerModeService(
            new CareerRepository(store, new MemorySerializer()));

        Assert.That(service.LoadStatus, Is.EqualTo(CareerLoadStatus.Invalid));
        Assert.That(service.TryCreateNew(TeamId.UK, Field, CreateTech("node-a"), false), Is.False);
        Assert.That(service.TryCreateNew(TeamId.UK, Field, CreateTech("node-a"), true), Is.True);
        Assert.That(service.LoadStatus, Is.EqualTo(CareerLoadStatus.Loaded));
        Assert.That(service.CurrentState.LockedTeam, Is.EqualTo(TeamId.UK));
    }

    private static CareerSeasonState StartSeason(CareerTechSnapshot tech)
    {
        var state = new CareerSeasonState();
        Assert.That(CareerModeRules.TryStartSeason(state, TeamId.UK, Field, tech), Is.True);
        return state;
    }

    private static CareerTechSnapshot CreateTech(params string[] active)
    {
        Assert.That(
            CareerTechSnapshot.TryCreate(TeamId.UK, 10, active, active, null, out CareerTechSnapshot snapshot),
            Is.True);
        return snapshot;
    }

    private static CareerTechSnapshot CreateTechFor(TeamId team)
    {
        Assert.That(
            CareerTechSnapshot.TryCreate(team, 0, Array.Empty<string>(), Array.Empty<string>(), null, out CareerTechSnapshot snapshot),
            Is.True);
        return snapshot;
    }

    private static TeamId[] NewField(TeamId playerTeam)
    {
        return new[] { playerTeam, TeamId.UK, TeamId.IT, TeamId.US };
    }

    private static CareerRaceResult BuildResult(CareerSeasonState state, int raceIndex)
    {
        return new CareerRaceResult(
            $"race-{raceIndex + 1}",
            CareerModeRules.GetNextTrackId(state),
            new[]
            {
                new CareerCompetitorResult(TeamId.UK, 1),
                new CareerCompetitorResult(TeamId.DE, 2),
                new CareerCompetitorResult(TeamId.IT, 3),
                new CareerCompetitorResult(TeamId.US, 4)
            });
    }

    private sealed class MemoryStore : ICareerKeyValueStore
    {
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        public bool FailWrites { get; set; }

        public bool HasKey(string key) => Values.ContainsKey(key);
        public string GetString(string key) => Values.TryGetValue(key, out string value) ? value : string.Empty;

        public bool TrySetAndSave(string key, string value)
        {
            if (FailWrites) return false;
            Values[key] = value;
            return true;
        }

        public bool TryDeleteAndSave(string key)
        {
            if (FailWrites) return false;
            Values.Remove(key);
            return true;
        }
    }

    private sealed class MemorySerializer : ICareerSerializer
    {
        private readonly Dictionary<string, CareerSaveData> values =
            new Dictionary<string, CareerSaveData>();
        private int nextId;

        public string Serialize(CareerSaveData data)
        {
            string id = $"save-{++nextId}";
            values[id] = data;
            return id;
        }

        public CareerSaveData Deserialize(string serialized)
        {
            if (!values.TryGetValue(serialized, out CareerSaveData data))
                throw new FormatException("Malformed save token.");
            return data;
        }
    }
}
