using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Xenos;

/// <summary>
/// Sticky (slow) resin does not slow this entity. Fast resin still only speeds up hive members.
/// Read in SharedXenoWeedsSystem's movement speed refresh.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUIgnoreResinSlowdownComponent : Component;
