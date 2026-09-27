using System;
using System.Collections.Generic;

/// <summary>
/// Career application service. Mutations happen on a validated clone and only
/// replace CurrentState after persistence succeeds.
/// </summary>
public sealed class CareerModeService
{
    private readonly CareerRepository repository;

    public CareerSeasonState CurrentState { get; private set; }
    public CareerLoadStatus LoadStatus { get; private set; }
    public bool HasStoredCareer => repository.HasStoredSave;

    public CareerModeService(CareerRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        CareerLoadResult loaded = repository.Load();
        CurrentState = loaded.State;
        LoadStatus = loaded.Status;
    }

    public bool TryCreateNew(
        TeamId playerTeam,
        IReadOnlyList<TeamId> competitors,
        CareerTechSnapshot initialTech,
        bool confirmReplaceExisting)
    {
        if (repository.HasStoredSave && !confirmReplaceExisting)
            return false;

        var candidate = new CareerSeasonState();
        if (!CareerModeRules.TryStartSeason(candidate, playerTeam, competitors, initialTech) ||
            !repository.Save(candidate))
        {
            return false;
        }

        CurrentState = candidate;
        LoadStatus = CareerLoadStatus.Loaded;
        return true;
    }

    public bool TryRecordRace(CareerRaceResult result)
    {
        if (!CareerSaveCodec.TryClone(CurrentState, out CareerSeasonState candidate) ||
            !CareerModeRules.TryRecordRace(candidate, result) ||
            !repository.Save(candidate))
        {
            return false;
        }

        CurrentState = candidate;
        return true;
    }

    public bool TryConfirmSummerBreak(CareerTechSnapshot snapshot)
    {
        if (!CareerSaveCodec.TryClone(CurrentState, out CareerSeasonState candidate) ||
            !CareerModeRules.ConfirmSummerBreakTechTree(candidate, snapshot) ||
            !repository.Save(candidate))
        {
            return false;
        }

        CurrentState = candidate;
        return true;
    }

    public bool TryAbandon(bool confirmed)
    {
        if (!confirmed || !repository.Abandon())
            return false;

        CurrentState = new CareerSeasonState();
        LoadStatus = CareerLoadStatus.Missing;
        return true;
    }
}
