using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.ZLevels.Vehicles;

/// <summary>
/// Enables grid vehicles to use derived footprint support for CMU multi-Z traversal.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleZTraversalComponent : Component
{
    /// <summary>
    /// Maximum spacing between generated support samples inside the vehicle footprint.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SupportSampleSpacing = CMUVehicleSupportFootprint.DefaultSampleSpacing;

    /// <summary>
    /// Inset from the collision bounds when generating support samples.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SupportSampleInset = CMUVehicleSupportFootprint.DefaultSampleInset;

    /// <summary>
    /// Unsupported footprint fraction above which the vehicle tips into a Z fall.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float EdgeTipUnsupportedFraction = CMUVehicleSupportFootprint.DefaultEdgeTipUnsupportedFraction;

    /// <summary>
    /// Maximum upward step a non-sticky support sample can snap to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxSupportStepHeight = 0.5f;

    /// <summary>
    /// Maximum downward distance still treated as supported.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SupportSnapDistance = 0.05f;

    /// <summary>
    /// Multiplier for hull damage from vehicle landing impact.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LandingHullDamageMultiplier = 1f;

    /// <summary>
    /// Multiplier for wheel or tread damage from vehicle landing impact.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LandingWheelDamageMultiplier = 1f;

    /// <summary>
    /// Multiplier for occupant damage from vehicle landing impact.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LandingOccupantDamageMultiplier = 0.35f;

    /// <summary>
    /// Multiplier for damage dealt to entities under the landing footprint.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LandingCrushDamageMultiplier = 1f;

    /// <summary>
    /// Minimum impact velocity that applies hard-landing vehicle effects.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HardLandingMinVelocity = 4f;

    /// <summary>
    /// Multiplier applied to normal drive deceleration while the vehicle is falling.
    /// Driver acceleration and steering are disabled, but existing speed can drift.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AirDriftDecelerationMultiplier = 0.2f;

    /// <summary>
    /// Maximum absolute horizontal speed retained while the vehicle is falling.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxAirDriftSpeed = 4f;
}
