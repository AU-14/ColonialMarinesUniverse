using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.ZLevels.Core.Components;

/// <summary>
/// Automatically added to the map when it appears in zLevelNetwork.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class CMUZLevelMapComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid NetworkUid = EntityUid.Invalid;

    [DataField, AutoNetworkedField]
    public EntityUid? MapAbove;

    [DataField, AutoNetworkedField]
    public EntityUid? MapBelow;

    [DataField, AutoNetworkedField]
    public int Depth = 0;
}
