using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._CMU14.Ape;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ApeDestroyLeapingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityCoordinates? Target;

    [DataField, AutoNetworkedField]
    public TimeSpan? LeapMoveAt;

    [DataField, AutoNetworkedField]
    public TimeSpan? LeapEndAt;
}

