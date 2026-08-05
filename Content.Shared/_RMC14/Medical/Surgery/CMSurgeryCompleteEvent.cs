using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Medical.Surgery;

[ByRefEvent]
public record struct CMSurgeryCompleteEvent(EntityUid Patient, EntityUid Surgeon, EntProtoId Surgery);
