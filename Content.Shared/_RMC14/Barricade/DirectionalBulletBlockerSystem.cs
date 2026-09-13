using System.Numerics;
using Content.Shared._RMC14.Marines;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Barricade;

public sealed class DirectionalBulletBlockerSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DirectionalBulletBlockerComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<DirectionalBulletBlockerComponent> ent, ref PreventCollideEvent args)
    {
        if (!TryComp(args.OtherEntity, out ProjectileComponent? projectile))
            return;

        if (!TryComp(args.OtherEntity, out PhysicsComponent? physics))
            return;

        // Get barricade rotation
        var barricadeRotation = _transform.GetWorldRotation(ent);

        // Get projectile velocity direction
        var projectileVelocity = physics.LinearVelocity;
        if (projectileVelocity.LengthSquared() < 0.001f)
            return;

        var projectileDirection = projectileVelocity.Normalized();

        // Calculate barricade's front direction vector (South is front for barricades)
        var frontDirection = barricadeRotation.RotateVec(new Vector2(0, -1));

        // Calculate angle between projectile direction (from barricade's perspective) and barricade front
        // Negate projectile direction to get "direction from projectile to barricade"
        var dotProduct = Vector2.Dot(-projectileDirection, frontDirection);
        var angleToProjectile = MathF.Acos(Math.Clamp(dotProduct, -1f, 1f));
        var angleDegrees = angleToProjectile * 180f / MathF.PI;

        // Check if projectile is coming from behind (outside the front blocking cone)
        // If the angle is greater than half the block angle, projectile is coming from side/behind
        if (angleDegrees > ent.Comp.FrontBlockAngle / 2)
        {
            args.Cancelled = true; // Allow projectile to pass
            return;
        }

        // Apply block chance
        if (_random.NextFloat() > ent.Comp.BlockChance)
        {
            args.Cancelled = true; // Allow projectile to pass based on chance
        }
    }

}
