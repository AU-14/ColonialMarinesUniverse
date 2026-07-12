using System;
using System.Collections.Generic;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.RoundStatistics;

[Serializable, NetSerializable]
public enum CMURoundStatisticsPreset : byte
{
    DistressSignal,
    Insurgency,
    ColonyFall,
}

[Serializable, NetSerializable]
public enum CMURoundStatisticsWinner : byte
{
    Xeno,
    Govfor,
    Clf,
    Colonists,
    Threat,
    Draw,
    Unknown,
}

[Serializable, NetSerializable]
public enum CMURoundStatisticsOutcome : byte
{
    Unknown,
    XenoMajorHijackWin,
    XenoMinorHijackLoss,
    MarineMinorHiveCollapse,
    MarineMajorXenoWipe,
    DrawAlmayerAutodestruct,
    InsurgencyClfVictory,
    InsurgencyGovforVictory,
    ColonyFallThreatVictory,
    ColonyFallSurvivorVictory,
    Stalemate,
    ObjectiveVictory,
}

[Serializable, NetSerializable]
public readonly record struct CMURoundOutcomeRecord(
    int RoundId,
    CMURoundStatisticsPreset Preset,
    CMURoundStatisticsWinner Winner,
    CMURoundStatisticsOutcome Outcome,
    string Source,
    string? SelectedThreatId,
    string? PlanetId,
    string? GovforPlatoonId,
    string? OpforPlatoonId,
    int PlayerCount,
    int DurationSeconds,
    DateTime RecordedAt);

[Serializable, NetSerializable]
public readonly record struct CMURoundOutcomeBreakdown(
    CMURoundStatisticsOutcome Outcome,
    CMURoundStatisticsWinner Winner,
    int Count);

[Serializable, NetSerializable]
public readonly record struct CMURoundManualReasonBreakdown(
    string Reason,
    int Count);

[Serializable, NetSerializable]
public readonly record struct CMURoundThreatBreakdown(
    string ThreatId,
    int SideAWins,
    int SideBWins,
    int Draws,
    int Unknown,
    int Total);

[Serializable, NetSerializable]
public readonly record struct CMURoundRecentForm(
    int Rounds,
    int SideAWins,
    int SideBWins,
    int Draws,
    int Unknown,
    List<CMURoundStatisticsWinner> Winners);

[Serializable, NetSerializable]
public readonly record struct CMURoundStreak(
    CMURoundStatisticsWinner Winner,
    int Count);

[Serializable, NetSerializable]
public readonly record struct CMURoundDurationBreakdown(
    int AverageSeconds,
    int SideAAverageSeconds,
    int SideBAverageSeconds,
    int DrawAverageSeconds,
    int UnknownAverageSeconds);

[Serializable, NetSerializable]
public readonly record struct CMURoundPlanetBreakdown(
    string PlanetId,
    int SideAWins,
    int SideBWins,
    int Draws,
    int Unknown,
    int Total,
    int AverageDurationSeconds);

[Serializable, NetSerializable]
public readonly record struct CMURoundPlatoonMatchupBreakdown(
    string GovforPlatoonId,
    string OpforPlatoonId,
    int SideAWins,
    int SideBWins,
    int Draws,
    int Unknown,
    int Total);

[Serializable, NetSerializable]
public readonly record struct CMURoundPlayerCountBandBreakdown(
    string Band,
    int MinPlayers,
    int MaxPlayers,
    int SideAWins,
    int SideBWins,
    int Draws,
    int Unknown,
    int Total);

[Serializable, NetSerializable]
public sealed class CMURoundModeStatistics(
    CMURoundStatisticsPreset preset,
    string title,
    string sideA,
    string sideB,
    int sideAWins,
    int sideBWins,
    int draws,
    int unknown,
    List<CMURoundOutcomeBreakdown> outcomes,
    List<CMURoundManualReasonBreakdown> manualReasons,
    List<CMURoundThreatBreakdown> threats,
    CMURoundRecentForm recentForm,
    CMURoundStreak currentStreak,
    CMURoundStreak longestStreak,
    CMURoundDurationBreakdown durations,
    List<CMURoundPlanetBreakdown> planets,
    List<CMURoundPlatoonMatchupBreakdown> platoonMatchups,
    List<CMURoundPlayerCountBandBreakdown> playerCountBands)
{
    public readonly CMURoundStatisticsPreset Preset = preset;
    public readonly string Title = title;
    public readonly string SideA = sideA;
    public readonly string SideB = sideB;
    public readonly int SideAWins = sideAWins;
    public readonly int SideBWins = sideBWins;
    public readonly int Draws = draws;
    public readonly int Unknown = unknown;
    public readonly List<CMURoundOutcomeBreakdown> Outcomes = outcomes;
    public readonly List<CMURoundManualReasonBreakdown> ManualReasons = manualReasons;
    public readonly List<CMURoundThreatBreakdown> Threats = threats;
    public readonly CMURoundRecentForm RecentForm = recentForm;
    public readonly CMURoundStreak CurrentStreak = currentStreak;
    public readonly CMURoundStreak LongestStreak = longestStreak;
    public readonly CMURoundDurationBreakdown Durations = durations;
    public readonly List<CMURoundPlanetBreakdown> Planets = planets;
    public readonly List<CMURoundPlatoonMatchupBreakdown> PlatoonMatchups = platoonMatchups;
    public readonly List<CMURoundPlayerCountBandBreakdown> PlayerCountBands = playerCountBands;

    public int Total => SideAWins + SideBWins + Draws + Unknown;
    public int DecidedTotal => SideAWins + SideBWins;
}

[Serializable, NetSerializable]
public sealed class CMURoundStatisticsDashboard(
    List<CMURoundModeStatistics> modes,
    List<CMURoundOutcomeRecord> recentRounds)
{
    public readonly List<CMURoundModeStatistics> Modes = modes;
    public readonly List<CMURoundOutcomeRecord> RecentRounds = recentRounds;
}

[Serializable, NetSerializable]
public sealed class CMURoundStatisticsEuiState(CMURoundStatisticsDashboard dashboard) : EuiStateBase
{
    public readonly CMURoundStatisticsDashboard Dashboard = dashboard;
}

[Serializable, NetSerializable]
public sealed class CMURoundStatisticsRefreshMsg : EuiMessageBase;
