using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// A crate holding a vehicle in parts. The first time it is opened the bare chassis takes its place,
/// and the parts, tools and manual it held are laid out on either side of it.
/// </summary>
[RegisterComponent]
public sealed partial class CMUVehicleKitCrateComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Chassis;

    /// <summary>
    /// Tiles out from the chassis' centre line the contents are set down at, on each side.
    /// </summary>
    [DataField]
    public float LayOut = 1.1f;

    /// <summary>
    /// What the crate held as it was opened.
    /// </summary>
    public List<EntityUid> Contents = new();
}
