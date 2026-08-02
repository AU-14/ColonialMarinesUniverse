using Robust.Shared.Maths;

namespace Content.Shared._CMU14.Yautja;

/// <summary>
/// Makes a strapped rider face the direction the Yautja chair is facing.
/// </summary>
[RegisterComponent]
public sealed partial class YautjaChairFacingComponent : Component
{
    [DataField]
    public Direction Direction = Direction.South;
}
