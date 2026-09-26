using System.Numerics;

namespace Content.Shared.CMU14.Hijack;

/// <summary>
/// CMSS13's directed stair edge: leave this tile towards Direction to arrive
/// on the adjacent tile of Offset's deck. Decorative stairs do not have this.
/// </summary>
[RegisterComponent]
public sealed partial class CMUAlmayerStairsComponent : Component
{
    [DataField(required: true)]
    public Vector2 Direction;

    [DataField(required: true)]
    public int Offset;
}
