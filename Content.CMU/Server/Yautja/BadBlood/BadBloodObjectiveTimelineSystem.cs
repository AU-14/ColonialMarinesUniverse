using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// What a timeline entry records.
/// </summary>
public enum BadBloodTimelineKind : byte
{
    GatePart,
    GateMet,
    Signature,
    Uplink,
    StrikeStarted,
    StrikePaused,
    StrikeResumed,
    Rerouted,
    StrikeCancelled,
    StrikeCompleted,
    Hijack,
    TotemDesecrated,
    TotemReconsecrated,
    TotemRead,
    LodgeRevealed,
    LodgeDestroyed,
    RipeHoldStarted,
    RipeHoldBroken,
    ShipCalled,
    BracerDecrypted,
    StarChartRecovered,
    TrophyStripped,
    SiteSabotaged,
    RoundDecided,
}

/// <summary>
/// One objective transition. <paramref name="SinceRule"/> is measured from the Bad Blood rule start,
/// <paramref name="RoundTime"/> from roundstart (zero when the rule runs outside a round, as in tests).
/// </summary>
public readonly record struct BadBloodTimelineEntry(
    TimeSpan SinceRule,
    TimeSpan RoundTime,
    BadBloodTimelineKind Kind,
    string Detail,
    EntityUid? Site);

/// <summary>
/// Deaths near one tracked objective site.
/// </summary>
public readonly record struct BadBloodEngagement(string Name, int MarineDeaths, int BadBloodDeaths);

