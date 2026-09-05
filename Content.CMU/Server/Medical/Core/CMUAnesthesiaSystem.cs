using Content.Server.Body.Systems;
using Content.Shared._RMC14.Synth;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical.Core;

/// <summary>
/// Owns one continuous nitrous exposure per patient. Connection events start exposure;
/// keyed deadlines validate only exposed patients, including changes to a live tank's gas.
/// </summary>
public sealed partial class CMUAnesthesiaSystem : EntitySystem
{
    [Dependency] private SharedInternalsSystem _internals = default!;
    [Dependency] private CMUMedicalSchedulerSystem _scheduler = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SleepingSystem _sleeping = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float MinimumNitrousMoles = 0.01f;
    private static readonly EntProtoId AnesthesiaSleeping = "StatusEffectCMUAnesthesia";
    private static readonly EntProtoId InductionDrowsiness = "StatusEffectCMUAnesthesiaInduction";
    private static readonly CMUMedicalWorkKey InductionWork = new("anesthesia.induction");
    private static readonly CMUMedicalWorkKey ExposureWork = new("anesthesia.exposure");
    private static readonly TimeSpan InductionDuration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan ExposureCheckInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHumanMedicalComponent, CMUInternalsChangedEvent>(OnInternalsChanged);
        SubscribeLocalEvent<CMUHumanMedicalComponent, InhaleLocationEvent>(OnInhale,
            after: new[] { typeof(InternalsSystem) });
        SubscribeLocalEvent<CMUAnesthesiaStateComponent, CMUMedicalWorkDueEvent>(OnWorkDue);
        SubscribeLocalEvent<CMUAnesthesiaStateComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUAnesthesiaStateComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnInternalsChanged(Entity<CMUHumanMedicalComponent> ent, ref CMUInternalsChangedEvent args)
    {
        // Shutdown exposes the old Internals component until its callback returns.
        if (!args.Working)
        {
            ClearAnesthesia(ent.Owner);
            return;
        }
        RefreshExposure(ent.Owner);
    }

    private void OnInhale(Entity<CMUHumanMedicalComponent> ent, ref InhaleLocationEvent args)
        => RefreshExposure(ent.Owner);

    private bool TryGetExposure(EntityUid body, out EntityUid tank)
    {
        tank = default;
        if (TerminatingOrDeleted(body) || !HasComp<CMUHumanMedicalComponent>(body) ||
            HasComp<SynthComponent>(body) ||
            TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead ||
            !TryComp<InternalsComponent>(body, out var internals) ||
            internals.LifeStage > ComponentLifeStage.Running ||
            !_internals.AreInternalsWorking(internals) ||
            internals.GasTankEntity is not { } connected || TerminatingOrDeleted(connected) ||
            !TryComp<GasTankComponent>(connected, out var gasTank) ||
            gasTank.LifeStage > ComponentLifeStage.Running ||
            !float.IsFinite(gasTank.ReleasePressure) || gasTank.ReleasePressure <= 0 ||
            !float.IsFinite(gasTank.Air.Pressure) || gasTank.Air.Pressure <= 0 ||
            gasTank.Air.GetMoles(Gas.NitrousOxide) <= MinimumNitrousMoles)
        {
            return false;
        }
        tank = connected;
        return true;
    }

    private void RefreshExposure(EntityUid body)
    {
        if (!TryGetExposure(body, out var tank))
        {
            ClearAnesthesia(body);
            return;
        }
        if (TryComp<CMUAnesthesiaStateComponent>(body, out var current))
        {
            if (current.GasTank == tank)
                return;
            ClearAnesthesia(body);
        }

        var anesthesia = AddComp<CMUAnesthesiaStateComponent>(body);
        anesthesia.GasTank = tank;
        _status.TrySetStatusEffectDuration(body, InductionDrowsiness, out anesthesia.Drowsiness,
            InductionDuration);
        _popup.PopupEntity(Loc.GetString("effect-sleepy"), body, body, PopupType.Medium);
        _scheduler.Schedule(body, InductionWork, _timing.CurTime + InductionDuration);
        _scheduler.Schedule(body, ExposureWork, _timing.CurTime + ExposureCheckInterval);
    }

