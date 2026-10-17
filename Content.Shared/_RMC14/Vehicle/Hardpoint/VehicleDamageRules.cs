using System;
using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared._RMC14.Vehicle;

// CMU14
public enum VehicleDamageRegion : byte
{
    Exterior,
    Front,
    Rear,
    Sides,
    Internal,
}

// CMU14
public static class VehicleDamageRules
{
    public static int GetRegionPriority(VehicleDamageRegion region, Vector2 approach)
    {
        if (region == VehicleDamageRegion.Internal)
            return 0;

        if (approach.LengthSquared() < 0.0001f)
            return 1;

        var forward = Vector2.Dot(approach, Angle.Zero.ToWorldVec());
        var side = MathF.Abs(approach.X) > MathF.Abs(approach.Y);
        return region switch
        {
            VehicleDamageRegion.Front when !side && forward > 0f => 2,
            VehicleDamageRegion.Rear when !side && forward < 0f => 2,
            VehicleDamageRegion.Sides when side => 2,
            _ => 1,
        };
    }

    public static float GetConditionMultiplier(float integrityFraction, float healthyFraction, float minimum)
    {
        var progress = Math.Clamp(integrityFraction / MathF.Max(healthyFraction, 0.001f), 0f, 1f);
        return Math.Clamp(minimum, 0f, 1f) + (1f - Math.Clamp(minimum, 0f, 1f)) * progress;
    }
}
