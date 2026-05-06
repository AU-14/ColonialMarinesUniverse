using Content.Shared._CMU14.Medical.Organs.Kidneys;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Organs.Kidneys;

public sealed class KidneysSystem : SharedKidneysSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";

    protected override void ApplyToxin(EntityUid body, EntityUid kidneys, FixedPoint2 amount)
    {
        if (!_proto.TryIndex(Poison, out _))
            return;

        var spec = new DamageSpecifier { DamageDict = { [Poison.Id] = amount } };
        _damageable.TryChangeDamage(body, spec, origin: kidneys);
    }
}
