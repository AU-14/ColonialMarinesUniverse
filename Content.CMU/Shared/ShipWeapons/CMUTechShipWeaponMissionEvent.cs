using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ShipWeapons;

/// <summary>
///     A tech tree purchase that adds a fire mission to its team's ship: the strike becomes available to fire from
///     overwatch, even if the arming terminal didn't pick it, and the ship's mission pool grows by one.
/// </summary>
[DataRecord]
[Serializable, NetSerializable]
public sealed partial record CMUTechShipWeaponMissionEvent(ProtoId<CMUShipWeaponPrototype> Weapon)
{
    public string Team { get; init; } = string.Empty;
}
