using System.Numerics;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Pulling.Events;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Keeps riders of an open vehicle in their seats. The buckle system unbuckles a rider who moves
/// off the strap by even a few millimetres, and the physics solver adds every awake body's velocity
/// and contact corrections to its local position, so any bump to a rider would throw them out.
/// While seated, the rider's hard contacts are cancelled and their velocity is cleared around every
/// step (see <see cref="CMUJeepPhysicsController"/>). Their body keeps its type, so tile fires,
/// smoke and bullets still reach them, and knockbacks, throws and drags still pull them out.
/// </summary>
public sealed class CMUVehicleSeatedSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleSeatedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUVehicleSeatedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUVehicleSeatedComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<CMUVehicleSeatedComponent, StartPullAttemptEvent>(OnStartPullAttempt);
    }

    private void OnStartup(Entity<CMUVehicleSeatedComponent> ent, ref ComponentStartup args)
    {
        // A rider can't pull from a seat: the pull would tug them off it. Server state already
        // carries the stop, so it isn't repeated while that state is being applied.
        if (!_timing.ApplyingState &&
            TryComp(ent, out PullerComponent? puller) &&
            puller.Pulling is { } pulled &&
            TryComp(pulled, out PullableComponent? pullable))
        {
            _pulling.TryStopPull(pulled, pullable, ent);
        }

        if (!TryComp(ent, out PhysicsComponent? body))
            return;

        Hold((ent, body));

        // Contacts made before buckling skip the filter below until they are rebuilt.
        _physics.RegenerateContacts((ent, body));
    }

    private void OnShutdown(Entity<CMUVehicleSeatedComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent) && TryComp(ent, out PhysicsComponent? body))
            _physics.RegenerateContacts((ent, body));
    }

    private void OnStartPullAttempt(Entity<CMUVehicleSeatedComponent> ent, ref StartPullAttemptEvent args)
    {
        if (args.Puller == ent.Owner)
            args.Cancel();
    }

    private void OnPreventCollide(Entity<CMUVehicleSeatedComponent> ent, ref PreventCollideEvent args)
    {
        if (args.OurFixture.Hard && args.OtherFixture.Hard)
            args.Cancelled = true;
    }

    /// <summary>
    /// Clears a seated rider's velocity so the solver leaves them on the strap. A rider being thrown
    /// keeps it, so knockbacks still carry them off the seat.
    /// </summary>
    public void Hold(Entity<PhysicsComponent> rider)
    {
        if (HasComp<ThrownItemComponent>(rider) ||
            rider.Comp.LinearVelocity == Vector2.Zero && rider.Comp.AngularVelocity == 0f)
        {
            return;
        }

        _physics.SetLinearVelocity(rider, Vector2.Zero, wakeBody: false, body: rider.Comp);
        _physics.SetAngularVelocity(rider, 0f, body: rider.Comp);
    }
}
