using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Medical.Anatomy.Bones.Events;

[ByRefEvent]
public readonly record struct FractureSeverityChangedEvent(
    EntityUid Body,
    EntityUid Part,
    FractureSeverity Old,
    FractureSeverity New);
