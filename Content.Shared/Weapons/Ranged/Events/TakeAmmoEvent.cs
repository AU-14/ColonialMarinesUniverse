using Robust.Shared.Map;

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on a gun when it would like to take the specified amount of ammo.
/// </summary>
public sealed class TakeAmmoEvent : EntityEventArgs
{
    public readonly EntityUid? User;
    // RMC14: chamber providers consume shots before the primary ammo provider runs.
    public int Shots;
    public List<(EntityUid? Entity, IShootable Shootable)> Ammo;

    /// <summary>
    /// If no ammo returned what is the reason for it?
    /// </summary>
    public string? Reason;

    /// <summary>
    /// Coordinates to spawn the ammo at.
    /// </summary>
    public EntityCoordinates Coordinates;

    public TakeAmmoEvent(int shots, List<(EntityUid? Entity, IShootable Shootable)> ammo, EntityCoordinates coordinates, EntityUid? user)
    {
        Shots = shots;
        Ammo = ammo;
        Coordinates = coordinates;
        User = user;
    }
}
