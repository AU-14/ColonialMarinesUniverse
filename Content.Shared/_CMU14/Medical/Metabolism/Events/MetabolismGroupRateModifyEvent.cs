using Content.Shared.Body.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Metabolism.Events;

[ByRefEvent]
public record struct MetabolismGroupRateModifyEvent(
    EntityUid Body,
    ProtoId<MetabolismGroupPrototype> Group,
    float Multiplier);
