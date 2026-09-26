using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Dropship.Fabricator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(DropshipFabricatorSystem))]
public sealed partial class DropshipFabricatorPointsComponent : Component
{
    // CMU14: faction gameplay fixes.
    [DataField]
    public string? Faction;

    [DataField, AutoNetworkedField]
    public int Points;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextPointsAt;
}
