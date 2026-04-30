using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.Organs.Liver;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Organs.Liver;

public sealed class LiverSystem : SharedLiverSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";

    // Per-cycle bloodstream-direct damage. Tuned small so a single
    // painkiller dose doesn't crater a healthy liver — chronic exposure
    // accumulates over many metabolize cycles.
    private static readonly FixedPoint2 BloodstreamDirectAmount = (FixedPoint2)0.05f;

    protected override void ApplyToxin(EntityUid body, EntityUid liver, FixedPoint2 amount)
    {
        if (!_proto.TryIndex(Poison, out _))
            return;

        var spec = new DamageSpecifier { DamageDict = { [Poison.Id] = amount } };
        Damageable.TryChangeDamage(body, spec, origin: liver);
    }

    protected override void ApplyBloodstreamDirectHit(EntityUid body, EntityUid liver, string group)
    {
        if (!_proto.TryIndex(Poison, out _))
            return;

        var spec = new DamageSpecifier { DamageDict = { [Poison.Id] = BloodstreamDirectAmount } };
        var ev = new OrganDamagedEvent(body, liver, spec, OrganDamageSource.Reagent);
        RaiseLocalEvent(liver, ref ev);
    }
}
