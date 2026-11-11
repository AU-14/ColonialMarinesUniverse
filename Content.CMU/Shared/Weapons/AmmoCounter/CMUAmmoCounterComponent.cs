using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Weapons.AmmoCounter;

/// <summary>
///     A gun with a built-in ammo counter, like the M41A's. Only these show how many rounds are left, on examine and in
///     the hand ammo display; for any other gun you have to take the magazine out and look at it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUAmmoCounterComponent : Component;
