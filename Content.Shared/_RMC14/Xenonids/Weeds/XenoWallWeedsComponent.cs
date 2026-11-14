using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Weeds;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedXenoWeedsSystem))]
public sealed partial class XenoWallWeedsComponent : Component
{
    [DataField]
    public EntityUid? Weeds;

    // CMU14: server-owned backlink; wall coverings must release their weedable when destroyed.
    [DataField]
    public EntityUid? Weedable;
}
