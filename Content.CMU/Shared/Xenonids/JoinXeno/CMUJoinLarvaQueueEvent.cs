using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Xenonids.JoinXeno;

[Serializable, NetSerializable]
public sealed record CMUJoinLarvaQueueEvent(NetEntity Hive);
