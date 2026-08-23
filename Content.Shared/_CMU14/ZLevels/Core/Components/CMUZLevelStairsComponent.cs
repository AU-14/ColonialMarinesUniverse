using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.ZLevels.Core.Components;

/// <summary>
/// Directional CMSS13-style multi-Z stairs.
/// Moving off the stair in the configured direction projects the user to the adjacent Z-level.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUZLevelStairsComponent : Component
{
    /// <summary>
    /// The BYOND stair direction. Up stairs trigger when moving this way; down stairs trigger opposite this way.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Direction Direction = Direction.North;

    /// <summary>
    /// Relative Z-level offset. Positive moves up, negative moves down.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Offset = 1;

    /// <summary>
    /// Local Z position after the transition.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LandingLocalPosition = 0.05f;
}
