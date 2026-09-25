using System.Numerics;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Vehicles;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Dropship.MultiDeck;

public sealed partial class MohawkSystem
{
    private Dictionary<EntityUid, (Vector2 Position, Angle Rotation)> CollectRampVehicles(
        EntityUid ship, MohawkMechanismsComponent mechanisms, List<EntityUid> parts, int deployedStages)
    {
        var riders = new Dictionary<EntityUid, (Vector2, Angle)>();
        var inverse = _transform.GetInvWorldMatrix(ship);

        // Query every moving tile against actual collision fixtures. A vehicle's
        // origin can be several tiles away while its bumper is on the platform.
        // Raising waits for the complete cabin floor, since a large vehicle can
        // span several stages. Re-query at completion to include late arrivals.
        if (deployedStages == 0)
        {
            foreach (var part in parts)
            {
                if (TryComp<MohawkRampSegmentComponent>(part, out var segment) && segment.Lower &&
                    segment.Stage != 4 && (segment.Deployed || mechanisms.RampDeployed))
                    Collect(part, _transform.GetWorldPosition(part));
            }
        }
        else
        {
            foreach (var (marker, position) in mechanisms.CabinRampMarkers)
            {
                if (TryComp<MohawkRampSegmentComponent>(marker, out var segment) &&
                    segment.Stage < 4 && segment.Stage < deployedStages && !segment.Deployed)
                    Collect(ship, _transform.ToMapCoordinates(new EntityCoordinates(ship, position)).Position);
            }
        }

        return riders;

        void Collect(EntityUid origin, Vector2 center)
        {
            var bounds = new Box2Rotated(Box2.CenteredAround(center, Vector2.One),
                _transform.GetWorldRotation(ship), center);
            // The typed lookup can select an origin-only prefilter for rare
            // components. Use the spatial lookup so even bumper overlaps count.
            var candidates = _lookup.GetEntitiesIntersecting(Transform(origin).MapID, bounds,
                LookupFlags.Dynamic | LookupFlags.Static);
            foreach (var vehicle in candidates)
            {
                if (!HasComp<CMUVehicleZTraversalComponent>(vehicle))
                    continue;

                var local = Vector2.Transform(_transform.GetWorldPosition(vehicle), inverse);
                riders.TryAdd(vehicle, (local, _transform.GetWorldRotation(vehicle)));
            }
        }
    }

    private void MoveRampVehicles(EntityUid ship,
        Dictionary<EntityUid, (Vector2 Position, Angle Rotation)> riders, bool descending)
    {
        if (!TryComp<MultiDeckDropshipComponent>(ship, out var assembly) ||
            !assembly.Decks.TryGetValue(-1, out var lower))
            return;

        var mechanisms = Comp<MohawkMechanismsComponent>(ship);
        var offset = mechanisms.LoweredRampOffset;
        foreach (var (vehicle, (position, rotation)) in riders)
        {
            var target = descending ? lower : ship;
            var displacement = descending ? offset + mechanisms.VehicleUnloadOffset : -offset;
            _transform.SetCoordinates(vehicle, new EntityCoordinates(target, position + displacement));
            _transform.SetWorldRotation(vehicle, rotation);
            if (descending)
                _transform.AttachToGridOrMap(vehicle);
            if (TryComp<CMUZPhysicsComponent>(vehicle, out var physics))
            {
                _zLevels.SetZLocalPosition((vehicle, physics), 0f);
                _zLevels.SetZVelocity((vehicle, physics), 0f);
            }
        }
    }
}
