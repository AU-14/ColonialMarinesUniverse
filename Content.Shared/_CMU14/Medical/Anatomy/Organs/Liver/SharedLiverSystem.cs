using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared._CMU14.Medical.Core;

namespace Content.Shared._CMU14.Medical.Anatomy.Organs.Liver;

public abstract partial class SharedLiverSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] protected DamageableSystem Damageable = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;
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
        SubscribeLocalEvent<LiverComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<LiverComponent, ComponentStartup>(OnLiverStartup);
        SubscribeLocalEvent<LiverComponent, OrganRemovedFromBodyEvent>(OnLiverRemovedFromBody);
        SubscribeLocalEvent<LiverComponent, OrganAddedToBodyEvent>(OnLiverAddedToBody);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnLiverStartup(Entity<LiverComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextSelfDamageTick = Timing.CurTime + TimeSpan.FromSeconds(1);
    }

    private void OnLiverRemovedFromBody(Entity<LiverComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (!_medicalEnabled || !_organEnabled || TerminatingOrDeleted(args.OldBody))
            return;

        var missing = EnsureComp<MissingLiverComponent>(args.OldBody);
        missing.NextSelfDamageTick = Timing.CurTime;
        Status.TrySetStatusEffectDuration(args.OldBody, HepaticFailure, duration: null);
    }

    private void OnLiverAddedToBody(Entity<LiverComponent> ent, ref OrganAddedToBodyEvent args)
    {
        RemCompDeferred<MissingLiverComponent>(args.Body);
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
        ent.Comp.ToxinClearMultiplier = GetClearance(args.New);
        Dirty(ent);

        var body = args.Body;
        if (args.New.IsAtLeast(OrganDamageStage.Damaged))
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

    public void ApplyBloodstreamDirectDamage(EntityUid body, string group)
    {
        if (group != "Poison" && group != "Alcohol")
            return;

        foreach (var (organId, _) in MedicalIndex.GetOrgans(body))
        {
            if (!HasComp<LiverComponent>(organId))
                continue;
            ApplyBloodstreamDirectHit(body, organId, group);
        }
    }

    protected virtual void ApplyBloodstreamDirectHit(EntityUid body, EntityUid liver, string group)
    {
    }

    protected void UpdateServer(float frameTime)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;

        _selfDamageScanAccumulator += frameTime;
        if (_selfDamageScanAccumulator < SelfDamageScanInterval)
            return;
        _selfDamageScanAccumulator = 0f;

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<LiverComponent, OrganHealthComponent>();
        while (query.MoveNext(out var uid, out var liver, out var oh))
        {
            if (liver.NextSelfDamageTick > now)
                continue;
            liver.NextSelfDamageTick = now + TimeSpan.FromSeconds(1);

            if (!liver.ToxinPerSecond.TryGetValue(oh.Stage, out var rate) || rate <= FixedPoint2.Zero)
                continue;

            var body = GetBody(uid);
            if (body is null)
                continue;

            if (TryComp<MobStateComponent>(body.Value, out var mob) && mob.CurrentState == MobState.Dead)
                continue;

            ApplyToxin(body.Value, uid, rate);
        }

        var missingQuery = EntityQueryEnumerator<MissingLiverComponent>();
        while (missingQuery.MoveNext(out var uid, out var missing))
        {
            if (MedicalIndex.TryGetOrgan<LiverComponent>(uid, out _))
            {
                RemCompDeferred<MissingLiverComponent>(uid);
                continue;
            }

            if (missing.NextSelfDamageTick > now)
                continue;
            missing.NextSelfDamageTick = now + TimeSpan.FromSeconds(1);

            if (!Stasis.CanBodyMetabolize(uid))
                continue;

            if (TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Dead)
                continue;

            Status.TrySetStatusEffectDuration(uid, HepaticFailure, duration: null);
            ApplyToxin(uid, uid, MissingLiverToxinPerSecond);
        }
    }

    protected virtual void ApplyToxin(EntityUid body, EntityUid liver, FixedPoint2 amount)
    {
    }

    protected EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
