using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// A signal flare that, burning at rest under open sky, calls a vehicle crate down by parachute
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

    [DataField]
    public float SkyFall = 8f;

    [DataField]
    public float DropTime = 3f;

    public bool Called;

    public bool Warned;

    public TimeSpan? SettledAt;

    public EntityCoordinates SettledWhere;
}
