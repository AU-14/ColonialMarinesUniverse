namespace Content.Shared.CMU14.Yautja;

// CMU14: Yautja Leap support. Records the exact collision masks of every fixture so they can be restored when the
// throw stops, is interrupted, or the component is removed - a leap with altered masks left behind would let the
// Yautja walk through walls.
[RegisterComponent]
public sealed partial class YautjaLeapingComponent : Component
{
    public Dictionary<string, int> OriginalCollisionMasks = new();
}
