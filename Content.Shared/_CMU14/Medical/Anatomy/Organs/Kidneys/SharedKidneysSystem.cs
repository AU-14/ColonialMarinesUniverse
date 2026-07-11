using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Anatomy.Organs.Kidneys;

public abstract partial class SharedKidneysSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;
    [Dependency] protected CMStasisBagSystem Stasis = default!;

    private static readonly EntProtoId RenalFailure = "StatusEffectCMURenalFailure";
    private static readonly FixedPoint2 MissingKidneysToxinPerSecond = FixedPoint2.New(0.75);
    private const float SelfDamageScanInterval = 1f;
    private float _selfDamageScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<KidneysComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<KidneysComponent, ComponentStartup>(OnKidneysStartup);
        SubscribeLocalEvent<KidneysComponent, OrganRemovedFromBodyEvent>(OnKidneysRemovedFromBody);
        SubscribeLocalEvent<KidneysComponent, OrganAddedToBodyEvent>(OnKidneysAddedToBody);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnKidneysStartup(Entity<KidneysComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextSelfDamageTick = Timing.CurTime + TimeSpan.FromSeconds(1);
    }

    private void OnKidneysRemovedFromBody(Entity<KidneysComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (!_medicalEnabled || !_organEnabled || TerminatingOrDeleted(args.OldBody))
            return;

        var missing = EnsureComp<MissingKidneysComponent>(args.OldBody);
        missing.NextSelfDamageTick = Timing.CurTime;
        Status.TrySetStatusEffectDuration(args.OldBody, RenalFailure, duration: null);
    }

    private void OnKidneysAddedToBody(Entity<KidneysComponent> ent, ref OrganAddedToBodyEvent args)
    {
        RemCompDeferred<MissingKidneysComponent>(args.Body);
        if (TryComp<OrganHealthComponent>(ent, out var health) &&
            health.Stage.IsAtLeast(OrganDamageStage.Damaged))
        {
            Status.TrySetStatusEffectDuration(args.Body, RenalFailure, duration: null);
        }
        else
        {
            Status.TryRemoveStatusEffect(args.Body, RenalFailure);
        }
    }

    private void OnStageChanged(Entity<KidneysComponent> ent, ref OrganStageChangedEvent args)
    {
        ent.Comp.WasteFiltration = GetFiltration(args.New);
        Dirty(ent);

        var body = args.Body;
        if (args.New.IsAtLeast(OrganDamageStage.Damaged))
            Status.TrySetStatusEffectDuration(body, RenalFailure, duration: null);
        else
            Status.TryRemoveStatusEffect(body, RenalFailure);
    }

    private static float GetFiltration(OrganDamageStage stage) => stage switch
    {
        OrganDamageStage.Bruised => 0.85f,
        OrganDamageStage.Damaged => 0.6f,
        OrganDamageStage.Failing => 0.3f,
        OrganDamageStage.Dead => 0.0f,
        _ => 1.0f,
    };

    /// <summary>
    ///     Pair survival via the best functioning kidney. A body whose kidneys
    ///     were removed has no clearance; bodies without a tracked kidney or
    ///     removal marker retain the legacy 1.0 fallback.
    /// </summary>
    public float GetClearanceMultiplier(EntityUid body)
    {
        if (HasComp<MissingKidneysComponent>(body))
            return 0f;

        var best = -1f;
        foreach (var (organId, _) in MedicalIndex.GetOrgans(body))
        {
            if (!TryComp<KidneysComponent>(organId, out var kidney))
                continue;
            if (kidney.WasteFiltration > best)
                best = kidney.WasteFiltration;
        }

        return best < 0f ? 1.0f : best;
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
        var query = EntityQueryEnumerator<KidneysComponent, OrganHealthComponent>();
        while (query.MoveNext(out var uid, out var kidneys, out var oh))
        {
            if (kidneys.NextSelfDamageTick > now)
                continue;
            kidneys.NextSelfDamageTick = now + TimeSpan.FromSeconds(1);

            if (!kidneys.ToxinPerSecond.TryGetValue(oh.Stage, out var rate) || rate <= FixedPoint2.Zero)
                continue;

            var body = GetBody(uid);
            if (body is null)
                continue;

            if (TryComp<MobStateComponent>(body.Value, out var mob) && mob.CurrentState == MobState.Dead)
                continue;

            ApplyToxin(body.Value, uid, rate);
        }

        var missingQuery = EntityQueryEnumerator<MissingKidneysComponent>();
        while (missingQuery.MoveNext(out var uid, out var missing))
        {
            if (MedicalIndex.TryGetOrgan<KidneysComponent>(uid, out _))
            {
                RemCompDeferred<MissingKidneysComponent>(uid);
                continue;
            }

            if (missing.NextSelfDamageTick > now)
                continue;
            missing.NextSelfDamageTick = now + TimeSpan.FromSeconds(1);

            if (!Stasis.CanBodyMetabolize(uid))
                continue;

            if (TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Dead)
                continue;

            Status.TrySetStatusEffectDuration(uid, RenalFailure, duration: null);
            ApplyToxin(uid, uid, MissingKidneysToxinPerSecond);
        }
    }

    protected virtual void ApplyToxin(EntityUid body, EntityUid kidneys, FixedPoint2 amount)
    {
    }

    private EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
