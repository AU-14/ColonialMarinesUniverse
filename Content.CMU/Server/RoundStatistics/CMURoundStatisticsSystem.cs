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
        switch (GetCurrentPreset())
        {
            case CMURoundStatisticsPreset.Insurgency:
                TrySetPendingOutcome(
                    CMURoundStatisticsWinner.Clf,
                    CMURoundStatisticsOutcome.InsurgencyClfVictory,
                    "KillAllGovforRule");
                break;
            case CMURoundStatisticsPreset.DistressSignal:
                TrySetPendingOutcome(
                    CMURoundStatisticsWinner.Xeno,
                    CMURoundStatisticsOutcome.XenoMajorHijackWin,
                    "KillAllGovforRule");
                break;
        }
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
        switch (GetCurrentPreset())
        {
            case CMURoundStatisticsPreset.ColonyFall:
                TrySetPendingOutcome(
                    CMURoundStatisticsWinner.Threat,
                    CMURoundStatisticsOutcome.ColonyFallThreatVictory,
                    "ThreatSurviveRule");
                break;
            case CMURoundStatisticsPreset.DistressSignal:
                TrySetPendingOutcome(
                    CMURoundStatisticsWinner.Xeno,
                    CMURoundStatisticsOutcome.XenoMajorHijackWin,
                    "ThreatSurviveRule");
                break;
        }
    }

    public void RecordThreatDefeatedRule(string source)
    {
        switch (GetCurrentPreset())
        {
            case CMURoundStatisticsPreset.ColonyFall:
                TrySetPendingOutcome(
                    CMURoundStatisticsWinner.Colonists,
                    CMURoundStatisticsOutcome.ColonyFallSurvivorVictory,
                    source);
                break;
            case CMURoundStatisticsPreset.DistressSignal:
                TrySetPendingOutcome(GetDistressThreatDefeatedOutcome(source));
                break;
        }
    }

    public void RecordWithdrawal(string? faction, bool isStalemate)
    {
        switch (GetCurrentPreset())
        {
            case CMURoundStatisticsPreset.Insurgency:
                TrySetPendingOutcome(GetInsurgencyWithdrawalOutcome(faction, isStalemate));
                break;
            case CMURoundStatisticsPreset.ColonyFall:
                TrySetPendingOutcome(GetColonyFallWithdrawalOutcome(faction, isStalemate));
                break;
        }
    }

    public void RecordObjectiveVictory(string? faction)
    {
        switch (GetCurrentPreset())
        {
            case CMURoundStatisticsPreset.DistressSignal:
                TrySetPendingOutcome(GetDistressObjectiveOutcome(faction));
                break;
            case CMURoundStatisticsPreset.Insurgency:
                TrySetPendingOutcome(GetInsurgencyObjectiveOutcome(faction));
                break;
            case CMURoundStatisticsPreset.ColonyFall:
                TrySetPendingOutcome(GetColonyFallObjectiveOutcome(faction));
                break;
        }
    }

    private void TrySetPendingOutcome(PendingRoundOutcome outcome)
    {
        _pendingOutcome ??= outcome;
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
            ? GetDistressOutcome() ?? _pendingOutcome
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

    private PendingRoundOutcome GetDistressThreatDefeatedOutcome(string source)
    {
        var query = EntityQueryEnumerator<CMDistressSignalRuleComponent>();
        while (query.MoveNext(out var distress))
        {
            if (!distress.Hijack)
                continue;

            return new PendingRoundOutcome(
                CMURoundStatisticsWinner.Xeno,
                CMURoundStatisticsOutcome.XenoMinorHijackLoss,
                source);
        }

        return new PendingRoundOutcome(
            CMURoundStatisticsWinner.Govfor,
            CMURoundStatisticsOutcome.MarineMajorXenoWipe,
            source);
    }

    private PendingRoundOutcome GetInsurgencyWithdrawalOutcome(string? faction, bool isStalemate)
    {
        var source = GetWithdrawalSource(faction, isStalemate);
        if (isStalemate)
        {
            return new PendingRoundOutcome(
                CMURoundStatisticsWinner.Draw,
                CMURoundStatisticsOutcome.Stalemate,
                source);
        }

        if (IsGovforFaction(faction))
        {
            return new PendingRoundOutcome(
                CMURoundStatisticsWinner.Clf,
                CMURoundStatisticsOutcome.InsurgencyClfVictory,
                source);
        }

        if (IsOpforFaction(faction))
        {
            return new PendingRoundOutcome(
                CMURoundStatisticsWinner.Govfor,
                CMURoundStatisticsOutcome.InsurgencyGovforVictory,
                source);
        }

        return new PendingRoundOutcome(
            CMURoundStatisticsWinner.Unknown,
            CMURoundStatisticsOutcome.Unknown,
            source);
    }

    private PendingRoundOutcome GetColonyFallWithdrawalOutcome(string? faction, bool isStalemate)
    {
        var source = GetWithdrawalSource(faction, isStalemate);
        if (isStalemate)
        {
            return new PendingRoundOutcome(
                CMURoundStatisticsWinner.Draw,
                CMURoundStatisticsOutcome.Stalemate,
                source);
        }

        if (IsColonyFaction(faction))
        {
            return new PendingRoundOutcome(
                CMURoundStatisticsWinner.Colonists,
                CMURoundStatisticsOutcome.ColonyFallSurvivorVictory,
                source);
        }

        return new PendingRoundOutcome(
            CMURoundStatisticsWinner.Unknown,
            CMURoundStatisticsOutcome.Unknown,
            source);
    }

    private PendingRoundOutcome GetInsurgencyObjectiveOutcome(string? faction)
    {
        var source = GetObjectiveSource(faction);
        if (IsGovforFaction(faction))
        {
            return new PendingRoundOutcome(
                CMURoundStatisticsWinner.Govfor,
                CMURoundStatisticsOutcome.ObjectiveVictory,
                source);
        }

        if (IsOpforFaction(faction))
        {
            return new PendingRoundOutcome(
                CMURoundStatisticsWinner.Clf,
                CMURoundStatisticsOutcome.ObjectiveVictory,
                source);
        }

        return new PendingRoundOutcome(
            CMURoundStatisticsWinner.Unknown,
            CMURoundStatisticsOutcome.ObjectiveVictory,
            source);
    }

    private PendingRoundOutcome GetColonyFallObjectiveOutcome(string? faction)
    {
        var source = GetObjectiveSource(faction);
        if (IsColonyFaction(faction) || IsGovforFaction(faction))
        {
            return new PendingRoundOutcome(
                CMURoundStatisticsWinner.Colonists,
                CMURoundStatisticsOutcome.ObjectiveVictory,
                source);
        }

        if (IsThreatFaction(faction) || IsOpforFaction(faction))
        {
            return new PendingRoundOutcome(
                CMURoundStatisticsWinner.Threat,
                CMURoundStatisticsOutcome.ObjectiveVictory,
                source);
        }

        return new PendingRoundOutcome(
            CMURoundStatisticsWinner.Unknown,
            CMURoundStatisticsOutcome.ObjectiveVictory,
            source);
    }

    private PendingRoundOutcome GetDistressObjectiveOutcome(string? faction)
    {
        var source = GetObjectiveSource(faction);
        if (IsGovforFaction(faction) || IsColonyFaction(faction))
        {
            return new PendingRoundOutcome(
                CMURoundStatisticsWinner.Govfor,
                CMURoundStatisticsOutcome.ObjectiveVictory,
                source);
        }

        if (IsThreatFaction(faction))
        {
            return new PendingRoundOutcome(
                CMURoundStatisticsWinner.Xeno,
                CMURoundStatisticsOutcome.ObjectiveVictory,
                source);
        }

        return new PendingRoundOutcome(
            CMURoundStatisticsWinner.Unknown,
            CMURoundStatisticsOutcome.ObjectiveVictory,
            source);
    }

    private static string GetObjectiveSource(string? faction)
    {
        return $"AuObjective:{faction ?? "unknown"}";
    }

    private static string GetWithdrawalSource(string? faction, bool isStalemate)
    {
        if (isStalemate)
            return "WithdrawConsoleStalemate";

        return $"WithdrawConsole:{faction ?? "unknown"}";
    }

    private static bool IsGovforFaction(string? faction)
    {
        return string.Equals(faction, "govfor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOpforFaction(string? faction)
    {
        return string.Equals(faction, "opfor", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(faction, "clf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsColonyFaction(string? faction)
    {
        return string.Equals(faction, "colony", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(faction, "colonist", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsThreatFaction(string? faction)
    {
        return string.Equals(faction, "threat", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(faction, "xeno", StringComparison.OrdinalIgnoreCase);
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
