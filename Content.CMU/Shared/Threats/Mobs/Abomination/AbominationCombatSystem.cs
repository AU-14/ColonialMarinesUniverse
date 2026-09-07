using Content.Shared.Interaction.Events;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;

namespace Content.Shared.CMU14.Threats.Mobs.Abomination;

/// <summary>
///     Friendly-fire rules for the abomination team. Spit passes through
///     fellow abominations (flesh forms and disguised mimics) instead of
///     hurting them, and melee swings against them are cancelled the same
///     way XenoSystem blocks xeno-on-xeno attacks.
/// </summary>
public sealed partial class AbominationCombatSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationProjectileComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<AbominationProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<AbominationComponent, AttackAttemptEvent>(OnAbominationAttackAttempt);
        SubscribeLocalEvent<AbominationMimicTransformedComponent, AttackAttemptEvent>(OnMimicAttackAttempt);
    }

    // Pass through friendlies so the horde can spray over its own front line.
    private void OnPreventCollide(Entity<AbominationProjectileComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (IsFriendly(args.OtherEntity))
            args.Cancelled = true;
    }

    // Backstop for collisions that land anyway: no damage, eat the projectile.
    private void OnProjectileHit(Entity<AbominationProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (!IsFriendly(args.Target))
            return;

        args.Handled = true;
        QueueDel(ent);
    }

    private void OnAbominationAttackAttempt(Entity<AbominationComponent> ent, ref AttackAttemptEvent args)
        => CancelUnfriendlyMelee(ref args);

    private void OnMimicAttackAttempt(Entity<AbominationMimicTransformedComponent> ent, ref AttackAttemptEvent args)
        => CancelUnfriendlyMelee(ref args);

    private void CancelUnfriendlyMelee(ref AttackAttemptEvent args)
    {
        if (args.Target is not { } target)
            return;

        // Aboms can't trash their own kudzu coverage either
        if (HasComp<AbominationFleshKudzuComponent>(target))
        {
            args.Cancel();
            return;
        }

        if (!IsFriendly(target))
            return;

        // Disarm stays allowed, mirroring xeno melee rules.
        if (!args.Disarm)
            args.Cancel();
    }

    private bool IsFriendly(EntityUid target)
        => HasComp<AbominationComponent>(target)
           || HasComp<AbominationMimicTransformedComponent>(target);
}
