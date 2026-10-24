using System.Numerics;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.Yautja;

// CMU14: Yautja Leap support. Records the exact collision masks of every fixture so they can be restored when the
// throw stops, is interrupted, or the component is removed - a leap with altered masks left behind would let the
// Yautja walk through walls.
[RegisterComponent]
public sealed partial class YautjaLeapingComponent : Component
{
    public Dictionary<string, int> OriginalCollisionMasks = new();

    public MapCoordinates Destination;

    /// <summary>
    /// Unit vector from the leap's origin to its destination, used to detect passing the destination.
    /// </summary>
    public Vector2 Direction;

    /// <summary>
    /// Set when the leap has a destination to snap to. Cross-level leaps use the Z transition instead.
    /// </summary>
    public bool SnapToDestination;

}
