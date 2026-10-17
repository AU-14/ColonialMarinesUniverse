namespace Content.Shared.Body.Events;

/// <summary>
/// Raised on an organ when it is related to a body part.
/// </summary>
[ByRefEvent]
public readonly record struct OrganAddedEvent(EntityUid Part);

/// <summary>
/// Raised on an organ when it is related to a body part in a body.
/// </summary>
[ByRefEvent]
public readonly record struct OrganAddedToBodyEvent(EntityUid Body, EntityUid Part);

/// <summary>
/// Raised on an organ when its relationship to a body part is removed.
/// </summary>
[ByRefEvent]
public readonly record struct OrganRemovedEvent(EntityUid OldPart);

/// <summary>
/// Raised on an organ when its relationship to a body part in a body is removed.
/// </summary>
[ByRefEvent]
public readonly record struct OrganRemovedFromBodyEvent(EntityUid OldBody, EntityUid OldPart);
