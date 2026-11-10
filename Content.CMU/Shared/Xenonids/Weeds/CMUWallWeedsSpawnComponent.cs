using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Xenonids.Weeds;

// goes on a weed tile prototype: walls these weeds reach grow this wall weed instead of the xeno one
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUWallWeedsSpawnComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Wall;
}
