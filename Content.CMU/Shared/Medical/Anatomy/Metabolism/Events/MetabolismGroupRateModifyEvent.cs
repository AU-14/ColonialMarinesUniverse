using Content.Shared.Metabolism;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Medical.Anatomy.Metabolism.Events;

[ByRefEvent]
public record struct MetabolismRateModifyEvent(
    EntityUid Body,
    ProtoId<MetabolismStagePrototype> Stage,
    IReadOnlySet<CMUMetabolismClass> ToxicityClasses,
    float Multiplier);

public enum CMUMetabolismClass : byte
{
    Poison,
    Alcohol,
}
