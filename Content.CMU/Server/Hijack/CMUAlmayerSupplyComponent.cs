using Content.Shared.Access;
using Content.Shared.CMU14.util;
using Robust.Shared.Prototypes;

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
}
