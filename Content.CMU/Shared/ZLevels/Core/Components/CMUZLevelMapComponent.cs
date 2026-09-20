using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.ZLevels.Core.Components;

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

    /// <summary>
    /// Screen-space height per deck. Aligned ship plans use zero so that stairs,
    /// walls and openings on adjacent decks share the same projected tile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float VisualOffset = 0.75f;
}
