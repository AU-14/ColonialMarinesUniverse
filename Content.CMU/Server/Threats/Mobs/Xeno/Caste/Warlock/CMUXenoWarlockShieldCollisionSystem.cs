using Content.Server.Explosion.EntitySystems;
using Content.Shared._CMU14.Threats.Mobs.Xeno.Caste.Warlock;

namespace Content.Server._CMU14.Threats.Mobs.Xeno.Caste.Warlock;

/// <summary>
/// Cancels any <see cref="BeforeTriggerEvent"/> raised on a projectile that is currently frozen by
/// a psychic shield. The freeze itself happens in the shared warlock system via
/// <see cref="Robust.Shared.Physics.Events.PreventCollideEvent"/> and the projectile's own
/// reflect-attempt path; both add <see cref="CMUXenoFrozenProjectileComponent"/> to the projectile.
///
/// While that component is present, this handler tells <see cref="TriggerSystem"/> not to fire the
/// trigger. When the shield reflects or releases the projectile the frozen component is removed,
/// this handler stops cancelling, and any subsequent collision triggers normally - so a reflected
/// SLAW still detonates on the marine it comes back to.
///
/// This system is server-only because <see cref="BeforeTriggerEvent"/> lives in
/// <c>Content.Server.Explosion.EntitySystems</c>.
/// </summary>
public sealed class CMUXenoWarlockShieldCollisionSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoFrozenProjectileComponent, BeforeTriggerEvent>(OnFrozenProjectileBeforeTrigger);
    }

    private void OnFrozenProjectileBeforeTrigger(Entity<CMUXenoFrozenProjectileComponent> frozen, ref BeforeTriggerEvent args)
    {
        // Thrown grenades opt into keeping their fuse alive by setting AllowTriggerWhileFrozen.
        // For those, the trigger runs normally - if the shield does not reflect in time, the
        // grenade detonates at the shield face. Rockets and other collision-triggered projectiles
        // keep AllowTriggerWhileFrozen=false so they cannot self-detonate on shield contact.
        if (frozen.Comp.AllowTriggerWhileFrozen)
            return;

        args.Cancelled = true;
    }
}
