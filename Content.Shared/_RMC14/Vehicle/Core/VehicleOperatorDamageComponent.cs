using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Vehicle;

/// <summary>
/// Controls how much exterior vehicle damage is passed to its current operator.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(VehicleOperatorDamageSystem))]
public sealed partial class VehicleOperatorDamageComponent : Component
{
    /// <summary>
    /// Explosion epicenters within this many tiles of the chassis center count as direct hits.
    /// </summary>
    [DataField]
    public float DirectExplosionRange = 2f;

    /// <summary>
    /// Fraction of direct explosion damage passed to the operator.
    /// </summary>
    [DataField]
    public float DirectExplosionDamageMultiplier = 0.35f;

    /// <summary>
    /// Fraction of non-direct explosion damage passed to the operator.
    /// </summary>
    [DataField]
    public float NearbyExplosionDamageMultiplier = 0.1f;

    /// <summary>
    /// Fraction of damage from another vehicle passed to the operator.
    /// </summary>
    [DataField]
    public float RammingDamageMultiplier = 0.15f;
}
