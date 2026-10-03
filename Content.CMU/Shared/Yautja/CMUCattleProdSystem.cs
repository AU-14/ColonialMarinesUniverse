using Content.Shared._RMC14.Xenonids;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.CMU14.Yautja;

/// <summary>Adds a direct xeno stun to the prod's existing charged stamina hit.</summary>
public sealed class CMUCattleProdSystem : EntitySystem
{
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        // The normal stamina handler consumes the charge, even for targets without stamina.
        SubscribeLocalEvent<CMUCattleProdComponent, MeleeHitEvent>(OnHit, before: [typeof(SharedStaminaSystem)]);
    }

    private void OnHit(Entity<CMUCattleProdComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var attempt = new StaminaDamageOnHitAttemptEvent();
        RaiseLocalEvent(ent, ref attempt);
        if (attempt.Cancelled)
            return;

        foreach (var target in args.HitEntities)
        {
            if (HasComp<XenoComponent>(target))
                _stun.TryParalyze(target, ent.Comp.StunDuration, refresh: true);
        }
    }
}
