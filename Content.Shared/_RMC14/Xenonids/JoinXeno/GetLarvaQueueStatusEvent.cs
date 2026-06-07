using Robust.Shared.Network;

namespace Content.Shared._RMC14.Xenonids.JoinXeno;

public sealed class GetLarvaQueueStatusEvent(NetUserId userId) : EntityEventArgs
{
    public NetUserId UserId { get; } = userId;

    public Dictionary<EntityUid, LarvaQueueUserStatus> Queues { get; } = new();
}
