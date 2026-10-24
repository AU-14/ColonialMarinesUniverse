using System;
using System.Collections.Generic;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared.CMU14.RoundStatistics;
using Content.Shared.Eui;
using Robust.Shared.Asynchronous;
using Robust.Shared.Log;

namespace Content.Server.CMU14.RoundStatistics;

public sealed partial class CMURoundStatisticsEui : BaseEui
{
    private const int RecentRounds = 30;

    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ITaskManager _task = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("cmu.round_statistics");
    private CMURoundStatisticsDashboard _dashboard = EmptyDashboard();
    private CMURoundStatisticsPeriod _period = CMURoundStatisticsPeriod.OneMonth;
    private int _loadVersion;

    public CMURoundStatisticsEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override void Opened()
    {
        base.Opened();

        LoadFromDb();
    }

    public override EuiStateBase GetNewState()
    {
        return new CMURoundStatisticsEuiState(_dashboard, _period);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is CMURoundStatisticsRefreshMsg)
            LoadFromDb();
        else if (msg is CMURoundStatisticsPeriodMsg period &&
                 Enum.IsDefined(period.Period) && period.Period != _period)
        {
            _period = period.Period;
            LoadFromDb();
        }
    }

    private async void LoadFromDb()
    {
        var version = ++_loadVersion;
        var period = _period;
        var now = DateTime.UtcNow;
        var from = period switch
        {
            CMURoundStatisticsPeriod.OneWeek => now.AddDays(-7),
            CMURoundStatisticsPeriod.TwoWeeks => now.AddDays(-14),
            CMURoundStatisticsPeriod.OneMonth => now.AddMonths(-1),
            CMURoundStatisticsPeriod.ThreeMonths => now.AddMonths(-3),
            CMURoundStatisticsPeriod.SixMonths => now.AddMonths(-6),
            _ => now.AddYears(-1),
        };
        try
        {
            var dashboard = await _db.GetCMURoundStatisticsDashboard(RecentRounds, from);
            _task.RunOnMainThread(() =>
            {
                if (version != _loadVersion)
                    return;
                _dashboard = dashboard;
                StateDirty();
            });
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to load CMU round statistics dashboard:\n{e}");
        }
    }

    private static CMURoundStatisticsDashboard EmptyDashboard()
    {
        return new CMURoundStatisticsDashboard(
            new List<CMURoundModeStatistics>
            {
                new(
                    CMURoundStatisticsPreset.DistressSignal,
                    "Distress Signal",
                    "Xeno",
                    "Govfor",
                    0,
                    0,
                    0,
                    0,
                    new List<CMURoundOutcomeBreakdown>(),
                    new List<CMURoundManualReasonBreakdown>(),
                    new List<CMURoundThreatBreakdown>(),
                    EmptyRecentForm(),
                    EmptyStreak(),
                    EmptyStreak(),
                    EmptyDurations(),
                    new List<CMURoundPlanetBreakdown>(),
                    new List<CMURoundPlatoonMatchupBreakdown>(),
                    new List<CMURoundPlayerCountBandBreakdown>()),
                new(
                    CMURoundStatisticsPreset.Insurgency,
                    "Insurgency",
                    "Govfor",
                    "CLF",
                    0,
                    0,
                    0,
                    0,
                    new List<CMURoundOutcomeBreakdown>(),
                    new List<CMURoundManualReasonBreakdown>(),
                    new List<CMURoundThreatBreakdown>(),
                    EmptyRecentForm(),
                    EmptyStreak(),
                    EmptyStreak(),
                    EmptyDurations(),
                    new List<CMURoundPlanetBreakdown>(),
                    new List<CMURoundPlatoonMatchupBreakdown>(),
                    new List<CMURoundPlayerCountBandBreakdown>()),
                new(
                    CMURoundStatisticsPreset.ColonyFall,
                    "Colony Fall",
                    "Colonists",
                    "Threat",
                    0,
                    0,
                    0,
                    0,
                    new List<CMURoundOutcomeBreakdown>(),
                    new List<CMURoundManualReasonBreakdown>(),
                    new List<CMURoundThreatBreakdown>(),
                    EmptyRecentForm(),
                    EmptyStreak(),
                    EmptyStreak(),
                    EmptyDurations(),
                    new List<CMURoundPlanetBreakdown>(),
                    new List<CMURoundPlatoonMatchupBreakdown>(),
                    new List<CMURoundPlayerCountBandBreakdown>()),
            },
            new List<CMURoundOutcomeRecord>());
    }

    private static CMURoundRecentForm EmptyRecentForm()
    {
        return new CMURoundRecentForm(0, 0, 0, 0, 0, new List<CMURoundStatisticsWinner>());
    }

    private static CMURoundStreak EmptyStreak()
    {
        return new CMURoundStreak(CMURoundStatisticsWinner.Unknown, 0);
    }

    private static CMURoundDurationBreakdown EmptyDurations()
    {
        return new CMURoundDurationBreakdown(0, 0, 0, 0, 0);
    }
}
