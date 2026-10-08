using System.Numerics;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>
/// One oriented solid in a live review snapshot. X/Y are relative to the controlled
/// actor, Z is up, and one unit is one tile. Source groups are admitted atomically.
/// </summary>
public readonly record struct CMU3DSceneBox(
    Vector3 Center,
    Vector3 HalfSize,
    float Yaw,
    Color Color,
    EntityUid? Source = null,
    CMU3DPartShape Shape = CMU3DPartShape.Box,
    ushort SurfaceIndex = 0,
    CMU3DSurfaceAxis SurfaceAxis = CMU3DSurfaceAxis.XZ,
    float SurfaceVScale = 1,
    bool SurfaceFlipU = false)
{
    /// <summary>Local tilt in radians, lifting +X toward +Z before yaw.</summary>
    public float Pitch { get; init; }

    public Vector3 AxisAlignedHalfSize
    {
        get
        {
            var cp = MathF.Abs(MathF.Cos(Pitch));
            var sp = MathF.Abs(MathF.Sin(Pitch));
            var x = HalfSize.X * cp + HalfSize.Z * sp;
            var z = HalfSize.X * sp + HalfSize.Z * cp;
            var c = MathF.Abs(MathF.Cos(Yaw));
            var s = MathF.Abs(MathF.Sin(Yaw));
            return new Vector3(x * c + HalfSize.Y * s, x * s + HalfSize.Y * c, z);
        }
    }
}
