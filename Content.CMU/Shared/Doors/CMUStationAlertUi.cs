using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Doors;

[Serializable, NetSerializable]
public enum CMUStationAlertUiKey : byte
{
    Key,
}

/// <summary>What's wrong with the air in an area. An area can have several at once.</summary>
[Flags, Serializable, NetSerializable]
public enum CMUStationAlertHazard : ushort
{
    None = 0,

    /// <summary>Near-vacuum: the hull is open somewhere.</summary>
    Breach = 1 << 0,

    LowPressure = 1 << 1,
    HighPressure = 1 << 2,
    Fire = 1 << 3,
    Freezing = 1 << 4,
    Toxic = 1 << 5,
    LowOxygen = 1 << 6,

    /// <summary>One of the area's firelocks has shut itself against escaping air.</summary>
    FirelockSealed = 1 << 7,
}

/// <summary>One area with something wrong in it.</summary>
[Serializable, NetSerializable]
public sealed record CMUStationAlertEntry(
    string Area,
    int Deck,
    CMUStationAlertHazard Hazards,
    int Tiles,
    float LowestPressure,
    float HighestTemperature,
    int SealedFirelocks);

[Serializable, NetSerializable]
public sealed class CMUStationAlertBuiState(List<CMUStationAlertEntry> alerts, TimeSpan scannedAt) : BoundUserInterfaceState
{
    public readonly List<CMUStationAlertEntry> Alerts = alerts;
    public readonly TimeSpan ScannedAt = scannedAt;
}
