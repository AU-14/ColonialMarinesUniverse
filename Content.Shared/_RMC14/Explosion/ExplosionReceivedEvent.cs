using Content.Shared.Damage;
using Content.Shared.Explosion;
using Content.Shared.Mobs;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Explosion;

[ByRefEvent]
public readonly record struct ExplosionReceivedEvent(
    ProtoId<ExplosionPrototype> Explosion,
    MapCoordinates Epicenter,
    DamageSpecifier Damage,
    MobState? StateBeforeDamage = null);

/// <summary>Allows content to scale the single incoming explosion transaction before aggregate damage commits.</summary>
[ByRefEvent]
public record struct ExplosionDamagePreparingEvent(MapCoordinates Epicenter, DamageSpecifier Damage);
