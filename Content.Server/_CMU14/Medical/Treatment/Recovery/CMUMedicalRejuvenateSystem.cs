using System.Collections.Generic;
using Content.Server._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Treatment.Recovery;

public sealed partial class CMUMedicalRejuvenateSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedBoneSystem _bone = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedFractureSystem _fracture = default!;
    [Dependency] private CMUHandRestorationSystem _handRestoration = default!;
    [Dependency] private SharedHeartSystem _heart = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedOrganHealthSystem _organHealth = default!;
    [Dependency] private SharedBodyPartHealthSystem _partHealth = default!;
    [Dependency] private IPrototypeManager _protoMgr = default!;
    [Dependency] private SharedStatusEffectsSystem _status = default!;
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
        _handRestoration.RestoreUsableHands(body);

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
        if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Prototype is null)
            return;
        if (!_protoMgr.TryIndex(bodyComp.Prototype.Value, out var proto))
            return;
        if (!_medicalIndex.TryGetRootPart(body, out var root))
            return;

        var rootSlotId = proto.Root;
        var slotEntities = new Dictionary<string, EntityUid> { [rootSlotId] = root.Owner };
        var visited = new HashSet<string> { rootSlotId };
        var frontier = new Queue<string>();
        frontier.Enqueue(rootSlotId);

        while (frontier.TryDequeue(out var slotId))
        {
            if (!proto.Slots.TryGetValue(slotId, out var protoSlot))
                continue;
            if (!slotEntities.TryGetValue(slotId, out var parentPart))
                continue;

            foreach (var connection in protoSlot.Connections)
            {
                if (!visited.Add(connection))
                    continue;
                if (!proto.Slots.TryGetValue(connection, out var connSlot) || connSlot.Part is null)
                    continue;

                var containerId = SharedBodySystem.GetPartSlotContainerId(connection);
                EntityUid childPart;
                if (_containers.TryGetContainer(parentPart, containerId, out var container) &&
                    container.ContainedEntities.Count > 0)
                {
                    childPart = container.ContainedEntities[0];
                }
                else
                {
                    childPart = Spawn(connSlot.Part, new EntityCoordinates(parentPart, default));
                    if (!TryComp(parentPart, out BodyPartComponent? parentPartComp) ||
                        !TryComp(childPart, out BodyPartComponent? childPartComp))
                    {
                        QueueDel(childPart);
                        continue;
                    }

                    if (!_body.AttachPart(parentPart, connection, childPart, parentPartComp, childPartComp) &&
                        (!_body.TryCreatePartSlot(parentPart, connection, childPartComp.PartType, out _, parentPartComp) ||
                         !_body.AttachPart(parentPart, connection, childPart, parentPartComp, childPartComp)))
                    {
                        QueueDel(childPart);
                        continue;
                    }

                    foreach (var (organSlotId, organProto) in connSlot.Organs)
                    {
                        var organContainerId = SharedBodySystem.GetOrganContainerId(organSlotId);
                        if (!_containers.TryGetContainer(childPart, organContainerId, out var organContainer))
                            continue;
                        if (organContainer.ContainedEntities.Count > 0)
                            continue;
                        var organEnt = Spawn(organProto, new EntityCoordinates(childPart, default));
                        if (!_containers.Insert(organEnt, organContainer))
                            QueueDel(organEnt);
                    }
                }

                slotEntities[connection] = childPart;
                frontier.Enqueue(connection);
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
