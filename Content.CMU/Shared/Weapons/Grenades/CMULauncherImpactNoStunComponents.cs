using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.Weapons.Grenades;

/// <summary>
///     A grenade (M40 HEDP and HEFA) that doesn't stun the mob it hits when fired from a launcher. The blast
///     still hurts them, and still stuns everyone else.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMULauncherImpactNoStunComponent : Component
{
    public EntityUid? HitMob;
}

/// <summary>
///     Spared the stun of the blast centred on <see cref="Epicenter"/>, from a grenade that struck them directly.
/// </summary>
[RegisterComponent]
public sealed partial class CMUExplosionStunExemptComponent : Component
{
    public MapCoordinates Epicenter;
    public TimeSpan ExpireAt;

    /// <summary>How close a blast has to be to <see cref="Epicenter"/> to count as the same one.</summary>
    public const float Range = 1.5f;
}
