using Robust.Shared.Network;

namespace Content.Shared._RMC14.Xenonids.JoinXeno;

public sealed class GetLarvaPoolStatusEvent(NetUserId userId) : EntityEventArgs
{
    public NetUserId UserId { get; } = userId;

    public Dictionary<EntityUid, LarvaPoolUserStatus> Pools { get; } = new();
}

public readonly record struct LarvaPoolUserStatus(
    LarvaPoolStatus Status,
    int Position,
    LarvaPoolIneligibilityReason IneligibilityReason,
    bool PreferenceLoaded,
    bool OptedIn);
