using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleOpticsComponent : Component
{
    [DataField]
    public EntProtoId NightVisionAction = "CMUActionVehicleNightVision";

    [DataField]
    public EntProtoId ThermalVisionAction = "CMUActionVehicleThermalVision";

    [DataField]
    public TimeSpan ThermalDuration;

    [DataField]
    public TimeSpan ThermalCooldown;

    [DataField]
    public Color ThermalTint = Color.White;

    [DataField]
    public bool NightVisionOverlay = true;

    [DataField]
    public bool ThermalOverlay = true;

    [DataField]
    public bool ThermalSeeThroughContainers;
}
