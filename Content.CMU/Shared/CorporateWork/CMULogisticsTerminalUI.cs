using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CorporateWork;

[Serializable, NetSerializable]
public enum CMULogisticsTerminalUi : byte
{
    Key,
}

[Serializable, NetSerializable]
public readonly record struct CMULogisticsCrate(string Item, int Weight, int Hazard, int Value);

[Serializable, NetSerializable]
public readonly record struct CMULogisticsTierInfo(string Name, int Crates, int Reward);

[Serializable, NetSerializable]
public readonly record struct CMULogisticsResult(int Value, int BestValue, int Payout);

[Serializable, NetSerializable]
public sealed class CMULogisticsTerminalBuiState(
    List<CMULogisticsTierInfo> tiers,
    int? activeTier,
    List<CMULogisticsCrate> crates,
    int weightLimit,
    int hazardLimit,
    TimeSpan nextCaseAt,
    CMULogisticsResult? lastResult) : BoundUserInterfaceState
{
    public readonly List<CMULogisticsTierInfo> Tiers = tiers;
    public readonly int? ActiveTier = activeTier;
    public readonly List<CMULogisticsCrate> Crates = crates;
    public readonly int WeightLimit = weightLimit;
    public readonly int HazardLimit = hazardLimit;
    public readonly TimeSpan NextCaseAt = nextCaseAt;
    public readonly CMULogisticsResult? LastResult = lastResult;
}

[Serializable, NetSerializable]
public sealed class CMULogisticsStartBuiMsg(int tier) : BoundUserInterfaceMessage
{
    public readonly int Tier = tier;
}

[Serializable, NetSerializable]
public sealed class CMULogisticsSubmitBuiMsg(List<int> loaded) : BoundUserInterfaceMessage
{
    public readonly List<int> Loaded = loaded;
}
