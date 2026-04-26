using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Dropship.TacticalLand;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDropshipTacticalLandSystem))]
public sealed partial class DropshipTacticalLandSessionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Pilot;

    [DataField, AutoNetworkedField]
    public EntityUid? Eye;

    [DataField, AutoNetworkedField]
    public Vector2 OriginalZoom = Vector2.One;

    [DataField, AutoNetworkedField]
    public float OriginalPvsScale = 1f;
}
