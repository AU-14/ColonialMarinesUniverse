using System.Numerics;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private Vector2 SkilledAim(CMUExpeditionAgentComponent agent, EntityUid target, Vector2 origin,
        Vector2 position, Vector2 velocity, float projectileSpeed)
    {
        agent.AimSkill = CMUInfantryAimPolicy.ClampSkill(agent.AimSkill);
        var now = _timing.CurTime;
        if (agent.AimSkill < 1 && (agent.AimErrorTarget != target || now >= agent.NextAimCorrection))
        {
            agent.AimErrorTarget = target;
            agent.AimError = new Vector2(_visionRandom.NextFloat(-1, 1), _visionRandom.NextFloat(-1, 1));
            agent.NextAimCorrection = now + TimeSpan.FromSeconds(CMUInfantryAimPolicy.CorrectionInterval(agent.AimSkill));
        }
        return CMUInfantryAimPolicy.AimPoint(origin, position, velocity, projectileSpeed, agent.AimSkill, agent.AimError);
    }

    private bool HasSteadyAim(EntityUid uid, CMUExpeditionAgentComponent agent, Entity<GunComponent> gun)
    {
        if (CMUInfantryAimPolicy.ClampSkill(agent.AimSkill) >= 1)
            return true;
        if (agent.Target is not { } target || !Visible(uid, target, agent.FireRange))
            return false;
        var origin = _transform.GetWorldPosition(uid);
        var position = _transform.GetWorldPosition(target);
        var velocity = TryComp<PhysicsComponent>(target, out var body) ? body.LinearVelocity : Vector2.Zero;
        var actual = SkilledAim(agent, target, origin, position, velocity, gun.Comp.ProjectileSpeedModified);
        var ideal = CMUInfantryAimPolicy.AimPoint(origin, position, velocity, gun.Comp.ProjectileSpeedModified, 1, Vector2.Zero);
        // Entity-homing shots retain their native behavior, but require the shooter to hold
        // their reticle on the body while acquiring the lock. Ordinary shots remain available.
        return Vector2.DistanceSquared(actual, ideal) <= 0.35f * 0.35f;
    }
}
