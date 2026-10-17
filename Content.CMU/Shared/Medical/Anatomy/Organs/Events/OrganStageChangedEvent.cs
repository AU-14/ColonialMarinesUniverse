using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Events;

[ByRefEvent]
public readonly record struct OrganStageChangedEvent(
    EntityUid Body,
    EntityUid Organ,
    OrganDamageStage Old,
    OrganDamageStage New);
