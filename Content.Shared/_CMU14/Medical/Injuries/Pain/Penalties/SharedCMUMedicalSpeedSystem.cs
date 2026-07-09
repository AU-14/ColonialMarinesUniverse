using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Bones.Events;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared._CMU14.Medical.Injuries.Pain.Events;
using Content.Shared.Body.Part;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Injuries.Pain.Penalties;

public abstract partial class SharedCMUMedicalSpeedSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected SharedFractureSystem Fracture = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] protected MovementSpeedModifierSystem Movement = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected SharedPainShockSystem Pain = default!;
    [Dependency] protected IGameTiming Timing = default!;

    private bool _medicalEnabled;
    private bool _statusEffectsEnabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUHumanMedicalComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovement);

        SubscribeLocalEvent<BoneFracturedEvent>(OnBoneFractured);
        SubscribeLocalEvent<FractureSeverityChangedEvent>(OnFractureSeverityChanged);
        SubscribeLocalEvent<CMUSplintChangedEvent>(OnSplintChanged);
        SubscribeLocalEvent<CMUCastChangedEvent>(OnCastChanged);
        SubscribeLocalEvent<PainShockComponent, ComponentStartup>(OnPainStartup);
        SubscribeLocalEvent<PainTierChangedEvent>(OnPainTierChanged);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.StatusEffectsEnabled, v => _statusEffectsEnabled = v, true);
    }

    public bool IsLayerEnabled()
    {
        return _medicalEnabled && _statusEffectsEnabled;
    }

    // ---- Lifecycle refresh fan-in ---------------------------------------

    private void OnBoneFractured(ref BoneFracturedEvent args)
    {
        RefreshAggregatedPenalties(args.Body);
    }

    private void OnFractureSeverityChanged(ref FractureSeverityChangedEvent args)
    {
        RefreshAggregatedPenalties(args.Body);
    }

    // Lifecycle handlers fire on the client during PVS state apply too. The aggregated
    // results (CMUAimAccuracyComponent, MovementSpeedModifierComponent) are networked,
    // so recomputing on state-replay is pure burn — and bursts hard when several injured
    // mobs come back into view at once. Skip the recompute during state apply.
    private void OnSplintChanged(ref CMUSplintChangedEvent args)
    {
        if (Timing.ApplyingState)
            return;
        RefreshForPart(args.Part);
    }

    private void OnCastChanged(ref CMUCastChangedEvent args)
    {
        if (Timing.ApplyingState)
            return;
        RefreshForPart(args.Part);
    }

    private void OnPainStartup(Entity<PainShockComponent> ent, ref ComponentStartup _)
    {
        if (Timing.ApplyingState)
            return;
        RefreshAggregatedPenalties(ent.Owner);
    }

    private void OnPainTierChanged(ref PainTierChangedEvent args)
        => RefreshAggregatedPenalties(args.Body);

    private void RefreshForPart(EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp) || partComp.Body is not { } body)
            return;
        RefreshAggregatedPenalties(body);
    }

    private void OnRefreshMovement(Entity<CMUHumanMedicalComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (Net.IsClient)
            return;
        if (!IsLayerEnabled())
            return;
        var mult = ComputeMovementMultiplier(ent.Owner);
        args.ModifySpeed(mult, mult);
    }

    public virtual void RefreshAggregatedPenalties(EntityUid body)
    {
        if (Net.IsClient)
            return;
        if (!HasComp<CMUHumanMedicalComponent>(body))
            return;

        var aim = EnsureComp<CMUAimAccuracyComponent>(body);
        var multiplier = ComputeAimSwayMultiplier(body);
        if (aim.SwayMultiplier != multiplier || aim.SpreadMultiplier != multiplier)
        {
            aim.SwayMultiplier = multiplier;
            aim.SpreadMultiplier = multiplier;
            Dirty(body, aim);
        }

        Movement.RefreshMovementSpeedModifiers(body);
        RefreshAimDependentWeapons(body);
    }

    protected virtual void RefreshAimDependentWeapons(EntityUid body)
    {
    }

    public float ComputeMovementMultiplier(EntityUid body)
    {
        var mult = 1f;

        foreach (var (partUid, partComp) in MedicalIndex.GetBodyParts(body))
        {
            if (partComp.PartType is not (BodyPartType.Leg or BodyPartType.Foot))
                continue;
            if (TryComp<FractureComponent>(partUid, out var frac))
            {
                var sev = Fracture.GetEffectiveSeverity((partUid, frac));
                if (sev != FractureSeverity.None)
                    mult *= (float)FractureProfile.Get(sev).MovementMult;
            }
            if (TryComp<CMUCastComponent>(partUid, out var cast) && cast.ImmobilizesLimb)
                mult *= 0.5f;
        }

        if (TryComp<PainShockComponent>(body, out var pain))
            mult *= CMUPainTierPenaltyMultipliers.GetMovementMultiplier(Pain.GetEffectiveTier(body, pain));

        var hasLungs = false;
        var impairedLungs = false;
        foreach (var organ in MedicalIndex.GetOrgans(body))
        {
            if (!TryComp<LungsComponent>(organ.Owner, out var lungs))
                continue;

            hasLungs = true;
            impairedLungs |= lungs.Efficiency < 0.5f;
        }

        if (!hasLungs || impairedLungs)
            mult *= 0.85f;

        if (HasComp<RecoveringFromSurgeryComponent>(body))
            mult = MathF.Min(mult, 0.7f);

        return MathF.Max(mult, 0.20f);
    }

    public float ComputeAimSwayMultiplier(EntityUid body)
    {
        var mult = 1f;

        foreach (var (partUid, partComp) in MedicalIndex.GetBodyParts(body))
        {
            if (partComp.PartType is not (BodyPartType.Arm or BodyPartType.Hand))
                continue;
            if (!TryComp<FractureComponent>(partUid, out var frac))
                continue;
            var sev = Fracture.GetEffectiveSeverity((partUid, frac));
            if (sev != FractureSeverity.None)
                mult *= (float)FractureProfile.Get(sev).AimSwayMult;
        }

        if (TryComp<PainShockComponent>(body, out var pain))
            mult *= CMUPainTierPenaltyMultipliers.GetAimSwayMultiplier(Pain.GetEffectiveTier(body, pain));

        foreach (var organ in MedicalIndex.GetOrgans(body))
        {
            if (!HasComp<EyesComponent>(organ.Owner))
                continue;
            if (!TryComp<OrganHealthComponent>(organ.Owner, out var oh))
                continue;
            mult *= oh.Stage switch
            {
                OrganDamageStage.Damaged => 1.10f,
                OrganDamageStage.Failing => 1.30f,
                OrganDamageStage.Dead => 2.00f,
                _ => 1f,
            };
        }

        return MathF.Min(mult, 2.5f);
    }

    public float ComputeActionSpeedMultiplier(EntityUid body)
    {
        var mult = 1f;

        foreach (var organ in MedicalIndex.GetOrgans(body))
        {
            if (TryComp<CMUBrainComponent>(organ.Owner, out var brain) && brain.ActionSpeedMultiplier > 0f)
                mult *= 1f / brain.ActionSpeedMultiplier;
        }

        if (TryComp<PainShockComponent>(body, out var pain))
            mult *= CMUPainTierPenaltyMultipliers.GetActionSpeedMultiplier(Pain.GetEffectiveTier(body, pain));

        return MathF.Min(mult, 3.0f);
    }
}
