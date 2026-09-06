using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Bones.Events;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Pain.Events;
using Content.Shared.CMU14.Medical.Injuries;
using Content.Shared.Body.Part;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Content.Shared.CMU14.Chemistry.Effects;

namespace Content.Shared.CMU14.Medical.Injuries.Pain.Penalties;

public abstract partial class SharedCMUMedicalSpeedSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected SharedFractureSystem Fracture = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] private SharedLungsSystem _lungs = default!;
    [Dependency] protected MovementSpeedModifierSystem Movement = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected SharedPainShockSystem Pain = default!;
    [Dependency] protected IGameTiming Timing = default!;

    private bool _medicalEnabled;
    private bool _statusEffectsEnabled;
    private bool _configurationRefreshPending;

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
        SubscribeLocalEvent<CMUMedicalChangedEvent>(OnMedicalChanged);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => SetLayerEnabled(ref _medicalEnabled, v), true);
        Cfg.OnValueChanged(CMUMedicalCCVars.StatusEffectsEnabled, v => SetLayerEnabled(ref _statusEffectsEnabled, v), true);
    }

    private void SetLayerEnabled(ref bool field, bool enabled)
    {
        if (field == enabled)
            return;
        field = enabled;
        if (!Net.IsClient)
            _configurationRefreshPending = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_configurationRefreshPending)
            return;
        _configurationRefreshPending = false;

        // Other penalty consumers have their own CVar callbacks. Refresh after
        // those callbacks finish so held-gun caches use the new configuration too.
        // Paused patients also need current projections when the layer is toggled.
        var query = EntityManager.AllEntityQueryEnumerator<CMUHumanMedicalComponent>();
        while (query.MoveNext(out var body, out _))
        {
            if (!TerminatingOrDeleted(body))
                RefreshAggregatedPenalties(body);
        }
    }

    public bool IsLayerEnabled()
    {
        return _medicalEnabled && _statusEffectsEnabled;
    }

    // ---- Lifecycle refresh fan-in ---------------------------------------

    private void OnMedicalChanged(ref CMUMedicalChangedEvent args)
    {
        // Anatomy construction, transplantation and organ-stage effects finish
        // before this coalesced notification. Do not retain a startup "no lungs"
        // penalty or wait for an unrelated pain/drug change to refresh the cache.
        if ((args.Changes & (CMUMedicalChangeFlags.Topology | CMUMedicalChangeFlags.OrganStage)) != 0 &&
            !TerminatingOrDeleted(args.Body))
            RefreshAggregatedPenalties(args.Body);
    }

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
        if (!IsLayerEnabled())
            return 1f;
        var mult = 1f;
        TryComp(body, out CMUMedicalResilienceComponent? resilience);

        foreach (var (partUid, partComp) in MedicalIndex.GetBodyParts(body))
        {
            if (partComp.PartType is not (BodyPartType.Leg or BodyPartType.Foot))
                continue;
            if (TryComp<FractureComponent>(partUid, out var frac))
            {
                var sev = Fracture.GetEffectiveSeverity((partUid, frac));
                var minimumSeverity = resilience?.MinimumPenalizingFractureSeverity ?? FractureSeverity.Hairline;
                if (sev.IsAtLeast(minimumSeverity))
                    mult *= (float)FractureProfile.Get(sev).MovementMult;
            }
            if (TryComp<CMUCastComponent>(partUid, out var cast) && cast.ImmobilizesLimb)
                mult *= 0.5f;
        }

        if (TryComp<PainShockComponent>(body, out var pain))
            mult *= CMUPainTierPenaltyMultipliers.GetMovementMultiplier(Pain.GetEffectiveTier(body, pain));

        if (!_lungs.TryGetRespiratoryCapacity(body, out var capacity) || capacity.Efficiency < 0.5f)
            mult *= 0.85f;

        if (HasComp<RecoveringFromSurgeryComponent>(body))
            mult = MathF.Min(mult, 0.7f);

        return MathF.Max(mult, resilience?.MovementPenaltyFloor ?? 0.20f);
    }

    public float ComputeAimSwayMultiplier(EntityUid body)
    {
        var nerveMultiplier = GetNerveStimulationMultiplier(body);
        if (!IsLayerEnabled())
            return nerveMultiplier;
        var mult = 1f;
        TryComp(body, out CMUMedicalResilienceComponent? resilience);

        foreach (var (partUid, partComp) in MedicalIndex.GetBodyParts(body))
        {
            if (partComp.PartType is not (BodyPartType.Arm or BodyPartType.Hand))
                continue;
            if (!TryComp<FractureComponent>(partUid, out var frac))
                continue;
            var sev = Fracture.GetEffectiveSeverity((partUid, frac));
            var minimumSeverity = resilience?.MinimumPenalizingFractureSeverity ?? FractureSeverity.Hairline;
            if (sev.IsAtLeast(minimumSeverity))
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

        return MathF.Min(mult * nerveMultiplier, resilience?.AimPenaltyCeiling ?? 2.5f);
    }

    public float ComputeActionSpeedMultiplier(EntityUid body)
    {
        var nerveMultiplier = GetNerveStimulationMultiplier(body);
        if (!IsLayerEnabled())
            return nerveMultiplier;
        var mult = 1f;
        TryComp(body, out CMUMedicalResilienceComponent? resilience);

        foreach (var organ in MedicalIndex.GetOrgans(body))
        {
            if (TryComp<CMUBrainComponent>(organ.Owner, out var brain) && brain.ActionSpeedMultiplier > 0f)
                mult *= 1f / brain.ActionSpeedMultiplier;
        }

        if (TryComp<PainShockComponent>(body, out var pain))
            mult *= CMUPainTierPenaltyMultipliers.GetActionSpeedMultiplier(Pain.GetEffectiveTier(body, pain));

        return MathF.Min(mult * nerveMultiplier, resilience?.ActionSpeedPenaltyCeiling ?? 3.0f);
    }

    private float GetNerveStimulationMultiplier(EntityUid body)
    {
        return TryComp<ChemicalNerveStimulationComponent>(body, out var nerve) &&
               nerve.LifeStage <= ComponentLifeStage.Running
            ? MathF.Max(0.7f, 1f - nerve.Strength * 0.1f)
            : 1f;
    }
}
