using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Xenonids.Evolution;

/// <summary>
///     Sent from the evolutions UI when a xeno clicks a caste it lacks the points for,
///     toggling its membership in that tier's evolution queue.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUXenoEvolveQueueBuiMsg(EntProtoId choice) : BoundUserInterfaceMessage
{
    public readonly EntProtoId Choice = choice;
}

/// <summary>
///     Hive management dialog option: open the evolution queue overview.
/// </summary>
[Serializable, NetSerializable]
public sealed record CMUManageHiveEvolutionQueueEvent;

/// <summary>
///     Hive management dialog option: open one tier's queue.
/// </summary>
[Serializable, NetSerializable]
public sealed record CMUEvolutionQueueTierEvent(int Tier);

/// <summary>
///     Hive management dialog option: open the reorder options for one queued xeno.
/// </summary>
[Serializable, NetSerializable]
public sealed record CMUEvolutionQueueMemberEvent(NetEntity Xeno, int Tier);

[Serializable, NetSerializable]
public enum CMUEvolutionQueueMove : byte
{
    Front,
    Up,
    Down,
    Remove,
}

/// <summary>
///     Hive management dialog option: move or remove one queued xeno.
/// </summary>
[Serializable, NetSerializable]
public sealed record CMUEvolutionQueueMoveEvent(NetEntity Xeno, int Tier, CMUEvolutionQueueMove Move);
