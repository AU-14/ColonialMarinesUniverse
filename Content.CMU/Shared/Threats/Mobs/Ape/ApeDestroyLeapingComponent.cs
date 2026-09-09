using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.Threats.Mobs.Ape;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ApeDestroyLeapingComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan? LeapEndAt;

    [DataField, AutoNetworkedField]
    public EntityCoordinates? Target;
}
