using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Dropship.Weapon;

/// <summary>
/// Marks dropship ammo that can be loaded and unloaded BY HAND instead of
/// requiring a powerloader, e.g. 40mm BOFORS magazines and 105mm howitzer shells.
/// Loading is done by clicking the attach point of the matching installed weapon
/// with the ammo in hand; clicking with an empty hand unloads it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DropshipHandLoadedAmmoComponent : Component
{
}
