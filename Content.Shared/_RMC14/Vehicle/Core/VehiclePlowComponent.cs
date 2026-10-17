using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Vehicle;

/// <summary>
/// Marks a hardpoint as a structure-clearing plow. Its damage bonus only
/// applies when the obstruction contacts the front face of the vehicle.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VehiclePlowComponent : Component
{
    /// <summary>
    /// Multiplier applied to the vehicle's impact damage against structures.
    /// The bonus above 1 scales with the hardpoint's current performance.
    /// </summary>
    [DataField]
    public float StructureDamageMultiplier = 1.5f;
}

/// <summary>
/// Optional vehicle-side multiplier for chassis designed specifically around a
/// plow, such as the AEV. This combines with the installed plow's multiplier.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VehiclePlowChassisComponent : Component
{
    [DataField]
    public float StructureDamageMultiplier = 1f;

    /// <summary>
    /// Raw structure damage applied per second while a functional plow is held
    /// against the front of the chassis. This models continued engine force
    /// rather than requiring the vehicle to repeatedly build ramming speed.
    /// </summary>
    [DataField]
    public float PoweredDemolitionDamagePerSecond;

    /// <summary>
    /// Seconds of uninterrupted front-plow contact before powered demolition begins.
    /// </summary>
    [DataField]
    public float PoweredDemolitionWarmup = 0.5f;

    /// <summary>
    /// Audible feedback while the plow is applying sustained force to a
    /// destructible obstruction.
    /// </summary>
    [DataField]
    public SoundSpecifier? PoweredDemolitionSound = new SoundPathSpecifier("/Audio/Machines/airlock_creaking.ogg");

    /// <summary>
    /// Minimum delay between powered-demolition creaks while contact continues.
    /// </summary>
    [DataField]
    public float PoweredDemolitionSoundCooldown = 2f;
}
