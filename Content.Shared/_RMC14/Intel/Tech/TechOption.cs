using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Intel.Tech;

[DataRecord]
[Serializable, NetSerializable]
public readonly partial record struct TechOption(
    string Name,
    string Description,
    int Cost,
    int Increase,
    bool Repurchasable,
    int CurrentCost,
    bool Purchased,
    List<object> Events,
    SpriteSpecifier.Rsi Icon,
    TimeSpan TimeLock,
    // cmu edit start: how many times a repurchasable option can be bought; 0 is no limit
    int MaxPurchases = 0,
    int TimesPurchased = 0
    // cmu edit end
);
