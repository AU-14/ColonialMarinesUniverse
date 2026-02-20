using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Intel.Tech;

[DataRecord]
[Serializable, NetSerializable]
public sealed record TechPartySpawnEvent(string ThirdPartyId)
{
    public string Team { get; init; } = string.Empty;
}
