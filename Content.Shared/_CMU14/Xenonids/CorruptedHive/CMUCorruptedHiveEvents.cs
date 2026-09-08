using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Xenonids.CorruptedHive;

[Serializable, NetSerializable]
public sealed record CMUCorruptedParasiteClaimChoiceEvent(
    NetEntity Claimant,
    NetEntity Parasite,
    uint OfferId,
    bool Claim);
