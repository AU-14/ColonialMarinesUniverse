using System;
using System.Collections.Generic;
using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Yautja;

[Serializable, NetSerializable]
public sealed class YautjaClanAdminEuiState : EuiStateBase
{
    public YautjaClanAdminEuiState(
        List<YautjaClanAdminClanState> clans,
        string inspectedPlayer,
        string inspectedSummary,
        string statusMessage,
        long clanMutationVersion,
        int? lastMutatedClanId,
        YautjaClanAdminMutationKind lastMutationKind,
        List<YautjaClanAdminMemberState>? clanlessPlayers = null)
    {
        Clans = clans;
        InspectedPlayer = inspectedPlayer;
        InspectedSummary = inspectedSummary;
        StatusMessage = statusMessage;
        ClanMutationVersion = clanMutationVersion;
        LastMutatedClanId = lastMutatedClanId;
        LastMutationKind = lastMutationKind;
        ClanlessPlayers = clanlessPlayers ?? [];
    }

    public List<YautjaClanAdminClanState> Clans { get; }
    public string InspectedPlayer { get; }
    public string InspectedSummary { get; }
    public string StatusMessage { get; }
    public long ClanMutationVersion { get; }
    public int? LastMutatedClanId { get; }
    public YautjaClanAdminMutationKind LastMutationKind { get; }
    public List<YautjaClanAdminMemberState> ClanlessPlayers { get; }
}

[Serializable, NetSerializable]
public sealed class YautjaClanAdminClanState
{
    public YautjaClanAdminClanState(
        int id,
        string name,
        string description,
        int honor,
        string color,
        int memberCount,
        List<YautjaClanAdminMemberState>? members = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Honor = honor;
        Color = color;
        MemberCount = memberCount;
        Members = members ?? [];
    }

    public int Id { get; }
    public string Name { get; }
    public string Description { get; }
    public int Honor { get; }
    public string Color { get; }
    public int MemberCount { get; }
    public List<YautjaClanAdminMemberState> Members { get; }
}

[Serializable, NetSerializable]
public sealed class YautjaClanAdminMemberState
{
    public YautjaClanAdminMemberState(
        NetUserId playerId,
        string name,
        YautjaRank rank,
        bool online,
        YautjaWhitelistFlags whitelistFlags = YautjaWhitelistFlags.None)
    {
        PlayerId = playerId;
        Name = name;
        Rank = rank;
        Online = online;
        WhitelistFlags = whitelistFlags;
    }

    public NetUserId PlayerId { get; }
    public string Name { get; }
    public YautjaRank Rank { get; }
    public bool Online { get; }
    public YautjaWhitelistFlags WhitelistFlags { get; }
}

[Serializable, NetSerializable]
public sealed class YautjaClanAdminRefreshMessage : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class YautjaClanAdminCreateClanMessage(
    string name,
    string description,
    string color) : EuiMessageBase
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public string Color { get; } = color;
}

[Serializable, NetSerializable]
public enum YautjaClanAdminMutationKind : byte
{
    None,
    Created,
    Updated,
    Deleted,
}

[Serializable, NetSerializable]
public sealed class YautjaClanAdminUpdateClanMessage(
    int clanId,
    string name,
    string description,
    string color) : EuiMessageBase
{
    public int ClanId { get; } = clanId;
    public string Name { get; } = name;
    public string Description { get; } = description;
    public string Color { get; } = color;
}

[Serializable, NetSerializable]
public sealed class YautjaClanAdminDeleteClanMessage(int clanId) : EuiMessageBase
{
    public int ClanId { get; } = clanId;
}

[Serializable, NetSerializable]
public sealed class YautjaClanAdminRemoveMemberMessage(NetUserId playerId) : EuiMessageBase
{
    public NetUserId PlayerId { get; } = playerId;
}

[Serializable, NetSerializable]
public sealed class YautjaClanAdminClearWhitelistMessage(NetUserId playerId) : EuiMessageBase
{
    public NetUserId PlayerId { get; } = playerId;
}

[Serializable, NetSerializable]
public sealed class YautjaClanAdminSetMembershipMessage(
    string player,
    string clanId,
    YautjaRank rank) : EuiMessageBase
{
    public string Player { get; } = player;
    public string ClanId { get; } = clanId;
    public YautjaRank Rank { get; } = rank;
}

[Serializable, NetSerializable]
public sealed class YautjaClanAdminSetRankMessage(string player, YautjaRank rank) : EuiMessageBase
{
    public string Player { get; } = player;
    public YautjaRank Rank { get; } = rank;
}

[Serializable, NetSerializable]
public sealed class YautjaClanAdminSetWhitelistMessage(string player, YautjaWhitelistFlags flags) : EuiMessageBase
{
    public string Player { get; } = player;
    public YautjaWhitelistFlags Flags { get; } = flags;
}

[Serializable, NetSerializable]
public sealed class YautjaClanAdminInspectMessage(string player) : EuiMessageBase
{
    public string Player { get; } = player;
}
