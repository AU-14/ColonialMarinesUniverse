using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.ZLevels.Core.Components;

/// <summary>
/// Automatically added to the map when it appears in zLevelNetwork.
/// </summary>
[RegisterComponent, NetworkedComponent, UnsavedComponent]
public sealed partial class CMUZLevelMapComponent : Component
{
    /// <summary>
    /// Process-local topology data derived from the owning network's canonical depth map on clients.
    /// </summary>
    [DataField]
    public EntityUid NetworkUid = EntityUid.Invalid;

    [DataField]
    public EntityUid? MapAbove;

    [DataField]
    public EntityUid? MapBelow;

    [DataField]
    public int Depth = 0;
}
