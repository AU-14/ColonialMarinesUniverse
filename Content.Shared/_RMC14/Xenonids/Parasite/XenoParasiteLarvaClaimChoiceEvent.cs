using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Parasite;

[Serializable, NetSerializable]
public sealed record XenoParasiteLarvaClaimChoiceEvent(NetEntity Ghost, NetEntity Parasite, NetEntity Victim, bool Claim);
