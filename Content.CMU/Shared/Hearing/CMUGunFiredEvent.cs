using Robust.Shared.Map;

namespace Content.Shared.CMU14.Hearing;

/// <summary>
///     Raised broadcast every time a gun fires.
/// </summary>
[ByRefEvent]
public readonly record struct CMUGunFiredEvent(EntityUid Gun, EntityUid User, EntityCoordinates From);
