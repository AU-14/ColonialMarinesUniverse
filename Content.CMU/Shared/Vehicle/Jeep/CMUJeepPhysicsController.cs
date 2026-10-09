using System.Numerics;
using Content.Shared.Physics.Controllers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Jeeps are moved by the grid mover, not by physics, but their dynamic bodies still take contact
/// impulses, for example when another jeep is driven into a parked one. Clearing that velocity after
/// every step stops a bumped jeep from sliding and spinning away, and keeps tile friction from
/// meeting an input mover with a moving dynamic body. Seated riders are held still on both sides of
/// the step, so nothing the solver integrates moves them off their strap.
/// </summary>
public sealed class CMUJeepPhysicsController : VirtualController
{
    [Dependency] private CMUVehicleSeatedSystem _seated = default!;

    public override void Initialize()
    {
        // Conveyors, and mob movement before them, set velocities before the solve; hold riders after.
        UpdatesAfter.Add(typeof(SharedConveyorController));
        base.Initialize();
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);
        HoldRiders();
    }

    public override void UpdateAfterSolve(bool prediction, float frameTime)
    {
        base.UpdateAfterSolve(prediction, frameTime);
        HoldRiders();

        var query = EntityQueryEnumerator<CMUJeepComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out _, out var body))
        {
            if (body.LinearVelocity == Vector2.Zero && body.AngularVelocity == 0f)
                continue;

            PhysicsSystem.SetLinearVelocity(uid, Vector2.Zero, wakeBody: false, body: body);
            PhysicsSystem.SetAngularVelocity(uid, 0f, body: body);
        }
    }

    private void HoldRiders()
    {
        var riders = EntityQueryEnumerator<CMUVehicleSeatedComponent, PhysicsComponent>();
        while (riders.MoveNext(out var uid, out _, out var body))
        {
            _seated.Hold((uid, body));
        }
    }
}
