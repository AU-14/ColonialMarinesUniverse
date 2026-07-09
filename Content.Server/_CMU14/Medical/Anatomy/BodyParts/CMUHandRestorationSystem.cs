using System;
using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Anatomy.BodyParts;

public sealed partial class CMUHandRestorationSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;

    public void RestoreUsableHands(EntityUid body)
    {
        if (!TryComp<HandsComponent>(body, out var hands))
            return;

        foreach (var (partId, part) in _medicalIndex.GetBodyParts(body))
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

    private static bool NormalizeBodyHandOrder(HandsComponent hands)
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
