using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.ZLevels.Core.Components;

/// <summary>
/// Tracker that tracks all maps added to the zLevel network. Usually, entity in Nullspace,
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true),
 Access(typeof(CMUSharedZLevelsSystem), typeof(CMUZLevelViewerRefresh))]
public sealed partial class CMUZLevelsNetworkComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<int, EntityUid?> ZLevels = new();

    /// <summary>
    /// Process-local reverse lookup derived from <see cref="ZLevels"/> on clients.
    /// </summary>
    [DataField]
    public Dictionary<EntityUid, int> ZLevelByEntity = new();
}
