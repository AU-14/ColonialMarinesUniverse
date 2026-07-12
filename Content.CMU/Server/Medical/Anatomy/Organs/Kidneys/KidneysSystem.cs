using Content.Shared._CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Anatomy.Organs.Kidneys;

public sealed partial class KidneysSystem : SharedKidneysSystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateServer(frameTime);
    }

    protected override void ApplyToxin(EntityUid body, EntityUid kidneys, FixedPoint2 amount)
    {
        if (!_proto.TryIndex(Poison, out _))
            return;

        var spec = new DamageSpecifier { DamageDict = { [Poison.Id] = amount } };
        _damageable.TryChangeDamage(body, spec, origin: kidneys);
    }
}
