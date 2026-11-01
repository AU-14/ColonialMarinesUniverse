using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.ThreeD.Scene;

public static class CMU3DVehiclePose
{
    /// <summary>
    /// Solids are authored once in the south-facing source frame. Directional
    /// sprite offsets compensate for different 2D drawings, not moving 3D mounts.
    /// </summary>
    public static (Vector2 Position, Angle Yaw) Mounted(Vector2 vehiclePosition, Angle vehicleYaw,
        VehicleTurretComponent anchor, VehicleTurretComponent? attachment)
    {
        var yaw = vehicleYaw + (anchor.RotateToCursor ? anchor.WorldRotation : Angle.Zero);
        var position = vehiclePosition +
                       (anchor.OffsetRotatesWithTurret ? yaw : vehicleYaw).RotateVec(SourceOffset(anchor));
        if (attachment != null)
            position += (attachment.OffsetRotatesWithTurret ? yaw : vehicleYaw).RotateVec(SourceOffset(attachment));
        return (position, yaw);
    }

    private static Vector2 SourceOffset(VehicleTurretComponent turret)
    {
        return (turret.PixelOffset + (turret.UseDirectionalOffsets ? turret.PixelOffsetSouth : Vector2.Zero)) / 32;
    }
}
