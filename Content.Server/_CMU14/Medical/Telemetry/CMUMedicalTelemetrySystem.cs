using System.Collections.Generic;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Bones.Events;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared.Body.Part;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Telemetry;

public sealed class CMUMedicalTelemetrySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _log = default!;

    private ISawmill _sawmill = default!;

    private readonly Dictionary<BodyPartType, int> _hitCounts = new();
    private readonly Dictionary<FractureSeverity, int> _fractureCounts = new();
    private readonly Dictionary<EntityUid, int> _surgeriesPerMarine = new();
    private readonly Dictionary<EntityUid, int> _organStageTransitions = new();
    private readonly Dictionary<EntityUid, int> _painShockEntries = new();
    private int _defibAttempts;
    private int _defibCancels;

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

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundEnd);
    }

    private void OnHitResolved(Entity<HitLocationComponent> ent, ref HitLocationResolvedEvent args)
    {
        _hitCounts.TryGetValue(args.ResolvedPart, out var prior);
        _hitCounts[args.ResolvedPart] = prior + 1;
        _sawmill.Debug($"hit body={ent.Owner} part={args.ResolvedPart} attacker={args.Attacker}");
    }

    private void OnFractureSpawn(Entity<Content.Shared._CMU14.Medical.BodyPart.BodyPartHealthComponent> ent, ref BoneFracturedEvent args)
    {
        if (args.Old == args.New)
            return;
        _fractureCounts.TryGetValue(args.New, out var prior);
        _fractureCounts[args.New] = prior + 1;
        _sawmill.Info($"fracture body={args.Body} part={args.Part} {args.Old}->{args.New}");
    }

    private void OnOrganStage(ref OrganStageChangedEvent args)
    {
        _organStageTransitions.TryGetValue(args.Body, out var prior);
        _organStageTransitions[args.Body] = prior + 1;
        _sawmill.Info($"organStage body={args.Body} organ={args.Organ} {args.Old}->{args.New}");
    }

    private void OnSurgeryDone(ref CMSurgeryCompleteEvent args)
    {
        _surgeriesPerMarine.TryGetValue(args.Patient, out var prior);
        _surgeriesPerMarine[args.Patient] = prior + 1;
        _sawmill.Info($"surgeryComplete patient={args.Patient} surgeon={args.Surgeon} surgery={args.Surgery}");
    }

    private void OnDefibAttempt(RMCDefibrillatorAttemptEvent ev)
    {
        _defibAttempts++;
        if (ev.Cancelled)
        {
            _defibCancels++;
            _sawmill.Info($"defibCancelled target={ev.Target} reason={ev.CancelReason ?? "unspecified"}");
        }
        else
        {
            _sawmill.Info($"defibAllowed target={ev.Target}");
        }
    }

    private void OnPainShockEntered(Entity<CMUPainShockStatusComponent> ent, ref ComponentStartup args)
    {
        _painShockEntries.TryGetValue(ent.Owner, out var prior);
        _painShockEntries[ent.Owner] = prior + 1;
        _sawmill.Info($"painShockEntered statusEntity={ent.Owner} t={_timing.CurTime}");
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
    }

    private void EmitRoundSummary()
    {
        _sawmill.Info("=== CMU medical round summary ===");

        var hitTotal = 0;
        foreach (var (zone, count) in _hitCounts)
            hitTotal += count;
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

        var fractureTotal = 0;
        foreach (var (_, count) in _fractureCounts)
            fractureTotal += count;
        _sawmill.Info($"fractures total={fractureTotal}");
        foreach (var (severity, count) in _fractureCounts)
            _sawmill.Info($"fractures severity={severity} count={count}");

        var organTotal = 0;
        foreach (var (_, count) in _organStageTransitions)
            organTotal += count;
        _sawmill.Info($"organStageTransitions total={organTotal} marinesAffected={_organStageTransitions.Count}");

        var surgeryTotal = 0;
        foreach (var (_, count) in _surgeriesPerMarine)
            surgeryTotal += count;
        _sawmill.Info($"surgeries total={surgeryTotal} marinesOperated={_surgeriesPerMarine.Count}");

        _sawmill.Info($"defib attempts={_defibAttempts} cancels={_defibCancels} (CMU layer rejections only)");
        _sawmill.Info($"painShockEntries total={_painShockEntries.Count}");
        _sawmill.Info("=== end CMU medical round summary ===");
    }
}
