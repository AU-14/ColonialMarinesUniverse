using Content.Shared.Explosion;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Weapons.ATGM;

/// <summary>
///     A heavy anti-armour warhead's effect on whatever it strikes directly, on top of the projectile's damage
///     and its explosion. Applies only to direct hits (a projectile collision, or the A.T.G.M. proximity fuse
///     delivering its hit), never to targets caught in the blast.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUDirectHitWarheadSystem))]
public sealed partial class CMUDirectHitWarheadComponent : Component
{
    /// <summary>
    ///     Multiplies the projectile's direct-hit damage against xenonids.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float XenoDamageMultiplier = 1f;

    /// <summary>
    ///     Chance that a xenonid the direct hit left below its critical threshold is brought down to exactly that
    ///     threshold anyway. Never kills: on success the xeno is also spared this warhead's own blast for
    ///     <see cref="XenoCritBlastShield"/>, so the blast cannot carry it on past crit into death.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float XenoCritChance = 0.75f;

    /// <summary>
    ///     Fallback lifetime of the blast shield granted by <see cref="XenoCritChance"/>. The shield is spent by the
    ///     first matching explosion to reach the xeno; this only bounds it if that blast never arrives.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan XenoCritBlastShield = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Bodies a direct hit gibs. Null gibs nothing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? GibWhitelist;

    /// <summary>
    ///     Bodies a direct hit never gibs, even if they pass <see cref="GibWhitelist"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? GibBlacklist;
}

/// <summary>
///     Server-side marker on a xenonid brought down to crit by a direct hit: it ignores explosions of
///     <see cref="ExplosionType"/> (the warhead's own blast) until <see cref="ExpiresAt"/>.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUDirectHitWarheadSystem))]
public sealed partial class CMUDirectHitBlastShieldComponent : Component
{
    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ExpiresAt;
}
