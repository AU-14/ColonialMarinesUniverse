using System.Collections.Generic;
using System.Numerics;
using Content.Shared.CMU14.Blackfoot;
using Content.Shared.CMU14.Ship;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Vehicles;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Shared.Vehicle;

public sealed partial class GridVehicleMoverSystem
{
    [Dependency] private CMUZeroGSpaceSystem _spaceTraction = default!;
    [Dependency] private CMUVehicleZTraversalSystem _vehicleSupport = default!;

    private readonly List<Vector2> _spaceSupportSamples = new();

    private bool HasCMUSpaceTraction(Entity<CMUVehicleZTraversalComponent> vehicle)
    {
        if (HasComp<CMUZHoverComponent>(vehicle))
            return true;

        if (TryComp(vehicle, out BlackfootFlightComponent? flight) &&
            flight.State is BlackfootFlightState.VTOL or BlackfootFlightState.Flight)
        {
            return true;
        }

        var xform = Transform(vehicle);
        if (HasComp<CMUZLevelMapComponent>(xform.MapUid))
        {
            // Normal Z falling already suppresses driving; zero-g ships suppress falling itself.
            if (!_spaceTraction.IsOnZeroGMap(vehicle))
                return true;

            if (_vehicleSupport.TryGetSupportState(vehicle, out var support))
                return !support.ShouldTip(vehicle.Comp.EdgeTipUnsupportedFraction);
        }

        // Single-level space maps do not have a Z distance query; sample their floor directly.
        if (!TryComp(vehicle, out FixturesComponent? fixtures) ||
            !CMUVehicleSupportFootprint.TryGetFixtureLocalAabb(fixtures, out var bounds))
        {
            return false;
        }

        CMUVehicleSupportFootprint.GenerateWorldSamples(bounds,
            vehicle.Comp.SupportSampleSpacing, vehicle.Comp.SupportSampleInset,
            transform.GetWorldPosition(xform), transform.GetWorldRotation(xform), _spaceSupportSamples);
        var supported = 0;
        foreach (var sample in _spaceSupportSamples)
        {
            if (!_spaceTraction.IsOpenAt(new MapCoordinates(sample, xform.MapID)))
                supported++;
        }

        return !CMUVehicleSupportFootprint.ShouldTip(supported, _spaceSupportSamples.Count,
            vehicle.Comp.EdgeTipUnsupportedFraction);
    }
}
