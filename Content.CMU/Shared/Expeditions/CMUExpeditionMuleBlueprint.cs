using System;
using System.Collections.Generic;
using static Content.Shared.CMU14.Expeditions.CMUExpeditionWreckBlueprints;

namespace Content.Shared.CMU14.Expeditions;

/// <summary>
/// Original MULE equipment lander: open cargo keel, separate cockpit, outboard engine pods and aft ramp.
/// Coordinates face north. Existing licensed Alamo art supplies individual structural modules.
/// </summary>
internal static class CMUExpeditionMuleBlueprint
{
    internal static readonly Hull[] Hulls = BuildHull();
    internal static readonly Floor[] Floors = BuildFloors();
    internal static readonly Hull[] Fittings =
    [
        new(-1, 8, "CMUExpeditionMuleFlightConsole", 0),
        new(1, 8, "CMUExpeditionMuleFlightConsole", 0),
        new(-1, 7, "CMUExpeditionWreckSeat", 2),
        new(1, 7, "CMUExpeditionWreckSeat", 2),
        new(-3, 4, "CMUExpeditionMulePowerRack", 0),
        new(3, 4, "CMUExpeditionMulePowerRack", 0),
        new(-3, 2, "CMUExpeditionWreckSeat", 3),
        new(3, 2, "CMUExpeditionWreckSeat", 1),
        new(-3, -3, "CMUExpeditionMuleCargo", 0),
        new(3, -3, "CMUExpeditionMuleCargo", 0),
        new(-3, -6, "CMUExpeditionMuleCargo", 0),
        new(3, -6, "CMUExpeditionMuleCargo", 0),
        new(-3, -8, "CMUExpeditionMuleToolCase", 0),
        new(3, -8, "CMUExpeditionAbandonedPack", 0),
    ];

    private static Hull[] BuildHull()
    {
        var hull = new List<Hull>();
        // Personnel exits are forward of the wing roots, opening onto terrain rather than enclosed struts.
        for (var y = -9; y <= 5; y++)
        {
            if (y is 3 or 4)
                continue;
            hull.Add(new(-4, y, "CMUExpeditionWreckCMAlamoWall18", 0));
            hull.Add(new(4, y, "CMUExpeditionWreckCMAlamoWall19", 0));
        }
        foreach (var piece in FallujahHull)
        {
            // Retain the complete cockpit cap as a module, not the donor ship's floor plan.
            if (piece.Y >= 6)
                hull.Add(piece);
            // Each engine is mounted three tiles outboard on a short structural wing.
            if (Math.Abs(piece.X) >= 3 && piece.Y is >= -7 and <= -2)
                hull.Add(piece with { X = piece.X + Math.Sign(piece.X) * 3, Y = piece.Y + 3 });
        }
        foreach (var side in new[] { -1, 1 })
        {
            for (var x = 5; x <= 6; x++)
            {
                hull.Add(new(side * x, 2, "CMUExpeditionWreckCMAlamoWall78", 0));
                hull.Add(new(side * x, -1, "CMUExpeditionWreckCMAlamoWall78", 2));
            }
            // Aft coaming leaves a three-tile loading ramp and clear central towing lane.
            hull.Add(new(side * 4, -10, side < 0 ? "CMUExpeditionWreckCMAlamoWall83" : "CMUExpeditionWreckCMAlamoWall75", 2));
            hull.Add(new(side * 3, -10, "CMUExpeditionWreckCMAlamoWall78", 2));
            hull.Add(new(side * 2, -10, "CMUExpeditionWreckCMAlamoWall78", 2));
            hull.Add(new(side * 3, 5, "CMUExpeditionWreckCMAlamoWall78", 0));
            hull.Add(new(side * 2, 5, "CMUExpeditionWreckCMAlamoWall78", 0));
        }
        return hull.ToArray();
    }

    private static Floor[] BuildFloors()
    {
        var floor = new List<Floor>();
        for (var y = -10; y <= 5; y++)
        for (var x = -4; x <= 4; x++)
        {
            var tile = Math.Abs(x) == 2 && y <= 3 ? "CMFloorPlatingWarnplate" :
                Math.Abs(x) < 2 ? "CMShuttleTileRasputin3" : "CMShuttleTileRasputin15";
            floor.Add(new(x, y, tile, 0));
        }
        foreach (var tile in FallujahFloor)
            if (tile.Y >= 6)
                floor.Add(tile);
        for (var y = -13; y < -10; y++)
        for (var x = -1; x <= 1; x++)
            floor.Add(new(x, y, "RMCFloorPlatingDamage1", 0));
        foreach (var side in new[] { -1, 1 })
        for (var y = 0; y <= 1; y++)
        for (var x = 5; x <= 6; x++)
            floor.Add(new(side * x, y, "CMFloorPlating", 0));
        return floor.ToArray();
    }
}
