using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TankCookOffComponent : Component
{
    [DataField, AutoNetworkedField] public bool Enabled = true;
    [DataField] public float CriticalChance = .15f;
    [DataField, AutoNetworkedField] public float RuptureDelay = 2.4f;
    [DataField] public float EjectionDistance = 4;
    [DataField] public float EjectionSpeed = 8;
    [DataField, AutoNetworkedField] public SpriteSpecifier TurretSprite = new SpriteSpecifier.Rsi(
        new ResPath("_RMC14/Structures/Vehicles/tank.rsi"), "tank_turret_0");
    [DataField] public SoundSpecifier RuptureSound = new SoundPathSpecifier("/Audio/_RMC14/Explosion/bigboom3.ogg");

    // explosion types that total the tank outright. module multipliers turn an OB into chip damage,
    // so without this a tank parked under one stayed repairable
    [DataField] public List<ProtoId<ExplosionPrototype>> CatastrophicExplosions = new() { "RMCOB", "RMCOBXenoTunnel" };

    // damage the frame has to actually take, so catching the edge of the blast doesn't count
    [DataField] public FixedPoint2 CatastrophicExplosionThreshold = 300;

    /// <summary>
    ///     A single non-explosive hit (after hull armour) of at least this fraction of the hull's maximum integrity
    ///     is catastrophic for this tank and removes <see cref="CatastrophicHullFraction"/> of the hull. Below 1 so a
    ///     fully kitted tank's armour (an anti-armour rocket lands at about 0.79 of the hull with concussive armour)
    ///     cannot turn a heavy hit back into chip damage. Explosions still need the whole hull.
    /// </summary>
    [DataField] public float CatastrophicHitFraction = 0.7f;

    /// <summary>
    ///     Fraction of the hull's maximum integrity a catastrophic hit removes, instead of wrecking the tank
    ///     outright. It replaces the hit's normal module damage. Below 1, so one such hit never zeroes a full hull;
    ///     a tank with less than this left goes to zero (and so cooks off).
    /// </summary>
    [DataField] public float CatastrophicHullFraction = 0.4f;
}

/// <summary>Only designated anti-armour rockets and tank shells may roll a critical cook-off.</summary>
[RegisterComponent]
public sealed partial class TankCookOffProjectileComponent : Component
{
    /// <summary>
    ///     Replaces the tank's <see cref="TankCookOffComponent.CriticalChance"/> for direct hits by this
    ///     projectile, for warheads that are deadlier (or weaker) against armour than the default. Null uses the
    ///     tank's chance.
    /// </summary>
    [DataField]
    public float? CriticalChance;

    /// <summary>
    ///     FLOW-09: fraction of a cook-off tank's maximum hull a direct hit by this round removes, in place of the
    ///     hit's normal module damage and the tank's <see cref="TankCookOffComponent.CatastrophicHitFraction"/> rule.
    ///     The round's own blast is handled separately. Zero or null opts the round out: a child of an anti-armour
    ///     round that is not one itself sets 0, and its direct hits follow the untagged rule.
    /// </summary>
    [DataField]
    public float? HullFraction;

    /// <summary>
    ///     Tanks this round has already taken its hull chunk from, so a penetrating or re-colliding round never takes
    ///     two from the same tank. Tracked per tank rather than as one flag: a round that goes on to strike a second,
    ///     different tank makes a fresh direct hit on it and takes that tank's chunk too.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> ChunkedVehicles = new();
}

/// <summary>Irreversible ammunition ignition. Persists on the wreck and prevents repairs.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ActiveTankCookOffComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;
    [DataField, AutoNetworkedField] public bool Ruptured;
}

[ByRefEvent]
public readonly record struct TankCookOffRupturedEvent;
