using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.ZLevels.Core.Components;

/// <summary>
/// Tracker that tracks all maps added to the zLevel network. Usually, entity in Nullspace,
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CMUSharedZLevelsSystem))]
public sealed partial class CMUZLevelsNetworkComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<int, EntityUid?> ZLevels = new();

    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, int> ZLevelByEntity = new();
}
