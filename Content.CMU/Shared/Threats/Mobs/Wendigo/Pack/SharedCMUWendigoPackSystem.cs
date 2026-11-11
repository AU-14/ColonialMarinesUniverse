using Content.Shared.CMU14.Threats.Mobs.Wendigo.Lab;
using Content.Shared.Interaction.Events;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Pack;

/// <summary>
/// Friendly-fire rules for the Wendigo pack. Melee swings between allies are cancelled (disarms stay
/// allowed, as for xenos) and projectiles fired by an ally pass through pack members.
/// </summary>
public sealed partial class SharedCMUWendigoPackSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUWendigoPackComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<CMUWendigoPackComponent, GettingAttackedAttemptEvent>(OnGettingAttacked);
        SubscribeLocalEvent<CMUWendigoPackComponent, PreventCollideEvent>(OnPreventCollide);
    }

    /// <summary>
    /// True when both entities are pack members, or when one is a tamed Wendigo whose master is the other.
    /// </summary>
    public bool IsAlly(EntityUid a, EntityUid b)
    {
        if (HasComp<CMUWendigoPackComponent>(a) && HasComp<CMUWendigoPackComponent>(b))
            return true;

        if (TryComp(a, out CMUWendigoTamedComponent? tamedA) && tamedA.Master == b)
            return true;

        return TryComp(b, out CMUWendigoTamedComponent? tamedB) && tamedB.Master == a;
    }

    private void OnAttackAttempt(Entity<CMUWendigoPackComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Cancelled || args.Disarm || args.Target is not { } target || target == ent.Owner)
            return;

        if (IsAlly(ent, target))
            args.Cancel();
    }

    // Covers a master swinging at their own tamed Wendigo.
    private void OnGettingAttacked(Entity<CMUWendigoPackComponent> ent, ref GettingAttackedAttemptEvent args)
    {
        if (args.Cancelled || args.Disarm || args.Attacker == ent.Owner)
            return;

        if (IsAlly(args.Attacker, ent))
            args.Cancelled = true;
    }

    private void OnPreventCollide(Entity<CMUWendigoPackComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp(args.OtherEntity, out ProjectileComponent? projectile) ||
            projectile.Shooter is not { } shooter ||
            shooter == ent.Owner)
        {
            return;
        }

        if (IsAlly(shooter, ent))
            args.Cancelled = true;
    }
}
