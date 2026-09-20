using Content.Shared.Access;
using Content.Shared.CMU14.util;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Hijack;

/// <summary>Supplies and legacy crew access for the standalone, five-deck Almayer.</summary>
[RegisterComponent]
public sealed partial class CMUAlmayerSupplyComponent : Component
{
    [DataField]
    public ProtoId<PlatoonPrototype> DefaultPlatoon = "USCM";

    // Authored by the map generator from the same mapping used for its doors.
    [DataField]
    public Dictionary<ProtoId<AccessLevelPrototype>, ProtoId<AccessLevelPrototype>> LegacyAccess = new();

    [DataField]
    public List<ResPath> DropshipMaps =
    [
        new("/Maps/CMU14/Shuttles/alamo.yml"),
        new("/Maps/CMU14/Shuttles/normandy.yml"),
    ];

    // Round rules and the standalone map share this record to avoid duplicate arrivals.
    public readonly Dictionary<ResPath, EntityUid> InitialDropships = new();
}
