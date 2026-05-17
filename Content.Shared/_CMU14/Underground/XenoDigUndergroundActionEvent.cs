using Content.Shared.Actions;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Underground;

/// <summary>
/// InstantAction event raised when a burrower activates the dig-underground-entrance ability.
/// </summary>
public sealed partial class XenoDigUndergroundActionEvent : InstantActionEvent
{
    [DataField]
    public FixedPoint2 PlasmaCost = 200;
}
