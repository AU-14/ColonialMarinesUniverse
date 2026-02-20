using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Intel.Tech;

[DataRecord]
[Serializable, NetSerializable]
public sealed record TechDropshipBudgetEvent(int Amount = 2000)
{
    public string Team { get; init; } = String.Empty;
}
