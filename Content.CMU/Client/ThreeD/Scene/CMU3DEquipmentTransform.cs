using System.Numerics;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Rigid equipment transforms shared by presentation and targeting.</summary>
public readonly record struct CMU3DEquipmentTransform(Vector3 Origin, Vector3 X, Vector3 Y, Vector3 Z)
{
    public Vector3 Point(Vector3 local) => Origin + Direction(local);
    public Vector3 Direction(Vector3 local) => X * local.X + Y * local.Y + Z * local.Z;
    public Vector3 InversePoint(Vector3 world) => InverseDirection(world - Origin);
    public Vector3 InverseDirection(Vector3 world) => new(
        Vector3.Dot(world, X) / X.LengthSquared(),
        Vector3.Dot(world, Y) / Y.LengthSquared(),
        Vector3.Dot(world, Z) / Z.LengthSquared());

    // Targeting uses the same fixed-point root vectors uploaded to the shader.
    public CMU3DEquipmentTransform Quantized() => new(Quantize(Origin), Quantize(X), Quantize(Y), Quantize(Z));
    private static Vector3 Quantize(Vector3 value) => new(Quantize(value.X), Quantize(value.Y), Quantize(value.Z));
    private static float Quantize(float value) => Math.Clamp(MathF.Round(value * 1024), -32768, 32767) / 1024;

    public static CMU3DEquipmentTransform Create(CMU3DEquipmentPosePrototype pose, Vector3 position,
        float facing, CMU3DCameraFrame? camera = null, bool leftHand = false)
    {
        var c = MathF.Cos(facing);
        var s = MathF.Sin(facing);
        var basis = new CMU3DEquipmentTransform(position, new(c, s, 0), new(-s, c, 0), Vector3.UnitZ);
        var origin = basis.Point(pose.Offset with { X = leftHand ? -pose.Offset.X : pose.Offset.X });
        if (camera is { } view)
        {
            // Both horizontal axes reverse when looking out from the wearer's eyes.
            // A reflection would reverse the gun's ejection port and inscriptions.
            basis = new CMU3DEquipmentTransform(view.Origin, -view.Right, -view.Forward, view.Up);
            var offset = pose.FirstPersonOffset;
            origin = view.Origin + view.Right * (leftHand ? -offset.X : offset.X) +
                     view.Forward * offset.Y + view.Up * offset.Z;
        }
        var yaw = pose.Yaw * MathF.PI / 180;
        var pitch = pose.Pitch * MathF.PI / 180;
        var cy = MathF.Cos(yaw);
        var sy = MathF.Sin(yaw);
        var cp = MathF.Cos(pitch);
        var sp = MathF.Sin(pitch);
        var roll = pose.Roll * MathF.PI / 180;
        var cr = MathF.Cos(roll);
        var sr = MathF.Sin(roll);
        var x = basis.Direction(new(cy * cp, sy * cp, sp)) * pose.Scale;
        var y = basis.Direction(new(-sy, cy, 0)) * pose.Scale;
        var z = basis.Direction(new(-cy * sp, -sy * sp, cp)) * pose.Scale;
        (y, z) = (y * cr + z * sr, z * cr - y * sr);
        return new CMU3DEquipmentTransform(origin - x * pose.Pivot.X - y * pose.Pivot.Y - z * pose.Pivot.Z, x, y, z);
    }
}
