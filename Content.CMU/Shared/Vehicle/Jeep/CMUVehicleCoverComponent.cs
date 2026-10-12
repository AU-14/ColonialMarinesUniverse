namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// What an open vehicle's doors and windshield do for its riders. A shut door stops shots from its
/// side at the legs and groin of the rider beside it, the bodywork taking them instead. A raised
/// windshield catches some shots from ahead at the heads and chests of the riders behind it, and
/// wears by them; down or gone, it lets grit into their eyes at speed over rough ground.
/// </summary>
[RegisterComponent]
public sealed partial class CMUVehicleCoverComponent : Component
{
    /// <summary>
    /// How far, in degrees, a shot can come in off straight from a door's side, or from dead ahead
    /// for the windshield, and still be stopped.
    /// </summary>
    [DataField]
    public float CoverAngle = 60f;

    /// <summary>
    /// The share of a shot stopped by a door that the vehicle's bodywork takes.
    /// </summary>
    [DataField]
    public float DoorDamage = 1f;

    /// <summary>
    /// The chance the raised windshield catches a shot from ahead at a rider's head or chest.
    /// </summary>
    [DataField]
    public float WindshieldChance = 0.5f;

    /// <summary>
    /// The share of a caught shot's damage the windshield wears by. Once cracked it stops nothing.
    /// </summary>
    [DataField]
    public float WindshieldWear = 0.5f;

    /// <summary>
    /// The share of top speed from which grit flies up off rough ground into the riders' eyes
    /// behind a lowered windshield.
    /// </summary>
    [DataField]
    public float GritSpeed = 0.5f;

    /// <summary>
    /// Each rider's chance a second of getting grit in their eyes while it flies.
    /// </summary>
    [DataField]
    public float GritChance = 0.1f;

    /// <summary>
    /// How long grit blurs a rider's sight.
    /// </summary>
    [DataField]
    public TimeSpan GritDuration = TimeSpan.FromSeconds(3);
}
