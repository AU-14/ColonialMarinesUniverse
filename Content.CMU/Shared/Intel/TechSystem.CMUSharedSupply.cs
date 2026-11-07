namespace Content.Shared._RMC14.Intel.Tech;

public sealed partial class TechSystem
{
    /// <summary>
    /// Counts an externally paid delivery against an intel option's shared round limit and cooldown,
    /// without charging intel points or executing the option's events. A failed request with a positive
    /// retry delay can be retried; otherwise the supply is exhausted or unavailable.
    /// </summary>
    public bool TryConsumeSharedSupply(string team, string optionName, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (_net.IsClient)
            return false;

        var tree = _intel.EnsureTechTree(team);
        foreach (var tier in tree.Comp.Tree.Options)
        {
            for (var index = 0; index < tier.Count; index++)
            {
                var option = tier[index];
                if (option.Name != optionName)
                    continue;

                if ((option.Purchased && !option.Repurchasable) ||
                    (option.MaxPurchases > 0 && option.TimesPurchased >= option.MaxPurchases))
                {
                    return false;
                }

                var now = _ticker.RoundDuration();
                if (option.TimeLock > now)
                {
                    retryAfter = option.TimeLock - now;
                    return false;
                }

                tier[index] = option with
                {
                    Purchased = true,
                    TimesPurchased = option.TimesPurchased + 1,
                    TimeLock = option.PurchaseCooldown > TimeSpan.Zero
                        ? now + option.PurchaseCooldown
                        : option.TimeLock,
                };
                Dirty(tree);
                _intel.UpdateTree(tree);
                return true;
            }
        }

        return false;
    }
}
