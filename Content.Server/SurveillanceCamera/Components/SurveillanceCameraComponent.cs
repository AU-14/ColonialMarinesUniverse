using Content.Shared.Camera;
using Robust.Shared.Prototypes;

namespace Content.Server.SurveillanceCamera;

[RegisterComponent]
[Access(typeof(SurveillanceCameraSystem))]
public sealed partial class SurveillanceCameraComponent : Component
{
    // If this camera is active or not. Deactivating a camera
    // will not allow it to obtain any new viewers.
    [ViewVariables]
    public bool Active { get; set; } = true;

    // This one isn't easy to deal with. Will require a UI
    // to change/set this so mapping these in isn't
    // the most terrible thing possible.
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("id")]
    public string CameraId { get; set;  } = "camera";

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("nameSet")]
    public bool NameSet { get; set; }

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("networkSet")]
    public bool NetworkSet { get; set; }

    [DataField("setupAvailableNetworks")]
    public List<ProtoId<CameraNetworkPrototype>> AvailableNetworks { get; private set; } = new();
}
