using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Organs.Lungs.Events;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Human.Organs.Lungs;

public abstract partial class SharedLungsSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected SharedBodySystem Body = default!;
    [Dependency] protected DamageableSystem Damageable = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId PulmonaryEdema = "StatusEffectCMUPulmonaryEdema";
    private static readonly FixedPoint2 MissingLungsAsphyxPerSecond = FixedPoint2.New(5);

    private const float AsphyxScanInterval = 1f;
    private float _asphyxScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    private static readonly Dictionary<OrganDamageStatus, float> EfficiencyByStatus = new()
    {
        { OrganDamageStatus.None, 1.0f },
        { OrganDamageStatus.LittleBruised, 0.85f },
        { OrganDamageStatus.Bruised, 0.6f },
        { OrganDamageStatus.Broken, 0.3f },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanMedicalLedgerChangedEvent>(OnLedgerChanged);
        SubscribeLocalEvent<HumanMedicalComponent, LungEfficiencyMultiplyEvent>(OnEfficiencyMultiply);
        SubscribeLocalEvent<LungsComponent, ComponentStartup>(OnLungsStartup);
        SubscribeLocalEvent<LungsComponent, OrganRemovedFromBodyEvent>(OnLungsRemovedFromBody);
        SubscribeLocalEvent<LungsComponent, OrganAddedToBodyEvent>(OnLungsAddedToBody);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnLungsStartup(Entity<LungsComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextAsphyxTick = Timing.CurTime + TimeSpan.FromSeconds(1);
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

    private void OnLedgerChanged(ref HumanMedicalLedgerChangedEvent args)
    {
        if (!HumanOrganLedgerUtility.OrgansChanged(args.Result))
            return;

        if (!TryComp(args.Body, out HumanMedicalComponent? medical))
            return;

        SyncLungState(args.Body, medical);
    }

    private void OnEfficiencyMultiply(Entity<HumanMedicalComponent> ent, ref LungEfficiencyMultiplyEvent args)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;

        args.Multiplier *= GetEfficiency(HumanOrganLedgerUtility.BestStatus(
            ent.Comp,
            OrganSlot.LeftLung,
            OrganSlot.RightLung));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient || !_medicalEnabled || !_organEnabled)
            return;

        _asphyxScanAccumulator += frameTime;
        if (_asphyxScanAccumulator < AsphyxScanInterval)
            return;

        _asphyxScanAccumulator = 0f;
        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<HumanMedicalComponent, ActiveOrganSymptomsComponent>();
        while (query.MoveNext(out var uid, out var medical, out _))
        {
            var status = HumanOrganLedgerUtility.BestStatus(
                medical,
                OrganSlot.LeftLung,
                OrganSlot.RightLung);
            if (status == OrganDamageStatus.None)
                continue;

            foreach (var lungs in Body.GetBodyOrganEntityComps<LungsComponent>(uid))
            {
                if (lungs.Comp1.NextAsphyxTick > now)
                    continue;

                lungs.Comp1.NextAsphyxTick = now + TimeSpan.FromSeconds(1);
                Dirty(lungs.Owner, lungs.Comp1);

                if (TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Dead)
                    continue;

                ApplyBreathingSymptom(uid, lungs.Owner, status);

                if (!lungs.Comp1.AsphyxPerSecond.TryGetValue(status, out var rate) || rate <= FixedPoint2.Zero)
                    continue;

                ApplyAsphyx(uid, lungs.Owner, rate);
            }
        }

        var missingQuery = EntityQueryEnumerator<MissingLungsComponent>();
        while (missingQuery.MoveNext(out var uid, out var missing))
        {
            if (TryComp<HumanMedicalComponent>(uid, out var medical) &&
                !BothLungsMissing(medical) &&
                Body.GetBodyOrganEntityComps<LungsComponent>(uid).Count != 0)
            {
                RemCompDeferred<MissingLungsComponent>(uid);
                continue;
            }

            TickMissingLungs((uid, missing), now);
        }
    }

    private void SyncLungState(EntityUid body, HumanMedicalComponent medical)
    {
        var status = HumanOrganLedgerUtility.BestStatus(
            medical,
            OrganSlot.LeftLung,
            OrganSlot.RightLung);
        var efficiency = GetEfficiency(status);

        foreach (var lungs in Body.GetBodyOrganEntityComps<LungsComponent>(body))
        {
            lungs.Comp1.Efficiency = efficiency;
            Dirty(lungs.Owner, lungs.Comp1);
        }

        if (BothLungsMissing(medical))
        {
            var missing = EnsureComp<MissingLungsComponent>(body);
            missing.NextAsphyxTick = Timing.CurTime;
            Status.TrySetStatusEffectDuration(body, PulmonaryEdema, duration: null);
            return;
        }

        RemCompDeferred<MissingLungsComponent>(body);
        if (status >= OrganDamageStatus.Bruised)
            Status.TrySetStatusEffectDuration(body, PulmonaryEdema, duration: null);
        else
            Status.TryRemoveStatusEffect(body, PulmonaryEdema);
    }

    private void TickMissingLungs(Entity<MissingLungsComponent> ent, TimeSpan now)
    {
        if (ent.Comp.NextAsphyxTick > now)
            return;
        ent.Comp.NextAsphyxTick = now + TimeSpan.FromSeconds(1);

        if (TryComp<MobStateComponent>(ent.Owner, out var mob) && mob.CurrentState == MobState.Dead)
            return;

        Status.TrySetStatusEffectDuration(ent.Owner, PulmonaryEdema, duration: null);

        if (MissingLungsAsphyxPerSecond > FixedPoint2.Zero)
            ApplyAsphyx(ent.Owner, ent.Owner, MissingLungsAsphyxPerSecond);
    }

    protected virtual void ApplyAsphyx(EntityUid body, EntityUid lung, FixedPoint2 amount)
    {
    }

    protected virtual void ApplyBreathingSymptom(EntityUid body, EntityUid lung, OrganDamageStatus status)
    {
    }

    private static bool BothLungsMissing(HumanMedicalComponent medical)
    {
        return HumanOrganLedgerUtility.IsMissing(medical, OrganSlot.LeftLung) &&
            HumanOrganLedgerUtility.IsMissing(medical, OrganSlot.RightLung);
    }

    private static float GetEfficiency(OrganDamageStatus status)
    {
        return EfficiencyByStatus.TryGetValue(status, out var multiplier)
            ? multiplier
            : 1.0f;
    }
}
