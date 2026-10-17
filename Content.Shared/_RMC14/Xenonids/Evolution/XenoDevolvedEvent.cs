using Content.Shared.FixedPoint;

namespace Content.Shared._RMC14.Xenonids.Evolution;

[ByRefEvent]
public readonly record struct XenoDevolvedEvent(EntityUid OldXeno, EntityUid NewXeno, FixedPoint2 PointsRefund = default);
