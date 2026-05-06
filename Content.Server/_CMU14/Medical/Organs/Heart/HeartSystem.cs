using Content.Shared._CMU14.Medical.Organs.Heart;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Organs.Heart;

public sealed class HeartSystem : SharedHeartSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";

    protected override void ApplyCardiacArrestAsphyx(EntityUid body, EntityUid heart, FixedPoint2 amount)
    {
        if (!_proto.TryIndex(Asphyxiation, out _))
            return;

        var spec = new DamageSpecifier { DamageDict = { [Asphyxiation.Id] = amount } };
        _damageable.TryChangeDamage(body, spec, ignoreResistances: true, origin: heart);
    }
}
