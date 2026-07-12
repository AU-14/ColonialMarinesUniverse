using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Anatomy.Organs.Heart;

public sealed partial class HeartSystem : SharedHeartSystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateServer(frameTime);
    }

    protected override void ApplyCardiacArrestAsphyx(EntityUid body, EntityUid heart, FixedPoint2 amount)
    {
        if (!_proto.TryIndex(Asphyxiation, out _))
            return;

        var spec = new DamageSpecifier { DamageDict = { [Asphyxiation.Id] = amount } };
        _damageable.TryChangeDamage(body, spec, ignoreResistances: true, origin: heart);
    }

    protected override void ApplyHeartOrganDamage(
        EntityUid body,
        EntityUid heart,
        FixedPoint2 asphyx,
        FixedPoint2 toxin)
    {
        var spec = new DamageSpecifier();
        if (asphyx > FixedPoint2.Zero && _proto.TryIndex(Asphyxiation, out _))
            spec.DamageDict[Asphyxiation.Id] = asphyx;
        if (toxin > FixedPoint2.Zero && _proto.TryIndex(Poison, out _))
            spec.DamageDict[Poison.Id] = toxin;
        if (spec.GetTotal() <= FixedPoint2.Zero)
            return;

        _damageable.TryChangeDamage(body, spec, ignoreResistances: true, origin: heart);
    }
}
