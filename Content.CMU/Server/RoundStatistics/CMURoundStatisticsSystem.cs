using System;
using Content.Server.AU14.Round;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared._CMU14.RoundStatistics;
using Content.Shared._RMC14.Rules;
using Content.Shared.GameTicking;
using Robust.Shared.Log;

namespace Content.Server._CMU14.RoundStatistics;

public sealed partial class CMURoundStatisticsSystem : EntitySystem
{
    private const string DistressSignalPreset = "DistressSignal";
    private const string InsurgencyPreset = "Insurgency";
    private const string ColonyFallPreset = "ColonyFall";

    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("cmu.round_statistics");

    private PendingRoundOutcome? _pendingOutcome;
    private int? _recordedRoundId;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEndMessage);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public void RecordKillAllGovforRule()
    {
        if (GetCurrentPreset() != CMURoundStatisticsPreset.Insurgency)
            return;

        TrySetPendingOutcome(
            CMURoundStatisticsWinner.Clf,
            CMURoundStatisticsOutcome.InsurgencyClfVictory,
            "KillAllGovforRule");
    }

    public void RecordKillAllClfRule()
    {
        if (GetCurrentPreset() != CMURoundStatisticsPreset.Insurgency)
            return;

        TrySetPendingOutcome(
            CMURoundStatisticsWinner.Govfor,
            CMURoundStatisticsOutcome.InsurgencyGovforVictory,
            "KillAllClfRule");
    }

    public void RecordKillAllColonistRule()
    {
        if (GetCurrentPreset() != CMURoundStatisticsPreset.ColonyFall)
            return;

        TrySetPendingOutcome(
            CMURoundStatisticsWinner.Threat,
            CMURoundStatisticsOutcome.ColonyFallThreatVictory,
            "KillAllColonistRule");
    }

    public void RecordKillAllHumanRule()
    {
        if (GetCurrentPreset() != CMURoundStatisticsPreset.ColonyFall)
            return;

        TrySetPendingOutcome(
            CMURoundStatisticsWinner.Threat,
            CMURoundStatisticsOutcome.ColonyFallThreatVictory,
            "KillAllHumanRule");
    }

    public void RecordThreatSurviveRule()
    {
        if (GetCurrentPreset() != CMURoundStatisticsPreset.ColonyFall)
            return;

        TrySetPendingOutcome(
            CMURoundStatisticsWinner.Threat,
            CMURoundStatisticsOutcome.ColonyFallThreatVictory,
            "ThreatSurviveRule");
    }

    public void RecordThreatDefeatedRule(string source)
    {
        if (GetCurrentPreset() != CMURoundStatisticsPreset.ColonyFall)
            return;

        TrySetPendingOutcome(
            CMURoundStatisticsWinner.Colonists,
            CMURoundStatisticsOutcome.ColonyFallSurvivorVictory,
            source);
    }

    private void TrySetPendingOutcome(
        CMURoundStatisticsWinner winner,
        CMURoundStatisticsOutcome outcome,
        string source)
    {
        _pendingOutcome ??= new PendingRoundOutcome(winner, outcome, source);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _pendingOutcome = null;
        _recordedRoundId = null;
    }

    private async void OnRoundEndMessage(RoundEndMessageEvent ev)
    {
        if (_recordedRoundId == ev.RoundId)
            return;

        var preset = GetCurrentPreset();
        if (preset == null)
            return;

        var outcome = preset == CMURoundStatisticsPreset.DistressSignal
            ? GetDistressOutcome()
            : _pendingOutcome;

        outcome ??= new PendingRoundOutcome(
            CMURoundStatisticsWinner.Unknown,
            CMURoundStatisticsOutcome.Unknown,
            "RoundEndMessageEvent");

        var record = new CMURoundOutcomeRecord(
            ev.RoundId,
            preset.Value,
            outcome.Value.Winner,
            outcome.Value.Outcome,
            outcome.Value.Source,
            _auRound.SelectedThreat?.ID,
            _auRound.GetSelectedPlanetId(),
            _platoons.SelectedGovforPlatoon?.ID,
            _platoons.SelectedOpforPlatoon?.ID,
            ev.PlayerCount,
            (int) ev.RoundDuration.TotalSeconds,
            DateTime.UtcNow);

        _recordedRoundId = ev.RoundId;

        try
        {
            await _db.UpsertCMURoundOutcome(record);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to save CMU round outcome for round {ev.RoundId}:\n{e}");
        }
    }

    private PendingRoundOutcome? GetDistressOutcome()
    {
        var query = EntityQueryEnumerator<CMDistressSignalRuleComponent>();
        while (query.MoveNext(out var distress))
        {
            if (distress.Result is not { } result ||
                result == DistressSignalRuleResult.None)
            {
                continue;
            }

            return result switch
            {
                DistressSignalRuleResult.MajorXenoVictory => new PendingRoundOutcome(
                    CMURoundStatisticsWinner.Xeno,
                    CMURoundStatisticsOutcome.XenoMajorHijackWin,
                    nameof(DistressSignalRuleResult.MajorXenoVictory)),
                DistressSignalRuleResult.MinorXenoVictory => new PendingRoundOutcome(
                    CMURoundStatisticsWinner.Xeno,
                    CMURoundStatisticsOutcome.XenoMinorHijackLoss,
                    nameof(DistressSignalRuleResult.MinorXenoVictory)),
                DistressSignalRuleResult.MinorMarineVictory => new PendingRoundOutcome(
                    CMURoundStatisticsWinner.Govfor,
                    CMURoundStatisticsOutcome.MarineMinorHiveCollapse,
                    nameof(DistressSignalRuleResult.MinorMarineVictory)),
                DistressSignalRuleResult.MajorMarineVictory => new PendingRoundOutcome(
                    CMURoundStatisticsWinner.Govfor,
                    CMURoundStatisticsOutcome.MarineMajorXenoWipe,
                    nameof(DistressSignalRuleResult.MajorMarineVictory)),
                DistressSignalRuleResult.AllDied => new PendingRoundOutcome(
                    CMURoundStatisticsWinner.Draw,
                    CMURoundStatisticsOutcome.DrawAlmayerAutodestruct,
                    nameof(DistressSignalRuleResult.AllDied)),
                _ => null,
            };
        }

        return null;
    }

    private CMURoundStatisticsPreset? GetCurrentPreset()
    {
        var presetId = _gameTicker.CurrentPreset?.ID ??
                       _gameTicker.Preset?.ID ??
                       _auRound.SelectedPreset?.ID;

        return presetId switch
        {
            DistressSignalPreset => CMURoundStatisticsPreset.DistressSignal,
            InsurgencyPreset => CMURoundStatisticsPreset.Insurgency,
            ColonyFallPreset => CMURoundStatisticsPreset.ColonyFall,
            _ => null,
        };
    }

    private readonly record struct PendingRoundOutcome(
        CMURoundStatisticsWinner Winner,
        CMURoundStatisticsOutcome Outcome,
        string Source);
}
