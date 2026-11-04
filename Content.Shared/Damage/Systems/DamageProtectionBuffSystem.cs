using Content.Shared.Damage.Components;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageProtectionBuffSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageProtectionBuffComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<DamageProtectionBuffComponent> ent, ref DamageModifyEvent args)
    {
        foreach (var modifier in ent.Comp.Modifiers.Values)
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifier);
    }
}
