using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Tracker.SquadLeader;

[Serializable, NetSerializable]
public sealed record BattleBuddyAcceptEvent(NetEntity Requester, NetEntity Target);

[Serializable, NetSerializable]
public sealed record BattleBuddyDeclineEvent(NetEntity Requester, NetEntity Target);
