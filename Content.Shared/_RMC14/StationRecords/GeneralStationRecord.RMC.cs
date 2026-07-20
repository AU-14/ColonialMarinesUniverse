using Robust.Shared.Serialization;

namespace Content.Shared.StationRecords;

public sealed partial record GeneralStationRecord
{
    [DataField]
    public string? Squad;

    [DataField]
    public Color? SquadColor;
}
