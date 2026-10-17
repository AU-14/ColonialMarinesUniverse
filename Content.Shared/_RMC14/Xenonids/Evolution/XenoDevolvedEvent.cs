using Content.Shared.FixedPoint;

namespace Content.Shared._RMC14.Xenonids.Evolution;

[ByRefEvent]
// CMU14 Devolution Refund Begin: carry the conditional refund through the existing event
public readonly record struct XenoDevolvedEvent(EntityUid OldXeno, EntityUid NewXeno, FixedPoint2 PointsRefund = default);
// CMU14 End
