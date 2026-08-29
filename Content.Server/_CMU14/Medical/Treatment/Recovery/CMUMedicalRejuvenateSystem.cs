using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Treatment.Recovery;

public sealed partial class CMUMedicalRejuvenateSystem : EntitySystem
{
    [Dependency] private SharedBoneSystem _bone = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedFractureSystem _fracture = default!;
    [Dependency] private SharedHeartSystem _heart = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedOrganHealthSystem _organHealth = default!;
    [Dependency] private SharedBodyPartHealthSystem _partHealth = default!;
    [Dependency] private OrganRelationSystem _organRelations = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedCMUSurgeryFlowSystem _surgery = default!;
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
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHumanMedicalComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(Entity<CMUHumanMedicalComponent> ent, ref RejuvenateEvent args)
    {
        var body = ent.Owner;

        if (TryComp<CMUSurgeryArmedStepComponent>(body, out var armed))
            _surgery.ClearArmed(body, armed, popup: false);
        _surgery.ClearSurgeryInFlight(body);

        RestoreMissingParts(body);

        foreach (var (partId, _) in _medicalIndex.GetBodyParts(body))
        {
            ResetPart(body, partId);
            foreach (var organ in _medicalIndex.GetPartOrgans(partId))
                ResetOrgan(body, organ.Owner);
        }

        foreach (var effect in CmuStatusEffects)
            _status.TryRemoveStatusEffect(body, effect);
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

        if (HasComp<InternalBleedingComponent>(part))
            RemComp<InternalBleedingComponent>(part);

        if (HasComp<CMUEscharComponent>(part))
            RemComp<CMUEscharComponent>(part);

        if (HasComp<CMUNecroticComponent>(part))
            RemComp<CMUNecroticComponent>(part);

        if (HasComp<CMUSplintedComponent>(part))
            RemComp<CMUSplintedComponent>(part);

        if (HasComp<CMUCastComponent>(part))
            RemComp<CMUCastComponent>(part);

        if (HasComp<CMUTourniquetComponent>(part))
            RemComp<CMUTourniquetComponent>(part);

        RemComp<CMIncisionOpenComponent>(part);
        RemComp<CMBleedersClampedComponent>(part);
        RemComp<CMSkinRetractedComponent>(part);
        RemComp<CMRibcageSawedComponent>(part);
        RemComp<CMRibcageOpenComponent>(part);

        if (TryComp<BodyPartWoundComponent>(part, out var wounds))
            _wounds.ClearAllWounds((part, wounds));
    }

    private void ResetOrgan(EntityUid body, EntityUid organ)
    {
        if (TryComp<OrganHealthComponent>(organ, out var oh))
            _organHealth.HealOrgan((organ, oh), body, oh.Max);

        if (HasComp<OrganStasisComponent>(organ))
            RemComp<OrganStasisComponent>(organ);

        if (TryComp<HeartComponent>(organ, out var heart))
            _heart.ResetHeart((organ, heart));
    }
}
