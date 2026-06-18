using Content.Shared._CMU14.Medical.Human.Organs.Liver;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Human.Organs.Liver;

public sealed partial class LiverSystem : SharedLiverSystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";

    protected override void ApplyToxin(EntityUid body, EntityUid liver, FixedPoint2 amount)
    {
        if (!_proto.TryIndex(Poison, out _))
            return;

        var spec = new DamageSpecifier { DamageDict = { [Poison.Id] = amount } };
        Damageable.TryChangeDamage(body, spec, origin: liver);
    }
}
