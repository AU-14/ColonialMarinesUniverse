using System.Numerics;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Trigger.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.CMU14.Weapons.Grenades;

public sealed class CMUIgnoreFixedPointSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Update(float frameTime)
    {
        // The launcher hands every fired projectile a fixed-distance arc when it's shot; strip it straight back off.
        var query = EntityQueryEnumerator<CMUIgnoreFixedPointComponent, ProjectileFixedDistanceComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var ignore, out _, out var physics))
        {
            RemComp<ProjectileFixedDistanceComponent>(uid);

            // The launcher's own fuse is meant for grenades, not pellets.
            RemComp<ActiveTimerTriggerComponent>(uid);
            RemComp<TimerTriggerComponent>(uid);

            if (physics.LinearVelocity == Vector2.Zero)
                continue;

            var velocity = physics.LinearVelocity.Normalized() * ignore.Speed;
            _physics.SetLinearVelocity(uid, velocity, body: physics);
        }
    }
}
