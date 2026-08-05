using Content.Shared.FixedPoint;

namespace Content.Shared._RMC14.Medical.Wounds;

/// <summary>
///     Raised on a woundable entity when a new wound is recorded on it.
/// </summary>
[ByRefEvent]
public readonly record struct WoundAddedEvent(FixedPoint2 Total, WoundType Type);
