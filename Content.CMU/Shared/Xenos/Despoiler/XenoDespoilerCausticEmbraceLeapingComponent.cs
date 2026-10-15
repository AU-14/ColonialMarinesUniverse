using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.CMU;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoDespoilerCausticEmbraceLeapingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Action;

    [DataField, AutoNetworkedField]
    public EntityUid? Victim;

    [DataField, AutoNetworkedField]
    public EntityCoordinates Destination;

    [DataField, AutoNetworkedField]
    public Vector2 Direction;

    [DataField, AutoNetworkedField]
    public TimeSpan LeapEndTime;

    [DataField, AutoNetworkedField]
    public bool Empowered;

    [DataField, AutoNetworkedField]
    public bool Resolved;
}
