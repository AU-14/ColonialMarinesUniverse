using Content.Shared._CMU14.Medical.Organs.Lungs;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Organs.Lungs;

public sealed class LungsSystem : SharedLungsSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";

    /// <summary>
    ///     Bypasses resistances so a marine drowning on damaged lungs cannot
    ///     be saved by armour.
    /// </summary>
    protected override void ApplyAsphyx(EntityUid body, EntityUid lung, FixedPoint2 amount)
    {
        if (!_proto.TryIndex(Asphyxiation, out _))
            return;

        var spec = new DamageSpecifier { DamageDict = { [Asphyxiation.Id] = amount } };
        Damageable.TryChangeDamage(body, spec, ignoreResistances: true, origin: lung);
    }
}
