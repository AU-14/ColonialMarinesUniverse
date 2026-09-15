using System.Numerics;
using Robust.Shared.Map;

namespace Content.Shared.CMU;

[RegisterComponent]
public sealed partial class XenoDespoilerCausticEmbraceLeapingComponent : Component
{
    public EntityUid Action;

    public EntityUid? Victim;

    public EntityCoordinates Destination;

    public Vector2 Direction;

    public TimeSpan LeapEndTime;

    public bool Empowered;
}
