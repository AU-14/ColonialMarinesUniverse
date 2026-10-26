using System;
using System.Collections.Generic;
using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja;

[Serializable, NetSerializable]
public enum YautjaClanHallPage : byte
{
    Clan,
    Leaderboard,
    Hunter,
    Whitelists,
    Round,
}

[Serializable, NetSerializable]
public enum YautjaClanHallText : byte
{
    Description,
    Notice,
    EliteNotes,
}

// one state for the whole hall. detail blocks are null when the viewer isn't allowed them,
// so nothing private ever leaves the server
[Serializable, NetSerializable]
public sealed class YautjaClanHallEuiState : EuiStateBase
{
    public YautjaClanHallPage Page;

    // true when the server wants the window on Page (first open, after a find or a new clan).
    // plain refreshes leave the viewer on whatever tab they're reading
    public bool Navigate;
    public YautjaClanHallViewer Viewer = new();
    public List<YautjaClanHallClanRow> Clans = [];
    public List<YautjaClanHallHunterRow> Hunters = [];
    public YautjaClanHallClanPage? Clan;
    public YautjaClanHallDossier? Hunter;
    public List<YautjaClanHallWhitelistRow>? Whitelist;
    public YautjaClanHallRoundState? Round;
    public string Status = string.Empty;
    public bool StatusIsError;

    // bumped on every reply so the window knows its request landed
    public int Revision;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallViewer
{
    public NetUserId PlayerId;
    public string Name = string.Empty;
    public YautjaRank Rank = YautjaRank.Blooded;
    public int? ClanId;
    public int Honor;
    public bool Staff;
    public bool Host;
    public bool Council;
    public bool CanPetition;
    public int? PetitionClanId;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallClanRow
{
    public int Id;
    public string Name = string.Empty;
    public string Color = "#ffffff";
    public string Sigil = YautjaClanSigils.Default;
    public int Honor;
    public int Members;
    public string? LeaderName;
    public bool CanView;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallHunterRow
{
    public NetUserId PlayerId;
    public string Name = string.Empty;
    public int? ClanId;
    public YautjaRank Rank;
    public int Honor;
    public bool Online;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallClanPage
{
    public int Id;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public string Notice = string.Empty;
    public string? EliteNotes;
    public string Color = "#ffffff";
    public string Sigil = YautjaClanSigils.Default;
    public int Honor;
    public int Standing;
    public List<YautjaClanHallHunterRow> Members = [];
    public List<YautjaClanHallHunterRow>? Petitions;
    public bool CanEditNotice;
    public bool CanEditEliteNotes;
    public bool CanEditPresentation;
    public bool CanRename;
    public bool CanSetHonor;
    public bool CanDisband;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallDossier
{
    public NetUserId PlayerId;
    public string Name = string.Empty;
    public YautjaRank Rank;
    public int? ClanId;
    public int Honor;
    public bool Online;
    public int Standing;
    public bool IsSelf;
    public YautjaWhitelistFlags? Whitelist;
    public List<YautjaRank> AssignableRanks = [];
    public bool CanExile;
    public bool CanMove;
    public bool CanSetAncient;
    public bool IsAncient;
    public bool CanPurge;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallWhitelistRow
{
    public NetUserId PlayerId;
    public string Name = string.Empty;
    public YautjaWhitelistFlags Flags;
    public bool Online;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallRoundState
{
    public int RoundId;
    public bool RoundActive;
    public bool Initialized;
    public int ActiveHunterSlots;
    public int HunterSlots;
    public bool RandomEnabled;
    public int RandomMinimumRounds;
    public int RandomMaximumRounds;
    public int RandomRoundsRemaining;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallShowMessage(YautjaClanHallPage page, int? clanId = null, NetUserId? hunter = null)
    : EuiMessageBase
{
    public readonly YautjaClanHallPage Page = page;
    public readonly int? ClanId = clanId;
    public readonly NetUserId? Hunter = hunter;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallFindMessage(string query) : EuiMessageBase
{
    public readonly string Query = query;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallSetRankMessage(NetUserId target, YautjaRank rank) : EuiMessageBase
{
    public readonly NetUserId Target = target;
    public readonly YautjaRank Rank = rank;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallExileMessage(NetUserId target) : EuiMessageBase
{
    public readonly NetUserId Target = target;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallMoveMessage(NetUserId target, int? clanId) : EuiMessageBase
{
    public readonly NetUserId Target = target;
    public readonly int? ClanId = clanId;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallSetAncientMessage(NetUserId target, bool enabled) : EuiMessageBase
{
    public readonly NetUserId Target = target;
    public readonly bool Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallPurgeMessage(NetUserId target) : EuiMessageBase
{
    public readonly NetUserId Target = target;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallEditTextMessage(int clanId, YautjaClanHallText field, string text) : EuiMessageBase
{
    public readonly int ClanId = clanId;
    public readonly YautjaClanHallText Field = field;
    public readonly string Text = text;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallEditLookMessage(int clanId, string name, string color, string sigil) : EuiMessageBase
{
    public readonly int ClanId = clanId;
    public readonly string Name = name;
    public readonly string Color = color;
    public readonly string Sigil = sigil;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallSetHonorMessage(int clanId, int honor) : EuiMessageBase
{
    public readonly int ClanId = clanId;
    public readonly int Honor = honor;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallFoundClanMessage(string name, string color) : EuiMessageBase
{
    public readonly string Name = name;
    public readonly string Color = color;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallDisbandMessage(int clanId) : EuiMessageBase
{
    public readonly int ClanId = clanId;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallPetitionMessage(int? clanId) : EuiMessageBase
{
    // null withdraws the open petition
    public readonly int? ClanId = clanId;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallAnswerPetitionMessage(NetUserId target, bool accept) : EuiMessageBase
{
    public readonly NetUserId Target = target;
    public readonly bool Accept = accept;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallSetWhitelistMessage(string player, YautjaWhitelistFlags flags) : EuiMessageBase
{
    public readonly string Player = player;
    public readonly YautjaWhitelistFlags Flags = flags;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallRoundInitializeMessage : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class YautjaClanHallRoundSlotsMessage(int slots) : EuiMessageBase
{
    public readonly int Slots = slots;
}

[Serializable, NetSerializable]
public sealed class YautjaClanHallRoundRandomMessage(bool enabled, int minimumRounds, int maximumRounds) : EuiMessageBase
{
    public readonly bool Enabled = enabled;
    public readonly int MinimumRounds = minimumRounds;
    public readonly int MaximumRounds = maximumRounds;
}
