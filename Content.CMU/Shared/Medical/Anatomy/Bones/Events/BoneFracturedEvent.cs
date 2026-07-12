using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Anatomy.Bones.Events;

[ByRefEvent]
public readonly record struct BoneFracturedEvent(
    EntityUid Body,
    EntityUid Part,
    FractureSeverity Old,
    FractureSeverity New);
