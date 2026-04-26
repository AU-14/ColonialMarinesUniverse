using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Dropship.TacticalLand;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDropshipTacticalLandSystem))]
public sealed partial class DropshipPilotEyeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Pilot;

    [DataField, AutoNetworkedField]
    public EntityUid? Console;

    [DataField, AutoNetworkedField]
    public Vector2i Footprint = new(11, 21);

    [DataField, AutoNetworkedField]
    public List<Vector2i> BlockedTiles = new();

    [DataField, AutoNetworkedField]
    public bool ClearForLanding;
}
