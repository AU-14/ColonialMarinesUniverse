using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Radio;

[Serializable, NetSerializable]
public enum AU14NetSpliceUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum AU14NetSpliceStatus : byte
{
    Running,
    Success,
    Failed,
}

/// <summary>One probe taken against the carrier currently being hunted: the position looked at, and the
/// strength that came back. The noise is already applied server-side.</summary>
[Serializable, NetSerializable]
public readonly record struct AU14NetSpliceReading(int Position, int Strength);

[Serializable, NetSerializable]
public sealed class AU14NetSpliceBuiState : BoundUserInterfaceState
{
    public int BandSize;
    public int Stage;
    public int Carriers;
    public int ProbesLeft;
    public float Detection;
    public AU14NetSpliceStatus Status;
    public List<AU14NetSpliceReading> Readings;
    public List<int> Locked;

    public AU14NetSpliceBuiState(
        int bandSize,
        int stage,
        int carriers,
        int probesLeft,
        float detection,
        AU14NetSpliceStatus status,
        List<AU14NetSpliceReading> readings,
        List<int> locked)
    {
        BandSize = bandSize;
        Stage = stage;
        Carriers = carriers;
        ProbesLeft = probesLeft;
        Detection = detection;
        Status = status;
        Readings = readings;
        Locked = locked;
    }
}

/// <summary>Sample the band at one position. Costs one probe and a little detection.</summary>
[Serializable, NetSerializable]
public sealed class AU14NetSpliceProbeMsg : BoundUserInterfaceMessage
{
    public int Position;

    public AU14NetSpliceProbeMsg(int position)
    {
        Position = position;
    }
}

/// <summary>Commit to a position as the carrier. Missing is what gets the operator caught.</summary>
[Serializable, NetSerializable]
public sealed class AU14NetSpliceLockMsg : BoundUserInterfaceMessage
{
    public int Position;

    public AU14NetSpliceLockMsg(int position)
    {
        Position = position;
    }
}
