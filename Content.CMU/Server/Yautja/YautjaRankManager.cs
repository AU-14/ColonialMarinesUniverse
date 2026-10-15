using System;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Yautja;

/// <summary>
/// Resolves the server-owned clan rank without allowing the client profile to grant Young Blood status.
/// </summary>
public sealed partial class YautjaRankManager : IPostInjectInit
{
    [Dependency] private YautjaClanManager _clanManager = default!;
    [Dependency] private UserDbDataManager _userDb = default!;

    public async Task<YautjaRank> Resolve(NetUserId userId, bool youngbloodRole = false)
    {
        return (await _clanManager.Resolve(userId, youngbloodRole)).Rank;
    }

    public async Task Prime(NetUserId userId)
    {
        await Resolve(userId);
    }

    public YautjaRank ResolveCached(NetUserId userId, bool youngbloodRole = false)
    {
        // Use the same cache as profile capabilities. Player-data loading and
        // refreshes prime it asynchronously; never wait for database work here.
        return _clanManager.ResolveCached(userId, youngbloodRole).Rank;
    }

    public YautjaProfileCapabilities ResolveProfileCapabilitiesCached(
        NetUserId userId,
        bool youngbloodRole = false)
    {
        var resolution = _clanManager.ResolveCached(userId, youngbloodRole);
        var rank = youngbloodRole
            ? YautjaRank.YoungBlood
            : CanonicalHunterSpawnRank(resolution.Rank);
        // A persisted clanless Ancient keeps its legacy entitlement. Leader also canonicalizes to Ancient,
        // but its current whitelist must not implicitly grant Council status.
        var externalCouncil = resolution.ClanId == null &&
                              rank == YautjaRank.Ancient &&
                              !resolution.WhitelistFlags.HasFlag(YautjaWhitelistFlags.Leader);
        var externalLeader = resolution.ClanId == null && rank == YautjaRank.Leader;

        return new(
            rank,
            YautjaRankResolver.CanUseUnique(rank),
            resolution.WhitelistFlags.HasFlag(YautjaWhitelistFlags.Legacy) ||
            resolution.WhitelistFlags.HasFlag(YautjaWhitelistFlags.CouncilLegacy),
            resolution.WhitelistFlags.HasFlag(YautjaWhitelistFlags.Council) ||
            resolution.WhitelistFlags.HasFlag(YautjaWhitelistFlags.CouncilLegacy) ||
            externalCouncil,
            resolution.WhitelistFlags.HasFlag(YautjaWhitelistFlags.Leader) ||
            externalLeader);
    }

    public static YautjaRank CanonicalHunterSpawnRank(YautjaRank rank)
    {
        return rank == YautjaRank.Unblooded ? YautjaRank.Blooded : Sanitize(rank);
    }

    public async Task Set(NetUserId userId, YautjaRank rank)
    {
        if (!IsPersistentRank(rank))
            throw new ArgumentException("Young Blood is reserved for the special hunt role.", nameof(rank));

        _clanManager.InvalidateCache(userId);
        if (!await _clanManager.SetMaintenanceRank(userId, rank))
            throw new InvalidOperationException("The player's Yautja clan no longer exists or is inactive.");

        await Refresh(userId);
    }

    public async Task Refresh(NetUserId userId)
    {
        _clanManager.InvalidateCache(userId);
        await Prime(userId);
    }

    public static YautjaRank Sanitize(YautjaRank? rank)
    {
        if (rank is not { } value || !Enum.IsDefined(value) || value == YautjaRank.YoungBlood)
            return YautjaRank.Blooded;

        return value;
    }

    public static bool IsPersistentRank(YautjaRank rank)
    {
        return Enum.IsDefined(rank) && rank != YautjaRank.YoungBlood;
    }

    private async Task LoadData(ICommonSession session, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        await Prime(session.UserId);
        cancel.ThrowIfCancellationRequested();
    }

    void IPostInjectInit.PostInject()
    {
        _userDb.AddOnLoadPlayer(LoadData);
    }
}
