namespace Content.Shared.CMU14.Stealth;

/// <summary>Raised on the entity when its active invisibility starts.</summary>
[ByRefEvent]
public readonly record struct CMUInvisibilityStartedEvent;

/// <summary>Raised on the entity when its active invisibility shuts down, before the component is removed.</summary>
[ByRefEvent]
public readonly record struct CMUInvisibilityStoppedEvent;
