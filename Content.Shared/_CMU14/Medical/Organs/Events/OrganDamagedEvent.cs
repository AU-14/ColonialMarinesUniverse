using Content.Shared.Damage;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Organs.Events;

public enum OrganDamageSource : byte
{
    PartDistribution = 0,
    Direct,
    RibFracture,
    Reagent,
    Surgery,
}

[ByRefEvent]
public record struct OrganDamagedEvent(
    EntityUid Body,
    EntityUid Organ,
    DamageSpecifier Damage,
    OrganDamageSource Source);
