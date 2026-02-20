using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Intel.Tech;

[DataRecord]
[Serializable, NetSerializable]
public sealed record TechRequisitionsBudgetEvent(int Amount)
{
    public string Team { get; init; } = String.Empty;
}
