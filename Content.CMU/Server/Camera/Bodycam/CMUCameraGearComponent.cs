using Content.Shared.Camera;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Camera.Bodycam;

/// <summary>
/// Camera gear that only broadcasts while it's worn. Putting it on joins <see cref="Network"/> under the wearer's
/// surname and badge number; taking it off leaves the network.
/// </summary>
[RegisterComponent, Access(typeof(CMUCameraGearSystem))]
public sealed partial class CMUCameraGearComponent : Component
{
    /// <summary>
    /// The camera network the gear broadcasts on while worn.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CameraNetworkPrototype> Network;

    /// <summary>
    /// The slots it broadcasts from.
    /// </summary>
    [DataField]
    public SlotFlags Slots = SlotFlags.EARS | SlotFlags.HEAD;

    [ViewVariables]
    public EntityUid? Wearer;

    [ViewVariables]
    public string? ShownName;
}
