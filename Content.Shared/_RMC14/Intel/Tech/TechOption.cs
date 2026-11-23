using System.Linq; // CMU14
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
    int TimesPurchased = 0,
    TimeSpan PurchaseCooldown = default, // CMU14: delay between purchases, using the round-relative TimeLock.
    List<string>? ModeLock = default // CMU14: optional game-mode allow list.
)
{
    public bool IsAllowedInMode(string currentMode)
    {
        if (ModeLock is not { Count: > 0 })
            return true;

        if (string.IsNullOrWhiteSpace(currentMode))
            return false;

        return ModeLock.Any(lockedMode =>
            string.Equals(lockedMode, currentMode, StringComparison.OrdinalIgnoreCase));
    }
}

// cmu edit end
