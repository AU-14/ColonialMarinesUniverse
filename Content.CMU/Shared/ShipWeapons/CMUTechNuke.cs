using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ShipWeapons;

[DataRecord]
[Serializable, NetSerializable]
public sealed partial record CMUTechNuke(EntProtoId Nuke)
{
    public string Team { get; init; } = string.Empty;
}
