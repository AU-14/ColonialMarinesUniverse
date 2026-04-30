using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Wounds;

public sealed class CMUWoundsSystem : SharedCMUWoundsSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedRMCDamageableSystem _rmcDamageable = default!;

    private static readonly ProtoId<DamageTypePrototype> Bloodloss = "Bloodloss";
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    protected override void ApplyInternalBleed(EntityUid body, EntityUid part, float amount)
    {
        if (amount <= 0f)
            return;
        if (!_proto.TryIndex(Bloodloss, out _))
            return;

        var spec = new DamageSpecifier { DamageDict = { [Bloodloss.Id] = (FixedPoint2)amount } };
        Damageable.TryChangeDamage(body, spec, ignoreResistances: true, origin: part);
    }

    protected override void ApplyWoundHealingDamage(EntityUid body, EntityUid part, WoundType type, FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero)
            return;

        switch (type)
        {
            case WoundType.Brute:
                ApplyWoundHealingDamage(body, part, BruteGroup, amount);
                break;
            case WoundType.Burn:
                ApplyWoundHealingDamage(body, part, BurnGroup, amount);
                break;
        }
    }

    private void ApplyWoundHealingDamage(
        EntityUid body,
        EntityUid part,
        ProtoId<DamageGroupPrototype> group,
        FixedPoint2 amount)
    {
        if (!TryComp<DamageableComponent>(body, out var damageable))
            return;

        var spec = _rmcDamageable.DistributeHealing((body, damageable), group, amount);
        Damageable.TryChangeDamage(body,
            spec,
            ignoreResistances: true,
            interruptsDoAfters: false,
            damageable: damageable,
            origin: part);
    }

    public bool TryApplyTreaterDamage(
        EntityUid body,
        EntityUid user,
        EntityUid tool,
        ProtoId<DamageGroupPrototype> group,
        FixedPoint2 damage)
    {
        if (damage == FixedPoint2.Zero)
            return false;

        if (!TryComp<DamageableComponent>(body, out var damageable))
            return false;

        var spec = _rmcDamageable.DistributeDamageCached((body, damageable), group, damage);
        if (spec.Empty)
            return false;

        return Damageable.TryChangeDamage(body,
            spec,
            ignoreResistances: true,
            interruptsDoAfters: false,
            damageable: damageable,
            origin: user,
            tool: tool) is not null;
    }
}
