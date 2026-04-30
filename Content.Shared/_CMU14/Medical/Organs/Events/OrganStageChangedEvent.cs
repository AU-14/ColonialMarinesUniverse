using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Organs.Events;

[ByRefEvent]
public readonly record struct OrganStageChangedEvent(
    EntityUid Body,
    EntityUid Organ,
    OrganDamageStage Old,
    OrganDamageStage New);
