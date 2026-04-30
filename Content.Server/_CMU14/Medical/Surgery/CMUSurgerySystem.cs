using System;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Surgery;

public sealed class CMUSurgerySystem : SharedCMUSurgerySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedBodyPartHealthSystem _partHealth = default!;

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

        var hpFraction = (float)_cfg.GetCVar(CMUMedicalCCVars.SurgeryLimbReattachStartingHpFraction);
        if (TryComp<BodyPartHealthComponent>(limb, out var bph))
            _partHealth.SetCurrent((limb, bph), bph.Max * (FixedPoint2)hpFraction);

        // forceUpgrade:false — if the limb already carries a higher severity
        // (Comminuted) from prior trauma, leave it.
        if (HasComp<BoneComponent>(limb))
        {
            var fracture = EnsureComp<FractureComponent>(limb);
            Fracture.SetSeverity((limb, fracture), startingFracture, forceUpgrade: false);
        }

        TryClearMissingLimbStatus(body, limbPart.PartType, limbPart.Symmetry);

        _popup.PopupEntity(Loc.GetString("cmu-medical-reattach-success"), body, user, PopupType.Medium);
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
