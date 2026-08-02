using System;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._CMU14.Yautja;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._CMU14.Yautja;

/// <summary>
/// Resolves the server-owned clan rank without allowing the client profile to grant Young Blood status.
/// </summary>
public sealed partial class YautjaRankManager : IPostInjectInit
{
    [Dependency] private YautjaClanManager _clanManager = default!;
    [Dependency] private UserDbDataManager _userDb = default!;
    private readonly Dictionary<NetUserId, YautjaRank> _cache = new();
    private readonly Dictionary<NetUserId, long> _cacheVersions = new();

    public async Task<YautjaRank> Resolve(NetUserId userId, bool youngbloodRole = false)
    {
        if (youngbloodRole)
            return YautjaRank.YoungBlood;

        var requestVersion = GetCacheVersion(userId);
        var rank = (await _clanManager.Resolve(userId)).Rank;
        if (IsCacheVersionCurrent(requestVersion, GetCacheVersion(userId)))
            _cache[userId] = rank;

        return rank;
    }

    public async Task Prime(NetUserId userId)
    {
        await Resolve(userId);
    }

    public YautjaRank ResolveCached(NetUserId userId, bool youngbloodRole = false)
    {
        if (youngbloodRole)
            return YautjaRank.YoungBlood;

        if (_cache.TryGetValue(userId, out var rank))
            return rank;

        return YautjaRank.Blooded;
    }

    public YautjaProfileCapabilities ResolveProfileCapabilitiesCached(
        NetUserId userId,
        bool youngbloodRole = false)
    {
        var resolution = _clanManager.ResolveCached(userId, youngbloodRole);
        var rank = youngbloodRole
            ? YautjaRank.YoungBlood
            : CanonicalHunterSpawnRank(resolution.Rank);
        var externalCouncil = resolution.ClanId == null && rank == YautjaRank.Ancient;
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

        InvalidateCached(userId);
        if (!await _clanManager.SetMaintenanceRank(userId, rank))
            throw new InvalidOperationException("The player's Yautja clan no longer exists or is inactive.");

        await Refresh(userId);
    }

    public async Task Refresh(NetUserId userId)
    {
        _clanManager.InvalidateCache(userId);
        InvalidateCached(userId);
        await Prime(userId);
    }

    public void InvalidateCached(NetUserId userId)
    {
        NextCacheVersion(userId);
        _cache.Remove(userId);
    }

    private long GetCacheVersion(NetUserId userId)
    {
        return _cacheVersions.TryGetValue(userId, out var version) ? version : 0;
    }

    private long NextCacheVersion(NetUserId userId)
    {
        var version = GetCacheVersion(userId) + 1;
        _cacheVersions[userId] = version;
        return version;
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

    public static bool IsCacheVersionCurrent(long requestVersion, long currentVersion)
    {
        return requestVersion == currentVersion;
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
