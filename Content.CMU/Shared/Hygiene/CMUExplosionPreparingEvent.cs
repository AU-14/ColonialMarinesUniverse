using Content.Shared.Explosion;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Hygiene;

[ByRefEvent]
public record struct CMUExplosionPreparingEvent(ProtoId<ExplosionPrototype> ExplosionType);
