using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.CMU14.TacticalMap.Scout;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUScoutReporterComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextReport;

    [ViewVariables]
    public readonly List<CMUScoutReport> Reports = new();
}

public readonly record struct CMUScoutReport(
    int Key,
    EntityUid Map,
    EntityUid Grid,
    string Faction,
    TacticalMapBlip Blip);
