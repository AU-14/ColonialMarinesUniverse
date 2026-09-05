using System.Linq;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Metabolism.Events;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Events;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.CMU14.Medical.Core;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;

public abstract partial class SharedLiverSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] protected DamageableSystem Damageable = default!;
    [Dependency] protected StatusEffectsSystem Status = default!;
    [Dependency] protected CMStasisBagSystem Stasis = default!;

    private static readonly EntProtoId HepaticFailure = "StatusEffectCMUHepaticFailure";
    private static readonly FixedPoint2 MissingLiverToxinPerSecond = FixedPoint2.New(1);

    private const float SelfDamageScanInterval = 1f;
    private float _selfDamageScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUOrganPhysiologyBoundaryEvent>(OnBodyPhysiologyBoundary);
        SubscribeLocalEvent<LiverComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<LiverComponent, ComponentStartup>(OnLiverStartup,
            after: new[] { typeof(SharedOrganHealthSystem) });
        SubscribeLocalEvent<LiverComponent, OrganRemovedFromBodyEvent>(OnLiverRemovedFromBody,
            after: new[] { typeof(CMUMedicalBodyIndexSystem) });
        SubscribeLocalEvent<LiverComponent, OrganAddedToBodyEvent>(OnLiverAddedToBody,
            after: new[] { typeof(CMUMedicalBodyIndexSystem) });
        SubscribeLocalEvent<LiverComponent, EntityPausedEvent>(OnOrganPaused);
        SubscribeLocalEvent<LiverComponent, EntityUnpausedEvent>(OnOrganUnpaused);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => SetLayerEnabled(ref _medicalEnabled, v), true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => SetLayerEnabled(ref _organEnabled, v), true);
    }

    private void OnLiverStartup(Entity<LiverComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient)
            return;
        ent.Comp.LastPhysiologyUpdate = Timing.CurTime;
        ent.Comp.PhysiologyActive = false;
        AdvanceOrgan(ent, GetBody(ent.Owner), Timing.CurTime);
        RefreshClearance(ent);
    }

    private void OnLiverRemovedFromBody(Entity<LiverComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (_net.IsClient)
            return;

        AdvanceOrgan(ent, args.OldBody, Timing.CurTime);
        // Fractional pressure from the old recipient cannot follow a donor.
        ent.Comp.ToxinRemainder = 0;
        if (!_medicalEnabled || !_organEnabled)
            return;

        if (PhysiologyUnavailable(ent.Owner) || PhysiologyUnavailable(args.OldBody) ||
            MedicalIndex.TryGetOrgan<LiverComponent>(args.OldBody, out _))
            return;

        var missing = EnsureComp<MissingLiverComponent>(args.OldBody);
        AdvanceMissing((args.OldBody, missing), Timing.CurTime);
        Status.TrySetStatusEffectDuration(args.OldBody, HepaticFailure, duration: null);
    }

    private void OnLiverAddedToBody(Entity<LiverComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (_net.IsClient || PhysiologyUnavailable(args.Body))
            return;

        if (TryComp<MissingLiverComponent>(args.Body, out var missing))
        {
            AdvanceMissing((args.Body, missing), Timing.CurTime);
            if (PhysiologyUnavailable(ent.Owner) || PhysiologyUnavailable(args.Body) || GetBody(ent.Owner) != args.Body)
                return;
            if (TryComp<MissingLiverComponent>(args.Body, out var currentMissing) && ReferenceEquals(currentMissing, missing))
                RemComp<MissingLiverComponent>(args.Body);
        }
        if (PhysiologyUnavailable(ent.Owner) || PhysiologyUnavailable(args.Body) || GetBody(ent.Owner) != args.Body)
            return;
        AdvanceOrgan(ent, args.Body, Timing.CurTime);
        if (PhysiologyUnavailable(ent.Owner) || PhysiologyUnavailable(args.Body) || GetBody(ent.Owner) != args.Body)
            return;
        RefreshClearance(ent);
        if (TryComp<OrganHealthComponent>(ent, out var health) &&
            health.Stage.IsAtLeast(OrganDamageStage.Damaged))
        {
            Status.TrySetStatusEffectDuration(args.Body, HepaticFailure, duration: null);
        }
        else
        {
            Status.TryRemoveStatusEffect(args.Body, HepaticFailure);
        }
    }

    private void OnStageChanged(Entity<LiverComponent> ent, ref OrganStageChangedEvent args)
    {
        if (_net.IsClient || PhysiologyUnavailable(ent.Owner) || PhysiologyUnavailable(args.Body) ||
            GetBody(ent.Owner) != args.Body)
            return;

        AdvanceOrgan(ent, args.Body, Timing.CurTime);
        if (PhysiologyUnavailable(ent.Owner) || PhysiologyUnavailable(args.Body) || GetBody(ent.Owner) != args.Body)
            return;
        RefreshClearance(ent);

        var body = args.Body;
        if (ent.Comp.PhysiologyStage.IsAtLeast(OrganDamageStage.Damaged))
            Status.TrySetStatusEffectDuration(body, HepaticFailure, duration: null);
        else
            Status.TryRemoveStatusEffect(body, HepaticFailure);
    }

    private static float GetClearance(OrganDamageStage stage) => stage switch
    {
        OrganDamageStage.Bruised => 0.8f,
        OrganDamageStage.Damaged => 0.5f,
        OrganDamageStage.Failing => 0.2f,
        OrganDamageStage.Dead => 0.0f,
        _ => 1.0f,
    };

    /// <summary>
    ///     Returns the worst-stage liver's clearance multiplier. A body whose
    ///     liver was removed has no clearance; bodies without a tracked liver
    ///     or removal marker retain the legacy 1.0 fallback.
    /// </summary>
    public float GetClearanceMultiplier(EntityUid body)
    {
        if (HasComp<MissingLiverComponent>(body))
            return 0f;

        var worst = -1f;
        foreach (var (organId, _) in MedicalIndex.GetOrgans(body))
        {
            if (!TryComp<LiverComponent>(organId, out var liver))
                continue;
            // Worst liver wins — a single failed liver poisons the system even
            // if a hypothetical second liver still works.
            if (worst < 0f || liver.ToxinClearMultiplier < worst)
                worst = liver.ToxinClearMultiplier;
        }

        return worst < 0f ? 1.0f : worst;
    }

    public void ApplyBloodstreamDirectDamage(EntityUid body, CMUMetabolismClass toxicity)
    {
        foreach (var (organId, _) in MedicalIndex.GetOrgans(body))
        {
            if (!HasComp<LiverComponent>(organId))
                continue;
            ApplyBloodstreamDirectHit(body, organId, toxicity.ToString());
        }
    }

    protected virtual void ApplyBloodstreamDirectHit(EntityUid body, EntityUid liver, string group)
    {
    }

    private bool Enabled => _medicalEnabled && _organEnabled;

    private void RefreshClearance(Entity<LiverComponent> ent)
    {
        var value = GetClearance(ent.Comp.PhysiologyStage);
        if (ent.Comp.ToxinClearMultiplier == value)
            return;
        ent.Comp.ToxinClearMultiplier = value;
        Dirty(ent);
    }

    /// <summary>Discards unserviced pressure before rejuvenation publishes ordinary healing boundaries.</summary>
    public void ResetPhysiology(EntityUid body)
    {
        if (_net.IsClient || PhysiologyUnavailable(body))
            return;
        var now = Timing.CurTime;
        foreach (var (uid, _) in MedicalIndex.GetOrgans(body).ToArray())
        {
            if (!TryComp<LiverComponent>(uid, out var organ))
                continue;
            organ.LastPhysiologyUpdate = now;
            organ.ToxinRemainder = 0;
            AdvanceOrgan((uid, organ), body, now);
        }
        if (TryComp<MissingLiverComponent>(body, out var missing))
        {
            missing.LastPhysiologyUpdate = now;
            missing.ToxinRemainder = 0;
            AdvanceMissing((body, missing), now);
        }
    }

    private void SetLayerEnabled(ref bool field, bool value)
    {
        if (field == value)
            return;
        if (_net.IsServer)
            ServicePhysiology(Timing.CurTime, force: true);
        field = value;
        if (_net.IsServer)
            ServicePhysiology(Timing.CurTime, force: true);
    }

    protected void UpdateServer(float frameTime)
    {
        if (!Enabled)
            return;
        _selfDamageScanAccumulator += frameTime;
        if (_selfDamageScanAccumulator < SelfDamageScanInterval)
            return;
        _selfDamageScanAccumulator %= SelfDamageScanInterval;
        ServicePhysiology(Timing.CurTime);
    }

    private void ServicePhysiology(TimeSpan now, bool force = false)
    {
        var query = EntityQueryEnumerator<LiverComponent, OrganHealthComponent>();
        while (query.MoveNext(out var uid, out var organ, out _))
        {
            if (force || organ.NextSelfDamageTick <= now)
                AdvanceOrgan((uid, organ), GetBody(uid), now);
        }

        var missingQuery = EntityQueryEnumerator<MissingLiverComponent>();
        while (missingQuery.MoveNext(out var uid, out var missing))
        {
            if (MedicalIndex.TryGetOrgan<LiverComponent>(uid, out _))
            {
                AdvanceMissing((uid, missing), now);
                if (!PhysiologyUnavailable(uid) && MedicalIndex.TryGetOrgan<LiverComponent>(uid, out _) &&
                    TryComp<MissingLiverComponent>(uid, out var currentMissing) && ReferenceEquals(currentMissing, missing))
                    RemComp<MissingLiverComponent>(uid);
                continue;
            }
            if (force || missing.NextSelfDamageTick <= now)
            {
                AdvanceMissing((uid, missing), now);
                if (!PhysiologyUnavailable(uid) && missing.PhysiologyActive &&
                    TryComp<MissingLiverComponent>(uid, out var currentMissing) && ReferenceEquals(currentMissing, missing))
                    Status.TrySetStatusEffectDuration(uid, HepaticFailure, duration: null);
            }
        }
    }

    private bool PhysiologyUnavailable(EntityUid uid)
        => TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid);
    private bool CanAdvanceBody(EntityUid body, bool? inStasis)
    {
        if (!Enabled || PhysiologyUnavailable(body) || _metadata.EntityPaused(body) ||
            TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
            return false;
        // The marker is still queryable during its shutdown callback.
        return inStasis is { } stasis
            ? !stasis && !HasComp<SynthComponent>(body)
            : Stasis.CanBodyMetabolize(body);
    }

    private void AdvanceOrgan(Entity<LiverComponent> ent, EntityUid? body, TimeSpan now, bool? inStasis = null)
    {
        var organ = ent.Comp;
        var revision = organ.PhysiologyRevision;
        var attachedBody = GetBody(ent.Owner);
        if (!TryComp<OrganHealthComponent>(ent, out var health))
            return;
        // Permission is a public callback boundary. Do not claim elapsed time until
        // it returns: nested healing/removal can settle it, while rejuvenation discards it.
        var active = body is { } current && attachedBody == current &&
            !PhysiologyUnavailable(ent.Owner) && !_metadata.EntityPaused(ent.Owner) &&
            CanAdvanceBody(current, inStasis);
        if (organ.PhysiologyRevision != revision || PhysiologyUnavailable(ent.Owner) ||
            body is { } recipient && PhysiologyUnavailable(recipient) ||
            !TryComp<LiverComponent>(ent, out var currentOrgan) || !ReferenceEquals(currentOrgan, organ) ||
            GetBody(ent.Owner) != attachedBody || !TryComp<OrganHealthComponent>(ent, out var currentHealth) ||
            !ReferenceEquals(currentHealth, health))
            return;

        var elapsed = now - organ.LastPhysiologyUpdate;
        var wasActive = organ.PhysiologyActive;
        var oldStage = organ.PhysiologyStage;
        // Commit the interval and its new state before toxin callbacks can reenter.
        organ.LastPhysiologyUpdate = now;
        organ.NextSelfDamageTick = now + TimeSpan.FromSeconds(1);
        organ.PhysiologyRevision++;
        organ.PhysiologyStage = health.Stage;
        organ.PhysiologyActive = active;
        if (!wasActive || elapsed <= TimeSpan.Zero || body is not { } patient ||
            PhysiologyUnavailable(ent.Owner) || PhysiologyUnavailable(patient) ||
            !organ.ToxinPerSecond.TryGetValue(oldStage, out var rate) || rate <= FixedPoint2.Zero)
            return;

        var amount = TakeToxin(ref organ.ToxinRemainder, rate.Value * elapsed.TotalSeconds);
        if (amount > FixedPoint2.Zero)
            ApplyToxin(patient, ent.Owner, amount);
    }

    private void AdvanceMissing(Entity<MissingLiverComponent> ent, TimeSpan now, bool? inStasis = null)
    {
        var revision = ent.Comp.PhysiologyRevision;
        var active = !MedicalIndex.TryGetOrgan<LiverComponent>(ent.Owner, out _) && CanAdvanceBody(ent.Owner, inStasis);
        if (ent.Comp.PhysiologyRevision != revision || PhysiologyUnavailable(ent.Owner) ||
            !TryComp<MissingLiverComponent>(ent.Owner, out var currentMissing) || !ReferenceEquals(currentMissing, ent.Comp))
            return;
        var elapsed = now - ent.Comp.LastPhysiologyUpdate;
        var wasActive = ent.Comp.PhysiologyActive;
        ent.Comp.LastPhysiologyUpdate = now;
        ent.Comp.NextSelfDamageTick = now + TimeSpan.FromSeconds(1);
        ent.Comp.PhysiologyRevision++;
        ent.Comp.PhysiologyActive = active;
        if (!wasActive || elapsed <= TimeSpan.Zero || PhysiologyUnavailable(ent.Owner))
            return;
        var amount = TakeToxin(ref ent.Comp.ToxinRemainder, MissingLiverToxinPerSecond.Value * elapsed.TotalSeconds);
        if (amount > FixedPoint2.Zero)
            ApplyToxin(ent.Owner, ent.Owner, amount);
    }

    private static FixedPoint2 TakeToxin(ref double remainder, double cents)
    {
        remainder += cents;
        var whole = (int)Math.Clamp(Math.Floor(remainder + 0.0000001), 0, int.MaxValue);
        remainder -= whole;
        return FixedPoint2.FromCents(whole);
    }

    private void AdvanceBody(EntityUid body, TimeSpan now, bool? inStasis = null)
    {
        if (_net.IsClient || PhysiologyUnavailable(body))
            return;
        foreach (var (uid, _) in MedicalIndex.GetOrgans(body).ToArray())
        {
            if (TryComp<LiverComponent>(uid, out var organ))
                AdvanceOrgan((uid, organ), body, now, inStasis);
        }
        if (TryComp<MissingLiverComponent>(body, out var missing))
            AdvanceMissing((body, missing), now, inStasis);
    }

    private void OnBodyPhysiologyBoundary(ref CMUOrganPhysiologyBoundaryEvent args)
    {
        if (_net.IsClient || PhysiologyUnavailable(args.Body) || !HasComp<CMUHumanMedicalComponent>(args.Body))
            return;
        if (args.Reset)
            ResetPhysiology(args.Body);
        else
            AdvanceBody(args.Body, args.Time, args.InStasis);
    }

    private void OnOrganPaused(Entity<LiverComponent> ent, ref EntityPausedEvent args)
    {
        if (_net.IsServer)
            AdvanceOrgan(ent, GetBody(ent.Owner), Timing.CurTime);
    }

    private void OnOrganUnpaused(Entity<LiverComponent> ent, ref EntityUnpausedEvent args)
    {
        if (_net.IsServer)
            AdvanceOrgan(ent, GetBody(ent.Owner), Timing.CurTime);
    }
    protected virtual void ApplyToxin(EntityUid body, EntityUid liver, FixedPoint2 amount)
    {
    }

    protected EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
