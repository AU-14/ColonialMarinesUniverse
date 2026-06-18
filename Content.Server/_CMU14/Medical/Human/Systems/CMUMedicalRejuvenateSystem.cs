using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Human.Systems;

public sealed partial class CMUMedicalRejuvenateSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedHumanMedicalSystem _medical = default!;
    [Dependency] private SharedStatusEffectsSystem _status = default!;
    [Dependency] private IPrototypeManager _protoMgr = default!;

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

        SubscribeLocalEvent<HumanMedicalComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(Entity<HumanMedicalComponent> ent, ref RejuvenateEvent args)
    {
        var body = ent.Owner;

        RestoreMissingParts(body);
        RestoreUsableHands(body);

        HumanMedicalLedger.ResetToHealthy(ent.Comp);
        HumanMedicalLedger.RebuildSummaryIfDirty(ent.Comp);

        if (TryComp<HumanMedicalSummaryComponent>(body, out var summary))
        {
            summary.Summary = ent.Comp.Summary;
            Dirty(body, summary);
        }

        _medical.RefreshActiveMarkers(body, ent.Comp);

        foreach (var effect in CmuStatusEffects)
            _status.TryRemoveStatusEffect(body, effect);
    }

    private void RestoreMissingParts(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Prototype is null)
            return;

        if (!_protoMgr.TryIndex(bodyComp.Prototype.Value, out var proto))
            return;

        if (_body.GetRootPartOrNull(body, bodyComp) is not { } root)
            return;

        var rootSlotId = proto.Root;
        var slotEntities = new Dictionary<string, EntityUid> { [rootSlotId] = root.Entity };
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

    private void RestoreUsableHands(EntityUid body)
    {
        if (!TryComp<HandsComponent>(body, out var hands))
            return;

        foreach (var (partId, part) in _body.GetBodyChildren(body))
        {
            if (part.PartType != BodyPartType.Hand)
                continue;

            var location = part.Symmetry switch
            {
                BodyPartSymmetry.Left => HandLocation.Left,
                BodyPartSymmetry.Right => HandLocation.Right,
                _ => HandLocation.Middle,
            };

            string? handId = null;
            if (_body.GetParentPartAndSlotOrNull(partId) is { } parentSlot)
                handId = SharedBodySystem.GetPartSlotContainerId(parentSlot.Slot);
            else if (part.Symmetry is BodyPartSymmetry.Left or BodyPartSymmetry.Right)
                handId = SharedBodySystem.GetPartSlotContainerId(part.Symmetry == BodyPartSymmetry.Left
                    ? "left_hand"
                    : "right_hand");

            if (handId == null)
                continue;

            if (!_hands.TrySetHandLocation((body, hands), handId, location))
                _hands.AddHand((body, hands), handId, location);
        }

        if (NormalizeBodyHandOrder(hands))
            Dirty(body, hands);

        if (hands.ActiveHandId == null && hands.SortedHands.Count > 0)
            _hands.SetActiveHand((body, hands), hands.SortedHands[0]);
    }

    private bool NormalizeBodyHandOrder(HandsComponent hands)
    {
        var sortedHands = hands.SortedHands;
        if (sortedHands.Count < 2)
            return false;

        var ordered = new List<string>(sortedHands.Count);
        AddCanonicalHand(sortedHands, ordered, "right_hand");
        AddCanonicalHand(sortedHands, ordered, "left_hand");

        foreach (var hand in sortedHands)
        {
            if (!ordered.Contains(hand))
                ordered.Add(hand);
        }

        var changed = false;
        for (var i = 0; i < sortedHands.Count; i++)
        {
            if (sortedHands[i] == ordered[i])
                continue;

            changed = true;
            break;
        }

        if (!changed)
            return false;

        sortedHands.Clear();
        sortedHands.AddRange(ordered);
        return true;
    }

    private static void AddCanonicalHand(IReadOnlyList<string> sortedHands, List<string> ordered, string canonicalSlot)
    {
        foreach (var hand in sortedHands)
        {
            if (BarePartSlot(hand) != canonicalSlot || ordered.Contains(hand))
                continue;

            ordered.Add(hand);
            return;
        }
    }

    private static string BarePartSlot(string slot)
    {
        const string prefix = SharedBodySystem.PartSlotContainerIdPrefix;
        return slot.StartsWith(prefix, StringComparison.Ordinal)
            ? slot[prefix.Length..]
            : slot;
    }
}
