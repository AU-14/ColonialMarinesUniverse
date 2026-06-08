using System.Collections.Generic;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Bones.Events;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.Shrapnel;
using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared.Body.Part;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Telemetry;

public sealed partial class CMUMedicalTelemetrySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _log = default!;

    private ISawmill _sawmill = default!;

    private readonly Dictionary<BodyPartType, int> _hitCounts = new();
    private readonly Dictionary<FractureSeverity, int> _fractureCounts = new();
    private readonly Dictionary<EntityUid, int> _surgeriesPerMarine = new();
    private readonly Dictionary<EntityUid, int> _organStageTransitions = new();
    private readonly Dictionary<EntityUid, int> _painShockEntries = new();
    private int _defibAttempts;
    private int _defibCancels;
    private int _severedLimbs;
    private int _internalBleedsStarted;
    private int _internalBleedsStopped;
    private int _shrapnelEmbedded;
    private int _shrapnelExtracted;
    private int _limbsReattached;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("cmu.medical.telemetry");

        SubscribeLocalEvent<HitLocationComponent, HitLocationResolvedEvent>(OnHitResolved);
        SubscribeLocalEvent<Content.Shared._CMU14.Medical.BodyPart.BodyPartHealthComponent, BoneFracturedEvent>(OnFractureSpawn);
        SubscribeLocalEvent<OrganStageChangedEvent>(OnOrganStage);
        SubscribeLocalEvent<CMSurgeryCompleteEvent>(OnSurgeryDone);
        SubscribeLocalEvent<RMCDefibrillatorAttemptEvent>(OnDefibAttempt);
        SubscribeLocalEvent<CMUPainShockStatusComponent, ComponentStartup>(OnPainShockEntered);
        SubscribeLocalEvent<BodyPartComponent, BodyPartSeveredEvent>(OnBodyPartSevered);
        SubscribeLocalEvent<InternalBleedingChangedEvent>(OnInternalBleedingChanged);
        SubscribeLocalEvent<CMUShrapnelChangedEvent>(OnShrapnelChanged);
        SubscribeLocalEvent<RoundEndSummaryStatsEvent>(OnRoundEndStats);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundEnd);
    }

    private void OnHitResolved(Entity<HitLocationComponent> ent, ref HitLocationResolvedEvent args)
    {
        _hitCounts.TryGetValue(args.ResolvedPart, out var prior);
        _hitCounts[args.ResolvedPart] = prior + 1;
    }

    private void OnFractureSpawn(Entity<Content.Shared._CMU14.Medical.BodyPart.BodyPartHealthComponent> ent, ref BoneFracturedEvent args)
    {
        if (args.Old == args.New)
            return;
        _fractureCounts.TryGetValue(args.New, out var prior);
        _fractureCounts[args.New] = prior + 1;
    }

    private void OnOrganStage(ref OrganStageChangedEvent args)
    {
        _organStageTransitions.TryGetValue(args.Body, out var prior);
        _organStageTransitions[args.Body] = prior + 1;
    }

    private void OnSurgeryDone(ref CMSurgeryCompleteEvent args)
    {
        _surgeriesPerMarine.TryGetValue(args.Patient, out var prior);
        _surgeriesPerMarine[args.Patient] = prior + 1;

        if (SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(args.Surgery.Id))
            _limbsReattached++;
    }

    private void OnDefibAttempt(RMCDefibrillatorAttemptEvent ev)
    {
        _defibAttempts++;
        if (ev.Cancelled)
            _defibCancels++;
    }

    private void OnPainShockEntered(Entity<CMUPainShockStatusComponent> ent, ref ComponentStartup args)
    {
        _painShockEntries.TryGetValue(ent.Owner, out var prior);
        _painShockEntries[ent.Owner] = prior + 1;
    }

    private void OnBodyPartSevered(Entity<BodyPartComponent> ent, ref BodyPartSeveredEvent args)
    {
        if (args.Type is BodyPartType.Arm or BodyPartType.Leg)
            _severedLimbs++;
    }

    private void OnInternalBleedingChanged(ref InternalBleedingChangedEvent args)
    {
        if (args.Removed)
            _internalBleedsStopped++;
        else
            _internalBleedsStarted++;
    }

    private void OnShrapnelChanged(ref CMUShrapnelChangedEvent args)
    {
        if (args.Removed)
            _shrapnelExtracted++;
        else
            _shrapnelEmbedded++;
    }

    private void OnRoundEndStats(RoundEndSummaryStatsEvent ev)
    {
        var fractureTotal = SumValues(_fractureCounts);
        var surgeryTotal = SumValues(_surgeriesPerMarine);
        var organTotal = SumValues(_organStageTransitions);
        var painShockTotal = SumValues(_painShockEntries);

        ev.AddInjuryStat(
            "round-end-summary-window-stat-bones-broken",
            "round-end-summary-window-stat-bones-broken-detail",
            fractureTotal,
            RoundEndSummaryStatColor.Red);
        ev.AddInjuryStat(
            "round-end-summary-window-stat-surgeries",
            "round-end-summary-window-stat-surgeries-detail",
            surgeryTotal,
            RoundEndSummaryStatColor.Cyan);
        ev.AddInjuryStat(
            "round-end-summary-window-stat-pain-shock",
            "round-end-summary-window-stat-pain-shock-detail",
            painShockTotal,
            RoundEndSummaryStatColor.Gold);
        ev.AddInjuryStat(
            "round-end-summary-window-stat-organ-crises",
            "round-end-summary-window-stat-organ-crises-detail",
            organTotal,
            RoundEndSummaryStatColor.Purple);
        ev.AddInjuryStat(
            "round-end-summary-window-stat-defibs",
            "round-end-summary-window-stat-defibs-detail",
            _defibAttempts,
            RoundEndSummaryStatColor.Green);

        ev.AddOddityStat(
            "round-end-summary-window-stat-limbs-stolen",
            "round-end-summary-window-stat-limbs-stolen-detail",
            _severedLimbs,
            RoundEndSummaryStatColor.Purple);
        ev.AddOddityStat(
            "round-end-summary-window-stat-bleeds-started",
            "round-end-summary-window-stat-bleeds-started-detail",
            _internalBleedsStarted,
            RoundEndSummaryStatColor.Red);
        ev.AddOddityStat(
            "round-end-summary-window-stat-limbs-reattached",
            "round-end-summary-window-stat-limbs-reattached-detail",
            _limbsReattached,
            RoundEndSummaryStatColor.Green);
        ev.AddOddityStat(
            "round-end-summary-window-stat-shrapnel-extracted",
            "round-end-summary-window-stat-shrapnel-extracted-detail",
            _shrapnelExtracted,
            RoundEndSummaryStatColor.Gold);
        ev.AddOddityStat(
            "round-end-summary-window-stat-shrapnel-embedded",
            "round-end-summary-window-stat-shrapnel-embedded-detail",
            _shrapnelEmbedded,
            RoundEndSummaryStatColor.Cyan);
        ev.AddOddityStat(
            "round-end-summary-window-stat-bleeds-stopped",
            "round-end-summary-window-stat-bleeds-stopped-detail",
            _internalBleedsStopped,
            RoundEndSummaryStatColor.Blue);
    }

    private void OnRoundEnd(RoundRestartCleanupEvent ev)
    {
        EmitRoundSummary();
        _hitCounts.Clear();
        _fractureCounts.Clear();
        _surgeriesPerMarine.Clear();
        _organStageTransitions.Clear();
        _painShockEntries.Clear();
        _defibAttempts = 0;
        _defibCancels = 0;
        _severedLimbs = 0;
        _internalBleedsStarted = 0;
        _internalBleedsStopped = 0;
        _shrapnelEmbedded = 0;
        _shrapnelExtracted = 0;
        _limbsReattached = 0;
    }

    private void EmitRoundSummary()
    {
        _sawmill.Info("=== CMU medical round summary ===");

        var hitTotal = SumValues(_hitCounts);
        if (hitTotal == 0)
        {
            _sawmill.Info("hits: none recorded this round");
        }
        else
        {
            foreach (var (zone, count) in _hitCounts)
            {
                var pct = 100f * count / hitTotal;
                _sawmill.Info($"hits zone={zone} count={count} pct={pct:F1}%");
            }
        }

        var fractureTotal = SumValues(_fractureCounts);
        _sawmill.Info($"fractures total={fractureTotal}");
        foreach (var (severity, count) in _fractureCounts)
            _sawmill.Info($"fractures severity={severity} count={count}");

        var organTotal = SumValues(_organStageTransitions);
        _sawmill.Info($"organStageTransitions total={organTotal} marinesAffected={_organStageTransitions.Count}");

        var surgeryTotal = SumValues(_surgeriesPerMarine);
        _sawmill.Info($"surgeries total={surgeryTotal} marinesOperated={_surgeriesPerMarine.Count}");

        _sawmill.Info($"defib attempts={_defibAttempts} cancels={_defibCancels} (CMU layer rejections only)");
        _sawmill.Info($"painShockEntries total={SumValues(_painShockEntries)} marinesAffected={_painShockEntries.Count}");
        _sawmill.Info($"severedLimbs total={_severedLimbs}");
        _sawmill.Info($"internalBleeds started={_internalBleedsStarted} stopped={_internalBleedsStopped}");
        _sawmill.Info($"shrapnel embedded={_shrapnelEmbedded} extracted={_shrapnelExtracted}");
        _sawmill.Info($"limbsReattached total={_limbsReattached}");
        _sawmill.Info("=== end CMU medical round summary ===");
    }

    private static int SumValues<T>(Dictionary<T, int> counts)
        where T : notnull
    {
        var total = 0;
        foreach (var (_, count) in counts)
            total += count;

        return total;
    }
}
