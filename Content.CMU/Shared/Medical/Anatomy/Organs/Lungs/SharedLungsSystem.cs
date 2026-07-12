using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Lungs.Events;
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
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Anatomy.Organs.Lungs;

public abstract partial class SharedLungsSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] protected DamageableSystem Damageable = default!;
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;
    [Dependency] protected CMStasisBagSystem Stasis = default!;

    private static readonly EntProtoId PulmonaryEdema = "StatusEffectCMUPulmonaryEdema";
    private static readonly FixedPoint2 MissingLungsAsphyxPerSecond = FixedPoint2.New(5);

    private const float AsphyxScanInterval = 1f;
    private float _asphyxScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LungsComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<LungsComponent, ComponentStartup>(OnLungsStartup);
        SubscribeLocalEvent<LungsComponent, OrganRemovedFromBodyEvent>(OnLungsRemovedFromBody);
        SubscribeLocalEvent<LungsComponent, OrganAddedToBodyEvent>(OnLungsAddedToBody);
        SubscribeLocalEvent<CMUHumanMedicalComponent, LungEfficiencyMultiplyEvent>(OnEfficiencyMultiply);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnLungsStartup(Entity<LungsComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextAsphyxTick = Timing.CurTime + TimeSpan.FromSeconds(1);
        ent.Comp.NextBloodCoughCheck = Timing.CurTime + ent.Comp.BloodCoughInterval;
    }

    private void OnLungsRemovedFromBody(Entity<LungsComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;
        if (TerminatingOrDeleted(args.OldBody))
            return;

        var missing = EnsureComp<MissingLungsComponent>(args.OldBody);
        missing.NextAsphyxTick = Timing.CurTime;

        Status.TrySetStatusEffectDuration(args.OldBody, PulmonaryEdema, duration: null);
    }

    private void OnLungsAddedToBody(Entity<LungsComponent> ent, ref OrganAddedToBodyEvent args)
    {
        RemCompDeferred<MissingLungsComponent>(args.Body);

        if (ent.Comp.Efficiency >= 0.5f)
            Status.TryRemoveStatusEffect(args.Body, PulmonaryEdema);
    }

    private void OnStageChanged(Entity<LungsComponent> ent, ref OrganStageChangedEvent args)
    {
        ent.Comp.Efficiency = GetEfficiency(args.New);
        Dirty(ent);

        var body = args.Body;
        if (args.New.IsAtLeast(OrganDamageStage.Damaged))
            Status.TrySetStatusEffectDuration(body, PulmonaryEdema, duration: null);
        else
            Status.TryRemoveStatusEffect(body, PulmonaryEdema);
    }

    private static float GetEfficiency(OrganDamageStage stage) => stage switch
    {
        OrganDamageStage.Bruised => 0.85f,
        OrganDamageStage.Damaged => 0.6f,
        OrganDamageStage.Failing => 0.3f,
        OrganDamageStage.Dead => 0.0f,
        _ => 1.0f,
    };

    private void OnEfficiencyMultiply(Entity<CMUHumanMedicalComponent> ent, ref LungEfficiencyMultiplyEvent args)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;

        var best = -1f;
        foreach (var (organId, _) in MedicalIndex.GetOrgans(ent))
        {
            if (!TryComp<LungsComponent>(organId, out var lungs))
                continue;
            if (lungs.Efficiency > best)
                best = lungs.Efficiency;
        }

        if (best < 0f)
            return;

        args.Multiplier *= best;
    }

    protected void UpdateServer(float frameTime)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;

        _asphyxScanAccumulator += frameTime;
        if (_asphyxScanAccumulator < AsphyxScanInterval)
            return;
        _asphyxScanAccumulator = 0f;

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<LungsComponent, OrganHealthComponent>();
        while (query.MoveNext(out var uid, out var lungs, out var oh))
        {
            if (lungs.NextAsphyxTick > now)
                continue;
            lungs.NextAsphyxTick = now + TimeSpan.FromSeconds(1);

            var body = GetBody(uid);
            if (body is null)
                continue;

            if (TryComp<MobStateComponent>(body.Value, out var mob) && mob.CurrentState == MobState.Dead)
                continue;

            if (lungs.AsphyxPerSecond.TryGetValue(oh.Stage, out var rate) && rate > FixedPoint2.Zero)
                ApplyAsphyx(body.Value, uid, rate);

            TickBloodCough((uid, lungs, oh), body.Value, now);
        }

        var missingQuery = EntityQueryEnumerator<MissingLungsComponent>();
        while (missingQuery.MoveNext(out var uid, out var missing))
        {
            if (MedicalIndex.TryGetOrgan<LungsComponent>(uid, out _))
            {
                RemCompDeferred<MissingLungsComponent>(uid);
                continue;
            }

            TickMissingLungs((uid, missing), now);
        }
    }

    private void TickMissingLungs(Entity<MissingLungsComponent> ent, TimeSpan now)
    {
        if (ent.Comp.NextAsphyxTick > now)
            return;
        ent.Comp.NextAsphyxTick = now + TimeSpan.FromSeconds(1);

        if (!Stasis.CanBodyMetabolize(ent.Owner))
            return;

        if (TryComp<MobStateComponent>(ent.Owner, out var mob) && mob.CurrentState == MobState.Dead)
            return;

        Status.TrySetStatusEffectDuration(ent.Owner, PulmonaryEdema, duration: null);

        if (MissingLungsAsphyxPerSecond > FixedPoint2.Zero)
            ApplyAsphyx(ent.Owner, ent.Owner, MissingLungsAsphyxPerSecond);
    }

    private void TickBloodCough(
        Entity<LungsComponent, OrganHealthComponent> ent,
        EntityUid body,
        TimeSpan now)
    {
        if (ent.Comp1.NextBloodCoughCheck > now)
            return;
        ent.Comp1.NextBloodCoughCheck = now + ent.Comp1.BloodCoughInterval;

        if (!ent.Comp1.BloodCoughChance.TryGetValue(ent.Comp2.Stage, out var chance) ||
            chance <= 0f ||
            !Random.Prob(chance))
        {
            return;
        }

        ApplyBloodCough(body, ent.Owner, ent.Comp1.BloodLossPerCough);
    }

    protected virtual void ApplyAsphyx(EntityUid body, EntityUid lung, FixedPoint2 amount)
    {
    }

    protected virtual void ApplyBloodCough(EntityUid body, EntityUid lung, FixedPoint2 bloodLoss)
    {
    }

    protected EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
