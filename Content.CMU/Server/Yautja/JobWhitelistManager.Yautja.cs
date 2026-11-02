using Content.Shared.CMU14.Yautja;
using Robust.Shared.Network;

// ReSharper disable once CheckNamespace
namespace Content.Server.Players.JobWhitelist;

public sealed partial class JobWhitelistManager
{
    // an actual pred whitelist (job whitelist or the clan db flags), unlike AllowsYautjaHunter this
    // doesn't count Boosty tiers. hunt ui and the hunt notice are for real whitelist holders only
    public bool HoldsYautjaWhitelist(NetUserId player)
    {
        if (_whitelists.TryGetValue(player, out var whitelists) && whitelists.Contains(YautjaHunterJob))
            return true;

        return _yautjaWhitelistFlags.TryGetValue(player, out var flags) &&
               (flags & HunterWhitelistFlags) != YautjaWhitelistFlags.None;
    }
}
