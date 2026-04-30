using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Bones.Events;
using Content.Shared._CMU14.Medical.Items;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.Organs.Lungs;
using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared._CMU14.Medical.StatusEffects.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Penalties;

public abstract class SharedCMUMedicalSpeedSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedFractureSystem Fracture = default!;
    [Dependency] protected readonly MovementSpeedModifierSystem Movement = default!;
    [Dependency] protected readonly SharedPainShockSystem Pain = default!;

    private bool _medicalEnabled;
    private bool _statusEffectsEnabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUHumanMedicalComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovement);

        SubscribeLocalEvent<BoneFracturedEvent>(OnBoneFractured);
        SubscribeLocalEvent<CMUSplintedComponent, ComponentStartup>(OnSplintStartup);
        SubscribeLocalEvent<CMUSplintedComponent, ComponentRemove>(OnSplintRemove);
        SubscribeLocalEvent<CMUCastComponent, ComponentStartup>(OnCastStartup);
        SubscribeLocalEvent<CMUCastComponent, ComponentRemove>(OnCastRemove);
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

    // Lifecycle handlers fire on the client during PVS state apply too. The aggregated
    // results (CMUAimAccuracyComponent, MovementSpeedModifierComponent) are networked,
    // so recomputing on state-replay is pure burn — and bursts hard when several injured
    // mobs come back into view at once. Skip the recompute during state apply.
    private void OnSplintStartup(Entity<CMUSplintedComponent> ent, ref ComponentStartup _)
    {
        if (Timing.ApplyingState)
            return;
        RefreshForPart(ent.Owner);
    }

    private void OnSplintRemove(Entity<CMUSplintedComponent> ent, ref ComponentRemove _)
    {
        if (Timing.ApplyingState)
            return;
        RefreshForPart(ent.Owner);
    }

    private void OnCastStartup(Entity<CMUCastComponent> ent, ref ComponentStartup _)
    {
        if (Timing.ApplyingState)
            return;
        RefreshForPart(ent.Owner);
    }

    private void OnCastRemove(Entity<CMUCastComponent> ent, ref ComponentRemove _)
    {
        if (Timing.ApplyingState)
            return;
        RefreshForPart(ent.Owner);
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
        if (!IsLayerEnabled())
            return;
        var mult = ComputeMovementMultiplier(ent.Owner);
        args.ModifySpeed(mult, mult);
    }

    public void RefreshAggregatedPenalties(EntityUid body)
    {
        if (!HasComp<CMUHumanMedicalComponent>(body))
            return;

        var aim = EnsureComp<CMUAimAccuracyComponent>(body);
        aim.SwayMultiplier = ComputeAimSwayMultiplier(body);
        aim.SpreadMultiplier = aim.SwayMultiplier;
        Dirty(body, aim);

        Movement.RefreshMovementSpeedModifiers(body);
    }

    public float ComputeMovementMultiplier(EntityUid body)
    {
        var mult = 1f;

        foreach (var (partUid, partComp) in Body.GetBodyChildren(body))
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
        {
            mult *= Pain.GetEffectiveTier(body, pain) switch
            {
                PainTier.None => 1.00f,
                PainTier.Mild => 0.97f,
                PainTier.Moderate => 0.92f,
                PainTier.Severe => 0.85f,
                PainTier.Shock => 0.70f,
                _ => 1f,
            };
        }

        foreach (var organ in Body.GetBodyOrgans(body))
        {
            if (TryComp<LungsComponent>(organ.Id, out var lungs) && lungs.Efficiency < 0.5f)
            {
                mult *= 0.85f;
                break;
            }
        }

        if (HasComp<RecoveringFromSurgeryComponent>(body))
            mult = MathF.Min(mult, 0.7f);

        return MathF.Max(mult, 0.20f);
    }

    public float ComputeAimSwayMultiplier(EntityUid body)
    {
        var mult = 1f;

        foreach (var (partUid, partComp) in Body.GetBodyChildren(body))
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
        {
            mult *= Pain.GetEffectiveTier(body, pain) switch
            {
                PainTier.None => 1.00f,
                PainTier.Mild => 1.05f,
                PainTier.Moderate => 1.15f,
                PainTier.Severe => 1.40f,
                PainTier.Shock => 1.80f,
                _ => 1f,
            };
        }

        foreach (var organ in Body.GetBodyOrgans(body))
        {
            if (!HasComp<Organs.Eyes.EyesComponent>(organ.Id))
                continue;
            if (!TryComp<OrganHealthComponent>(organ.Id, out var oh))
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

        foreach (var organ in Body.GetBodyOrgans(body))
        {
            if (TryComp<Organs.Brain.CMUBrainComponent>(organ.Id, out var brain) && brain.ActionSpeedMultiplier > 0f)
                mult *= 1f / brain.ActionSpeedMultiplier;
        }

        if (TryComp<PainShockComponent>(body, out var pain))
        {
            mult *= Pain.GetEffectiveTier(body, pain) switch
            {
                PainTier.None => 1.00f,
                PainTier.Mild => 1.05f,
                PainTier.Moderate => 1.15f,
                PainTier.Severe => 1.30f,
                PainTier.Shock => 1.50f,
                _ => 1f,
            };
        }

        return MathF.Min(mult, 3.0f);
    }
}
