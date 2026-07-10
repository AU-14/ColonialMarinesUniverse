using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Conditions;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Effects;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared._CMU14.Medical.Injuries.Shrapnel;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Content.Shared._CMU14.Medical.Core;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

/// <summary>
///     Side effects that need server-only state mutation (organ extract,
///     status-effect attach, internal-bleed clear) are virtualised through
///     hooks the sealed server subclass overrides; the shared default no-ops
///     so prediction rollback can't re-apply state on the client.
/// </summary>
public abstract partial class SharedCMUSurgerySystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected SharedBodySystem Body = default!;
    [Dependency] protected SharedBoneSystem Bone = default!;
    [Dependency] protected SharedFractureSystem Fracture = default!;
    [Dependency] protected SharedHeartSystem Heart = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] protected SharedOrganHealthSystem OrganHealth = default!;
    [Dependency] protected SharedCMUSurgicalTraitSystem SurgicalTraits = default!;
    [Dependency] protected SharedCMUShrapnelSystem Shrapnel = default!;
    [Dependency] protected SharedCMUWoundsSystem Wounds = default!;

    private bool _medicalEnabled;
    private bool _surgeryEnabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUFracturedSurgeryConditionComponent, CMSurgeryValidEvent>(OnFracturedValid);
        SubscribeLocalEvent<CMUOrganDamagedSurgeryConditionComponent, CMSurgeryValidEvent>(OnOrganDamagedValid);
        SubscribeLocalEvent<CMUOrganDamagedSurgeryConditionComponent, CMSurgeryStepCompleteCheckEvent>(OnOrganDamagedCompleteCheck);
        SubscribeLocalEvent<CMUInternalBleedingSurgeryConditionComponent, CMSurgeryValidEvent>(OnInternalBleedingValid);
        SubscribeLocalEvent<CMUEscharSurgeryConditionComponent, CMSurgeryValidEvent>(OnEscharValid);
        SubscribeLocalEvent<CMUSurgicalTraitConditionComponent, CMSurgeryValidEvent>(OnSurgicalTraitValid);
        SubscribeLocalEvent<CMUSurgicalTraitConditionComponent, CMSurgeryStepCompleteCheckEvent>(OnSurgicalTraitCompleteCheck);

        SubscribeLocalEvent<CMUSurgeryStepRemoveOrganEffectComponent, CMSurgeryStepEvent>(OnRemoveOrganStep);
        SubscribeLocalEvent<CMUSurgeryStepReinsertOrganEffectComponent, CMSurgeryStepEvent>(OnReinsertOrganStep);
        SubscribeLocalEvent<CMUSurgeryStepSetBoneEffectComponent, CMSurgeryStepEvent>(OnSetBoneStep);
        SubscribeLocalEvent<CMUSurgeryStepSetBoneEffectComponent, CMSurgeryStepCompleteCheckEvent>(OnSetBoneCompleteCheck);
        SubscribeLocalEvent<CMUSurgeryStepRepairOrganEffectComponent, CMSurgeryStepEvent>(OnRepairOrganStep);
        SubscribeLocalEvent<CMUSurgeryStepCauterizeBleedEffectComponent, CMSurgeryStepEvent>(OnCauterizeBleedStep);
        SubscribeLocalEvent<CMUSurgeryStepReattachLimbEffectComponent, CMSurgeryStepEvent>(OnReattachLimbStep);
        SubscribeLocalEvent<CMUSurgeryStepRemoveLimbEffectComponent, CMSurgeryStepEvent>(OnRemoveLimbStep);
        SubscribeLocalEvent<CMUSurgeryStepDebrideEscharEffectComponent, CMSurgeryStepEvent>(OnDebrideEscharStep);
        SubscribeLocalEvent<CMUSurgeryStepResolveTraitEffectComponent, CMSurgeryStepEvent>(OnResolveSurgicalTraitStep);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.SurgeryEnabled, v => _surgeryEnabled = v, true);
    }

    public bool IsSurgeryEnabled()
    {
        return _medicalEnabled && _surgeryEnabled;
    }

    private void OnFracturedValid(Entity<CMUFracturedSurgeryConditionComponent> ent, ref CMSurgeryValidEvent args)
    {
        if (!TryComp<FractureComponent>(args.Part, out var frac))
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.RequireSeverity is { } req && frac.Severity != req)
            args.Cancelled = true;

        if (ent.Comp.RequireAtLeast is { } min && !frac.Severity.IsAtLeast(min))
            args.Cancelled = true;

        if (ent.Comp.RequireAtMost is { } max && frac.Severity.IsAtLeast(max) && frac.Severity != max)
            args.Cancelled = true;
    }

    private void OnOrganDamagedValid(Entity<CMUOrganDamagedSurgeryConditionComponent> ent, ref CMSurgeryValidEvent args)
    {
        if (!TryGetOrganInSlot(args.Part, ent.Comp.OrganSlot, out var organ))
        {
            args.Cancelled = true;
            return;
        }

        if (!TryComp<OrganHealthComponent>(organ, out var oh) || !oh.Stage.IsAtLeast(ent.Comp.MinStage))
            args.Cancelled = true;
    }

    // Without this, repair-organ steps look "complete" to GetNextStep
    // because they have no Add/Remove markers — the framework's default
    // OnToolCheck cancels nothing, the step is silently skipped, and the
    // next walk regresses to PriseOpenBones once CloseBones removes
    // CMRibcageOpen. Symptom: BUI cycles between steps 5–7 (1-indexed)
    // and the organ never heals.
    private void OnOrganDamagedCompleteCheck(Entity<CMUOrganDamagedSurgeryConditionComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (args.Cancelled)
            return;
        if (!TryGetOrganInSlot(args.Part, ent.Comp.OrganSlot, out var organ))
            return;
        if (!TryComp<OrganHealthComponent>(organ, out var oh))
            return;
        if (oh.Stage.IsAtLeast(ent.Comp.MinStage))
            args.Cancelled = true;
    }

    private void OnInternalBleedingValid(Entity<CMUInternalBleedingSurgeryConditionComponent> ent, ref CMSurgeryValidEvent args)
    {
        if (!HasComp<InternalBleedingComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnEscharValid(Entity<CMUEscharSurgeryConditionComponent> ent, ref CMSurgeryValidEvent args)
    {
        if (!HasComp<CMUEscharComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnSurgicalTraitValid(Entity<CMUSurgicalTraitConditionComponent> ent, ref CMSurgeryValidEvent args)
    {
        if (!SurgicalTraits.HasTrait(args.Part, ent.Comp.Trait))
            args.Cancelled = true;
    }

    private void OnSurgicalTraitCompleteCheck(Entity<CMUSurgicalTraitConditionComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (args.Cancelled)
            return;
        if (SurgicalTraits.HasTrait(args.Part, ent.Comp.Trait))
            args.Cancelled = true;
    }

    private void OnRemoveOrganStep(Entity<CMUSurgeryStepRemoveOrganEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!IsSurgeryEnabled())
            return;
        if (!TryGetOrganInSlot(args.Part, ent.Comp.OrganSlot, out var organ))
            return;

        Body.RemoveOrgan(organ);

        ApplyOrganRemovalSideEffects(args.User, args.Body, organ, ent.Comp.OrganSlot);

        Wounds.RecomputeInternalBleed(args.Part);
    }

    private void OnReinsertOrganStep(Entity<CMUSurgeryStepReinsertOrganEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!IsSurgeryEnabled())
            return;

        // Pulled from the surgeon's hand via the virtual hook to keep the
        // shared path prediction-safe.
        var organ = TryPickDonorOrganFromHand(args.User, ent.Comp.OrganSlot);
        if (organ is null)
            return;

        if (!Body.InsertOrgan(args.Part, organ.Value, ent.Comp.OrganSlot))
            return;

        ApplyOrganReinsertionSideEffects(args.User, args.Body, organ.Value, ent.Comp.OrganSlot);
        Wounds.RecomputeInternalBleed(args.Part);
    }

    private void OnSetBoneStep(Entity<CMUSurgeryStepSetBoneEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!IsSurgeryEnabled())
            return;

        if (!TryComp<FractureComponent>(args.Part, out var frac))
            return;
        if (!MatchesBoneEffectSeverity(ent.Comp, frac.Severity))
            return;

        Bone.RestoreIntegrity((args.Part, null), ent.Comp.IntegrityRestore);
        Fracture.SetSeverity((args.Part, frac), ent.Comp.DowngradeTo, forceUpgrade: false);
        if (ent.Comp.DowngradeTo == FractureSeverity.None)
        {
            if (HasComp<CMUSplintedComponent>(args.Part))
                RemComp<CMUSplintedComponent>(args.Part);
            if (HasComp<CMUMalunionComponent>(args.Part))
                RemComp<CMUMalunionComponent>(args.Part);
            if (HasComp<CMUPostOpBoneSetComponent>(args.Part))
                RemComp<CMUPostOpBoneSetComponent>(args.Part);
        }
        Wounds.RecomputeInternalBleed(args.Part);
    }

    private void OnSetBoneCompleteCheck(Entity<CMUSurgeryStepSetBoneEffectComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (args.Cancelled)
            return;
        if (!TryComp<FractureComponent>(args.Part, out var frac))
            return;
        if (MatchesBoneEffectSeverity(ent.Comp, frac.Severity))
            args.Cancelled = true;
    }

    private static bool MatchesBoneEffectSeverity(
        CMUSurgeryStepSetBoneEffectComponent effect,
        FractureSeverity severity)
    {
        return effect.DowngradeFromAnyOf.Count > 0
            ? effect.DowngradeFromAnyOf.Contains(severity)
            : severity == effect.DowngradeFrom;
    }

    private void OnRepairOrganStep(Entity<CMUSurgeryStepRepairOrganEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!IsSurgeryEnabled())
            return;
        if (!TryGetOrganInSlot(args.Part, ent.Comp.OrganSlot, out var organ))
            return;
        if (!TryComp<OrganHealthComponent>(organ, out var oh))
            return;

        HeartComponent? heart = null;
        var canRestartHeart = oh.Stage != OrganDamageStage.Dead &&
                              TryComp(organ, out heart);

        OrganHealth.HealOrgan((organ, oh), args.Body, oh.Max - oh.Current);
        if (canRestartHeart)
            Heart.TryRestartHeart((organ, heart));

        Wounds.RecomputeInternalBleed(args.Part);
    }

    private void OnCauterizeBleedStep(Entity<CMUSurgeryStepCauterizeBleedEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!IsSurgeryEnabled())
            return;
        Wounds.SuppressInternalBleed(args.Part);
    }

    private void OnReattachLimbStep(Entity<CMUSurgeryStepReattachLimbEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!IsSurgeryEnabled())
            return;
        ApplyLimbReattach(args.User, args.Body, args.Part, ent.Comp.StartingHpFraction, ent.Comp.StartingFracture);
    }

    private void OnRemoveLimbStep(Entity<CMUSurgeryStepRemoveLimbEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!IsSurgeryEnabled())
            return;
        ApplyLimbRemoval(args.User, args.Body, args.Part);
    }

    private void OnDebrideEscharStep(Entity<CMUSurgeryStepDebrideEscharEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!IsSurgeryEnabled())
            return;
        if (HasComp<CMUEscharComponent>(args.Part))
            RemComp<CMUEscharComponent>(args.Part);
    }

    private void OnResolveSurgicalTraitStep(Entity<CMUSurgeryStepResolveTraitEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!IsSurgeryEnabled())
            return;
        if (!SurgicalTraits.RemoveTrait(args.Part, ent.Comp.Trait))
            return;

        if (ent.Comp.Trait == CMUSurgicalTrait.VascularTear)
            Wounds.SuppressInternalBleed(args.Part);
        else if (ent.Comp.Trait == CMUSurgicalTrait.EmbeddedForeignBody)
            Shrapnel.TryClearShrapnel(args.Part);
    }

    protected virtual void ApplyOrganRemovalSideEffects(EntityUid user, EntityUid body, EntityUid organ, string slot)
    {
    }

    protected virtual void ApplyOrganReinsertionSideEffects(EntityUid user, EntityUid body, EntityUid organ, string slot)
    {
    }

    protected virtual void ApplyLimbReattach(EntityUid user, EntityUid body, EntityUid part, float startingHpFraction, FractureSeverity startingFracture)
    {
    }

    protected virtual void ApplyLimbRemoval(EntityUid user, EntityUid body, EntityUid part)
    {
    }

    protected virtual EntityUid? TryPickDonorOrganFromHand(EntityUid surgeon, string organSlot)
    {
        return null;
    }

    public bool TryGetOrganInSlot(EntityUid part, string slotId, out EntityUid organ)
    {
        return MedicalIndex.TryGetOrganInSlot(part, slotId, out organ);
    }
}
