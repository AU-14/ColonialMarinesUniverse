using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.Surgery.Conditions;
using Content.Shared._CMU14.Medical.Surgery.Effects;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Surgery;

/// <summary>
///     Side effects that need server-only state mutation (organ extract,
///     status-effect attach, internal-bleed clear) are virtualised through
///     hooks the sealed server subclass overrides; the shared default no-ops
///     so prediction rollback can't re-apply state on the client.
/// </summary>
public abstract class SharedCMUSurgerySystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedBoneSystem Bone = default!;
    [Dependency] protected readonly SharedContainerSystem Containers = default!;
    [Dependency] protected readonly SharedFractureSystem Fracture = default!;
    [Dependency] protected readonly SharedOrganHealthSystem OrganHealth = default!;
    [Dependency] protected readonly SharedCMUWoundsSystem Wounds = default!;

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

        SubscribeLocalEvent<CMUSurgeryStepRemoveOrganEffectComponent, CMSurgeryStepEvent>(OnRemoveOrganStep);
        SubscribeLocalEvent<CMUSurgeryStepReinsertOrganEffectComponent, CMSurgeryStepEvent>(OnReinsertOrganStep);
        SubscribeLocalEvent<CMUSurgeryStepSetBoneEffectComponent, CMSurgeryStepEvent>(OnSetBoneStep);
        SubscribeLocalEvent<CMUSurgeryStepRepairOrganEffectComponent, CMSurgeryStepEvent>(OnRepairOrganStep);
        SubscribeLocalEvent<CMUSurgeryStepCauterizeBleedEffectComponent, CMSurgeryStepEvent>(OnCauterizeBleedStep);
        SubscribeLocalEvent<CMUSurgeryStepReattachLimbEffectComponent, CMSurgeryStepEvent>(OnReattachLimbStep);
        SubscribeLocalEvent<CMUSurgeryStepDebrideEscharEffectComponent, CMSurgeryStepEvent>(OnDebrideEscharStep);

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
        if (frac.Severity != ent.Comp.DowngradeFrom)
            return;

        Bone.RestoreIntegrity((args.Part, null), ent.Comp.IntegrityRestore);
        Fracture.SetSeverity((args.Part, frac), ent.Comp.DowngradeTo, forceUpgrade: false);
        Wounds.RecomputeInternalBleed(args.Part);
    }

    private void OnRepairOrganStep(Entity<CMUSurgeryStepRepairOrganEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!IsSurgeryEnabled())
            return;
        if (!TryGetOrganInSlot(args.Part, ent.Comp.OrganSlot, out var organ))
            return;
        if (!TryComp<OrganHealthComponent>(organ, out var oh))
            return;

        OrganHealth.HealOrgan((organ, oh), args.Body, oh.Max - oh.Current);
        Wounds.RecomputeInternalBleed(args.Part);
    }

    private void OnCauterizeBleedStep(Entity<CMUSurgeryStepCauterizeBleedEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!IsSurgeryEnabled())
            return;
        Wounds.ClearInternalBleed(args.Part);
    }

    private void OnReattachLimbStep(Entity<CMUSurgeryStepReattachLimbEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!IsSurgeryEnabled())
            return;
        ApplyLimbReattach(args.User, args.Body, args.Part, ent.Comp.StartingHpFraction, ent.Comp.StartingFracture);
    }

    private void OnDebrideEscharStep(Entity<CMUSurgeryStepDebrideEscharEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!IsSurgeryEnabled())
            return;
        if (HasComp<CMUEscharComponent>(args.Part))
            RemComp<CMUEscharComponent>(args.Part);
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

    protected virtual EntityUid? TryPickDonorOrganFromHand(EntityUid surgeon, string organSlot)
    {
        return null;
    }

    public bool TryGetOrganInSlot(EntityUid part, string slotId, out EntityUid organ)
    {
        organ = default;
        var containerId = SharedBodySystem.GetOrganContainerId(slotId);
        if (!Containers.TryGetContainer(part, containerId, out var container))
            return false;
        foreach (var contained in container.ContainedEntities)
        {
            if (!HasComp<OrganComponent>(contained))
                continue;
            organ = contained;
            return true;
        }
        return false;
    }
}
