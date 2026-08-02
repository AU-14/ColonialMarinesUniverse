using System;
using System.Collections.Generic;
using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Yautja;

[Serializable, NetSerializable]
public sealed class YautjaClanInfoEuiState : EuiStateBase
{
    public YautjaClanInfoEuiState(
        int? clanId,
        string clanName,
        string clanDescription,
        int clanHonor,
        string clanColor,
        YautjaRank viewerRank,
        YautjaClanPermission viewerPermissions,
        List<YautjaClanInfoOption> availableClans,
        bool canEditDescription,
        bool canEditAppearance,
        bool canSetHonor,
        bool canPurge,
        bool canDeleteClan,
        bool canManageMembers,
        bool canSetAncient,
        bool canMove,
        List<YautjaClanInfoMemberState> members,
        string statusMessage)
    {
        ClanId = clanId;
        ClanName = clanName;
        ClanDescription = clanDescription;
        ClanHonor = clanHonor;
        ClanColor = clanColor;
        ViewerRank = viewerRank;
        ViewerPermissions = viewerPermissions;
        AvailableClans = availableClans;
        CanEditDescription = canEditDescription;
        CanEditAppearance = canEditAppearance;
        CanSetHonor = canSetHonor;
        CanPurge = canPurge;
        CanDeleteClan = canDeleteClan;
        CanManageMembers = canManageMembers;
        CanSetAncient = canSetAncient;
        CanMove = canMove;
        Members = members;
        StatusMessage = statusMessage;
    }

    public int? ClanId { get; }
    public string ClanName { get; }
    public string ClanDescription { get; }
    public int ClanHonor { get; }
    public string ClanColor { get; }
    public YautjaRank ViewerRank { get; }
    public YautjaClanPermission ViewerPermissions { get; }
    public List<YautjaClanInfoOption> AvailableClans { get; }
    public bool CanEditDescription { get; }
    public bool CanEditAppearance { get; }
    public bool CanSetHonor { get; }
    public bool CanPurge { get; }
    public bool CanDeleteClan { get; }
    public bool CanManageMembers { get; }
    public bool CanSetAncient { get; }
    public bool CanMove { get; }
    public List<YautjaClanInfoMemberState> Members { get; }
    public string StatusMessage { get; }
}

[Serializable, NetSerializable]
public sealed class YautjaClanInfoMemberState
{
    public YautjaClanInfoMemberState(
        NetUserId playerId,
        string name,
        YautjaRank rank,
        string rankIconState,
        int honor,
        bool online,
        bool canManage,
        bool canSetAncient,
        bool canMove)
    {
        PlayerId = playerId;
        Name = name;
        Rank = rank;
        RankIconState = rankIconState;
        Honor = honor;
        Online = online;
        CanManage = canManage;
        CanSetAncient = canSetAncient;
        CanMove = canMove;
    }

    public NetUserId PlayerId { get; }
    public string Name { get; }
    public YautjaRank Rank { get; }
    public string RankIconState { get; }
    public int Honor { get; }
    public bool Online { get; }
    public bool CanManage { get; }
    public bool CanSetAncient { get; }
    public bool CanMove { get; }
}

[Serializable, NetSerializable]
public sealed class YautjaClanInfoInitializeMessage : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class YautjaClanInfoRefreshMessage : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class YautjaClanInfoSetRankMessage(NetUserId target, YautjaRank rank) : EuiMessageBase
{
    public NetUserId Target { get; } = target;
    public YautjaRank Rank { get; } = rank;
}

[Serializable, NetSerializable]
public sealed class YautjaClanInfoSetAncientMessage(NetUserId target, bool enabled) : EuiMessageBase
{
    public NetUserId Target { get; } = target;
    public bool Enabled { get; } = enabled;
}

[Serializable, NetSerializable]
public sealed class YautjaClanInfoMoveMemberMessage(NetUserId target, int? clanId) : EuiMessageBase
{
    public NetUserId Target { get; } = target;
    public int? ClanId { get; } = clanId;
}

[Serializable, NetSerializable]
public sealed class YautjaClanInfoSelectClanMessage(int? clanId) : EuiMessageBase
{
    public int? ClanId { get; } = clanId;
}

[Serializable, NetSerializable]
public sealed class YautjaClanInfoUpdateDescriptionMessage(int clanId, string description) : EuiMessageBase
{
    public int ClanId { get; } = clanId;
    public string Description { get; } = description;
}

[Serializable, NetSerializable]
public sealed class YautjaClanInfoUpdateAppearanceMessage(int clanId, string name, string color) : EuiMessageBase
{
    public int ClanId { get; } = clanId;
    public string Name { get; } = name;
    public string Color { get; } = color;
}

[Serializable, NetSerializable]
public sealed class YautjaClanInfoSetHonorMessage(int clanId, int honor) : EuiMessageBase
{
    public int ClanId { get; } = clanId;
    public int Honor { get; } = honor;
}

[Serializable, NetSerializable]
public sealed class YautjaClanInfoPurgeMemberMessage(NetUserId target) : EuiMessageBase
{
    public NetUserId Target { get; } = target;
}

[Serializable, NetSerializable]
public sealed class YautjaClanInfoDeleteClanMessage(int clanId) : EuiMessageBase
{
    public int ClanId { get; } = clanId;
}
