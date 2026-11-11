using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.Network;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaClanManager
{
    [Dependency] private IServerDbManager _db = default!;

    private readonly Dictionary<NetUserId, YautjaClanResolution> _cache = new();
    private readonly YautjaClanCacheVersions _cacheVersions = new();

    public async Task<YautjaClanResolution> Resolve(NetUserId userId, bool youngbloodRole = false)
    {
        if (youngbloodRole)
            return ResolveSpecial(YautjaWhitelistFlags.None, true);

        if (_cache.TryGetValue(userId, out var cached))
            return cached;

        var requestVersion = _cacheVersions.Capture(userId);
        var whitelistFlags = (YautjaWhitelistFlags) await _db.GetYautjaWhitelistFlagsAsync(userId.UserId);
        var member = await _db.GetYautjaClanMemberAsync(userId.UserId);
        YautjaClanResolution resolution;

        if (whitelistFlags.HasFlag(YautjaWhitelistFlags.Leader) ||
            whitelistFlags.HasFlag(YautjaWhitelistFlags.Council) ||
            whitelistFlags.HasFlag(YautjaWhitelistFlags.CouncilLegacy))
        {
            resolution = ResolveSpecial(
                whitelistFlags,
                false,
                member?.ClanId,
                member?.Honor ?? 0,
                member?.IsLegacy ?? false);
        }
        else if (member == null)
        {
            var legacyRank = await _db.GetYautjaRank(userId.UserId);
            resolution = new(
                SanitizeStoredRank(legacyRank is { } rank ? (int) rank : null),
                null,
                YautjaClanPermission.UserView,
                legacyRank != null,
                0,
                whitelistFlags);
        }
        else
        {
            var rank = SanitizeStoredRank(member.Rank);
            var permissions = !member.IsLegacy && TryReadPermissions(member.Permissions, out var storedPermissions)
                ? storedPermissions
                : PermissionsForRank(rank);
            if (rank == YautjaRank.Leader)
                permissions = PermissionsForRank(YautjaRank.Leader);
            resolution = new(rank, member.ClanId, permissions, member.IsLegacy, member.Honor, whitelistFlags);
        }

        if (_cacheVersions.IsCurrent(userId, requestVersion))
            _cache[userId] = resolution;

        return resolution;
    }

    public YautjaClanResolution ResolveCached(NetUserId userId, bool youngbloodRole = false)
    {
        if (youngbloodRole)
            return ResolveSpecial(YautjaWhitelistFlags.None, true);

        if (_cache.TryGetValue(userId, out var cached))
            return cached;

        return ResolveSpecial(YautjaWhitelistFlags.None, false);
    }

    public async Task<bool> SetMaintenanceRank(NetUserId userId, YautjaRank rank)
    {
        if (!YautjaClanPolicy.GetNormalAssignableRanks().Contains(rank) && rank != YautjaRank.Ancient)
            throw new ArgumentException("The requested rank cannot be persisted.", nameof(rank));

        var existing = await _db.GetYautjaClanMemberAsync(userId.UserId);
        if (!await _db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
            userId.UserId,
            existing?.ClanId,
            (int) rank,
            (int) PermissionsForRank(rank),
            existing?.Honor ?? 0,
            true)))
        {
            return false;
        }

        InvalidateCache(userId);
        return true;
    }

    public static YautjaClanResolution ResolveSpecial(
        YautjaWhitelistFlags whitelistFlags,
        bool youngbloodRole,
        int? clanId = null,
        int honor = 0,
        bool isLegacy = false)
    {
        if (youngbloodRole)
            return new(YautjaRank.YoungBlood, null, YautjaClanPermission.None, false, 0, whitelistFlags);

        if (whitelistFlags.HasFlag(YautjaWhitelistFlags.Leader))
            return new(
                YautjaRank.Ancient,
                clanId,
                YautjaClanPermission.All,
                isLegacy || whitelistFlags.HasFlag(YautjaWhitelistFlags.Legacy),
                honor,
                whitelistFlags);

        if (whitelistFlags.HasFlag(YautjaWhitelistFlags.Council) ||
            whitelistFlags.HasFlag(YautjaWhitelistFlags.CouncilLegacy))
        {
            return new(
                YautjaRank.Ancient,
                clanId,
                YautjaClanPermission.AdminAncient,
                isLegacy ||
                whitelistFlags.HasFlag(YautjaWhitelistFlags.Legacy) ||
                whitelistFlags.HasFlag(YautjaWhitelistFlags.CouncilLegacy),
                honor,
                whitelistFlags);
        }

        return new(YautjaRank.Blooded, null, YautjaClanPermission.UserView, false, 0, whitelistFlags);
    }

    public static YautjaRank SanitizeStoredRank(int? value)
    {
        if (value is not { } raw || raw < byte.MinValue || raw > byte.MaxValue || !Enum.IsDefined((YautjaRank) raw))
            return YautjaRank.Blooded;

        var rank = (YautjaRank) raw;
        return rank == YautjaRank.YoungBlood ? YautjaRank.Blooded : rank;
    }

    private static bool TryReadPermissions(int raw, out YautjaClanPermission permissions)
    {
        if (raw < byte.MinValue || raw > byte.MaxValue)
        {
            permissions = YautjaClanPermission.None;
            return false;
        }

        permissions = (YautjaClanPermission) (byte) raw;
        return Enum.IsDefined(permissions);
    }

    public static YautjaClanPermission PermissionsForRank(YautjaRank rank)
    {
        return rank switch
        {
            YautjaRank.Unblooded => YautjaClanPermission.AdminModify,
            YautjaRank.Blooded => YautjaClanPermission.UserAll,
            YautjaRank.Elite => YautjaClanPermission.UserAll,
            YautjaRank.Elder => YautjaClanPermission.UserAll,
            YautjaRank.Leader => YautjaClanPermission.UserAll,
            YautjaRank.Ancient => YautjaClanPermission.AdminAncient,
            _ => YautjaClanPermission.None,
        };
    }

    private static YautjaClanMemberSnapshot ToSnapshot(NetUserId userId, YautjaClanResolution resolution)
    {
        return new(userId, resolution.ClanId, resolution.Rank, resolution.Permissions, resolution.IsLegacy, resolution.Honor);
    }

    public void InvalidateCache(params NetUserId[] userIds)
    {
        foreach (var userId in userIds)
        {
            _cacheVersions.Increment(userId);
            _cache.Remove(userId);
        }
    }
}

internal sealed class YautjaClanCacheVersions
{
    private readonly Dictionary<NetUserId, long> _versions = new();

    public long Capture(NetUserId userId)
    {
        return _versions.TryGetValue(userId, out var version) ? version : 0;
    }

    public void Increment(NetUserId userId)
    {
        _versions[userId] = Capture(userId) + 1;
    }

    public bool IsCurrent(NetUserId userId, long capturedVersion)
    {
        return Capture(userId) == capturedVersion;
    }
}

public readonly record struct YautjaClanMutationResult(
    bool Succeeded,
    string? Error,
    IReadOnlyList<NetUserId>? AffectedPlayers = null)
{
    public static readonly YautjaClanMutationResult Successful = new(true, null, Array.Empty<NetUserId>());

    // error is a loc id
    public static YautjaClanMutationResult Denied(string error)
    {
        return new(false, error, Array.Empty<NetUserId>());
    }
}
