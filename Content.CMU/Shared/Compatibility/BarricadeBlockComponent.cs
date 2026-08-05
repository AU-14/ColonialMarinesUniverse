using Robust.Shared.GameObjects;

namespace Content.Shared.BarricadeBlock;

[RegisterComponent]
public sealed partial class BarricadeBlockComponent : Component
{
    /// <summary>
    /// Percentage chance of blocking a projectile passing overhead.
    /// </summary>
    [DataField]
    public int Blocking = 66;

    /// <summary>
    /// Whether the cover works from both directions.
    /// </summary>
    [DataField]
    public bool Bidirectional = true;

    /// <summary>
    /// Maximum distance at which an aligned shot can pass over the cover.
    /// </summary>
    [DataField]
    public float Distance = 3.5f;
}
