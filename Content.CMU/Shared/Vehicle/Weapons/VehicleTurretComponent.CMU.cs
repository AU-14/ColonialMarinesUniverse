namespace Content.Shared._RMC14.Vehicle;

public sealed partial class VehicleTurretComponent
{
    /// <summary>
    /// CMU14: how far either side of the vehicle's front the turret may aim, in degrees; 0 for no limit.
    /// </summary>
    [DataField("cmuMaxYawDegrees")]
    public float CMUMaxYawDegrees;
}
