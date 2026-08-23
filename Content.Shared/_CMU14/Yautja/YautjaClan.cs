using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Yautja;

[Flags]
public enum YautjaClanPermission : byte
{
    None = 0,
    UserView = 1 << 0,
    UserModify = 1 << 1,
    AdminView = 1 << 2,
    AdminModify = 1 << 3,
    AdminMove = 1 << 4,
    AdminManager = 1 << 5,
    UserAll = UserView | UserModify,
    AdminAncient = AdminView | AdminModify | AdminMove,
    All = UserAll | AdminAncient | AdminManager,
}

[Flags]
public enum YautjaWhitelistFlags : byte
{
    None = 0,
    Yautja = 1 << 0,
    Legacy = 1 << 1,
    Council = 1 << 2,
    CouncilLegacy = 1 << 3,
    Leader = 1 << 4,
}

public sealed record YautjaClanMemberSnapshot(
    NetUserId PlayerId,
    int? ClanId,
    YautjaRank Rank,
    YautjaClanPermission Permissions,
    bool IsLegacy,
    int Honor);

public sealed record YautjaClanResolution(
    YautjaRank Rank,
    int? ClanId,
    YautjaClanPermission Permissions,
    bool IsLegacy,
    int Honor,
    YautjaWhitelistFlags WhitelistFlags);

[Serializable, NetSerializable]
public sealed record YautjaClanInfoOption(int? ClanId, string Name);

public sealed record YautjaClanView(
    YautjaClanResolution Viewer,
    int? ClanId,
    string ClanName,
    string ClanDescription,
    int ClanHonor,
    string ClanColor,
    IReadOnlyList<YautjaClanMemberSnapshot> Members,
    IReadOnlyList<YautjaClanInfoOption> AvailableClans);

public sealed record YautjaClanRankRule(
    YautjaRank Rank,
    YautjaClanPermission RequiredPermission,
    int? AbsoluteLimit,
    int? MembersPerRankLimit);

public static class YautjaClanPolicy
{
    private static readonly YautjaClanRankRule[] Rules =
    [
        new(YautjaRank.Unblooded, YautjaClanPermission.AdminModify, null, null),
        new(YautjaRank.YoungBlood, YautjaClanPermission.None, null, null),
        new(YautjaRank.Blooded, YautjaClanPermission.UserModify, null, null),
        new(YautjaRank.Elite, YautjaClanPermission.UserModify, 5, null),
        new(YautjaRank.Elder, YautjaClanPermission.UserModify, null, 12),
        new(YautjaRank.Leader, YautjaClanPermission.AdminModify, 1, null),
        new(YautjaRank.Ancient, YautjaClanPermission.AdminAncient, null, null),
    ];

    private static readonly YautjaRank[] NormalAssignableRanks =
    [
        YautjaRank.Unblooded,
        YautjaRank.Blooded,
        YautjaRank.Elite,
        YautjaRank.Elder,
        YautjaRank.Leader,
    ];

    public static YautjaClanRankRule GetRule(YautjaRank rank)
    {
        return Rules.FirstOrDefault(rule => rule.Rank == rank)
               ?? Rules.Single(rule => rule.Rank == YautjaRank.Blooded);
    }

    public static IReadOnlyList<YautjaRank> GetNormalAssignableRanks()
    {
        return NormalAssignableRanks;
    }

    public static bool CanView(YautjaClanMemberSnapshot actor)
    {
        return HasPermission(actor.Permissions, YautjaClanPermission.UserView) ||
               HasPermission(actor.Permissions, YautjaClanPermission.AdminView);
    }

    public static bool CanManageClan(
        YautjaClanMemberSnapshot actor,
        int? clanId,
        YautjaClanPermission permission)
    {
        if (clanId == null)
            return false;

        if (permission == YautjaClanPermission.UserModify)
        {
            if (HasPermission(actor.Permissions, YautjaClanPermission.AdminView) &&
                HasPermission(actor.Permissions, YautjaClanPermission.AdminModify))
            {
                return true;
            }

            return actor.ClanId == clanId &&
                   HasPermission(actor.Permissions, YautjaClanPermission.UserModify);
        }

        return HasPermission(actor.Permissions, permission);
    }

