using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared._RMC14.Xenonids.Damage;

[ByRefEvent]
public record struct ProjectileDamageDealtEvent(
    EntityUid? Origin,
    DamageSpecifier? DamageDelta,
    FixedPoint2? AuthoritativeTotal = null);
