using Content.Shared.CMU14.Round;

namespace Content.Shared._RMC14.Dropship;

public abstract partial class SharedDropshipSystem
{
    // third parties (round-start survivor dropships and co) skip the fueling lockout. it only
    // holds govfor/opfor back while the round sets up, third parties should already be planetside
    private bool IsExemptFromInitialDelay(EntityUid computer)
    {
        return TryComp<WhitelistedShuttleComponent>(computer, out var whitelist) &&
               string.Equals(whitelist.Faction, "thirdparty", StringComparison.OrdinalIgnoreCase);
    }
}
