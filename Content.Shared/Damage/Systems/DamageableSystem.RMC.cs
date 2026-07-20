using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem
{
    /// <summary>
    /// Compatibility entry point for RMC damage calls that carry tool and armor metadata.
    /// </summary>
    public DamageSpecifier? TryChangeDamage(
        EntityUid? uid,
        DamageSpecifier damage,
        bool ignoreResistances = false,
        bool interruptsDoAfters = true,
        DamageableComponent? damageable = null,
        EntityUid? origin = null,
        EntityUid? tool = null,
        int armorPiercing = 0,
        bool shouldIgnoreClawLogic = false)
    {
        if (uid is not { } owner || !_damageableQuery.Resolve(owner, ref damageable, false))
            return null;

        if (damage.Empty)
            return damage;

        return ChangeDamage(
            (owner, damageable),
            damage,
            ignoreResistances,
            interruptsDoAfters,
            origin,
            false,
            tool,
            armorPiercing,
            shouldIgnoreClawLogic);
    }

    public void SetDamage(EntityUid uid, DamageableComponent damageable, DamageSpecifier damage)
    {
        SetDamage((uid, damageable), damage);
    }

    public void AddDamage(EntityUid uid, DamageableComponent damageable, DamageSpecifier damage)
    {
        var total = GetAllDamage((uid, damageable));
        total += damage;
        SetDamage((uid, damageable), total);
    }

    public void SetAllDamage(EntityUid uid, DamageableComponent damageable, FixedPoint2 newValue)
    {
        SetAllDamage((uid, damageable), newValue);
    }

    public void SetDamageModifierSetId(
        EntityUid uid,
        ProtoId<DamageModifierSetPrototype>? damageModifierSetId,
        DamageableComponent? damageable = null)
    {
        SetDamageModifierSetId((uid, damageable), damageModifierSetId);
    }
}
