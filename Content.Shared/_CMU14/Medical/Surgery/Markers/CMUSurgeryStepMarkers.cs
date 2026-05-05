using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery.Markers;

/// <summary>
///     Bone-setting workflow markers. They keep multi-step fracture repair
///     explicit even when the fracture severity itself is only changed by
///     the CMU effect system.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUBoneAlignedMarkerComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUComminutedBoneRealignedMarkerComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUComminutedBoneGelAppliedMarkerComponent : Component;

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

/// <summary>
///     Per-organ-slot vessel-clamp markers. Without a marker, clamp-only
///     steps have no Add/Remove state and are considered complete before the
///     surgeon ever uses the organ clamp.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMULiverVesselsClampedMarkerComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMULungsVesselsClampedMarkerComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUKidneysVesselsClampedMarkerComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUHeartVesselsClampedMarkerComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUStomachVesselsClampedMarkerComponent : Component;
