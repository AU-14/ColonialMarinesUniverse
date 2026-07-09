using System;
using Content.Server._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.DroneOperator;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUSurgerySystem : SharedCMUSurgerySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private CMUHandRestorationSystem _handRestoration = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedBodyPartHealthSystem _partHealth = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;
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

        if (!CanPatientAcceptLimb(body, limb))
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-reattach-requires-robotic-limb"), body, user, PopupType.SmallCaution);
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

        _handRestoration.RestoreUsableHands(body);

        var hpFraction = (float)_cfg.GetCVar(CMUMedicalCCVars.SurgeryLimbReattachStartingHpFraction);
        if (TryComp<BodyPartHealthComponent>(limb, out var bph))
            _partHealth.SetCurrent((limb, bph), bph.Max * (FixedPoint2)hpFraction);

        // forceUpgrade:false — if the limb already carries a higher severity
        // (Shattered) from prior trauma, leave it.
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

    private bool CanPatientAcceptLimb(EntityUid body, EntityUid limb)
    {
        return !HasComp<CMUDroneAndroidComponent>(body) ||
               HasComp<CMURoboticLimbComponent>(limb);
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

        if (!MedicalIndex.TryGetRootPart(body, out var root))
            return false;

        rootPart = root.Owner;
        var sideToken = symmetry switch
        {
            BodyPartSymmetry.Left => "left",
            BodyPartSymmetry.Right => "right",
            _ => null,
        };
        if (sideToken is null)
            return false;

        foreach (var slot in MedicalIndex.GetBodyPartSlots(rootPart))
        {
            if (slot.Type != type)
                continue;
            if (!slot.SlotId.Contains(sideToken, StringComparison.Ordinal))
                continue;
            if (slot.Part is not null)
                continue;

            slotId = slot.SlotId;
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
