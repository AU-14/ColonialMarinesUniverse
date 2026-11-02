using Content.Server._RMC14.Marines;
using Content.Server.CMU14.Round.Objectives;
using Content.Server.CMU14.RoundStatistics;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.CMU14.Ops.ForceOnForce;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.CMU14.RoundStatistics;
using Content.Shared.GameTicking.Components;

namespace Content.Server.CMU14.Ops.ForceOnForce;

/// <summary>
/// Force on Force referee: sudden death at the time limit lets the side behind on hold-ticks
/// steal the round by taking the zone from the leader; if the lead survives to the overtime
/// cap, the hold-tick leader wins and an exact tie is a draw. Announces the score on interval.
/// </summary>
public sealed partial class FoFTimeLimitRuleSystem : GameRuleSystem<FoFTimeLimitRuleComponent>
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly ObjectiveControlSystem _objCtrl = default!;
    [Dependency] private readonly CMURoundStatisticsSystem _stats = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var rule, out _))
        {
            var duration = _gameTicker.RoundDuration();

            // Start of overtime: record who held the zone at the 75:00 limit.
            if (duration >= rule.TimeLimit && string.IsNullOrEmpty(rule.ZoneLeaderAtLimit))
            {
                rule.ZoneLeaderAtLimit = GetZoneController();
            }

            // Announce overtime start once.
            if (duration >= rule.TimeLimit && !rule.SuddenDeathAnnounced)
            {
                rule.SuddenDeathAnnounced = true;
                Announce(Loc.GetString("fof-time-limit-sudden-death"));
            }

            if (duration >= rule.TimeLimit + rule.Overtime)
            {
                ResolveOvertime(rule);
                continue;
            }

            if (rule.NextAnnounce == TimeSpan.Zero)
                rule.NextAnnounce = duration + rule.ScoreAnnounceInterval;

            if (duration < rule.NextAnnounce)
                continue;

            rule.NextAnnounce += rule.ScoreAnnounceInterval;
            AnnounceScore();
        }
    }

    private void EndByScore(FoFTimeLimitRuleComponent rule)
    {
        var ticks = SumTicks();
        var govfor = ticks.GetValueOrDefault("govfor");
        var opfor = ticks.GetValueOrDefault("opfor");

        if (govfor == opfor)
        {
            _stats.RecordObjectiveVictory(null, CMURoundStatisticsOutcome.DrawAlmayerAutodestruct);
            _gameTicker.EndRound(Loc.GetString("fof-time-limit-draw",
                ("govfor", govfor),
                ("opfor", opfor)));
            return;
        }

        var winner = govfor > opfor ? "govfor" : "opfor";
        _stats.RecordObjectiveVictory(winner, CMURoundStatisticsOutcome.FoFMajorVictory);
        _objCtrl.DeclareObjectiveVictory(winner,
            Loc.GetString("fof-time-limit-victory",
                ("govfor", govfor),
                ("opfor", opfor)));
    }

    private void ResolveOvertime(FoFTimeLimitRuleComponent rule)
    {
        var controller = GetZoneController();
        var zoneLeader = rule.ZoneLeaderAtLimit;

        if (!string.IsNullOrEmpty(controller) && controller != zoneLeader)
        {
            _stats.RecordObjectiveVictory(controller, CMURoundStatisticsOutcome.FoFMinorVictory);
            _objCtrl.DeclareObjectiveVictory(controller,
                Loc.GetString("fof-time-limit-zone-victory",
                    ("faction", controller.ToUpperInvariant())));
            return;
        }

        EndByScore(rule);
    }

    private string GetZoneController()
    {
        var query = EntityQueryEnumerator<HotspotObjectiveComponent>();
        while (query.MoveNext(out _, out var hotspot))
        {
            var controller = hotspot.CurrentController;
            if (!string.IsNullOrEmpty(controller))
                return controller;
        }
        return string.Empty;
    }

    private void Announce(string message)
    {
        _marineAnnounce.AnnounceToMarines(message, faction: "govfor");
        _marineAnnounce.AnnounceToMarines(message, faction: "opfor");
    }

    private void AnnounceScore()
    {
        var ticks = SumTicks();
        var govfor = ticks.GetValueOrDefault("govfor");
        var opfor = ticks.GetValueOrDefault("opfor");
        var key = govfor == opfor
            ? "fof-time-limit-score-tied"
            : "fof-time-limit-score-leading";
        var message = Loc.GetString(key,
            ("govfor", govfor),
            ("opfor", opfor),
            ("faction", (govfor > opfor ? "GOVFOR" : "OPFOR")));

        Announce(message);
    }

    private Dictionary<string, int> SumTicks()
    {
        var totals = new Dictionary<string, int>();
        var query = EntityQueryEnumerator<HotspotObjectiveComponent>();
        while (query.MoveNext(out _, out var hotspot))
        {
            foreach (var (faction, count) in hotspot.TicksPerFaction)
                totals[faction] = totals.GetValueOrDefault(faction) + count;
        }

        return totals;
    }
}
