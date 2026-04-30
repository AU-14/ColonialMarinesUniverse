using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery.Markers;

/// <summary>
///     Per-organ-slot "removed" markers. The framework's <c>OnToolCheck</c>
///     is type-only, so each slot needs its own marker type to keep separate
///     organ surgeries from satisfying each other's step-complete check on
///     the same part.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMULiverRemovedMarkerComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMULungsRemovedMarkerComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUKidneysRemovedMarkerComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUHeartRemovedMarkerComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUStomachRemovedMarkerComponent : Component;
