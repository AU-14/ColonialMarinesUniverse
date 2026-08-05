using Content.Shared.Damage.Components;
using Content.Shared.Explosion;

namespace Content.Shared.Damage.Systems;

public sealed class DamageProtectionBuffSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageProtectionBuffComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<DamageProtectionBuffComponent, GetExplosionResistanceEvent>(OnGetExplosionResistance);
    }

    private void OnDamageModify(Entity<DamageProtectionBuffComponent> ent, ref DamageModifyEvent args)
    {
        foreach (var modifier in ent.Comp.Modifiers.Values)
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifier);
    }

    private void OnGetExplosionResistance(
        Entity<DamageProtectionBuffComponent> ent,
        ref GetExplosionResistanceEvent args)
    {
        if (ent.Comp.ExplosionCoefficient is { } coefficient)
            args.DamageCoefficient *= coefficient;
    }
}
