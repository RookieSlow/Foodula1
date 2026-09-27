using System;

public enum CareerLoadStatus
{
    Missing,
    Loaded,
    Invalid
}

public sealed class CareerLoadResult
{
    public CareerLoadStatus Status { get; }
    public CareerSeasonState State { get; }

    public CareerLoadResult(CareerLoadStatus status, CareerSeasonState state)
    {
        Status = status;
        State = state ?? new CareerSeasonState();
    }
}

public interface ICareerKeyValueStore
{
    bool HasKey(string key);
    string GetString(string key);
    bool TrySetAndSave(string key, string value);
    bool TryDeleteAndSave(string key);
}

public interface ICareerSerializer
{
    string Serialize(CareerSaveData data);
    CareerSaveData Deserialize(string serialized);
}

/// <summary>Isolated repository using a career-only key.</summary>
public sealed class CareerRepository
{
    public const string SaveKey = "Foodula1.Career.V1";

    private readonly ICareerKeyValueStore store;
    private readonly ICareerSerializer serializer;
    private readonly Func<CareerSeasonState, bool> stateValidator;

    public CareerRepository(
        ICareerKeyValueStore store,
        ICareerSerializer serializer,
        Func<CareerSeasonState, bool> stateValidator = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.stateValidator = stateValidator ?? (_ => true);
    }

    /// <summary>
    /// Returns true if the save exists or its existence cannot be checked.
    /// A storage read failure must not be mistaken for permission to overwrite.
    /// </summary>
    public bool HasStoredSave
    {
        get
        {
            try
            {
                return store.HasKey(SaveKey);
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    public CareerLoadResult Load()
    {
        try
        {
            if (!store.HasKey(SaveKey))
                return new CareerLoadResult(CareerLoadStatus.Missing, new CareerSeasonState());

            CareerSaveData data = serializer.Deserialize(store.GetString(SaveKey));
            return CareerSaveCodec.TryFromData(data, out CareerSeasonState state) && stateValidator(state)
                ? new CareerLoadResult(CareerLoadStatus.Loaded, state)
                : new CareerLoadResult(CareerLoadStatus.Invalid, new CareerSeasonState());
        }
        catch (Exception)
        {
            return new CareerLoadResult(CareerLoadStatus.Invalid, new CareerSeasonState());
        }
    }

    public bool Save(CareerSeasonState state)
    {
        try
        {
            if (!stateValidator(state) || !CareerSaveCodec.TryToData(state, out CareerSaveData data))
                return false;
            string serialized = serializer.Serialize(data);
            return !string.IsNullOrEmpty(serialized) && store.TrySetAndSave(SaveKey, serialized);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool Abandon()
    {
        try
        {
            return !store.HasKey(SaveKey) || store.TryDeleteAndSave(SaveKey);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
