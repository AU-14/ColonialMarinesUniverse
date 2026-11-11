using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Fires a vehicle-mounted gun from its drawn barrel tip as the shooter sees it: the turret's pivot
/// where the art draws it for the vehicle's facing on the shooter's screen, turned as their client
/// reports (<see cref="CMUViewRotationComponent"/>), out along the aim by the barrel and up their
/// screen by the barrel's height, so the shots leave where their muzzle flash shows. A gun without
/// a turret to draw it falls back on <see cref="Pivot"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleGunOriginComponent : Component
{
    /// <summary>
    /// The gun's pivot on the vehicle, in tiles, the vehicle facing south (+y is back), for a gun
    /// with no turret offsets to place it.
    /// </summary>
    [DataField]
    public Vector2 Pivot;

    /// <summary>
    /// From the pivot to the muzzle, in tiles.
    /// </summary>
    [DataField]
    public float Barrel;

    /// <summary>
    /// How far up the screen the drawn barrel tip sits above its pivot, in tiles, in every direction:
    /// the muzzle flash is drawn there.
    /// </summary>
    [DataField]
    public float MuzzleUp = 2f / 32f;
}
