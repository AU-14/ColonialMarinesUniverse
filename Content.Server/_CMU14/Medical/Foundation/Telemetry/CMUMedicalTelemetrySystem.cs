using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Targeting;
using Content.Shared._CMU14.Medical.Targeting.Events;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Surgery;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Damage.Shrapnel;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared._CMU14.Medical.Human.Damage.Events;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared.Body.Part;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Timing;
using HumanOrganSlot = Content.Shared._CMU14.Medical.Human.Data.OrganSlot;

namespace Content.Server._CMU14.Medical.Foundation.Telemetry;

public sealed partial class CMUMedicalTelemetrySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _log = default!;

    private ISawmill _sawmill = default!;

    private readonly Dictionary<BodyPartType, int> _hitCounts = new();
    private readonly Dictionary<BodyRegion, int> _fractureCounts = new();
    private readonly Dictionary<EntityUid, int> _surgeriesPerMarine = new();
    private readonly Dictionary<EntityUid, int> _organStageTransitions = new();
    private readonly Dictionary<EntityUid, int> _painShockEntries = new();
    private readonly Dictionary<(EntityUid Body, HumanOrganSlot Slot), OrganDamageStatus> _lastOrganStatus = new();
    private readonly Dictionary<EntityUid, bool> _internalBleedingActive = new();
    private readonly HashSet<(EntityUid Body, BodyRegion Region)> _brokenRegions = new();
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
        SubscribeLocalEvent<HumanMedicalLedgerChangedEvent>(OnHumanLedgerChanged);
        SubscribeLocalEvent<HumanMedicalComponent, HumanSurgeryAppliedEvent>(OnSurgeryDone);
        SubscribeLocalEvent<RMCDefibrillatorAttemptEvent>(OnDefibAttempt);
        SubscribeLocalEvent<CMUPainShockStatusComponent, ComponentStartup>(OnPainShockEntered);
        SubscribeLocalEvent<BodyPartSeveranceAppliedEvent>(OnBodyPartSevered);
        SubscribeLocalEvent<HumanMedicalComponent, CMUShrapnelChangedEvent>(OnShrapnelChanged);
        SubscribeLocalEvent<RoundEndSummaryStatsEvent>(OnRoundEndStats);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundEnd);
    }

    private void OnHitResolved(Entity<HitLocationComponent> ent, ref HitLocationResolvedEvent args)
    {
        _hitCounts.TryGetValue(args.ResolvedPart, out var prior);
        _hitCounts[args.ResolvedPart] = prior + 1;
    }

    private void OnHumanLedgerChanged(ref HumanMedicalLedgerChangedEvent args)
    {
        if (!TryComp(args.Body, out HumanMedicalComponent? medical))
            return;

        var ent = (args.Body, medical);

        if (args.Result.DirtyFlags.HasFlag(MedicalDirtyFlags.Skeletal))
            TrackBrokenRegions(ent);

        if (args.Result.DirtyFlags.HasFlag(MedicalDirtyFlags.Organs))
            TrackOrganTransitions(ent);

        if (args.Result.DirtyFlags.HasFlag(MedicalDirtyFlags.Bleeding))
            TrackInternalBleeding(ent);
    }

    private void OnSurgeryDone(Entity<HumanMedicalComponent> ent, ref HumanSurgeryAppliedEvent args)
    {
        if (args.Surgeon is not { } surgeon)
            return;

        _surgeriesPerMarine.TryGetValue(surgeon, out var prior);
        _surgeriesPerMarine[surgeon] = prior + 1;
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

    private void OnBodyPartSevered(ref BodyPartSeveranceAppliedEvent args)
    {
        if (args.Type is BodyPartType.Arm or BodyPartType.Leg)
            _severedLimbs++;
    }

    private void TrackBrokenRegions(Entity<HumanMedicalComponent> ent)
    {
        foreach (var region in ent.Comp.Regions)
        {
            if (region.Region == BodyRegion.None)
                continue;

            var key = (ent.Owner, region.Region);
            if (region.Skeletal.Broken)
            {
                if (!_brokenRegions.Add(key))
                    continue;

                _fractureCounts.TryGetValue(region.Region, out var prior);
                _fractureCounts[region.Region] = prior + 1;
                continue;
            }

            _brokenRegions.Remove(key);
        }
    }

    private void TrackOrganTransitions(Entity<HumanMedicalComponent> ent)
    {
        foreach (var organ in ent.Comp.Organs)
        {
            if (organ.Slot == HumanOrganSlot.None)
                continue;

            var key = (ent.Owner, organ.Slot);
            _lastOrganStatus.TryGetValue(key, out var priorStatus);
            if (priorStatus == organ.Status)
                continue;

            _lastOrganStatus[key] = organ.Status;
            if (organ.Status == OrganDamageStatus.None)
                continue;

            _organStageTransitions.TryGetValue(ent.Owner, out var prior);
            _organStageTransitions[ent.Owner] = prior + 1;
        }
    }

    private void TrackInternalBleeding(Entity<HumanMedicalComponent> ent)
    {
        var active = HasInternalBleeding(ent.Comp);
        _internalBleedingActive.TryGetValue(ent.Owner, out var prior);
        if (prior == active)
            return;

        _internalBleedingActive[ent.Owner] = active;
        if (active)
            _internalBleedsStarted++;
        else
            _internalBleedsStopped++;
    }

    private static bool HasInternalBleeding(HumanMedicalComponent medical)
    {
        foreach (var source in medical.BleedSources)
        {
            if (source.Kind == BleedKind.Internal &&
                !source.Treatment.HasFlag(TreatmentFlags.Closed) &&
                !source.Treatment.HasFlag(TreatmentFlags.Sutured))
            {
                return true;
            }
        }

        return false;
    }

    private void OnShrapnelChanged(Entity<HumanMedicalComponent> ent, ref CMUShrapnelChangedEvent args)
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
        _lastOrganStatus.Clear();
        _internalBleedingActive.Clear();
        _brokenRegions.Clear();
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
