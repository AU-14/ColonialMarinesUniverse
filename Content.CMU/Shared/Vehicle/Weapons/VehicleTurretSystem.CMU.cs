namespace Content.Shared._RMC14.Vehicle;

public sealed partial class VehicleTurretSystem
{
    /// <summary>
    /// CMU14: keeps a turret's vehicle-relative aim inside its arc around the vehicle's front.
    /// </summary>
    private Angle CMUClampYaw(VehicleTurretComponent turret, EntityUid vehicle, Angle desired)
    {
        if (turret.CMUMaxYawDegrees <= 0f)
            return desired;

        var vehicleRot = _transform.GetWorldRotation(vehicle);
        var local = turret.StabilizedRotation ? (desired - vehicleRot).Reduced() : desired.Reduced();
        var max = MathHelper.DegreesToRadians(turret.CMUMaxYawDegrees);
        var clamped = new Angle(Math.Clamp(local.Theta, -max, max));
        return turret.StabilizedRotation ? (clamped + vehicleRot).Reduced() : clamped;
    }
}
