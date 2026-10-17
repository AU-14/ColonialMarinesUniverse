using Robust.Shared.Utility;

namespace Content.Server.CMU14.ZLevels.Mapping;

/// <summary>
/// Remembers which file each depth of a mapping zNetwork was loaded from,
/// letting znetwork-save write levels back to their original paths.
/// </summary>
[RegisterComponent]
public sealed partial class CMUZNetworkSourcePathsComponent : Component
{
    [DataField]
    public Dictionary<int, ResPath> Paths = new();
}
