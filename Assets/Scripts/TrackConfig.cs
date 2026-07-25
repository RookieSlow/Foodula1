using UnityEngine;

/// <summary>
/// Serializable data model matching the track JSON schema.
/// Maps 1:1 to the JSON file structure for JsonUtility deserialization.
/// </summary>
[System.Serializable]
public class TrackConfig
{
    public int schemaVersion;
    public string trackId;
    public string trackName;
    public string trackNameEn;
    public string country;
    public string homeTeam;
    public RealCircuitData realCircuit;
    public int gameCellCount;
    public float metersPerCell;
    public int laps;
    public bool hasPitLane;
    public string[] weatherPool;
    public string defaultWeather;
    public string weatherConfigurationStatus;
    public LayoutMeta layout;
    public string[] referenceSources;
    public CellData[] cells;

    /// <summary>Returns the total track length in meters (gameCellCount × metersPerCell).</summary>
    public float TotalLengthMeters => gameCellCount * metersPerCell;
}

[System.Serializable]
public class RealCircuitData
{
    public string name;
    public string layoutYear;
    public float lengthMeters;
    public string direction;
    public int corners;
    public float banking;
    public string note;
}

[System.Serializable]
public class LayoutMeta
{
    public string coordinateSpace;
    public string fidelity;
    public bool pathClosed;
}

/// <summary>
/// Single cell/node in the track layout.
/// Corner-specific fields (cornerId, cornerLevel, cornerLimit, isApex)
/// are only populated when type == "corner".
/// </summary>
[System.Serializable]
public class CellData
{
    public int index;
    public string type;
    public string name;
    public string segmentId;
    public float distanceMeters;
    public Vector2Data position;

    // --- Corner fields (only for type == "corner") ---
    public string cornerId;
    public int cornerLevel;
    public int cornerLimit;
    public bool isApex;

    public bool IsCorner => type == "corner";
    public bool IsStartFinish => type == "start_finish";
    public bool IsPitEntry => type == "pit_entry";
    public bool IsPitExit => type == "pit_exit";
    public bool IsStraight => type == "straight";
}

[System.Serializable]
public class Vector2Data
{
    public float x;
    public float y;

    public Vector2 ToVector2() => new Vector2(x, y);
}
