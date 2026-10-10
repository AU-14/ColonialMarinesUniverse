using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Fires a vehicle-mounted gun from where it really is on the vehicle: its pivot in the vehicle's
/// own space, out along the aim by the barrel. The turret's drawn offsets are per screen direction, so
/// a muzzle built from them only lines up while the camera isn't rotated; this one lines up for
/// everyone, a little under the barrel in side views, where the art lifts the gun.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleGunOriginComponent : Component
{
    /// <summary>
    /// The gun's pivot on the vehicle, in tiles, the vehicle facing south (+y is back).
    /// </summary>
    [DataField]
    public Vector2 Pivot;

    /// <summary>
    /// From the pivot to the muzzle, in tiles.
    /// </summary>
    [DataField]
    public float Barrel;
}
