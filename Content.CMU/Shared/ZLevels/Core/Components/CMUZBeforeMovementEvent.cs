namespace Content.Shared.CMU14.ZLevels.Core.Components;

/// <summary>
///     Raised before an active body's vertical simulation, allowing equipment to control its descent.
/// </summary>
[ByRefEvent]
public readonly record struct CMUZBeforeMovementEvent(float FrameTime);
