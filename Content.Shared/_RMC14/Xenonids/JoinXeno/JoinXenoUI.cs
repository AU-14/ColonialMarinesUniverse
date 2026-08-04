using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.JoinXeno;

[Serializable, NetSerializable]
public enum JoinXenoUIKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum JoinXenoQueueStatus : byte
{
    NotQueued,
    Queued,
    Waiting
}

[Serializable, NetSerializable]
public readonly record struct JoinXenoHiveEntry(
    NetEntity Hive,
    string HiveName,
    JoinXenoQueueStatus Status,
    int Position);

[Serializable, NetSerializable]
public sealed class JoinXenoBuiState(List<JoinXenoHiveEntry> entries) : BoundUserInterfaceState
{
    public readonly List<JoinXenoHiveEntry> Entries = entries;
}

[Serializable, NetSerializable]
public sealed class JoinXenoHiveChoiceBuiMsg(NetEntity hive) : BoundUserInterfaceMessage
{
    public readonly NetEntity Hive = hive;
}
