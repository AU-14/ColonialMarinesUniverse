using System.Collections.Generic;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Yautja;

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

// rank rules live in YautjaClanHallPolicy, this is just the shared bits
public static class YautjaClanPolicy
{
    private static readonly YautjaRank[] NormalAssignableRanks =
    [
        YautjaRank.Unblooded,
        YautjaRank.Blooded,
        YautjaRank.Elite,
        YautjaRank.Elder,
        YautjaRank.Leader,
    ];

    public static IReadOnlyList<YautjaRank> GetNormalAssignableRanks()
    {
        return NormalAssignableRanks;
    }

    public static bool HasPermission(
        YautjaClanPermission actual,
        YautjaClanPermission required)
    {
        return (actual & required) == required;
    }
}
