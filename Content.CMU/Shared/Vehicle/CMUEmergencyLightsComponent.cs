using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUEmergencyLightsSystem))]
public sealed partial class CMUEmergencyLightsComponent : Component
{
    [DataField]
    public EntProtoId Action = "CMUActionVehicleEmergencyLights";

    [DataField]
    public EntityUid? DriverAction;

    [DataField]
    public List<EntProtoId> LightPrototypes = new()
    {
        "CMUEmergencyLightbarSlow",
        "CMUEmergencyLightbarMedium",
        "CMUEmergencyLightbarFast",
    };

    [DataField]
    public Vector2 Offset = new(0, 0.5f);

    [DataField, AutoNetworkedField]
    public int Mode;

    [DataField]
    public EntityUid? Lightbar;
}

public sealed partial class CMUEmergencyLightsActionEvent : InstantActionEvent;
