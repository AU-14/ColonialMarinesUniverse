using System;
using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared.CMU14.Dropship.TacticalLand;

/// <summary>Limited thrust vectoring for lift nozzles, without turning lift into forward exhaust.</summary>
public static class GunshipHoverExhaust
{
    public const float MaximumGroundDeflection = 0.6f;
    public const float GroundContactDistance = 1.55f;

    public static Vector2 NozzleThrust(Vector2 translation, float turn, Vector2 leverArm)
    {
        var tangent = new Vector2(-leverArm.Y, leverArm.X) / MathF.Max(leverArm.Length(), 1f);
        var thrust = translation + tangent * (turn * 0.65f);
        var length = thrust.Length();
        return length > 1f ? thrust / length : thrust;
    }

    public static Vector2 GroundDeflection(Vector2 thrust, Angle shipRotation, Angle eyeRotation)
    {
        var viewThrust = (shipRotation + eyeRotation).RotateVec(thrust);
        // Shader UVs point down; world coordinates point up. Exhaust opposes the applied force.
        return new Vector2(-viewThrust.X, viewThrust.Y) * MaximumGroundDeflection;
    }

    public static Vector2 GroundContactOffset(Vector2 thrust, Angle shipRotation, Angle eyeRotation)
    {
        var deflection = GroundDeflection(thrust, shipRotation, eyeRotation);
        var viewOffset = new Vector2(deflection.X, -GroundContactDistance - deflection.Y);
        return (-eyeRotation).RotateVec(viewOffset);
    }
}