/// <summary>
/// STATE-504: the playtest instrumentation of the Bad Blood objective loop. A pure subscriber: it records every
/// objective broadcast into an in-memory timeline, counts deaths near uplink sites, Totems and the Lodge, and prints an
/// "Objective timeline" section at round end. No other system calls it; tests read it through the public API.
/// docs/GraphAudit-BadBlood-Objectives.md §5.6, docs/GraphAudit-BadBlood-ObjectivesPlan.md STATE-504.
/// <para>
/// Admin logs: the objective-chain stages, sabotage, bracer, star chart and trophy events are already logged by the
/// systems that raise them. This system only logs the kinds nobody else does (Totem state, Lodge reveals and loss,
/// the ripe hold, the ship call and the round decision).
/// </para>
/// </summary>
public sealed class BadBloodObjectiveTimelineSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // used before the rule entity carries the tunables component (events raised outside a rule)
    private const float DefaultEngagementRadius = 12f;
    private static readonly TimeSpan DefaultRevealDedupe = TimeSpan.FromSeconds(30);

    private readonly List<BadBloodTimelineEntry> _entries = new();
    private readonly Dictionary<EntityUid, BadBloodEngagement> _engagement = new();
    private readonly Dictionary<BadBloodRevealSource, TimeSpan> _lastReveal = new();

    private EntityUid? _ruleEntity;
    private TimeSpan _ruleStartedAt;

    // strike time summed over every strike of the round: the component's totals reset on each fire
    private TimeSpan _strikeRunBanked;
    private TimeSpan _strikePausedBanked;
    private bool _strikeFired;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<BadBloodRuleStartedEvent>(OnRuleStarted);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);

        SubscribeLocalEvent<BadBloodObjectiveStageEvent>(OnStage);
        SubscribeLocalEvent<BadBloodTotemDesecratedEvent>(OnTotemDesecrated);
        SubscribeLocalEvent<BadBloodTotemReconsecratedEvent>(OnTotemReconsecrated);
        SubscribeLocalEvent<BadBloodTotemReadEvent>(OnTotemRead);
        SubscribeLocalEvent<BadBloodLodgeRevealedEvent>(OnLodgeRevealed);
        SubscribeLocalEvent<BadBloodLodgeDestroyedEvent>(OnLodgeDestroyed);
        SubscribeLocalEvent<BadBloodRipeHoldStartedEvent>(OnRipeHoldStarted);
        SubscribeLocalEvent<BadBloodRipeHoldBrokenEvent>(OnRipeHoldBroken);
        SubscribeLocalEvent<BadBloodShipCalledEvent>(OnShipCalled);
        SubscribeLocalEvent<BadBloodBracerDecryptedEvent>(OnBracerDecrypted);
        SubscribeLocalEvent<BadBloodStarChartRecoveredEvent>(OnStarChartRecovered);
        SubscribeLocalEvent<BadBloodTrophyStrippedEvent>(OnTrophyStripped);
        SubscribeLocalEvent<BadBloodSiteSabotagedEvent>(OnSiteSabotaged);
        SubscribeLocalEvent<BadBloodRoundDecidedEvent>(OnRoundDecided);
    }

    #region Public read API

    /// <summary>
    /// Every recorded transition of this round, oldest first.
    /// </summary>
    public IReadOnlyList<BadBloodTimelineEntry> GetEntries()
    {
        return _entries;
    }

    /// <summary>
    /// Deaths counted near <paramref name="site"/> this round; zeros when none were.
    /// </summary>
    public BadBloodEngagement GetEngagement(EntityUid site)
    {
        return _engagement.TryGetValue(site, out var engagement) ? engagement : new BadBloodEngagement(string.Empty, 0, 0);
    }

    /// <summary>
    /// The share of Vessel Strike time spent paused this round, 0 to 1. Null when no strike was fired.
    /// </summary>
    public float? GetStrikePausedShare()
    {
        var (run, paused) = GetStrikeTotals();
        if (!_strikeFired || run <= TimeSpan.Zero)
            return null;

        return (float) (paused.TotalSeconds / run.TotalSeconds);
    }

    #endregion

    #region Reset

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        Reset();
    }

    private void OnRuleStarted(ref BadBloodRuleStartedEvent ev)
    {
        Reset();
        _ruleEntity = ev.Rule.Owner;
        _ruleStartedAt = _timing.CurTime;
        EnsureComp<BadBloodObjectiveTimelineComponent>(ev.Rule.Owner);
    }

    private void Reset()
    {
        _entries.Clear();
        _engagement.Clear();
        _lastReveal.Clear();
        _ruleEntity = null;
        _ruleStartedAt = TimeSpan.Zero;
        _strikeRunBanked = TimeSpan.Zero;
        _strikePausedBanked = TimeSpan.Zero;
        _strikeFired = false;
    }

    #endregion

    #region Recording

    private void Record(BadBloodTimelineKind kind, string detail, EntityUid? site = null, bool log = false)
    {
        var now = _timing.CurTime;
        var sinceRule = _ruleEntity != null ? now - _ruleStartedAt : TimeSpan.Zero;
        var roundTime = _gameTicker.RunLevel == GameRunLevel.InRound ? _gameTicker.RoundDuration() : TimeSpan.Zero;
        _entries.Add(new BadBloodTimelineEntry(sinceRule, roundTime, kind, detail, site));

        if (!log)
            return;

        if (site is { } uid && !TerminatingOrDeleted(uid))
        {
            _adminLog.Add(LogType.Action, LogImpact.Low,
                $"BadBlood obj: {kind} at {ToPrettyString(uid):site}: {detail}");
        }
        else
        {
            _adminLog.Add(LogType.Action, LogImpact.Low, $"BadBlood obj: {kind}: {detail}");
        }
    }

    private void OnStage(ref BadBloodObjectiveStageEvent ev)
    {
        var kind = ev.Stage switch
        {
            BadBloodObjectiveStage.GatePart => BadBloodTimelineKind.GatePart,
            BadBloodObjectiveStage.GateMet => BadBloodTimelineKind.GateMet,
            BadBloodObjectiveStage.Signature => BadBloodTimelineKind.Signature,
            BadBloodObjectiveStage.Uplink => BadBloodTimelineKind.Uplink,
            BadBloodObjectiveStage.StrikeStarted => BadBloodTimelineKind.StrikeStarted,
            BadBloodObjectiveStage.StrikePaused => BadBloodTimelineKind.StrikePaused,
            BadBloodObjectiveStage.StrikeResumed => BadBloodTimelineKind.StrikeResumed,
            BadBloodObjectiveStage.Rerouted => BadBloodTimelineKind.Rerouted,
            BadBloodObjectiveStage.StrikeCancelled => BadBloodTimelineKind.StrikeCancelled,
            BadBloodObjectiveStage.StrikeCompleted => BadBloodTimelineKind.StrikeCompleted,
            BadBloodObjectiveStage.Hijack => BadBloodTimelineKind.Hijack,
            _ => (BadBloodTimelineKind?) null,
        };

        if (kind is not { } recorded)
            return;

        switch (ev.Stage)
        {
            case BadBloodObjectiveStage.StrikeStarted:
                _strikeFired = true;
                break;
            // the component's totals still hold the strike that just ended; they reset on the next fire
            case BadBloodObjectiveStage.StrikeCancelled:
            case BadBloodObjectiveStage.StrikeCompleted:
                if (TryGetStrikeComp(out var strike))
                {
                    _strikeRunBanked += strike.RunTotal;
                    _strikePausedBanked += strike.PausedTotal;
                }

                break;
        }

        // the emitting systems already admin-log every stage
        Record(recorded, ev.Detail, ev.Site);
    }

    private void OnTotemDesecrated(ref BadBloodTotemDesecratedEvent ev)
    {
        Record(BadBloodTimelineKind.TotemDesecrated, Name(ev.Totem), ev.Totem, log: true);
    }

    private void OnTotemReconsecrated(ref BadBloodTotemReconsecratedEvent ev)
    {
        Record(BadBloodTimelineKind.TotemReconsecrated, Name(ev.Totem), ev.Totem, log: true);
    }

    private void OnTotemRead(ref BadBloodTotemReadEvent ev)
    {
        Record(BadBloodTimelineKind.TotemRead, $"read by {Name(ev.Reader)}", ev.Totem, log: true);
    }

    // every hit on the Lodge reveals it again: one entry per source per RevealDedupe
    private void OnLodgeRevealed(ref BadBloodLodgeRevealedEvent ev)
    {
        var now = _timing.CurTime;
        var dedupe = GetTunables(out var tunables) ? tunables.RevealDedupe : DefaultRevealDedupe;
        if (_lastReveal.TryGetValue(ev.Source, out var last) && now - last < dedupe)
            return;

        _lastReveal[ev.Source] = now;
        var duration = ev.Duration is { } time ? $"{(int) time.TotalSeconds}s" : "until cleared";
        Record(BadBloodTimelineKind.LodgeRevealed, $"{ev.Source}, {duration}", ev.Lodge, log: true);
    }

    private void OnLodgeDestroyed(ref BadBloodLodgeDestroyedEvent ev)
    {
        Record(BadBloodTimelineKind.LodgeDestroyed, Name(ev.Lodge), ev.Lodge, log: true);
    }

    private void OnRipeHoldStarted(ref BadBloodRipeHoldStartedEvent ev)
    {
        var callIn = ev.CallAt - _timing.CurTime;
        Record(BadBloodTimelineKind.RipeHoldStarted, $"ship call in {(int) callIn.TotalSeconds}s", log: true);
    }

    private void OnRipeHoldBroken(ref BadBloodRipeHoldBrokenEvent ev)
    {
        Record(BadBloodTimelineKind.RipeHoldBroken, string.Empty, log: true);
    }

    private void OnShipCalled(ref BadBloodShipCalledEvent ev)
    {
        var arrivesIn = ev.ArrivesAt - _timing.CurTime;
        Record(BadBloodTimelineKind.ShipCalled, $"arrives in {(int) arrivesIn.TotalSeconds}s", log: true);
    }

    private void OnBracerDecrypted(ref BadBloodBracerDecryptedEvent ev)
    {
        Record(BadBloodTimelineKind.BracerDecrypted, $"by {Name(ev.User)}", ev.Bracer);
    }

    private void OnStarChartRecovered(ref BadBloodStarChartRecoveredEvent ev)
    {
        Record(BadBloodTimelineKind.StarChartRecovered, $"by {Name(ev.User)}", ev.Chart);
    }

    private void OnTrophyStripped(ref BadBloodTrophyStrippedEvent ev)
    {
        Record(BadBloodTimelineKind.TrophyStripped, $"{Name(ev.Trophy)} by {Name(ev.User)}", ev.Lodge);
    }

    private void OnSiteSabotaged(ref BadBloodSiteSabotagedEvent ev)
    {
        Record(BadBloodTimelineKind.SiteSabotaged, $"{Name(ev.Site)} by {Name(ev.User)}", ev.Site);
    }

    private void OnRoundDecided(ref BadBloodRoundDecidedEvent ev)
    {
        Record(BadBloodTimelineKind.RoundDecided, ev.Outcome.ToString(), log: true);
    }

    #endregion

    #region Engagement counters

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead || _ruleEntity == null)
            return;

        var badBlood = HasComp<YautjaBadBloodComponent>(args.Target);
        if (!badBlood && !HasComp<MarineComponent>(args.Target))
            return;

        var where = _transform.GetMapCoordinates(args.Target);
        var radius = GetTunables(out var tunables) ? tunables.EngagementRadius : DefaultEngagementRadius;
        var radiusSquared = radius * radius;

        var sites = EntityQueryEnumerator<BadBloodUplinkSiteComponent, TransformComponent>();
        while (sites.MoveNext(out var uid, out _, out var xform))
        {
            CountIfNear(uid, xform, where, radiusSquared, badBlood);
        }

        var totems = EntityQueryEnumerator<BadBloodTotemComponent, TransformComponent>();
        while (totems.MoveNext(out var uid, out var totem, out var xform))
        {
            if (totem.Destroyed)
                continue;

            CountIfNear(uid, xform, where, radiusSquared, badBlood);
        }

        if (_rule.GetLodge() is { } lodge)
            CountIfNear(lodge, Transform(lodge), where, radiusSquared, badBlood);
    }

    private void CountIfNear(EntityUid site, TransformComponent xform, MapCoordinates where, float radiusSquared,
        bool badBlood)
    {
        var at = _transform.GetMapCoordinates(site, xform);
        if (at.MapId != where.MapId || (at.Position - where.Position).LengthSquared() > radiusSquared)
            return;

        var old = _engagement.TryGetValue(site, out var existing) ? existing : new BadBloodEngagement(Name(site), 0, 0);
        _engagement[site] = badBlood
            ? old with { BadBloodDeaths = old.BadBloodDeaths + 1 }
            : old with { MarineDeaths = old.MarineDeaths + 1 };
    }

    #endregion

    #region Round end

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        // only rounds where a Bad Blood rule ran
        if (_ruleEntity == null)
            return;

        ev.AddLine(string.Empty);
        ev.AddLine(Loc.GetString("cmu-bad-blood-timeline-header"));

        if (_entries.Count == 0)
            ev.AddLine(Loc.GetString("cmu-bad-blood-timeline-empty"));

        foreach (var entry in _entries)
        {
            // the round time when the rule ran in a round, else the time since the rule started
            var time = entry.RoundTime > TimeSpan.Zero ? entry.RoundTime : entry.SinceRule;
            var kind = Loc.GetString($"cmu-bad-blood-timeline-kind-{entry.Kind.ToString().ToLowerInvariant()}");
            ev.AddLine(entry.Detail.Length == 0
                ? Loc.GetString("cmu-bad-blood-timeline-entry-bare", ("time", FormatTime(time)), ("kind", kind))
                : Loc.GetString("cmu-bad-blood-timeline-entry",
                    ("time", FormatTime(time)),
                    ("kind", kind),
                    ("detail", FormattedMessage.EscapeText(entry.Detail))));
        }

        if (GetStrikePausedShare() is { } share)
        {
            var (run, paused) = GetStrikeTotals();
            ev.AddLine(Loc.GetString("cmu-bad-blood-timeline-paused",
                ("percent", (int) Math.Round(share * 100)),
                ("paused", (int) paused.TotalSeconds),
                ("run", (int) run.TotalSeconds)));
        }
        else
        {
            ev.AddLine(Loc.GetString("cmu-bad-blood-timeline-no-strike"));
        }

        ev.AddLine(Loc.GetString("cmu-bad-blood-timeline-counts",
            ("sabotages", Count(BadBloodTimelineKind.SiteSabotaged)),
            ("reroutes", Count(BadBloodTimelineKind.Rerouted)),
            ("cancels", Count(BadBloodTimelineKind.StrikeCancelled)),
            ("hijacks", Count(BadBloodTimelineKind.Hijack)),
            ("desecrations", Count(BadBloodTimelineKind.TotemDesecrated))));

        if (_engagement.Count == 0)
        {
            ev.AddLine(Loc.GetString("cmu-bad-blood-timeline-engagement-none"));
            return;
        }

        ev.AddLine(Loc.GetString("cmu-bad-blood-timeline-engagement-header"));
        foreach (var engagement in _engagement.Values)
        {
            ev.AddLine(Loc.GetString("cmu-bad-blood-timeline-engagement",
                ("site", FormattedMessage.EscapeText(engagement.Name)),
                ("marines", engagement.MarineDeaths),
                ("badBloods", engagement.BadBloodDeaths)));
        }
    }

    private int Count(BadBloodTimelineKind kind)
    {
        var count = 0;
        foreach (var entry in _entries)
        {
            if (entry.Kind == kind)
                count++;
        }

        return count;
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int) time.TotalMinutes:00}:{time.Seconds:00}";
    }

    #endregion

    // the banked totals of ended strikes plus the one still running
    private (TimeSpan Run, TimeSpan Paused) GetStrikeTotals()
    {
        var run = _strikeRunBanked;
        var paused = _strikePausedBanked;
        if (TryGetStrikeComp(out var strike) && strike.Running)
        {
            run += strike.RunTotal;
            paused += strike.PausedTotal;
        }

        return (run, paused);
    }

    private bool TryGetStrikeComp([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BadBloodVesselStrikeComponent? strike)
    {
        strike = null;
        return _ruleEntity is { } rule && TryComp(rule, out strike);
    }

    private bool GetTunables([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BadBloodObjectiveTimelineComponent? tunables)
    {
        tunables = null;
        return _ruleEntity is { } rule && TryComp(rule, out tunables);
    }
}
