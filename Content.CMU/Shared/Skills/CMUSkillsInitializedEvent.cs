namespace Content.Shared.CMU14.Skills;

/// <summary>
/// Raised when skills start up, before any preset is applied.
/// Preset changes subsequently raise SkillChangedEvent.
/// </summary>
[ByRefEvent]
public readonly record struct CMUSkillsInitializedEvent;
