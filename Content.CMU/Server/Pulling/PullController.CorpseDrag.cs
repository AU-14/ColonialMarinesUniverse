using System.Numerics;
using Content.Shared.CMU14.Pulling;
using Content.Shared.CMU14.Ship;
using Content.Shared.Movement.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server.Movement.Systems;

public sealed partial class PullController
{
    [Dependency] private CMUXenoCorpseDragSystem _corpseDrag = default!;

    private bool TryLimitCorpsePullImpulse(EntityUid puller, EntityUid target, PhysicsComponent body, ref Vector2 impulse)
    {
        var multiplier = _corpseDrag.GetCorpseDragMultiplier(target);
        if (multiplier == 1f)
            return true;

        if (_corpseDrag.IsPullerMoving(puller))
            return false;

        // Ctrl-right-click can position a corpse around a stationary puller. Bound the
        // velocity the command adds, including when a new destination arrives every tick.
        var walking = TryComp<InputMoverComponent>(puller, out var mover) && !mover.Sprinting;
        var speed = CompOrNull<MovementSpeedModifierComponent>(puller);
        var dragSpeed = walking
            ? speed?.CurrentWalkSpeed ?? MovementSpeedModifierComponent.DefaultBaseWalkSpeed * multiplier
            : speed?.CurrentSprintSpeed ?? MovementSpeedModifierComponent.DefaultBaseSprintSpeed * multiplier;
        if (_gravity.IsWeightless(puller))
        {
            var floating = new CMUZeroGMovementEvent();
            RaiseLocalEvent(puller, ref floating);
            var floatSpeed = walking
                ? speed?.WeightlessWalkSpeed ?? MovementSpeedModifierComponent.DefaultBaseWalkSpeed
                : speed?.WeightlessSprintSpeed ?? MovementSpeedModifierComponent.DefaultBaseSprintSpeed;
            dragSpeed = MathF.Min(dragSpeed, floatSpeed * floating.SpeedMultiplier);
        }

        var nextVelocity = body.LinearVelocity + impulse * body.InvMass;
        var limitSquared = MathF.Max(dragSpeed * dragSpeed, body.LinearVelocity.LengthSquared());
        if (nextVelocity.LengthSquared() > limitSquared)
            impulse = (Vector2.Normalize(nextVelocity) * MathF.Sqrt(limitSquared) - body.LinearVelocity) * body.Mass;

        return true;
    }
}
