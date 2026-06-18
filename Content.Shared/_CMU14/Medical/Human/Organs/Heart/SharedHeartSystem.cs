using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Organs.Heart.Events;
using Content.Shared._RMC14.Body;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Human.Organs.Heart;

public abstract partial class SharedHeartSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected SharedBodySystem Body = default!;
    [Dependency] protected SharedRMCBloodstreamSystem Bloodstream = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId Tachycardia = "StatusEffectCMUTachycardia";
    private static readonly EntProtoId Arrhythmia = "StatusEffectCMUArrhythmia";
    private static readonly EntProtoId CardiacArrest = "StatusEffectCMUCardiacArrest";
    private static readonly EntProtoId Unconscious = "StatusEffectCMUUnconscious";
    private static readonly FixedPoint2 MissingHeartAsphyxPerSecond = FixedPoint2.New(6);
    private static readonly TimeSpan MissingHeartUnconsciousDelay = TimeSpan.FromSeconds(5);

    private const float PulseScanInterval = 1f;
    private float _pulseScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanMedicalLedgerChangedEvent>(OnLedgerChanged);
        SubscribeLocalEvent<HeartComponent, ComponentStartup>(OnHeartStartup);
        SubscribeLocalEvent<HeartComponent, OrganRemovedFromBodyEvent>(OnHeartRemovedFromBody);
        SubscribeLocalEvent<HeartComponent, OrganAddedToBodyEvent>(OnHeartAddedToBody);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnHeartStartup(Entity<HeartComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextPulseUpdate = Timing.CurTime + ent.Comp.PulseUpdateInterval;
    }

    private void OnHeartRemovedFromBody(Entity<HeartComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;
        if (TerminatingOrDeleted(args.OldBody))
            return;

        var missing = EnsureComp<MissingHeartComponent>(args.OldBody);
        missing.NoPulseSince ??= Timing.CurTime;
        missing.NextCardiacArrestTick = Timing.CurTime;

        Status.TrySetStatusEffectDuration(args.OldBody, CardiacArrest, duration: null);
    }

    private void OnHeartAddedToBody(Entity<HeartComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (ent.Comp.Stopped)
            return;

        RemCompDeferred<MissingHeartComponent>(args.Body);
        Status.TryRemoveStatusEffect(args.Body, CardiacArrest);
    }

    private void OnLedgerChanged(ref HumanMedicalLedgerChangedEvent args)
    {
        if (!HumanOrganLedgerUtility.OrgansChanged(args.Result))
            return;

        if (!TryComp(args.Body, out HumanMedicalComponent? medical))
            return;

        SyncHeartState(args.Body, medical);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient || !_medicalEnabled || !_organEnabled)
            return;

        _pulseScanAccumulator += frameTime;
        if (_pulseScanAccumulator < PulseScanInterval)
            return;

        _pulseScanAccumulator = 0f;
        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<HeartComponent, OrganComponent>();
        while (query.MoveNext(out var uid, out var heart, out var organ))
        {
            if (organ.Body is not { } body)
                continue;
            if (!TryComp<HumanMedicalComponent>(body, out var medical))
                continue;

            if (heart.Stopped)
                TickCardiacArrest((uid, heart), body, now);

            if (heart.NextPulseUpdate > now)
                continue;

            heart.NextPulseUpdate = now + heart.PulseUpdateInterval;
            UpdatePulse((uid, heart), body, medical, now);
        }

        var missingQuery = EntityQueryEnumerator<MissingHeartComponent>();
        while (missingQuery.MoveNext(out var uid, out var missing))
        {
            if (TryComp<HumanMedicalComponent>(uid, out var medical) &&
                !HumanOrganLedgerUtility.IsMissing(medical, OrganSlot.Heart) &&
                Body.GetBodyOrganEntityComps<HeartComponent>(uid).Count != 0)
            {
                RemCompDeferred<MissingHeartComponent>(uid);
                continue;
            }

            TickMissingHeart((uid, missing), now);
        }
    }

    public void TickPulse(Entity<HeartComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, logMissing: false))
            return;
        if (GetBody(ent.Owner) is not { } body)
            return;
        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return;

        UpdatePulse((ent.Owner, ent.Comp), body, medical, Timing.CurTime);
    }

    private void UpdatePulse(
        Entity<HeartComponent> ent,
        EntityUid body,
        HumanMedicalComponent medical,
        TimeSpan now)
    {
        var (uid, heart) = ent;

        if (heart.Stopped)
        {
            if (heart.BeatsPerMinute != 0)
            {
                heart.BeatsPerMinute = 0;
                Dirty(uid, heart);
            }
            return;
        }

        if (TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
        {
            heart.BeatsPerMinute = 0;
            if (!heart.Stopped)
                StopHeart((uid, heart), body);
            return;
        }

        var status = HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Heart);
        var bpm = ComputeBpm(body, medical, status, out var unstablePulse);
        var clamped = Math.Clamp(bpm, 0, heart.MaxBpm);

        if (clamped < heart.MinBpmBeforeStop)
        {
            if (heart.BelowThresholdSince is null)
            {
                heart.BelowThresholdSince = now;
                Dirty(uid, heart);
            }

            if (now - heart.BelowThresholdSince.Value >= heart.StopGracePeriod)
            {
                StopHeart((uid, heart), body);
                return;
            }
        }
        else if (heart.BelowThresholdSince is not null)
        {
            heart.BelowThresholdSince = null;
            Dirty(uid, heart);
        }

        var displayed = clamped > 0
            ? unstablePulse ? Math.Max(1, clamped + Random.Next(-3, 4)) : clamped
            : 0;
        if (displayed != heart.BeatsPerMinute)
        {
            heart.BeatsPerMinute = displayed;
            Dirty(uid, heart);
        }
    }

    protected virtual int ComputeBpm(
        EntityUid body,
        HumanMedicalComponent medical,
        OrganDamageStatus heartStatus,
        out bool unstablePulse)
    {
        unstablePulse = heartStatus != OrganDamageStatus.None;

        var baseBpm = heartStatus switch
        {
            OrganDamageStatus.LittleBruised => 95,
            OrganDamageStatus.Bruised => 50,
            OrganDamageStatus.Broken => 20,
            _ => 70,
        };

        if (TryGetBloodFraction(body, out var fraction))
        {
            if (fraction < 0.7f)
            {
                unstablePulse = true;
                baseBpm += (int) ((0.7f - fraction) * 100f);
            }

            if (fraction < 0.4f)
                baseBpm = (int) (baseBpm * 0.5f);
        }

        foreach (var organ in medical.Organs)
        {
            if (!organ.Symptomatic || organ.Slot == OrganSlot.Heart)
                continue;

            if (organ.Status >= OrganDamageStatus.LittleBruised)
            {
                unstablePulse = true;
                baseBpm += 5;
            }

            if (organ.Status >= OrganDamageStatus.Bruised)
                baseBpm += 10;
        }

        return baseBpm;
    }

    private bool TryGetBloodFraction(EntityUid body, out float fraction)
    {
        fraction = 0f;
        if (!Bloodstream.TryGetBloodSolution(body, out var solution))
            return false;
        if (solution.MaxVolume <= FixedPoint2.Zero)
            return false;

        fraction = (float) solution.Volume / (float) solution.MaxVolume;
        return true;
    }

    private void SyncHeartState(EntityUid body, HumanMedicalComponent medical)
    {
        var heart = HumanMedicalLedger.GetOrgan(medical, OrganSlot.Heart);
        if (heart.Missing)
        {
            var missing = EnsureComp<MissingHeartComponent>(body);
            missing.NoPulseSince ??= Timing.CurTime;
            missing.NextCardiacArrestTick = Timing.CurTime;
            Status.TrySetStatusEffectDuration(body, CardiacArrest, duration: null);
            return;
        }

        if (Body.GetBodyOrganEntityComps<HeartComponent>(body).Count != 0)
            RemCompDeferred<MissingHeartComponent>(body);

        var status = HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Heart);
        foreach (var organHeart in Body.GetBodyOrganEntityComps<HeartComponent>(body))
        {
            ApplyHeartStatus(body, (organHeart.Owner, organHeart.Comp1), status);
        }
    }

    private void ApplyHeartStatus(
        EntityUid body,
        Entity<HeartComponent> ent,
        OrganDamageStatus status)
    {
        switch (status)
        {
            case OrganDamageStatus.None:
                ent.Comp.MinBpmBeforeStop = 30;
                ent.Comp.BelowThresholdSince = null;
                Dirty(ent);
                Status.TryRemoveStatusEffect(body, Tachycardia);
                Status.TryRemoveStatusEffect(body, Arrhythmia);
                break;
            case OrganDamageStatus.LittleBruised:
                ent.Comp.MinBpmBeforeStop = 30;
                ent.Comp.BelowThresholdSince = null;
                Dirty(ent);
                Status.TryRemoveStatusEffect(body, Arrhythmia);
                Status.TrySetStatusEffectDuration(body, Tachycardia, duration: null);
                break;
            case OrganDamageStatus.Bruised:
                ent.Comp.MinBpmBeforeStop = 30;
                Dirty(ent);
                Status.TryRemoveStatusEffect(body, Tachycardia);
                Status.TrySetStatusEffectDuration(body, Arrhythmia, duration: null);
                break;
            case OrganDamageStatus.Broken:
                Status.TryRemoveStatusEffect(body, Tachycardia);
                Status.TrySetStatusEffectDuration(body, Arrhythmia, duration: null);
                ent.Comp.MinBpmBeforeStop = 60;
                Dirty(ent);
                break;
        }
    }

    private void StopHeart(Entity<HeartComponent> ent, EntityUid body)
    {
        ent.Comp.Stopped = true;
        ent.Comp.BeatsPerMinute = 0;
        ent.Comp.NoPulseSince ??= Timing.CurTime;
        ent.Comp.NextCardiacArrestTick = Timing.CurTime;
        Dirty(ent);

        Status.TrySetStatusEffectDuration(body, CardiacArrest, duration: null);

        var ev = new HeartStoppedEvent(body, ent.Owner);
        RaiseLocalEvent(ent, ref ev);
    }

    public void TryRestartHeart(Entity<HeartComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, logMissing: false))
            return;
        if (!ent.Comp.Stopped)
            return;

        ent.Comp.Stopped = false;
        ent.Comp.BelowThresholdSince = null;
        ent.Comp.NoPulseSince = null;
        Dirty(ent.Owner, ent.Comp);

        if (GetBody(ent.Owner) is { } body)
            Status.TryRemoveStatusEffect(body, CardiacArrest);
    }

    public void ResetHeart(Entity<HeartComponent?> ent, int beatsPerMinute = 70)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, logMissing: false))
            return;

        ent.Comp.Stopped = false;
        ent.Comp.BeatsPerMinute = beatsPerMinute;
        ent.Comp.BelowThresholdSince = null;
        ent.Comp.NoPulseSince = null;
        Dirty(ent.Owner, ent.Comp);

        if (GetBody(ent.Owner) is { } body)
            Status.TryRemoveStatusEffect(body, CardiacArrest);
    }

    private void TickCardiacArrest(Entity<HeartComponent> ent, EntityUid body, TimeSpan now)
    {
        if (ent.Comp.NextCardiacArrestTick > now)
            return;

        ent.Comp.NextCardiacArrestTick = now + TimeSpan.FromSeconds(1);

        if (TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
            return;

        if (ent.Comp.NoPulseSince is null)
        {
            ent.Comp.NoPulseSince = now;
            Dirty(ent.Owner, ent.Comp);
        }

        if (ent.Comp.CardiacArrestAsphyxPerSecond > FixedPoint2.Zero)
            ApplyCardiacArrestAsphyx(body, ent.Owner, ent.Comp.CardiacArrestAsphyxPerSecond);

        if (now - ent.Comp.NoPulseSince.Value >= ent.Comp.CardiacArrestUnconsciousDelay)
            Status.TrySetStatusEffectDuration(body, Unconscious, TimeSpan.FromSeconds(3));
    }

    private void TickMissingHeart(Entity<MissingHeartComponent> ent, TimeSpan now)
    {
        if (ent.Comp.NextCardiacArrestTick > now)
            return;

        ent.Comp.NextCardiacArrestTick = now + TimeSpan.FromSeconds(1);

        if (TryComp<MobStateComponent>(ent.Owner, out var mob) && mob.CurrentState == MobState.Dead)
            return;

        ent.Comp.NoPulseSince ??= now;

        Status.TrySetStatusEffectDuration(ent.Owner, CardiacArrest, duration: null);

        if (MissingHeartAsphyxPerSecond > FixedPoint2.Zero)
            ApplyCardiacArrestAsphyx(ent.Owner, ent.Owner, MissingHeartAsphyxPerSecond);

        if (now - ent.Comp.NoPulseSince.Value >= MissingHeartUnconsciousDelay)
            Status.TrySetStatusEffectDuration(ent.Owner, Unconscious, TimeSpan.FromSeconds(3));
    }

    protected virtual void ApplyCardiacArrestAsphyx(EntityUid body, EntityUid heart, FixedPoint2 amount)
    {
    }

    protected EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
