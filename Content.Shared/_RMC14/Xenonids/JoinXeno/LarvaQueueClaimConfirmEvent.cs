using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.JoinXeno;

[Serializable, NetSerializable]
public sealed record LarvaQueueClaimConfirmEvent(NetUserId UserId, int ClaimId);

[Serializable, NetSerializable]
public sealed record LarvaQueueClaimDeclineEvent(NetUserId UserId, int ClaimId);
