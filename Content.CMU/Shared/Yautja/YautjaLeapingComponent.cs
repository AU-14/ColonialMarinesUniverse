using Robust.Shared.Physics;

namespace Content.Shared.CMU14.Yautja;

[RegisterComponent]
public sealed partial class YautjaLeapingComponent : Component
{
    public Dictionary<string, int> OriginalCollisionMasks = new();
}
