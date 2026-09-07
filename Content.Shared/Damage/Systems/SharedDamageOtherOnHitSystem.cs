using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;

namespace Content.Shared.Damage.Systems;

public abstract partial class SharedDamageOtherOnHitSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DamageExamineSystem _damageExamine = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageOtherOnHitComponent, DamageExamineEvent>(OnDamageExamine);
        SubscribeLocalEvent<DamageOtherOnHitComponent, AttemptPacifiedThrowEvent>(OnAttemptPacifiedThrow);
    }

    private void OnDamageExamine(Entity<DamageOtherOnHitComponent> ent, ref DamageExamineEvent args)
    {
        var damage = GetExamineDamage(ent) * _damageable.UniversalThrownDamageModifier;
        _damageExamine.AddDamageExamine(
            args.Message,
            _damageable.ApplyUniversalAllModifiers(damage),
            Loc.GetString("damage-throw"));
    }

    /// <summary>
    /// Gets the damage shown by examine. Derived platform systems can account for server-only item modifiers.
    /// </summary>
    protected virtual DamageSpecifier GetExamineDamage(Entity<DamageOtherOnHitComponent> ent)
    {
        return ent.Comp.Damage;
    }

    /// <summary>
    /// Prevent players with the Pacified status effect from throwing things that deal damage.
    /// </summary>
    private void OnAttemptPacifiedThrow(Entity<DamageOtherOnHitComponent> ent, ref AttemptPacifiedThrowEvent args)
    {
        args.Cancel("pacified-cannot-throw");
    }
}