    private void OnWorkDue(Entity<CMUAnesthesiaStateComponent> ent, ref CMUMedicalWorkDueEvent args)
    {
        if (args.Key != InductionWork && args.Key != ExposureWork)
            return;
        if (!TryGetExposure(ent.Owner, out var tank) || tank != ent.Comp.GasTank)
        {
            ClearAnesthesia(ent.Owner);
            return;
        }
        if (args.Key == ExposureWork)
        {
            _scheduler.Schedule(ent.Owner, ExposureWork, _timing.CurTime + ExposureCheckInterval);
            return;
        }
        if (ent.Comp.Induced)
            return;

        RemoveOwnedStatus(ent.Owner, ref ent.Comp.Drowsiness);
        // The forced-sleep status applies sleep synchronously. Capture ownership
        // here, so someone falling asleep during induction keeps their own sleep.
        ent.Comp.OwnsSleep = !HasComp<SleepingComponent>(ent.Owner);
        if (!_status.TrySetStatusEffectDuration(ent.Owner, AnesthesiaSleeping, out ent.Comp.ForcedSleep))
        {
            ent.Comp.OwnsSleep = false;
            ClearAnesthesia(ent.Owner);
            return;
        }
        // Applying forced sleep raises public callbacks. A disconnect or reset
        // there may retire this session before SleepingSystem finishes adding sleep.
        if (!TryComp<CMUAnesthesiaStateComponent>(ent.Owner, out var current) || current != ent.Comp ||
            !TryGetExposure(ent.Owner, out var currentTank) || currentTank != ent.Comp.GasTank ||
            !HasComp<SleepingComponent>(ent.Owner))
        {
            if (TerminatingOrDeleted(ent.Owner))
                return;
            if (current == ent.Comp)
                ClearAnesthesia(ent.Owner);
            else
            {
                RemoveOwnedStatus(ent.Owner, ref ent.Comp.ForcedSleep);
                if (ent.Comp.OwnsSleep)
                    _sleeping.TryWaking((ent.Owner, null));
            }
            return;
        }
        ent.Comp.Induced = true;
    }

    private void OnRejuvenate(Entity<CMUAnesthesiaStateComponent> ent, ref RejuvenateEvent args)
        => ClearAnesthesia(ent.Owner);

    private void ClearAnesthesia(EntityUid body)
        => RemComp<CMUAnesthesiaStateComponent>(body);

    private void OnShutdown(Entity<CMUAnesthesiaStateComponent> ent, ref ComponentShutdown args)
    {
        _scheduler.Cancel(ent.Owner, InductionWork);
        _scheduler.Cancel(ent.Owner, ExposureWork);
        // Entity deletion owns its status children. Never mutate that traversal.
        if (TerminatingOrDeleted(ent.Owner))
            return;
        RemoveOwnedStatus(ent.Owner, ref ent.Comp.Drowsiness);
        RemoveOwnedStatus(ent.Owner, ref ent.Comp.ForcedSleep);
        if (ent.Comp.OwnsSleep)
            _sleeping.TryWaking((ent.Owner, null));
    }

    private void RemoveOwnedStatus(EntityUid body, ref EntityUid? effect)
    {
        var owned = effect;
        effect = null;
        if (owned is not { } uid || TerminatingOrDeleted(uid) ||
            !TryComp<StatusEffectComponent>(uid, out var status) || status.AppliedTo != body)
            return;
        // Retire this exact source before reconnect/wake. QueueDel would leave
        // the old forced-sleep blocker visible and could reuse a doomed status.
        Del(uid);
    }
}
