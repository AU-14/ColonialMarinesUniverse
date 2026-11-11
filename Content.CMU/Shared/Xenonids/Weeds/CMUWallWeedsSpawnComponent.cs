using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Xenonids.Weeds;

// goes on a weed tile prototype: swaps the xeno wall weed a weedable would grow for this weed's own one.
// keyed by the weedable's own spawn, so walls, windows and frames each keep their shape and the comms tower (no spawn) stays bare
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUWallWeedsSpawnComponent : Component
{
    [DataField(required: true)]
    public Dictionary<EntProtoId, EntProtoId> Walls = new();
}
