using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.Sprite;

/// <summary>
/// Stores a server-authoritative sprite scale.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedScaleVisualsSystem))]
public sealed partial class ScaleVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    [ViewVariables]
    public Vector2 Scale = Vector2.One;

    /// <summary>
    /// The original client-side sprite scale restored when this component is removed.
    /// </summary>
    [DataField]
    [ViewVariables]
    public Vector2? OriginalScale;
}
