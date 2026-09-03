using System;
using Robust.Shared.Maths;

namespace Content.Shared._RMC14.Vehicle;

public static class VehicleTurretDirectionHelpers
{
    private const double EighthPi = Math.PI / 8d;
    private const double BoundaryEpsilon = 1e-12;

    public static Direction GetRenderAlignedCardinalDir(Angle facing)
    {
        var angle = facing.Reduced().FlipPositive();
        var theta = angle.Theta;

        // East and west each own a 45 degree sector in total, centered on their
        // cardinal direction. Every other angle resolves to north or south.
        if (theta >= EighthPi * 3 - BoundaryEpsilon &&
            theta <= EighthPi * 5 + BoundaryEpsilon)
            return Direction.East;

        if (theta >= EighthPi * 11 - BoundaryEpsilon &&
            theta <= EighthPi * 13 + BoundaryEpsilon)
            return Direction.West;

        if (theta > EighthPi * 5 && theta < EighthPi * 11)
            return Direction.North;

        return Direction.South;
    }
}
