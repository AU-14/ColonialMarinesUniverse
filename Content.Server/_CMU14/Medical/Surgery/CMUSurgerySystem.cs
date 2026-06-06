using System;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Surgery;

public sealed partial class CMUSurgerySystem : SharedCMUSurgerySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedStatusEffectsSystem _status = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedBodyPartHealthSystem _partHealth = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    protected override void ApplyOrganRemovalSideEffects(EntityUid user, EntityUid body, EntityUid organ, string slot)
    {
        var stasisMinutes = _cfg.GetCVar(CMUMedicalCCVars.OrganStasisMinutes);
        OrganHealth.SetStasisExpire(organ, _timing.CurTime + TimeSpan.FromMinutes(stasisMinutes));

        _hands.TryPickupAnyHand(user, organ, checkActionBlocker: false);

        if (OrganRemovalStatusEffect(slot) is { } effect)
            _status.TrySetStatusEffectDuration(body, effect, duration: null);
    }

    protected override void ApplyOrganReinsertionSideEffects(EntityUid user, EntityUid body, EntityUid organ, string slot)
    {
        if (HasComp<OrganStasisComponent>(organ))
            RemComp<OrganStasisComponent>(organ);

        if (OrganRemovalStatusEffect(slot) is { } removalEffect)
            _status.TryRemoveStatusEffect(body, removalEffect);

        var rejectionMinutes = _cfg.GetCVar(CMUMedicalCCVars.OrganTransplantRejectionMinutes);
        _status.TryAddStatusEffectDuration(body, "StatusEffectCMUTransplantRejection",
            TimeSpan.FromMinutes(rejectionMinutes));
    }

    protected override EntityUid? TryPickDonorOrganFromHand(EntityUid surgeon, string organSlot)
    {
        if (_hands.GetActiveItem(surgeon) is not { } held)
            return null;
        if (!HasComp<OrganComponent>(held))
            return null;
        // Drop from hand so the body system can re-insert without the
        // hands container blocking the transfer.
        if (!_hands.TryDrop(surgeon, held, targetDropLocation: null, checkActionBlocker: false))
            return null;
        return held;
    }

    protected override void ApplyLimbReattach(EntityUid user, EntityUid body, EntityUid part, float startingHpFraction, FractureSeverity startingFracture)
    {
        if (!HasComp<CMUHumanMedicalComponent>(body))
            return;

        if (!TryGetHeldLimb(user, out var limb, out var limbPart))
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-reattach-no-limb"), user, user, PopupType.SmallCaution);
            return;
        }

        if (!TryFindPartSlot(body, limbPart.PartType, limbPart.Symmetry, out var rootPart, out var slotId))
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-reattach-slot-occupied"), user, user, PopupType.SmallCaution);
            return;
        }

        // checkActionBlocker false so a downed surgeon can still complete
        // the step (skill gate is upstream).
        _hands.TryDrop(user, limb, targetDropLocation: null, checkActionBlocker: false);

        if (!Body.AttachPart(rootPart, slotId, limb))
        {
            // Roll back so the limb isn't lost on the floor.
            _hands.TryPickupAnyHand(user, limb, checkActionBlocker: false);
            _popup.PopupEntity(Loc.GetString("cmu-medical-reattach-attach-failed"), user, user, PopupType.MediumCaution);
            return;
        }

        RestoreUsableHands(body);

        var hpFraction = (float)_cfg.GetCVar(CMUMedicalCCVars.SurgeryLimbReattachStartingHpFraction);
        if (TryComp<BodyPartHealthComponent>(limb, out var bph))
            _partHealth.SetCurrent((limb, bph), bph.Max * (FixedPoint2)hpFraction);

        // forceUpgrade:false — if the limb already carries a higher severity
        // (Comminuted) from prior trauma, leave it.
        if (HasComp<SynthComponent>(body))
        {
            ClearSynthLimbOrganicMedicalState(limb);

            if (TryComp<FractureComponent>(limb, out var existingFracture))
                Fracture.SetSeverity((limb, existingFracture), FractureSeverity.None, forceUpgrade: false);
        }
        else if (HasComp<BoneComponent>(limb))
        {
            var fracture = EnsureComp<FractureComponent>(limb);
            Fracture.SetSeverity((limb, fracture), startingFracture, forceUpgrade: false);
        }

        TryClearMissingLimbStatus(body, limbPart.PartType, limbPart.Symmetry);

        _popup.PopupEntity(Loc.GetString("cmu-medical-reattach-success"), body, user, PopupType.Medium);
    }

    private void ClearSynthLimbOrganicMedicalState(EntityUid limb)
    {
        if (TryComp<BodyPartWoundComponent>(limb, out var wounds))
        {
            Wounds.ClearAllWounds((limb, wounds));

            if (HasComp<BodyPartWoundComponent>(limb))
                RemComp<BodyPartWoundComponent>(limb);
        }

        if (HasComp<InternalBleedingComponent>(limb))
            RemComp<InternalBleedingComponent>(limb);
        if (HasComp<CMUInternalBleedingSuppressedComponent>(limb))
            RemComp<CMUInternalBleedingSuppressedComponent>(limb);
        if (HasComp<CMUTourniquetComponent>(limb))
            RemComp<CMUTourniquetComponent>(limb);
        if (HasComp<CMUEscharComponent>(limb))
            RemComp<CMUEscharComponent>(limb);
        if (HasComp<CMUNecroticComponent>(limb))
            RemComp<CMUNecroticComponent>(limb);
    }

    private void RestoreUsableHands(EntityUid body)
    {
        if (!TryComp<HandsComponent>(body, out var hands))
            return;

        foreach (var (partId, part) in Body.GetBodyChildren(body))
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
            if (Body.GetParentPartAndSlotOrNull(partId) is { } parentSlot)
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
            ? slot.Substring(prefix.Length)
            : slot;
    }

    protected override void ApplyLimbRemoval(EntityUid user, EntityUid body, EntityUid part)
    {
        if (!HasComp<CMUHumanMedicalComponent>(body))
            return;

        if (!TryComp<BodyPartComponent>(part, out var limbPart))
            return;

        if (limbPart.Body != body)
            return;

        if (limbPart.PartType is not (BodyPartType.Arm or BodyPartType.Leg))
            return;

        _transform.SetCoordinates(part, Transform(body).Coordinates);

        _transform.AttachToGridOrMap(part);

        if (StatusForPart(limbPart.PartType, limbPart.Symmetry) is { } statusProto)
            _status.TrySetStatusEffectDuration(body, statusProto, duration: null);

        _hands.TryPickupAnyHand(user, part, checkActionBlocker: false);
        _popup.PopupEntity(Loc.GetString("cmu-medical-amputation-success"), body, user, PopupType.Medium);
    }

    private bool TryGetHeldLimb(EntityUid surgeon, out EntityUid limb, out BodyPartComponent limbPart)
    {
        limb = default;
        limbPart = default!;

        foreach (var held in _hands.EnumerateHeld(surgeon))
        {
            if (!TryComp<BodyPartComponent>(held, out var bp))
                continue;
            if (bp.PartType is not (BodyPartType.Arm or BodyPartType.Leg))
                continue;

            limb = held;
            limbPart = bp;
            return true;
        }
        return false;
    }

    private bool TryFindPartSlot(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry, out EntityUid rootPart, out string slotId)
    {
        rootPart = default;
        slotId = string.Empty;

        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return false;

        if (Body.GetRootPartOrNull(body, bodyComp) is not { } root)
            return false;

        rootPart = root.Entity;
        var rootBp = root.BodyPart;

        var sideToken = symmetry switch
        {
            BodyPartSymmetry.Left => "left",
            BodyPartSymmetry.Right => "right",
            _ => null,
        };
        if (sideToken is null)
            return false;

        foreach (var (id, slot) in rootBp.Children)
        {
            if (slot.Type != type)
                continue;
            if (!id.Contains(sideToken, StringComparison.Ordinal))
                continue;

            var containerId = SharedBodySystem.GetPartSlotContainerId(id);
            if (Containers.TryGetContainer(rootPart, containerId, out var container)
                && container.ContainedEntities.Count > 0)
            {
                continue;
            }

            slotId = id;
            return true;
        }

        return false;
    }

    private void TryClearMissingLimbStatus(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        if (StatusForPart(type, symmetry) is not { } statusProto)
            return;
        _status.TryRemoveStatusEffect(body, statusProto);
    }

    private static EntProtoId? StatusForPart(BodyPartType type, BodyPartSymmetry symmetry) =>
        (type, symmetry) switch
        {
            (BodyPartType.Arm, BodyPartSymmetry.Left) => "StatusEffectCMUMissingArmLeft",
            (BodyPartType.Arm, BodyPartSymmetry.Right) => "StatusEffectCMUMissingArmRight",
            (BodyPartType.Leg, BodyPartSymmetry.Left) => "StatusEffectCMUMissingLegLeft",
            (BodyPartType.Leg, BodyPartSymmetry.Right) => "StatusEffectCMUMissingLegRight",
            _ => null,
        };

    private static EntProtoId? OrganRemovalStatusEffect(string slot) => slot switch
    {
        "liver" => "StatusEffectCMUHepaticFailure",
        "lungs" => "StatusEffectCMUPulmonaryEdema",
        "kidneys" => "StatusEffectCMURenalFailure",
        "heart" => "StatusEffectCMUCardiacArrest",
        "stomach" => "StatusEffectCMUNausea",
        _ => null,
    };
}
