using System;
using System.Collections.Generic;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.RoundStatistics;

public static class CMUPlaytimeLeaderboardShared
{
    public static readonly TimeSpan RefreshCooldown = TimeSpan.FromSeconds(60);
}

[Serializable, NetSerializable]
public readonly record struct CMUPlaytimeLeaderboardRow(
    Guid PlayerId,
    string Tracker,
    string Player,
    double Hours);

// Rank 0 means the viewer's appended row, too far down to rank cheaply.
// Redacted entries carry no hours over the wire, only the redaction reason.
[Serializable, NetSerializable]
public readonly record struct CMUPlaytimeLeaderboardEntry(
    int Rank,
    string Player,
    double Hours,
    string Tag,
    bool IsYou,
    CMUPlaytimeRedaction Redaction);

public enum CMUPlaytimeRedaction
{
    None,
    HiddenByUser,
    Banned,
}

[Serializable, NetSerializable]
public sealed class CMUPlaytimeRoleLeaderboard(
    string role,
    int playerCount,
    double totalHours,
    List<CMUPlaytimeLeaderboardEntry> entries)
{
    public readonly string Role = role;
    public readonly int PlayerCount = playerCount;
    public readonly double TotalHours = totalHours;
    public readonly List<CMUPlaytimeLeaderboardEntry> Entries = entries;
}

[Serializable, NetSerializable]
public sealed class CMUPlaytimeLeaderboard(
    int players,
    List<CMUPlaytimeLeaderboardEntry> xenoChampions,
    List<CMUPlaytimeLeaderboardEntry> govforChampions,
    List<CMUPlaytimeRoleLeaderboard> xeno,
    List<CMUPlaytimeRoleLeaderboard> govfor,
    bool youHidden)
{
    public readonly int Players = players;
    public readonly List<CMUPlaytimeLeaderboardEntry> XenoChampions = xenoChampions;
    public readonly List<CMUPlaytimeLeaderboardEntry> GovforChampions = govforChampions;
    public readonly List<CMUPlaytimeRoleLeaderboard> Xeno = xeno;
    public readonly List<CMUPlaytimeRoleLeaderboard> Govfor = govfor;
    public readonly bool YouHidden = youHidden;
}

[Serializable, NetSerializable]
public sealed class CMUPlaytimeLeaderboardEuiState(CMUPlaytimeLeaderboard leaderboard) : EuiStateBase
{
    public readonly CMUPlaytimeLeaderboard Leaderboard = leaderboard;
}

[Serializable, NetSerializable]
public sealed class CMUPlaytimeLeaderboardRefreshMsg : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class CMUPlaytimeLeaderboardSetHiddenMsg(bool hidden) : EuiMessageBase
{
    public readonly bool Hidden = hidden;
}
