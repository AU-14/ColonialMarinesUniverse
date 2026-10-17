using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Xenonids.Destroy;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoDestroyLeapingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityCoordinates? Target;

    // CMU14: no mid-flight teleport; the move happens on landing (see SharedXenoDestroySystem)
    //[DataField, AutoNetworkedField]
    //public TimeSpan? LeapMoveAt;

    [DataField, AutoNetworkedField]
    public TimeSpan? LeapEndAt;
}
