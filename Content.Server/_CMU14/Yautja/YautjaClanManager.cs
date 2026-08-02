using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._CMU14.Yautja;
using Robust.Shared.Network;

namespace Content.Server._CMU14.Yautja;

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

    public async Task<YautjaClanView> GetView(NetUserId userId, int? selectedClanId = null)
    {
        var viewer = await Resolve(userId);
        var canViewAll = YautjaClanPolicy.HasPermission(viewer.Permissions, YautjaClanPermission.AdminView);
        var availableClans = new List<YautjaClanInfoOption>();
        List<YautjaClanRecord> activeClans = [];

        if (canViewAll)
        {
            activeClans = await _db.GetYautjaClansAsync();
            availableClans.Add(new(null, "Players without a clan"));
            availableClans.AddRange(activeClans.Select(clan => new YautjaClanInfoOption(clan.Id, clan.Name)));
        }
        else if (viewer.ClanId is { } ownClanId)
        {
            var ownClan = await _db.GetYautjaClanAsync(ownClanId);
            if (ownClan is not null && ownClan.Active)
                availableClans.Add(new(ownClan.Id, ownClan.Name));
        }
        else
        {
            availableClans.Add(new(null, "Players without a clan"));
        }

        var clanId = canViewAll
            ? selectedClanId
            : viewer.ClanId;
        if (clanId is { } requestedClanId &&
            (!canViewAll && requestedClanId != viewer.ClanId ||
             canViewAll && activeClans.All(clan => clan.Id != requestedClanId)))
        {
            clanId = viewer.ClanId;
        }

        YautjaClanRecord? clan = null;
        List<YautjaClanMemberRecord> memberRecords;
        if (clanId is { } selectedId)
        {
            clan = await _db.GetYautjaClanAsync(selectedId);
            if (clan is null || !clan.Active)
            {
                clanId = null;
                memberRecords = await _db.GetYautjaClanlessMembersAsync();
            }
            else
            {
                memberRecords = await _db.GetYautjaClanMembersAsync(selectedId);
            }
        }
        else
        {
            memberRecords = await _db.GetYautjaClanlessMembersAsync();
        }

        var members = new List<YautjaClanMemberSnapshot>(memberRecords.Count);
        foreach (var member in memberRecords)
        {
            var resolvedMember = await Resolve(new NetUserId(member.PlayerUserId));
            members.Add(new(
                new NetUserId(member.PlayerUserId),
                member.ClanId,
                resolvedMember.Rank,
                resolvedMember.Permissions,
                resolvedMember.IsLegacy,
                resolvedMember.Honor));
        }

        return new(
            viewer,
            clanId,
            clan?.Name ?? "",
            clan?.Description ?? "",
            clan?.Honor ?? 0,
            clan?.Color ?? "",
            members,
            availableClans);
    }

    public async Task<YautjaClanMutationResult> SetRank(
        NetUserId actorId,
        NetUserId targetId,
        YautjaRank requestedRank)
    {
        var actor = await Resolve(actorId);
        var target = await Resolve(targetId);
        var actorSnapshot = ToSnapshot(actorId, actor);
        var targetSnapshot = ToSnapshot(targetId, target);

        if (target.ClanId is not { } clanId)
            return YautjaClanMutationResult.Denied("Both hunters must belong to the same clan.");

        var members = await _db.GetYautjaClanMembersAsync(clanId);
        var clanSize = members.Count;
        var occupancy = members.Count(member => SanitizeStoredRank(member.Rank) == requestedRank);
        if (!YautjaClanPolicy.CanModifyRank(actorSnapshot, targetSnapshot, requestedRank, clanSize, occupancy))
            return YautjaClanMutationResult.Denied("You do not have permission to assign that rank.");

        if (!await _db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
            targetId.UserId,
            clanId,
            (int) requestedRank,
            (int) PermissionsForRank(requestedRank),
            target.Honor,
            false)))
        {
            return YautjaClanMutationResult.Denied("That clan no longer exists.");
        }

        InvalidateCache(targetId);
        return YautjaClanMutationResult.Successful;
    }

    public async Task<YautjaClanMutationResult> MoveMember(
        NetUserId actorId,
        NetUserId targetId,
        int? destinationClanId)
    {
        var actor = await Resolve(actorId);
        var target = await Resolve(targetId);
        if (!YautjaClanPolicy.CanMove(ToSnapshot(actorId, actor), ToSnapshot(targetId, target)))
            return YautjaClanMutationResult.Denied("You do not have permission to move that hunter.");

        var keepAncient = target.Rank == YautjaRank.Ancient &&
                          target.Permissions.HasFlag(YautjaClanPermission.AdminAncient);
        var rank = keepAncient ? YautjaRank.Ancient : YautjaRank.Blooded;
        var permissions = keepAncient ? YautjaClanPermission.AdminAncient : PermissionsForRank(YautjaRank.Blooded);
        if (!await _db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
            targetId.UserId,
            destinationClanId,
            (int) rank,
            (int) permissions,
            target.Honor,
            false)))
        {
            return YautjaClanMutationResult.Denied("That clan does not exist or is no longer active.");
        }

        InvalidateCache(targetId);
        return YautjaClanMutationResult.Successful;
    }

    public async Task<YautjaClanMutationResult> SetAncient(
        NetUserId actorId,
        NetUserId targetId,
        bool enabled)
    {
        var actor = await Resolve(actorId);
        var target = await Resolve(targetId);
        if (!YautjaClanPolicy.CanSetAncient(ToSnapshot(actorId, actor), ToSnapshot(targetId, target), enabled))
            return YautjaClanMutationResult.Denied("Only an Ancient manager can change Ancient status.");

        if (target.ClanId is not { } clanId)
            return YautjaClanMutationResult.Denied("Both hunters must belong to the same clan.");

        var rank = enabled ? YautjaRank.Ancient : YautjaRank.Blooded;
        if (!await _db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
            targetId.UserId,
            clanId,
            (int) rank,
            (int) PermissionsForRank(rank),
            target.Honor,
            false)))
        {
            return YautjaClanMutationResult.Denied("That clan no longer exists.");
        }

        InvalidateCache(targetId);
        return YautjaClanMutationResult.Successful;
    }

    public async Task<YautjaClanMutationResult> UpdateDescription(
        NetUserId actorId,
        int clanId,
        string description)
    {
        var actor = await Resolve(actorId);
        if (!YautjaClanPolicy.CanManageClan(
                ToSnapshot(actorId, actor),
                clanId,
                YautjaClanPermission.UserModify))
        {
            return YautjaClanMutationResult.Denied("You do not have permission to edit this clan.");
        }

        var clan = await _db.GetYautjaClanAsync(clanId);
        if (clan is null || !clan.Active || string.IsNullOrWhiteSpace(description))
            return YautjaClanMutationResult.Denied("That clan or description is invalid.");

        if (!await _db.UpdateYautjaClanAsync(clanId, clan.Name, description.Trim(), clan.Color))
            return YautjaClanMutationResult.Denied("That clan no longer exists.");

        return YautjaClanMutationResult.Successful;
    }

    public async Task<YautjaClanMutationResult> UpdateAppearance(
        NetUserId actorId,
        int clanId,
        string name,
        string color)
    {
        var actor = await Resolve(actorId);
        if (!YautjaClanPolicy.HasPermission(actor.Permissions, YautjaClanPermission.AdminView) ||
            !YautjaClanPolicy.HasPermission(actor.Permissions, YautjaClanPermission.AdminModify))
        {
            return YautjaClanMutationResult.Denied("You do not have permission to edit this clan.");
        }

        if (!YautjaClanAdminValidation.TryNormalize(
                name,
                "Valid clan description",
                color,
                out var fields,
                out var error))
        {
            var errorText = error == YautjaClanAdminValidationError.InvalidColor
                ? "That clan color is invalid."
                : "That clan name is invalid.";
            return YautjaClanMutationResult.Denied(errorText);
        }

        var clan = await _db.GetYautjaClanAsync(clanId);
        if (clan is null || !clan.Active)
            return YautjaClanMutationResult.Denied("That clan no longer exists.");

        if (!await _db.UpdateYautjaClanAsync(clanId, fields.Name, clan.Description, fields.Color))
            return YautjaClanMutationResult.Denied("That clan no longer exists.");

        return YautjaClanMutationResult.Successful;
    }

    public async Task<YautjaClanMutationResult> SetClanHonor(
        NetUserId actorId,
        int clanId,
        int honor)
    {
        var actor = await Resolve(actorId);
        if (!YautjaClanPolicy.HasPermission(actor.Permissions, YautjaClanPermission.AdminManager))
            return YautjaClanMutationResult.Denied("Only an Ancient manager can change clan honor.");

        if (!await _db.UpdateYautjaClanHonorAsync(clanId, honor))
            return YautjaClanMutationResult.Denied("That clan no longer exists.");

        return YautjaClanMutationResult.Successful;
    }

    public async Task<YautjaClanMutationResult> PurgeMember(
        NetUserId actorId,
        NetUserId targetId)
    {
        var actor = await Resolve(actorId);
        var target = await Resolve(targetId);
        if (!YautjaClanPolicy.HasPermission(actor.Permissions, YautjaClanPermission.AdminManager) ||
            !YautjaClanPolicy.CanMove(ToSnapshot(actorId, actor), ToSnapshot(targetId, target)))
        {
            return YautjaClanMutationResult.Denied("You do not have permission to purge that hunter.");
        }

        if (!await _db.DeleteYautjaClanMemberAsync(targetId.UserId))
            return YautjaClanMutationResult.Denied("That hunter has no clan profile.");

        InvalidateCache(targetId);
        return new(true, null, [targetId]);
    }

    public async Task<YautjaClanMutationResult> DeleteClan(
        NetUserId actorId,
        int clanId)
    {
        var actor = await Resolve(actorId);
        if (!YautjaClanPolicy.HasPermission(actor.Permissions, YautjaClanPermission.AdminManager))
            return YautjaClanMutationResult.Denied("Only an Ancient manager can delete clans.");

        var result = await _db.DeactivateYautjaClanAsync(clanId);
        if (!result.Succeeded)
            return YautjaClanMutationResult.Denied("That clan no longer exists.");

        foreach (var detachedPlayer in result.DetachedPlayers)
            InvalidateCache(new NetUserId(detachedPlayer));

        return new(
            true,
            null,
            result.DetachedPlayers.Select(playerId => new NetUserId(playerId)).ToArray());
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

    public static YautjaClanMutationResult Denied(string error)
    {
        return new(false, error, Array.Empty<NetUserId>());
    }
}
