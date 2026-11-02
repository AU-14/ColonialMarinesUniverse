using Content.Shared.CMU14.TacticalMap.Scout;

namespace Content.Shared._RMC14.TacticalMap;

public readonly partial record struct TacticalMapBlip
{
    public CMUScoutReportInfo? ScoutReport { get; init; }
}
