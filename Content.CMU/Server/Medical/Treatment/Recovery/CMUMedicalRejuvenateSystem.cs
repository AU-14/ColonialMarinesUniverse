using System.Collections.Generic;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Stomach;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Medical.Treatment.Recovery;

public sealed partial class CMUMedicalRejuvenateSystem : EntitySystem
{
    [Dependency] private SharedBoneSystem _bone = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedFractureSystem _fracture = default!;
    [Dependency] private SharedHeartSystem _heart = default!;
    [Dependency] private SharedLiverSystem _liver = default!;
    [Dependency] private SharedKidneysSystem _kidneys = default!;
    [Dependency] private SharedStomachSystem _stomach = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedOrganHealthSystem _organHealth = default!;
    [Dependency] private SharedBodyPartHealthSystem _partHealth = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private OrganRelationSystem _organRelations = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedCMUSurgeryFlowSystem _surgery = default!;
    [Dependency] private SharedCMUShrapnelSystem _shrapnel = default!;
    [Dependency] private SharedCMUSplintItemSystem _splints = default!;
    [Dependency] private SharedCMUSurgicalTraitSystem _traits = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;

    private static readonly EntProtoId[] CmuStatusEffects =
    {
        "StatusEffectCMUMissingArmLeft",
        "StatusEffectCMUMissingArmRight",
        "StatusEffectCMUMissingHandLeft",
        "StatusEffectCMUMissingHandRight",
        "StatusEffectCMUMissingLegLeft",
        "StatusEffectCMUMissingLegRight",
        "StatusEffectCMUMissingFootLeft",
        "StatusEffectCMUMissingFootRight",
        "StatusEffectCMUHepaticFailure",
        "StatusEffectCMUPulmonaryEdema",
        "StatusEffectCMURenalFailure",
        "StatusEffectCMUCardiacArrest",
        "StatusEffectCMUNausea",
        "StatusEffectCMUTransplantRejection",
        "StatusEffectCMUPainMild",
        "StatusEffectCMUPainModerate",
        "StatusEffectCMUPainSevere",
        "StatusEffectCMUPainShock",
        "StatusEffectCMUPainSuppression",
        "StatusEffectCMUWhiplash",
        "StatusEffectCMUNerveDamageArm",
        "StatusEffectCMUNerveDamageHand",
        "StatusEffectCMUNerveDamageLeg",
        "StatusEffectCMUNerveDamageFoot",
        "StatusEffectCMUConcussed",
        "StatusEffectCMUTraumaticBrainInjury",
        "StatusEffectCMUTinnitus",
        "StatusEffectCMUDeafened",
        "StatusEffectCMUBoneRegenBoost",
        "StatusEffectCMUUnconscious",
        "StatusEffectCMUAnesthesia",
        "StatusEffectCMURecoveringSurgery",
        "StatusEffectCMUOxycodoneHaze",
        "StatusEffectCMUFentanylHaze",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHumanMedicalComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(Entity<CMUHumanMedicalComponent> ent, ref RejuvenateEvent args)
    {
        var body = ent.Owner;
        // Clear pending physiology before healing publishes ordinary stage-change callbacks.
        _liver.ResetPhysiology(body);
        _kidneys.ResetPhysiology(body);
        _stomach.ResetPhysiology(body);

        if (TryComp<CMUSurgeryArmedStepComponent>(body, out var armed))
            _surgery.ClearArmed(body, armed, popup: false);
        _surgery.ClearSurgeryInFlight(body);

        RestoreMissingParts(body);

        foreach (var (partId, _) in _medicalIndex.GetBodyParts(body))
        {
            foreach (var organ in _medicalIndex.GetPartOrgans(partId))
                ResetOrgan(body, organ.Owner);
            ResetPart(body, partId);
        }

        foreach (var effect in CmuStatusEffects)
            _status.TryRemoveStatusEffect(body, effect);
        _pain.ResetPain(body);
    }

    private void RestoreMissingParts(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp) ||
            bodyComp.Organs is not { } container ||
            !TryComp<InitialBodyComponent>(body, out var initialBody))
            return;

        var organsByCategory = new Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid>();
        foreach (var organUid in container.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(organUid, out var organ) || organ.Category is not { } category)
                continue;

            organsByCategory.TryAdd(category, organUid);
        }

        foreach (var (category, prototype) in initialBody.Organs)
        {
            if (organsByCategory.ContainsKey(category))
                continue;

            var organUid = Spawn(prototype, new EntityCoordinates(body, default));
            if (!_containers.Insert(organUid, container))
            {
                QueueDel(organUid);
                continue;
            }

            organsByCategory[category] = organUid;
        }

        if (initialBody.Relationships is null)
            return;

        foreach (var (parentCategory, childCategories) in initialBody.Relationships)
        {
            if (!organsByCategory.TryGetValue(parentCategory, out var parentUid))
                continue;

            foreach (var childCategory in childCategories)
            {
                if (!organsByCategory.TryGetValue(childCategory, out var childUid) ||
                    !TryComp<ChildOrganComponent>(childUid, out var child) ||
                    child.Parent == parentUid)
                {
                    continue;
                }

                if (child.Parent is not null)
                    _organRelations.Orphan((childUid, child));

                _organRelations.Relate(parentUid, childUid);
            }
        }
    }

    private void ResetPart(EntityUid body, EntityUid part)
    {
        if (TryComp<BodyPartHealthComponent>(part, out var health))
            _partHealth.SetCurrent((part, health), health.Max);

        if (TryComp<BoneComponent>(part, out var bone))
            _bone.RestoreIntegrity((part, bone), bone.IntegrityMax);

        if (TryComp<FractureComponent>(part, out var fracture))
            _fracture.SetSeverity((part, fracture), FractureSeverity.None);

        if (HasComp<CMUEscharComponent>(part))
            RemComp<CMUEscharComponent>(part);

        if (HasComp<CMUNecroticComponent>(part))
            RemComp<CMUNecroticComponent>(part);

        _splints.ResetTreatment(part);
        _shrapnel.TryClearShrapnel(part);
        foreach (var trait in CMUSurgicalTraitMetadata.ResolutionOrder)
            _traits.RemoveTrait(part, trait);

        if (HasComp<CMUTourniquetComponent>(part))
            RemComp<CMUTourniquetComponent>(part);

        RemComp<CMIncisionOpenComponent>(part);
        RemComp<CMBleedersClampedComponent>(part);
        RemComp<CMSkinRetractedComponent>(part);
        RemComp<CMRibcageSawedComponent>(part);
        RemComp<CMRibcageOpenComponent>(part);
        _wounds.ClearInternalBleed(part);

        if (TryComp<BodyPartWoundComponent>(part, out var wounds))
            _wounds.ClearAllWounds((part, wounds));
    }

    private void ResetOrgan(EntityUid body, EntityUid organ)
    {
        // Administrative reset discards unserviced pressure before tissue healing
        // emits an ordinary stage transition that would otherwise settle it.
        if (TryComp<HeartComponent>(organ, out var heart))
            _heart.ResetHeart((organ, heart));

        if (TryComp<OrganHealthComponent>(organ, out var oh))
            _organHealth.HealOrgan((organ, oh), body, oh.Max);

        if (HasComp<OrganStasisComponent>(organ))
            RemComp<OrganStasisComponent>(organ);

    }
}
