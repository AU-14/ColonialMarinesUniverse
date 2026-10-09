using System.Numerics;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class VehicleSystem
{
    [Dependency] private SharedBuckleSystem _cookOffBuckle = default!;
    [Dependency] private PullingSystem _cookOffPulling = default!;
    [Dependency] private ThrowingSystem _cookOffThrowing = default!;
    [Dependency] private SharedContainerSystem _cookOffContainers = default!;

    private void InitializeTankCookOff()
    {
        SubscribeLocalEvent<ActiveTankCookOffComponent, VehicleEntryAttemptEvent>(OnCookOffEntry);
        SubscribeLocalEvent<TankCookOffComponent, TankCookOffRupturedEvent>(OnCookOffRuptured);
    }

    private void OnCookOffEntry(Entity<ActiveTankCookOffComponent> ent, ref VehicleEntryAttemptEvent args)
    {
        args.Cancelled = true;
        if (_net.IsServer)
            _popup.PopupEntity(Loc.GetString("cmu-tank-cook-off-entry-blocked"), ent.Owner, args.User, PopupType.SmallCaution);
    }

    private void OnCookOffRuptured(Entity<TankCookOffComponent> ent, ref TankCookOffRupturedEvent args)
    {
        EjectOccupants(ent, ent.Comp.EjectionDistance, ent.Comp.EjectionSpeed);
    }

    // shared with the OB write-off, which boots the crew the same way
    public void EjectOccupants(EntityUid vehicle, float distance, float speed)
    {
        if (_net.IsClient || !TryComp<VehicleInteriorComponent>(vehicle, out var interior) ||
            interior.MapId == MapId.Nullspace)
            return;

        var origin = _transform.GetMapCoordinates(vehicle);
        if (origin.MapId == MapId.Nullspace)
            return;

        // Include unconscious/dead crew and mobs placed inside without using the doorway.
        // Snapshot before moving: unbuckling and map changes update occupant tracking.
        var occupants = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesOnMap(interior.MapId, occupants);
        var index = 0;
        foreach (var occupant in occupants)
        {
            if (TerminatingOrDeleted(occupant) || HasComp<GhostComponent>(occupant))
                continue;

            if (TryComp<BuckleComponent>(occupant, out var buckle))
                _cookOffBuckle.Unbuckle((occupant.Owner, buckle), null);
            _cookOffContainers.TryRemoveFromContainer((occupant.Owner, null, null), force: true);
            if (TryComp<PullableComponent>(occupant, out var pullable))
                _cookOffPulling.TryStopPull(occupant, pullable);
            if (TryComp<PullerComponent>(occupant, out var puller) &&
                TryComp<PullableComponent>(puller.Pulling, out var pulled))
                _cookOffPulling.TryStopPull(puller.Pulling!.Value, pulled);

            var angle = index++ * MathF.Tau / Math.Max(1, occupants.Count);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var start = origin;
            // Start outside the hull on clear ground where possible. The throw itself uses normal collisions.
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var candidateAngle = angle + attempt * MathF.Tau / 16;
                var candidateDirection = new Vector2(MathF.Cos(candidateAngle), MathF.Sin(candidateAngle));
                var candidate = origin.Offset(candidateDirection * 2.2f);
                var coordinates = new EntityCoordinates(_mapSystem.GetMap(origin.MapId), candidate.Position);
                if (IsExitDestinationBlocked(coordinates, vehicle, occupant))
                    continue;
                direction = candidateDirection;
                start = candidate;
                break;
            }

            _transform.SetMapCoordinates(occupant, start);
            UntrackOccupant(occupant, vehicle);
            _cookOffThrowing.TryThrow(occupant, direction * distance, speed,
                recoil: false, playSound: false, doSpin: false);
        }
    }
}
