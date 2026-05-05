using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.Organs.Heart.Events;
using Content.Shared._RMC14.Body;
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

namespace Content.Shared._CMU14.Medical.Organs.Heart;

public abstract class SharedHeartSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedRMCBloodstreamSystem Bloodstream = default!;
    [Dependency] protected readonly SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId Tachycardia = "StatusEffectCMUTachycardia";
    private static readonly EntProtoId Arrhythmia = "StatusEffectCMUArrhythmia";
    private static readonly EntProtoId CardiacArrest = "StatusEffectCMUCardiacArrest";
    private static readonly EntProtoId Unconscious = "StatusEffectCMUUnconscious";

    private const float PulseScanInterval = 1f;
    private float _pulseScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HeartComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<HeartComponent, ComponentStartup>(OnHeartStartup);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnHeartStartup(Entity<HeartComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextPulseUpdate = Timing.CurTime + ent.Comp.PulseUpdateInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient)
            return;

        if (!_medicalEnabled || !_organEnabled)
            return;

        _pulseScanAccumulator += frameTime;
        if (_pulseScanAccumulator < PulseScanInterval)
            return;
        _pulseScanAccumulator = 0f;

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<HeartComponent, OrganHealthComponent>();
        while (query.MoveNext(out var uid, out var heart, out var oh))
        {
            if (heart.Stopped)
                TickCardiacArrest((uid, heart, oh), now);

            if (heart.NextPulseUpdate > now)
                continue;
            heart.NextPulseUpdate = now + heart.PulseUpdateInterval;
            UpdatePulse((uid, heart, oh), now);
        }
    }

    public void TickPulse(Entity<HeartComponent?, OrganHealthComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp1, ref ent.Comp2, logMissing: false))
            return;
        UpdatePulse((ent.Owner, ent.Comp1, ent.Comp2), Timing.CurTime);
    }

    private void UpdatePulse(Entity<HeartComponent, OrganHealthComponent> ent, TimeSpan now)
    {
        var (uid, heart, oh) = ent;

        if (heart.Stopped)
        {
            if (heart.BeatsPerMinute != 0)
            {
                heart.BeatsPerMinute = 0;
                Dirty(uid, heart);
            }
            return;
        }

        var body = GetBody(uid);
        if (body is null)
            return;

        if (TryComp<MobStateComponent>(body.Value, out var mob) && mob.CurrentState == MobState.Dead)
        {
            heart.BeatsPerMinute = 0;
            if (!heart.Stopped)
                StopHeart((uid, heart), body.Value);
            return;
        }

        var bpm = ComputeBpm(uid, body.Value, oh);
        var clamped = Math.Clamp(bpm, 0, heart.MaxBpm);

        // Threshold logic uses the stable (un-jittered) BPM so BelowThresholdSince
        // doesn't flicker when the per-update jitter randomly crosses
        // MinBpmBeforeStop. Cardiac arrest must be driven by the marine's actual
        // physiological state, not by display noise.
        if (clamped < heart.MinBpmBeforeStop)
        {
            heart.BelowThresholdSince ??= now;
            Dirty(uid, heart);
            if (now - heart.BelowThresholdSince.Value >= heart.StopGracePeriod)
            {
                StopHeart((uid, heart), body.Value);
                return;
            }
        }
        else if (heart.BelowThresholdSince is not null)
        {
            heart.BelowThresholdSince = null;
            Dirty(uid, heart);
        }

        // Floor at 1 if clamped is positive so a jittered-low marine doesn't
        // read 0 (which the UI treats as stopped).
        var displayed = clamped > 0
            ? Math.Max(1, clamped + Random.Next(-3, 4))
            : 0;
        if (displayed != heart.BeatsPerMinute)
        {
            heart.BeatsPerMinute = displayed;
            Dirty(uid, heart);
        }
    }

    protected virtual int ComputeBpm(EntityUid heartUid, EntityUid body, OrganHealthComponent oh)
    {
        var baseBpm = oh.Stage switch
        {
            OrganDamageStage.Bruised => 95,
            OrganDamageStage.Damaged => 50,
            OrganDamageStage.Failing => 20,
            OrganDamageStage.Dead => 0,
            _ => 70,
        };

        if (TryGetBloodFraction(body, out var fraction))
        {
            if (fraction < 0.7f)
                baseBpm += (int)((0.7f - fraction) * 100f);
            if (fraction < 0.4f)
                baseBpm = (int)(baseBpm * 0.5f);
        }

        foreach (var (organId, _) in Body.GetBodyOrgans(body))
        {
            if (!TryComp<OrganHealthComponent>(organId, out var organHealth))
                continue;
            if (organId == heartUid)
                continue;
            if (organHealth.Stage.IsAtLeast(OrganDamageStage.Bruised))
                baseBpm += 5;
            if (organHealth.Stage.IsAtLeast(OrganDamageStage.Damaged))
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
        fraction = (float)solution.Volume / (float)solution.MaxVolume;
        return true;
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

    private void OnStageChanged(Entity<HeartComponent> ent, ref OrganStageChangedEvent args)
    {
        var body = args.Body;
        switch (args.New)
        {
            case OrganDamageStage.Healthy:
                ent.Comp.MinBpmBeforeStop = 30;
                ent.Comp.BelowThresholdSince = null;
                Dirty(ent);
                Status.TryRemoveStatusEffect(body, Tachycardia);
                Status.TryRemoveStatusEffect(body, Arrhythmia);
                break;
            case OrganDamageStage.Bruised:
                ent.Comp.MinBpmBeforeStop = 30;
                ent.Comp.BelowThresholdSince = null;
                Dirty(ent);
                Status.TryRemoveStatusEffect(body, Arrhythmia);
                Status.TrySetStatusEffectDuration(body, Tachycardia, duration: null);
                break;
            case OrganDamageStage.Damaged:
                ent.Comp.MinBpmBeforeStop = 30;
                Dirty(ent);
                Status.TryRemoveStatusEffect(body, Tachycardia);
                Status.TrySetStatusEffectDuration(body, Arrhythmia, duration: null);
                break;
            case OrganDamageStage.Failing:
                Status.TryRemoveStatusEffect(body, Tachycardia);
                Status.TrySetStatusEffectDuration(body, Arrhythmia, duration: null);
                ent.Comp.MinBpmBeforeStop = 60;
                Dirty(ent);
                break;
            case OrganDamageStage.Dead:
                Status.TryRemoveStatusEffect(body, Tachycardia);
                Status.TryRemoveStatusEffect(body, Arrhythmia);
                if (!ent.Comp.Stopped)
                    StopHeart(ent, body);
                break;
        }
    }

    private void TickCardiacArrest(Entity<HeartComponent, OrganHealthComponent> ent, TimeSpan now)
    {
        if (ent.Comp1.NextCardiacArrestTick > now)
            return;
        ent.Comp1.NextCardiacArrestTick = now + TimeSpan.FromSeconds(1);

        var body = GetBody(ent.Owner);
        if (body is null)
            return;

        if (TryComp<MobStateComponent>(body.Value, out var mob) && mob.CurrentState == MobState.Dead)
            return;

        ent.Comp1.NoPulseSince ??= now;
        Dirty(ent.Owner, ent.Comp1);

        if (ent.Comp1.CardiacArrestAsphyxPerSecond > FixedPoint2.Zero)
            ApplyCardiacArrestAsphyx(body.Value, ent.Owner, ent.Comp1.CardiacArrestAsphyxPerSecond);

        if (now - ent.Comp1.NoPulseSince.Value >= ent.Comp1.CardiacArrestUnconsciousDelay)
            Status.TrySetStatusEffectDuration(body.Value, Unconscious, TimeSpan.FromSeconds(3));
    }

    protected virtual void ApplyCardiacArrestAsphyx(EntityUid body, EntityUid heart, FixedPoint2 amount)
    {
    }

    protected EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
