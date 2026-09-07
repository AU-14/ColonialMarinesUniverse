namespace Content.Shared.CMU14.ZLevels.Core;

/// <summary>
/// Client-side notification from the Z physics owner after startup, replicated state, local
/// prediction, or removal changes whether an entity needs elevation during rendering.
/// </summary>
[ByRefEvent]
public readonly record struct CMUZPhysicsPresentationChangedEvent(EntityUid Uid, bool Elevated);
