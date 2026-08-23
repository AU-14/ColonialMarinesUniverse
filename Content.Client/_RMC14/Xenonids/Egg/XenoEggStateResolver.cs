using System;
using System.Collections.Generic;

namespace Content.Client._RMC14.Xenonids.Egg;

public static class XenoEggStateResolver
{
    private static readonly Dictionary<string, string[]> CanonicalAliases = new(StringComparer.Ordinal)
    {
        ["egg"] = ["egg", "Egg"],
        ["egg_opening"] = ["egg_opening", "Egg Opening", "Egg_Opening"],
        ["egg_opened"] = ["egg_opened", "Egg Opened", "Egg_Opened"],
        ["egg_exploding"] = ["egg_exploding", "Egg Exploding", "Egg_Exploding"],
        ["egg_exploded"] = ["egg_exploded", "Egg Exploded", "Egg_Exploded"],
        ["egg_item"] = ["egg_item", "Egg Item", "Egg_Item"],
        ["egg_growing"] = ["egg_growing", "Egg Growing", "Egg_Growing"],
    };

    public static bool TryResolve(string? requested, Func<string, bool> hasState, out string resolved)
    {
        resolved = string.Empty;
        if (string.IsNullOrWhiteSpace(requested))
            return false;

        requested = requested.Trim();
        if (hasState(requested))
        {
            resolved = requested;
            return true;
        }

        var canonical = requested.ToLowerInvariant().Replace(' ', '_');
        if (!CanonicalAliases.TryGetValue(canonical, out var aliases))
            return false;

        foreach (var alias in aliases)
        {
            if (!hasState(alias))
                continue;

            resolved = alias;
            return true;
        }

        return false;
    }
}
