using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// A signal flare that, burning at rest under open sky, calls a vehicle crate down
/// onto the spot. It calls once.
/// </summary>
[RegisterComponent]
public sealed partial class CMUVehicleDropFlareComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Crate;

    /// <summary>
    /// How long the lit flare must lie still before it calls.
    /// </summary>
    [DataField]
    public TimeSpan Settle = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Seconds the crate spends on its way before it shows over the flare; its fall duration is set on the crate.
    /// </summary>
    [DataField]
    public float SkyFall = 8f;

    [DataField]
    public bool UseParachute = true;

    [DataField]
    public EntProtoId LandingEffect = "RMCEffectAlert";

    [DataField]
    public LocId CalledMessage = "cmu-vehicle-drop-flare-called";

    public bool Called;

    public bool Warned;

    public TimeSpan? SettledAt;

    public EntityCoordinates SettledWhere;
}
