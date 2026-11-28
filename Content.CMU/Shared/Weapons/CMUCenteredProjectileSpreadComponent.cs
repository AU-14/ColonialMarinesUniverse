using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Weapons;

/// <summary>
/// FLOW-02: on a gun whose cartridge projectile has a <c>ProjectileSpread</c>, fly the cartridge's own projectile down
/// the centre of the fan instead of along its left edge. The pellet code always fires the original at the first spread
/// angle and the extra <c>ProjectileSpread.Proto</c> projectiles at the rest. Use an odd spread count so a centre exists.
/// The DRGN uses this so its main burst lands where the gunner clicks and the shards fan out either side.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUCenteredProjectileSpreadSystem))]
public sealed partial class CMUCenteredProjectileSpreadComponent : Component;