    public static bool CanTarget(
        YautjaClanMemberSnapshot actor,
        YautjaClanMemberSnapshot target)
    {
        if (actor.PlayerId == target.PlayerId)
            return false;

        if (target.Rank == YautjaRank.Ancient ||
            HasPermission(target.Permissions, YautjaClanPermission.AdminAncient) ||
            HasPermission(target.Permissions, YautjaClanPermission.AdminManager))
        {
            return false;
        }

        if (HasPermission(actor.Permissions, YautjaClanPermission.AdminManager))
            return true;

        return actor.Rank > target.Rank;
    }

    public static bool CanModifyRank(
        YautjaClanMemberSnapshot actor,
        YautjaClanMemberSnapshot target,
        YautjaRank requestedRank,
        int clanSize,
        int currentRankOccupancy)
    {
        var hasGlobalModify = HasPermission(actor.Permissions, YautjaClanPermission.AdminView) &&
                              HasPermission(actor.Permissions, YautjaClanPermission.AdminModify);
        var hasSameClan = actor.ClanId != null && target.ClanId == actor.ClanId;
        if (!CanTarget(actor, target) || (!hasSameClan && !hasGlobalModify) || target.ClanId == null)
            return false;

        if (!NormalAssignableRanks.Contains(requestedRank))
            return false;

        var rule = GetRule(requestedRank);
        var requiredPermission = hasGlobalModify
            ? YautjaClanPermission.AdminModify
            : rule.RequiredPermission;
        if (!HasPermission(actor.Permissions, requiredPermission))
            return false;

        var occupancyAfterChange = currentRankOccupancy + (target.Rank == requestedRank ? 0 : 1);
        if (rule.AbsoluteLimit is { } absoluteLimit && occupancyAfterChange > absoluteLimit)
            return false;

        if (rule.MembersPerRankLimit is { } membersPerRankLimit)
        {
            if (clanSize < 1)
                return false;

            var rankLimit = (clanSize + membersPerRankLimit - 1) / membersPerRankLimit;
            if (occupancyAfterChange > rankLimit)
                return false;
        }

        return true;
    }

    public static bool CanMove(
        YautjaClanMemberSnapshot actor,
        YautjaClanMemberSnapshot target)
    {
        if (!HasPermission(actor.Permissions, YautjaClanPermission.AdminMove) ||
            actor.PlayerId == target.PlayerId ||
            HasPermission(target.Permissions, YautjaClanPermission.AdminManager))
        {
            return false;
        }

        return CanTarget(actor, target) ||
               HasPermission(actor.Permissions, YautjaClanPermission.AdminManager) &&
               target.Rank == YautjaRank.Ancient &&
               HasPermission(target.Permissions, YautjaClanPermission.AdminAncient);
    }

    public static bool CanSetAncient(
        YautjaClanMemberSnapshot actor,
        YautjaClanMemberSnapshot target)
    {
        return CanSetAncient(actor, target, true);
    }

    public static bool CanSetAncient(
        YautjaClanMemberSnapshot actor,
        YautjaClanMemberSnapshot target,
        bool enabled)
    {
        if (!HasPermission(actor.Permissions, YautjaClanPermission.AdminManager) ||
            actor.PlayerId == target.PlayerId)
        {
            return false;
        }

        // An Ancient can be demoted only by a full clan manager. This is the
        // one deliberate exception to the general Ancient protection rule.
        return enabled
            ? CanTarget(actor, target)
            : target.Rank == YautjaRank.Ancient &&
              !HasPermission(target.Permissions, YautjaClanPermission.AdminManager) &&
              HasPermission(target.Permissions, YautjaClanPermission.AdminAncient);
    }

    public static bool HasPermission(
        YautjaClanPermission actual,
        YautjaClanPermission required)
    {
        return (actual & required) == required;
    }
}
