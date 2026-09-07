using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;

public abstract partial class SharedHeartSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] protected SharedRMCBloodstreamSystem Bloodstream = default!;
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected CMStasisBagSystem Stasis = default!;
    [Dependency] protected StatusEffectsSystem Status = default!;
    [Dependency] protected IGameTiming Timing = default!;

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
        InitializeRhythmStatus();
        SubscribeLocalEvent<HeartComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<HeartComponent, ComponentStartup>(OnHeartStartup);
        SubscribeLocalEvent<HeartComponent, ComponentShutdown>(OnHeartShutdown);
        SubscribeLocalEvent<HeartComponent, OrganRemovedFromBodyEvent>(OnHeartRemovedFromBody,
            after: new[] { typeof(CMUMedicalBodyIndexSystem) });
        SubscribeLocalEvent<HeartComponent, OrganAddedToBodyEvent>(OnHeartAddedToBody,
            after: new[] { typeof(CMUMedicalBodyIndexSystem) });
        SubscribeLocalEvent<HeartComponent, EntityPausedEvent>(OnHeartPaused);
        SubscribeLocalEvent<HeartComponent, EntityUnpausedEvent>(OnHeartUnpaused);
        SubscribeLocalEvent<BodyComponent, EntityPausedEvent>(OnBodyPaused);
        SubscribeLocalEvent<BodyComponent, EntityUnpausedEvent>(OnBodyUnpaused);
        SubscribeLocalEvent<BodyComponent, CMUMedicalStasisChangedEvent>(OnStasisChanged);
        SubscribeLocalEvent<BodyComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BodyComponent, SolutionChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<BodyComponent, ChemicalCardiacPacingChangedEvent>(OnPacingChanged);
        SubscribeLocalEvent<ChemicalCardiacPacingComponent, ComponentShutdown>(OnPacingRemoved);
        SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnRejuvenate,
            before: new[] { typeof(ChemicalPropertyStatusSystem), typeof(BloodstreamSystem), typeof(DamageableSystem) });

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => SetLayerEnabled(ref _medicalEnabled, v), true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => SetLayerEnabled(ref _organEnabled, v), true);
    }

    private bool Enabled => _medicalEnabled && _organEnabled;

    private void SetLayerEnabled(ref bool field, bool value)
    {
        if (field == value)
            return;
        // Settle the old enabled interval before changing the layer. While disabled
        // the regular service still moves clocks, without applying physiology.
        if (_net.IsServer)
            ServiceHearts(Timing.CurTime);
        field = value;
        RefreshAllRhythmStatuses();
    }

    private void OnHeartStartup(Entity<HeartComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.LastPhysiologyUpdate = Timing.CurTime;
        ent.Comp.NextPulseUpdate = Timing.CurTime + ent.Comp.PulseUpdateInterval;
        ent.Comp.NextOrganDamageTick = Timing.CurTime + TimeSpan.FromSeconds(1);
        if (TryComp<OrganHealthComponent>(ent, out var health))
            ent.Comp.PhysiologyStage = health.Stage;
        if (GetBody(ent.Owner) is { } body)
            ReconcileRhythmStatus(body);
    }

    private void OnHeartShutdown(Entity<HeartComponent> ent, ref ComponentShutdown args)
    {
        if (GetBody(ent.Owner) is { } body)
            ReconcileRhythmStatus(body);
    }

    private void OnHeartRemovedFromBody(Entity<HeartComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (_net.IsClient || !TryComp<BodyComponent>(args.OldBody, out var bodyComponent))
            return;

        Entity<BodyComponent> body = (args.OldBody, bodyComponent);
        if (!IsCurrentHeartPatient(body))
            return;
        var now = Timing.CurTime;
        // Organ.Body already changed; the committed event retains the old patient.
        if (!TerminatingOrDeleted(ent.Owner))
            AdvanceHeart(ent, args.OldBody, now);
        if (!IsCurrentHeartPatient(body))
            return;
        var noPulseElapsed = TimeSpan.Zero;
        if (TryComp<HeartComponent>(ent.Owner, out var currentHeart) && ReferenceEquals(currentHeart, ent.Comp))
        {
            FreezeHeart(ent.Comp, now);
            ent.Comp.AsphyxRemainder = 0;
            ent.Comp.ToxinRemainder = 0;
            if (ent.Comp.Stopped && ent.Comp.NoPulseSince is { } stoppedAt)
                noPulseElapsed = now - stoppedAt;
        }
        ReconcileRhythmStatus(args.OldBody);
        // Retiring the old rhythm is a public callback. The old patient may have
        // been deleted/replaced or already received a new heart during that call.
        if (!IsCurrentHeartPatient(body) || !Enabled || MedicalIndex.TryGetOrgan<HeartComponent>(args.OldBody, out _))
            return;

        var missing = EnsureComp<MissingHeartComponent>(args.OldBody);
        if (!IsCurrentHeartPatient(body) || MedicalIndex.TryGetOrgan<HeartComponent>(args.OldBody, out _) ||
            !TryComp<MissingHeartComponent>(args.OldBody, out var currentMissing) || !ReferenceEquals(currentMissing, missing))
            return;
        missing.NoPulseElapsed = noPulseElapsed;
        missing.LastCardiacArrestUpdate = now;
        missing.NextCardiacArrestTick = now;
        missing.AsphyxRemainder = 0;
        Status.TrySetStatusEffectDuration(args.OldBody, CardiacArrest, duration: null);
    }

    private void OnHeartAddedToBody(Entity<HeartComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (_net.IsClient || !TryComp<BodyComponent>(args.Body, out var body) ||
            !TryComp<BodyPartComponent>(args.Part, out var part) ||
            !TryComp<OrganComponent>(ent.Owner, out var organ) ||
            !TryComp<OrganHealthComponent>(ent.Owner, out var health) ||
            !TryComp<ChildOrganComponent>(ent.Owner, out var relation))
            return;
        var attachment = new HeartAttachment(ent, health, organ, relation, (args.Part, part), (args.Body, body));
        if (!IsCurrentHeartAttachment(attachment))
            return;

        var now = Timing.CurTime;
        var patientArrestElapsed = TimeSpan.Zero;
        if (TryComp<MissingHeartComponent>(args.Body, out var missing))
        {
            AdvanceMissing((args.Body, missing), now);
            if (!IsCurrentHeartAttachment(attachment) ||
                !TryComp<MissingHeartComponent>(args.Body, out var currentMissing) || !ReferenceEquals(currentMissing, missing))
                return;
            patientArrestElapsed = missing.NoPulseElapsed;
            // Immediate retirement avoids a remove -> insert -> remove in one tick
            // reusing an already queued missing-heart component.
            RemComp<MissingHeartComponent>(args.Body);
            if (!IsCurrentHeartAttachment(attachment) || HasComp<MissingHeartComponent>(args.Body))
                return;
        }

        FreezeHeart(ent.Comp, now);
        ent.Comp.PacingUntil = TryComp<ChemicalCardiacPacingComponent>(args.Body, out var pacing)
            ? pacing.ExpiresAt
            : TimeSpan.Zero;
        RefreshPhysiology(ent, args.Body, now);
        if (!IsCurrentHeartAttachment(attachment))
            return;
        ReconcileRhythmStatus(args.Body);
        if (!IsCurrentHeartAttachment(attachment))
            return;
        if (ent.Comp.Stopped)
        {
            // Collapse belongs to the recipient's circulation history, never the
            // donor's previous patient or time spent detached.
            ent.Comp.NoPulseSince = now - patientArrestElapsed;
            Status.TrySetStatusEffectDuration(args.Body, CardiacArrest, duration: null);
        }
        else
            ClearCardiacArrest(args.Body);
    }

    // An insertion may synchronously cause metabolism, status or topology
    // callbacks. Authority belongs to this exact attached tissue and patient.
    private readonly record struct HeartAttachment(
        Entity<HeartComponent> Heart,
        OrganHealthComponent Health,
        OrganComponent Organ,
        ChildOrganComponent Relation,
        Entity<BodyPartComponent> Part,
        Entity<BodyComponent> Body);

    private bool IsCurrentHeartPatient(Entity<BodyComponent> body)
        => !TerminatingOrDeleted(body.Owner) && !EntityManager.IsQueuedForDeletion(body.Owner) &&
           body.Comp.LifeStage <= ComponentLifeStage.Running &&
           TryComp<BodyComponent>(body.Owner, out var current) && ReferenceEquals(current, body.Comp);

    private bool IsCurrentHeartAttachment(HeartAttachment attachment)
        => IsCurrentHeartPatient(attachment.Body) &&
           !TerminatingOrDeleted(attachment.Heart.Owner) && !EntityManager.IsQueuedForDeletion(attachment.Heart.Owner) &&
           !TerminatingOrDeleted(attachment.Part.Owner) && !EntityManager.IsQueuedForDeletion(attachment.Part.Owner) &&
           attachment.Heart.Comp.LifeStage <= ComponentLifeStage.Running &&
           attachment.Health.LifeStage <= ComponentLifeStage.Running && attachment.Organ.LifeStage <= ComponentLifeStage.Running &&
           attachment.Relation.LifeStage <= ComponentLifeStage.Running && attachment.Part.Comp.LifeStage <= ComponentLifeStage.Running &&
           TryComp<HeartComponent>(attachment.Heart.Owner, out var heart) && ReferenceEquals(heart, attachment.Heart.Comp) &&
           TryComp<OrganHealthComponent>(attachment.Heart.Owner, out var health) && ReferenceEquals(health, attachment.Health) &&
           TryComp<OrganComponent>(attachment.Heart.Owner, out var organ) && ReferenceEquals(organ, attachment.Organ) &&
           organ.Body == attachment.Body.Owner &&
           TryComp<ChildOrganComponent>(attachment.Heart.Owner, out var relation) && ReferenceEquals(relation, attachment.Relation) &&
           relation.Parent == attachment.Part.Owner &&
           TryComp<BodyPartComponent>(attachment.Part.Owner, out var part) && ReferenceEquals(part, attachment.Part.Comp) &&
           part.Body == attachment.Body.Owner;

    protected void UpdateServer(float frameTime)
    {
        _pulseScanAccumulator += frameTime;
        if (_pulseScanAccumulator < PulseScanInterval)
            return;
        _pulseScanAccumulator %= PulseScanInterval;
        ServiceHearts(Timing.CurTime);
    }

    private void ServiceHearts(TimeSpan now)
    {
        var query = EntityQueryEnumerator<HeartComponent, OrganHealthComponent>();
        while (query.MoveNext(out var uid, out var heart, out _))
        {
            if (GetBody(uid) is not { } body)
            {
                FreezeHeart(heart, now);
                continue;
            }
            if ((heart.Stopped ? heart.NextCardiacArrestTick : heart.NextOrganDamageTick) <= now)
                AdvanceHeart((uid, heart), body, now);
            if (Enabled && !heart.Stopped && heart.NextPulseUpdate <= now)
                UpdateDisplay((uid, heart), body, now);
        }

        var missingQuery = EntityQueryEnumerator<MissingHeartComponent>();
        while (missingQuery.MoveNext(out var uid, out var missing))
        {
            if (MedicalIndex.TryGetOrgan<HeartComponent>(uid, out _))
            {
                RemCompDeferred<MissingHeartComponent>(uid);
                continue;
            }
            AdvanceMissing((uid, missing), now);
        }
    }

    /// <summary>
    /// Settles active physiology to the current time and immediately refreshes the
    /// displayed pulse. Display cadence does not control arrest progression.
    /// </summary>
    public void TickPulse(Entity<HeartComponent?, OrganHealthComponent?> ent)
    {
        if (_net.IsClient || !Resolve(ent.Owner, ref ent.Comp1, ref ent.Comp2, logMissing: false) ||
            GetBody(ent.Owner) is not { } body)
            return;
        AdvanceHeart((ent.Owner, ent.Comp1), body, Timing.CurTime);
        RefreshPhysiology((ent.Owner, ent.Comp1), body, Timing.CurTime);
    }

    private bool CanAdvance(EntityUid body, bool ignoreStasis = false, bool ignoreBodyPause = false,
        bool wasAlive = false)
    {
        if (!Enabled || TerminatingOrDeleted(body) || (!ignoreBodyPause && _metadata.EntityPaused(body)) ||
            (!wasAlive && TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead))
            return false;
        // CMInStasis is the only timed cancellation source in this integration.
        // Synths remain non-metabolizing even at the marker's entry boundary.
        return ignoreStasis ? !HasComp<SynthComponent>(body) : Stasis.CanBodyMetabolize(body);
    }

    private void AdvanceHeart(Entity<HeartComponent> ent, EntityUid body, TimeSpan now,
        bool ignoreStasis = false, bool ignoreBodyPause = false, bool ignoreHeartPause = false,
        bool wasAlive = false)
    {
        var heart = ent.Comp;
        if ((!ignoreHeartPause && _metadata.EntityPaused(ent.Owner)) ||
            !CanAdvance(body, ignoreStasis, ignoreBodyPause, wasAlive))
        {
            FreezeHeart(heart, now);
            return;
        }

        var from = heart.LastPhysiologyUpdate;
        if (now <= from)
            return;
        // Commit the clock before damage/status events can synchronously re-enter.
        heart.LastPhysiologyUpdate = now;
        heart.NextOrganDamageTick = now + TimeSpan.FromSeconds(1);
        heart.NextCardiacArrestTick = now + TimeSpan.FromSeconds(1);
        double asphyx = 0;
        double toxin = 0;

        // There are at most three segments: pacing expiry, remaining grace, arrest.
        while (from < now)
        {
            if (heart.Stopped)
            {
                heart.NoPulseSince ??= from;
                asphyx += heart.CardiacArrestAsphyxPerSecond.Value * (now - from).TotalSeconds;
                break;
            }

            var until = heart.PacingUntil > from && heart.PacingUntil < now ? heart.PacingUntil : now;
            ReconcileGrace(heart, from);
            var grace = heart.PhysiologyStage == OrganDamageStage.Dead ? TimeSpan.Zero : heart.StopGracePeriod;
            var arrestAt = heart.BelowThresholdSince + grace;
            if (arrestAt is { } deadline && deadline <= until)
                until = deadline < from ? from : deadline;

            var seconds = (until - from).TotalSeconds;
            asphyx += heart.AsphyxPerSecond.GetValueOrDefault(heart.PhysiologyStage).Value * seconds;
            toxin += heart.ToxinPerSecond.GetValueOrDefault(heart.PhysiologyStage).Value * seconds;
            from = until;
            if (arrestAt is { } stopAt && stopAt <= until)
            {
                StopHeart(ent, body, until);
                if (TerminatingOrDeleted(ent.Owner) || TerminatingOrDeleted(body))
                    return;
                // A synchronous intervention in the committed stop event owns the
                // new state at current time; do not repeatedly arrest it in this pass.
                if (!heart.Stopped)
                    break;
            }
        }

        var asphyxDamage = TakeDamage(ref heart.AsphyxRemainder, asphyx);
        var toxinDamage = TakeDamage(ref heart.ToxinRemainder, toxin);
        if (asphyxDamage > FixedPoint2.Zero || toxinDamage > FixedPoint2.Zero)
            ApplyHeartOrganDamage(body, ent.Owner, asphyxDamage, toxinDamage);
        if (heart.Stopped && heart.NoPulseSince is { } noPulse &&
            now - noPulse >= heart.CardiacArrestUnconsciousDelay && !TerminatingOrDeleted(body))
            Status.TrySetStatusEffectDuration(body, Unconscious, TimeSpan.FromSeconds(3));
    }

    private static FixedPoint2 TakeDamage(ref double remainder, double cents)
    {
        remainder += cents;
        var whole = (int)Math.Clamp(Math.Floor(remainder + 0.0000001), 0, int.MaxValue);
        remainder -= whole;
        return FixedPoint2.FromCents(whole);
    }

    private static void FreezeHeart(HeartComponent heart, TimeSpan now)
    {
        var elapsed = now - heart.LastPhysiologyUpdate;
        if (elapsed <= TimeSpan.Zero)
            return;
        heart.LastPhysiologyUpdate = now;
        heart.BelowThresholdSince += elapsed;
        heart.NoPulseSince += elapsed;
        heart.NextPulseUpdate = OffsetDeadline(heart.NextPulseUpdate, elapsed);
        heart.NextCardiacArrestTick = OffsetDeadline(heart.NextCardiacArrestTick, elapsed);
        heart.NextOrganDamageTick = OffsetDeadline(heart.NextOrganDamageTick, elapsed);
    }

    private static TimeSpan OffsetDeadline(TimeSpan deadline, TimeSpan elapsed)
        => deadline == TimeSpan.MaxValue ? deadline : deadline + elapsed;

    private static int IntrinsicPulse(OrganDamageStage stage) => stage switch
    {
        OrganDamageStage.Bruised => 95,
        OrganDamageStage.Damaged => 50,
        OrganDamageStage.Failing => 20,
        OrganDamageStage.Dead => 0,
        _ => 70,
    };

    private static void ReconcileGrace(HeartComponent heart, TimeSpan at)
    {
        var intrinsic = IntrinsicPulse(heart.PhysiologyStage);
        if (heart.CriticalBloodVolume)
            intrinsic /= 2;
        var minimum = heart.PhysiologyStage == OrganDamageStage.Failing
            ? Math.Max(60, heart.MinBpmBeforeStop) : heart.MinBpmBeforeStop;
        if (heart.PacingUntil > at || intrinsic >= minimum)
            heart.BelowThresholdSince = null;
        else
            heart.BelowThresholdSince ??= at;
    }

    private void RefreshPhysiology(Entity<HeartComponent> ent, EntityUid body, TimeSpan now)
    {
        if (!TryComp<OrganHealthComponent>(ent, out var health))
            return;
        ent.Comp.PhysiologyStage = health.Stage;
        ent.Comp.CriticalBloodVolume = TryGetBloodFraction(body, out var fraction) && fraction < 0.4f;
        if (!ent.Comp.Stopped)
        {
            ReconcileGrace(ent.Comp, now);
            if (Enabled && health.Stage == OrganDamageStage.Dead)
                StopHeart(ent, body, now);
        }
        UpdateDisplay(ent, body, now);
    }

    private void UpdateDisplay(Entity<HeartComponent> ent, EntityUid body, TimeSpan now)
    {
        ent.Comp.NextPulseUpdate = now + ent.Comp.PulseUpdateInterval;
        var displayed = 0;
        if (!ent.Comp.Stopped && TryComp<OrganHealthComponent>(ent, out var health))
        {
            var clamped = Math.Clamp(ComputeBpm(ent.Owner, body, health, out var unstable), 0, ent.Comp.MaxBpm);
            displayed = clamped > 0 ? (unstable ? Math.Max(1, clamped + Random.Next(-3, 4)) : clamped) : 0;
            if (ent.Comp.PacingUntil > now)
                displayed = Math.Max(60, displayed);
        }
        if (ent.Comp.BeatsPerMinute == displayed)
            return;
        ent.Comp.BeatsPerMinute = displayed;
        Dirty(ent);
    }

    protected virtual int ComputeBpm(EntityUid heartUid, EntityUid body, OrganHealthComponent oh, out bool unstablePulse)
    {
        unstablePulse = oh.Stage != OrganDamageStage.Healthy;
        var baseBpm = IntrinsicPulse(oh.Stage);
        if (TryGetBloodFraction(body, out var fraction))
        {
            if (fraction < 0.7f)
            {
                unstablePulse = true;
                baseBpm += (int)((0.7f - fraction) * 100f);
            }
            if (fraction < 0.4f)
                baseBpm = (int)(baseBpm * 0.5f);
        }
        foreach (var (organId, _) in MedicalIndex.GetOrgans(body))
        {
            if (organId == heartUid || !TryComp<OrganHealthComponent>(organId, out var organHealth))
                continue;
            if (organHealth.Stage.IsAtLeast(OrganDamageStage.Bruised))
            {
                unstablePulse = true;
                baseBpm += 5;
            }
            if (organHealth.Stage.IsAtLeast(OrganDamageStage.Damaged))
                baseBpm += 10;
        }
        return baseBpm;
    }

    private bool TryGetBloodFraction(EntityUid body, out float fraction)
    {
        fraction = 0f;
        if (!Bloodstream.TryGetBloodReadout(body, out var current, out var normal) || normal <= FixedPoint2.Zero)
            return false;
        fraction = (float)current / (float)normal;
        return true;
    }

    private void StopHeart(Entity<HeartComponent> ent, EntityUid body, TimeSpan at)
    {
        if (ent.Comp.PacingUntil > at)
        {
            ent.Comp.BelowThresholdSince = null;
            return;
        }
        if (ent.Comp.Stopped)
            return;
        ent.Comp.Stopped = true;
        ent.Comp.BeatsPerMinute = 0;
        ent.Comp.NoPulseSince = at;
        ent.Comp.NextCardiacArrestTick = at;
        Dirty(ent);
        Status.TrySetStatusEffectDuration(body, CardiacArrest, duration: null);
        var ev = new HeartStoppedEvent(body, ent.Owner);
        RaiseLocalEvent(ent, ref ev);
    }

    public bool TryPrepareDefibrillation(EntityUid body, bool allowBeatingHeart,
        out CMUHeartRevivalToken? token, out string reason)
    {
        token = null;
        reason = "cmu-medical-defib-no-heart";
        if (_net.IsClient || TerminatingOrDeleted(body) || EntityManager.IsQueuedForDeletion(body) ||
            !MedicalIndex.TryGetOrgan<HeartComponent>(body, out var organ) ||
            EntityManager.IsQueuedForDeletion(organ) ||
            !TryComp<HeartComponent>(organ, out var heart) ||
            !TryComp<OrganHealthComponent>(organ, out var health) || GetBody(organ) != body)
            return false;
        reason = "cmu-medical-defib-heart-failing";
        if (health.Current <= FixedPoint2.Zero || health.Stage.IsAtLeast(OrganDamageStage.Damaged))
            return false;
        reason = "cmu-medical-defib-heart-beating";
        if (!heart.Stopped && !allowBeatingHeart)
            return false;
        token = new CMUHeartRevivalToken(body, organ, heart, health);
        return true;
    }

    public bool IsDefibrillationHeartValid(CMUHeartRevivalToken token)
    {
        return !TerminatingOrDeleted(token.Body) && !EntityManager.IsQueuedForDeletion(token.Body) &&
               !TerminatingOrDeleted(token.Heart) && !EntityManager.IsQueuedForDeletion(token.Heart) &&
               GetBody(token.Heart) == token.Body &&
               TryComp<HeartComponent>(token.Heart, out var heart) && ReferenceEquals(heart, token.HeartComponent) &&
               TryComp<OrganHealthComponent>(token.Heart, out var health) && ReferenceEquals(health, token.HealthComponent) &&
               health.Current > FixedPoint2.Zero && !health.Stage.IsAtLeast(OrganDamageStage.Damaged);
    }

    public bool TryApplyDefibrillationTrauma(CMUHeartRevivalToken token)
    {
        if (_net.IsClient || !IsDefibrillationHeartValid(token))
            return false;
        // Preserve the established 3–5 tissue damage. Eligibility itself is read-only;
        // this is reached only after all revival veto listeners have finished.
        var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(Random.Next(3, 6)) } };
        var ev = new OrganDamagedEvent(token.Body, token.Heart, damage, OrganDamageSource.Direct);
        RaiseLocalEvent(token.Heart, ref ev, broadcast: true);
        return IsDefibrillationHeartValid(token);
    }

    public bool TryCompleteDefibrillation(CMUHeartRevivalToken token)
    {
        if (!IsDefibrillationHeartValid(token))
            return false;
        TryRestartHeart((token.Heart, token.HeartComponent));
        return IsDefibrillationHeartValid(token) && !token.HeartComponent.Stopped;
    }

    public void TryRestartHeart(Entity<HeartComponent?> ent)
    {
        if (_net.IsClient || TerminatingOrDeleted(ent.Owner) || EntityManager.IsQueuedForDeletion(ent.Owner) ||
            !Resolve(ent.Owner, ref ent.Comp, logMissing: false) || !ent.Comp.Stopped ||
            GetBody(ent.Owner) is not { } body || !TryComp<OrganHealthComponent>(ent, out var health) ||
            health.Current <= FixedPoint2.Zero || TerminatingOrDeleted(body) || EntityManager.IsQueuedForDeletion(body) ||
            TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
            return;
        AdvanceHeart((ent.Owner, ent.Comp), body, Timing.CurTime);
        // Settling pressure queries public metabolism permission and can invoke tissue
        // removal/replacement callbacks. Never restart the now-detached old component.
        if (TerminatingOrDeleted(ent.Owner) || EntityManager.IsQueuedForDeletion(ent.Owner) ||
            TerminatingOrDeleted(body) || EntityManager.IsQueuedForDeletion(body) || GetBody(ent.Owner) != body ||
            !TryComp<HeartComponent>(ent.Owner, out var currentHeart) || !ReferenceEquals(currentHeart, ent.Comp) ||
            !TryComp<OrganHealthComponent>(ent.Owner, out var currentHealth) || !ReferenceEquals(currentHealth, health) ||
            health.Current <= FixedPoint2.Zero ||
            TryComp<MobStateComponent>(body, out var currentMob) && currentMob.CurrentState == MobState.Dead)
            return;
        ent.Comp.Stopped = false;
        ent.Comp.BelowThresholdSince = null;
        ent.Comp.NoPulseSince = null;
        ClearCardiacArrest(body);
        RefreshPhysiology((ent.Owner, ent.Comp), body, Timing.CurTime);
        Dirty(ent.Owner, ent.Comp);
    }

    public void ResetHeart(Entity<HeartComponent?> ent, int beatsPerMinute = 70)
    {
        if (_net.IsClient || !Resolve(ent.Owner, ref ent.Comp, logMissing: false))
            return;
        ent.Comp.Stopped = false;
        ent.Comp.BeatsPerMinute = beatsPerMinute;
        ent.Comp.BelowThresholdSince = null;
        ent.Comp.NoPulseSince = null;
        ent.Comp.LastPhysiologyUpdate = Timing.CurTime;
        ent.Comp.NextPulseUpdate = Timing.CurTime + ent.Comp.PulseUpdateInterval;
        ent.Comp.AsphyxRemainder = 0;
        ent.Comp.ToxinRemainder = 0;
        Dirty(ent.Owner, ent.Comp);
        if (GetBody(ent.Owner) is { } body)
            ClearCardiacArrest(body);
    }

    private void ClearCardiacArrest(EntityUid body)
    {
        // A same-tick restart -> arrest or insertion -> extraction must not reuse
        // the old status entity while its queued deletion still awaits a flush.
        if (Status.TryGetStatusEffect(body, CardiacArrest, out var effect))
            Del(effect.Value);
    }

    private void OnStageChanged(Entity<HeartComponent> ent, ref OrganStageChangedEvent args)
    {
        if (_net.IsClient || TerminatingOrDeleted(ent.Owner) || TerminatingOrDeleted(args.Body) ||
            GetBody(ent.Owner) != args.Body)
            return;
        // The cached stage is the preceding interval. Healing cannot retroactively
        // remove its pressure or rescue an already completed arrest grace period.
        AdvanceHeart(ent, args.Body, Timing.CurTime);
        RefreshPhysiology(ent, args.Body, Timing.CurTime);
        ReconcileRhythmStatus(args.Body);
        // A rhythm permission/removal callback can retire this exact component.
        // The current tissue projection has already reconciled its replacement.
        if (!TerminatingOrDeleted(ent.Owner) && !EntityManager.IsQueuedForDeletion(ent.Owner) &&
            TryComp<HeartComponent>(ent.Owner, out var current) && ReferenceEquals(current, ent.Comp) &&
            current.LifeStage <= ComponentLifeStage.Running)
            Dirty(ent);
    }

    private void AdvanceMissing(Entity<MissingHeartComponent> ent, TimeSpan now,
        bool ignoreStasis = false, bool ignoreBodyPause = false, bool wasAlive = false)
    {
        var elapsed = now - ent.Comp.LastCardiacArrestUpdate;
        ent.Comp.LastCardiacArrestUpdate = now;
        ent.Comp.NextCardiacArrestTick = now + TimeSpan.FromSeconds(1);
        if (elapsed <= TimeSpan.Zero || !CanAdvance(ent.Owner, ignoreStasis, ignoreBodyPause, wasAlive))
            return;
        ent.Comp.NoPulseElapsed += elapsed;
        Status.TrySetStatusEffectDuration(ent.Owner, CardiacArrest, duration: null);
        var amount = TakeDamage(ref ent.Comp.AsphyxRemainder, MissingHeartAsphyxPerSecond.Value * elapsed.TotalSeconds);
        if (amount > FixedPoint2.Zero)
            ApplyCardiacArrestAsphyx(ent.Owner, ent.Owner, amount);
        if (ent.Comp.NoPulseElapsed >= MissingHeartUnconsciousDelay && !TerminatingOrDeleted(ent.Owner))
            Status.TrySetStatusEffectDuration(ent.Owner, Unconscious, TimeSpan.FromSeconds(3));
    }

    private void AdvanceBody(EntityUid body, TimeSpan now, bool ignoreStasis = false,
        bool ignoreBodyPause = false, bool wasAlive = false)
    {
        foreach (var (uid, _) in MedicalIndex.GetOrgans(body))
        {
            if (TryComp<HeartComponent>(uid, out var heart))
                AdvanceHeart((uid, heart), body, now, ignoreStasis, ignoreBodyPause, wasAlive: wasAlive);
        }
        if (TryComp<MissingHeartComponent>(body, out var missing))
            AdvanceMissing((body, missing), now, ignoreStasis, ignoreBodyPause, wasAlive);
    }

    private void FreezeBody(EntityUid body, TimeSpan now, TimeSpan pacingPause = default)
    {
        foreach (var (uid, _) in MedicalIndex.GetOrgans(body))
        {
            if (!TryComp<HeartComponent>(uid, out var heart))
                continue;
            FreezeHeart(heart, now);
            if (heart.PacingUntil != TimeSpan.Zero)
                heart.PacingUntil += pacingPause;
            if (!heart.Stopped)
                ReconcileGrace(heart, now);
        }
        if (TryComp<MissingHeartComponent>(body, out var missing))
            missing.LastCardiacArrestUpdate = now;
    }

    // These body event slots already belong to the cardiac owner. Forward the exact
    // boundary independently of cardiac eligibility so other organs also receive it
    // when the body has no attached heart or cardiac gameplay is disabled.
    private void PublishOrganPhysiologyBoundary(EntityUid body, TimeSpan now, bool? inStasis = null, bool reset = false)
    {
        if (_net.IsClient || TerminatingOrDeleted(body) || !HasComp<CMUHumanMedicalComponent>(body))
            return;
        var boundary = new CMUOrganPhysiologyBoundaryEvent(body, now, inStasis, reset);
        RaiseLocalEvent(ref boundary);
    }

    private bool IsCurrentPhysiologyBody(Entity<BodyComponent> ent)
        => _net.IsServer && !TerminatingOrDeleted(ent.Owner) &&
           TryComp<BodyComponent>(ent.Owner, out var current) && ReferenceEquals(current, ent.Comp);

    private void OnStasisChanged(Entity<BodyComponent> ent, ref CMUMedicalStasisChangedEvent args)
    {
        PublishOrganPhysiologyBoundary(ent.Owner, args.Time, args.Active);
        if (!IsCurrentPhysiologyBody(ent))
            return;
        if (args.Active)
            AdvanceBody(ent.Owner, args.Time, ignoreStasis: true);
        else
            FreezeBody(ent.Owner, args.Time);
    }

    private void OnBodyPaused(Entity<BodyComponent> ent, ref EntityPausedEvent args)
    {
        PublishOrganPhysiologyBoundary(ent.Owner, Timing.CurTime);
        if (IsCurrentPhysiologyBody(ent))
            AdvanceBody(ent.Owner, Timing.CurTime, ignoreBodyPause: true);
    }

    private void OnBodyUnpaused(Entity<BodyComponent> ent, ref EntityUnpausedEvent args)
    {
        PublishOrganPhysiologyBoundary(ent.Owner, Timing.CurTime);
        if (IsCurrentPhysiologyBody(ent))
            FreezeBody(ent.Owner, Timing.CurTime, args.PausedTime);
    }

    private void OnHeartPaused(Entity<HeartComponent> ent, ref EntityPausedEvent args)
    {
        if (_net.IsServer && GetBody(ent.Owner) is { } body)
            AdvanceHeart(ent, body, Timing.CurTime, ignoreHeartPause: true);
    }

    private void OnHeartUnpaused(Entity<HeartComponent> ent, ref EntityUnpausedEvent args)
    {
        if (_net.IsServer)
            FreezeHeart(ent.Comp, Timing.CurTime);
    }

    private void OnMobStateChanged(Entity<BodyComponent> ent, ref MobStateChangedEvent args)
    {
        PublishOrganPhysiologyBoundary(ent.Owner, Timing.CurTime);
        if (!IsCurrentPhysiologyBody(ent) || !TryComp<MobStateComponent>(ent.Owner, out var currentState) ||
            currentState.CurrentState != args.NewMobState)
            return;
        if (args.OldMobState != MobState.Dead)
            AdvanceBody(ent.Owner, Timing.CurTime, wasAlive: true);
        else
            FreezeBody(ent.Owner, Timing.CurTime);
        if (args.NewMobState != MobState.Dead)
        {
            // Disabled organ gameplay bypasses cardiac eligibility and trauma. A
            // successful body revival must nevertheless reconcile viable tissue,
            // otherwise re-enabling the layer resurrects a stale arrest state.
            if (!Enabled && args.OldMobState == MobState.Dead)
            {
                foreach (var (uid, _) in MedicalIndex.GetOrgans(ent.Owner))
                {
                    if (TryComp<HeartComponent>(uid, out var heart))
                        TryRestartHeart((uid, heart));
                }
            }
            return;
        }
        foreach (var (uid, _) in MedicalIndex.GetOrgans(ent.Owner))
        {
            if (TryComp<HeartComponent>(uid, out var heart))
                StopHeart((uid, heart), ent.Owner, Timing.CurTime);
        }
    }

    private void OnSolutionChanged(Entity<BodyComponent> ent, ref SolutionChangedEvent args)
    {
        if (_net.IsClient || !TryComp<BloodstreamComponent>(ent, out var blood) ||
            args.Solution.Comp.Id != blood.BloodSolutionName)
            return;
        var critical = TryGetBloodFraction(ent.Owner, out var fraction) && fraction < 0.4f;
        foreach (var (uid, _) in MedicalIndex.GetOrgans(ent.Owner))
        {
            if (!TryComp<HeartComponent>(uid, out var heart) || heart.CriticalBloodVolume == critical)
                continue;
            AdvanceHeart((uid, heart), ent.Owner, Timing.CurTime);
            heart.CriticalBloodVolume = critical;
            if (!heart.Stopped)
                ReconcileGrace(heart, Timing.CurTime);
        }
    }

    private void OnPacingChanged(Entity<BodyComponent> ent, ref ChemicalCardiacPacingChangedEvent args)
        => SetPacing(ent.Owner, args.ExpiresAt);

    private void OnRejuvenate(Entity<BodyComponent> ent, ref RejuvenateEvent args)
    {
        PublishOrganPhysiologyBoundary(ent.Owner, Timing.CurTime, reset: true);
        if (!IsCurrentPhysiologyBody(ent))
            return;
        // Discard pressure before bloodstream restoration, revival or pacing
        // removal can emit ordinary mutation boundaries during an admin reset.
        foreach (var (uid, _) in MedicalIndex.GetOrgans(ent.Owner))
        {
            if (!TryComp<HeartComponent>(uid, out var heart))
                continue;
            heart.LastPhysiologyUpdate = Timing.CurTime;
            heart.AsphyxRemainder = 0;
            heart.ToxinRemainder = 0;
        }
        if (TryComp<MissingHeartComponent>(ent, out var missing))
        {
            missing.LastCardiacArrestUpdate = Timing.CurTime;
            missing.NoPulseElapsed = TimeSpan.Zero;
            missing.AsphyxRemainder = 0;
        }
    }

    private void OnPacingRemoved(Entity<ChemicalCardiacPacingComponent> ent, ref ComponentShutdown args)
        => SetPacing(ent.Owner, TimeSpan.Zero);

    private void SetPacing(EntityUid body, TimeSpan expiresAt)
    {
        if (_net.IsClient || TerminatingOrDeleted(body))
            return;
        foreach (var (uid, _) in MedicalIndex.GetOrgans(body))
        {
            if (!TryComp<HeartComponent>(uid, out var heart))
                continue;
            AdvanceHeart((uid, heart), body, Timing.CurTime);
            heart.PacingUntil = expiresAt;
            if (TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
                StopHeart((uid, heart), body, Timing.CurTime);
            if (!heart.Stopped)
                ReconcileGrace(heart, Timing.CurTime);
            UpdateDisplay((uid, heart), body, Timing.CurTime);
        }
    }

    protected virtual void ApplyCardiacArrestAsphyx(EntityUid body, EntityUid heart, FixedPoint2 amount) { }

    protected virtual void ApplyHeartOrganDamage(EntityUid body, EntityUid heart, FixedPoint2 asphyx, FixedPoint2 toxin) { }

    protected EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
