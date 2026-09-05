using System;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUSurgerySystem : SharedCMUSurgerySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedBodyPartHealthSystem _partHealth = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId DetachedBodyPrototype = "DetachedBody";

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

        // OrganAddedToBodyEvent reconciles missing-organ and donor-stage status.
        // Removing that status here would erase a damaged donor's contribution.

        var rejectionMinutes = _cfg.GetCVar(CMUMedicalCCVars.OrganTransplantRejectionMinutes);
        _status.TryAddStatusEffectDuration(body, "StatusEffectCMUTransplantRejection",
            TimeSpan.FromMinutes(rejectionMinutes));
    }

    protected override bool TryInsertDonorOrgan(EntityUid surgeon, EntityUid part, EntityUid? used, string organSlot, out EntityUid organ)
    {
        organ = default;
        if (used is not { } donor || !_hands.IsHolding(surgeon, donor)
            || !TryComp<OrganComponent>(donor, out var organComp)
            || SharedBodySystem.GetCanonicalSlotId(organComp.Category) != organSlot
            || !Body.CanInsertOrgan(part, organSlot)
            || Body.GetParentPartOrNull(donor) is not null)
        {
            return false;
        }

        // Validate the destination/category before releasing the exact committed donor.
        if (!_hands.TryDrop(surgeon, donor, targetDropLocation: null, checkActionBlocker: false))
            return false;
        if (!Body.InsertOrgan(part, donor, organSlot))
        {
            _hands.TryPickupAnyHand(surgeon, donor, checkActionBlocker: false);
            return false;
        }

        organ = donor;
        return true;
    }

    protected override bool ApplyLimbReattach(EntityUid user, EntityUid body, EntityUid part, EntityUid? used,
        BodyPartType? type, BodyPartSymmetry? symmetry, float? startingHpFraction, FractureSeverity startingFracture)
    {
        if (!HasComp<CMUHumanMedicalComponent>(body))
            return false;

        var configuredFraction = startingHpFraction ?? _cfg.GetCVar(CMUMedicalCCVars.SurgeryLimbReattachStartingHpFraction);
        if (!float.IsFinite(configuredFraction))
            return false;
        var hpFraction = Math.Clamp(configuredFraction, 0f, 1f);

        if (used is not { } held || !_hands.IsHolding(user, held)
            || !TryGetLimb(held, out var limb, out var limbPart)
            || limbPart.PartType != type || limbPart.Symmetry != symmetry)
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-reattach-no-limb"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!CanPatientAcceptLimb(body, limb))
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-reattach-requires-robotic-limb"), body, user, PopupType.SmallCaution);
            return false;
        }

        if (!TryFindPartSlot(body, limbPart.PartType, limbPart.Symmetry, out var rootPart, out var slotId)
            || rootPart != part)
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-reattach-slot-occupied"), user, user, PopupType.SmallCaution);
            return false;
        }

        // checkActionBlocker false so a downed surgeon can still complete
        // the step (skill gate is upstream).
        if (!_hands.TryDrop(user, held, targetDropLocation: null, checkActionBlocker: false))
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-reattach-attach-failed"), user, user, PopupType.MediumCaution);
            return false;
        }

        if (!Body.AttachPart(rootPart, slotId, limb))
        {
            // Roll back so the limb isn't lost on the floor.
            _hands.TryPickupAnyHand(user, held, checkActionBlocker: false);
            _popup.PopupEntity(Loc.GetString("cmu-medical-reattach-attach-failed"), user, user, PopupType.MediumCaution);
            return false;
        }

        if (held != limb)
            QueueDel(held);

        if (TryComp<BodyPartHealthComponent>(limb, out var bph))
            _partHealth.SetCurrent((limb, bph), bph.Max * (FixedPoint2)hpFraction);

        if (HasComp<SynthComponent>(body))
        {
            ClearSynthLimbOrganicMedicalState(limb);

            if (TryComp<FractureComponent>(limb, out var existingFracture))
                Fracture.SetSeverity((limb, existingFracture), FractureSeverity.None, forceUpgrade: false);
        }
        else if (HasComp<BoneComponent>(limb))
        {
            var fracture = EnsureComp<FractureComponent>(limb);
            if (startingFracture.IsAtLeast(fracture.Severity))
                Fracture.SetSeverity((limb, fracture), startingFracture, forceUpgrade: true);
        }

        TryClearMissingLimbStatus(body, limbPart.PartType, limbPart.Symmetry);

        _popup.PopupEntity(Loc.GetString("cmu-medical-reattach-success"), body, user, PopupType.Medium);
        return true;
    }

    public bool TryRegenerateLimb(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry,
        EntityUid expectedAnchor, string expectedSlot, Func<bool> isCurrent)
    {
        if (!isCurrent() || !HasComp<CMUHumanMedicalComponent>(body)
            || !TryFindPartSlot(body, type, symmetry, out var rootPart, out var slotId)
            || rootPart != expectedAnchor || slotId != expectedSlot
            || !TryComp<InitialBodyComponent>(body, out var initialBody)
            || !TryGetInitialBodyCategory(type, symmetry, out var category))
        {
            return false;
        }

        EntProtoId<OrganComponent>? prototype = null;
        foreach (var (initialCategory, initialPrototype) in initialBody.Organs)
        {
            if (initialCategory != category)
                continue;

            prototype = initialPrototype;
            break;
        }

        if (prototype is null)
            return false;

        var limb = Spawn(prototype.Value, new EntityCoordinates(body, default));
        if (!isCurrent() || !TryFindPartSlot(body, type, symmetry, out var currentAnchor, out var currentSlot)
            || currentAnchor != expectedAnchor || currentSlot != expectedSlot
            || !TryComp<BodyPartComponent>(limb, out var limbPart)
            || limbPart.PartType != type
            || limbPart.Symmetry != symmetry
            || !CanPatientAcceptLimb(body, limb)
            || !Body.AttachPart(rootPart, slotId, limb))
        {
            QueueDel(limb);
            return false;
        }

        bool IsAttachedHere() => isCurrent() && !TerminatingOrDeleted(limb) &&
            TryComp<BodyPartComponent>(limb, out var attached) && attached.Body == body &&
            MedicalIndex.TryGetBodyPartInSlot(expectedAnchor, expectedSlot, out var occupant) && occupant == limb;

        if (!IsAttachedHere())
            return false;
        if (TryComp<BodyPartHealthComponent>(limb, out var health))
            _partHealth.SetCurrent((limb, health), health.Max);
        if (!IsAttachedHere())
            return false;
        if (TryComp<BoneComponent>(limb, out var bone))
            Bone.RestoreIntegrity((limb, bone), bone.IntegrityMax);
        if (!IsAttachedHere())
            return false;
        if (TryComp<FractureComponent>(limb, out var fracture))
            Fracture.SetSeverity((limb, fracture), FractureSeverity.None);

        if (!IsAttachedHere())
            return false;
        TryClearMissingLimbStatus(body, type, symmetry);
        return true;
    }

    private bool CanPatientAcceptLimb(EntityUid body, EntityUid limb)
    {
        return !HasComp<CMUDroneAndroidComponent>(body) ||
               HasComp<CMURoboticLimbComponent>(limb);
    }

    private static bool TryGetInitialBodyCategory(
        BodyPartType type,
        BodyPartSymmetry symmetry,
        out ProtoId<OrganCategoryPrototype> category)
    {
        switch (type, symmetry)
        {
            case (BodyPartType.Arm, BodyPartSymmetry.Left):
                category = "ArmLeft";
                return true;
            case (BodyPartType.Arm, BodyPartSymmetry.Right):
                category = "ArmRight";
                return true;
            case (BodyPartType.Hand, BodyPartSymmetry.Left):
                category = "HandLeft";
                return true;
            case (BodyPartType.Hand, BodyPartSymmetry.Right):
                category = "HandRight";
                return true;
            case (BodyPartType.Leg, BodyPartSymmetry.Left):
                category = "LegLeft";
                return true;
            case (BodyPartType.Leg, BodyPartSymmetry.Right):
                category = "LegRight";
                return true;
            case (BodyPartType.Foot, BodyPartSymmetry.Left):
                category = "FootLeft";
                return true;
            case (BodyPartType.Foot, BodyPartSymmetry.Right):
                category = "FootRight";
                return true;
            default:
                category = default;
                return false;
        }
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

    protected override bool ApplyLimbRemoval(EntityUid user, EntityUid body, EntityUid part)
    {
        if (!HasComp<CMUHumanMedicalComponent>(body))
            return false;

        if (!TryComp<BodyPartComponent>(part, out var limbPart))
            return false;

        if (limbPart.Body != body)
            return false;

        if (!CMUBodyPartSlots.IsReportableMissingPart(limbPart.PartType))
            return false;

        var attempt = new BodyPartSeverAttemptEvent(body, part, limbPart.PartType) { Surgical = true };
        RaiseLocalEvent(part, ref attempt);
        if (!attempt.Succeeded || attempt.DetachedBody is not { } detachedBody)
            return false;

        _hands.TryPickupAnyHand(user, detachedBody, checkActionBlocker: false);
        _popup.PopupEntity(Loc.GetString("cmu-medical-amputation-success"), body, user, PopupType.Medium);
        return true;
    }

    private bool TryGetLimb(
        EntityUid held,
        out EntityUid limb,
        out BodyPartComponent limbPart)
    {
        limb = default;
        limbPart = default!;

        var candidate = held;
        BodyPartComponent? bp;
        if (!TryComp(candidate, out bp))
        {
            if (MetaData(held).EntityPrototype?.ID != DetachedBodyPrototype.ToString() ||
                !TryComp<BodyComponent>(held, out var carrierBody) ||
                Body.GetRootPartOrNull(held, carrierBody) is not { } root)
            {
                return false;
            }

            candidate = root.Entity;
            bp = root.BodyPart;
        }

        if (bp is null || !CMUBodyPartSlots.IsReportableMissingPart(bp.PartType))
            return false;

        limb = candidate;
        limbPart = bp;
        return true;
    }

    private bool TryFindPartSlot(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry, out EntityUid rootPart, out string slotId)
    {
        rootPart = default;
        slotId = string.Empty;

        foreach (var (parentId, parentComp) in MedicalIndex.GetBodyParts(body))
        {
            foreach (var slot in MedicalIndex.GetBodyPartSlots(parentId))
            {
                if (slot.Type != type || slot.Part is not null)
                    continue;
                if (!CMUBodyPartSlots.TryGetSymmetry(slot.SlotId, parentComp.Symmetry, out var slotSymmetry))
                    continue;
                if (slotSymmetry != symmetry)
                    continue;

                rootPart = parentId;
                slotId = slot.SlotId;
                return true;
            }
        }

        return false;
    }

    /// <summary>Captures the concrete parent and empty slot selected for delayed limb generation.</summary>
    public bool TryGetMissingPartSite(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry,
        out EntityUid parent, out string slot)
    {
        return TryFindPartSlot(body, type, symmetry, out parent, out slot);
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
            (BodyPartType.Hand, BodyPartSymmetry.Left) => "StatusEffectCMUMissingHandLeft",
            (BodyPartType.Hand, BodyPartSymmetry.Right) => "StatusEffectCMUMissingHandRight",
            (BodyPartType.Leg, BodyPartSymmetry.Left) => "StatusEffectCMUMissingLegLeft",
            (BodyPartType.Leg, BodyPartSymmetry.Right) => "StatusEffectCMUMissingLegRight",
            (BodyPartType.Foot, BodyPartSymmetry.Left) => "StatusEffectCMUMissingFootLeft",
            (BodyPartType.Foot, BodyPartSymmetry.Right) => "StatusEffectCMUMissingFootRight",
            _ => null,
        };

    private static EntProtoId? OrganRemovalStatusEffect(string slot) => slot switch
    {
        "liver" => "StatusEffectCMUHepaticFailure",
        "kidneys" => "StatusEffectCMURenalFailure",
        "heart" => "StatusEffectCMUCardiacArrest",
        "stomach" => "StatusEffectCMUNausea",
        _ => null,
    };
}
