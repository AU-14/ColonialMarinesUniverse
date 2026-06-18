using System;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Targeting;
using Content.Shared._CMU14.Medical.Equipment;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared._RMC14.Synth;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Care;

public abstract partial class SharedCMUTourniquetSystem : EntitySystem
{
    private static readonly BodyRegion[] TourniquetFallbackOrder =
    {
        BodyRegion.LeftArm,
        BodyRegion.RightArm,
        BodyRegion.LeftLeg,
        BodyRegion.RightLeg,
    };

    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected SharedHandsSystem Hands = default!;
    [Dependency] protected HumanTreatmentSystem HumanTreatment = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] protected SharedBodyZoneTargetingSystem ZoneTargeting = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUTourniquetItemComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CMUTourniquetItemComponent, CMUTourniquetApplyDoAfterEvent>(OnApplyDoAfter);
        SubscribeLocalEvent<HumanMedicalComponent, GetVerbsEvent<AlternativeVerb>>(OnPatientGetAltVerbs);
        SubscribeLocalEvent<HumanMedicalComponent, CMUTourniquetVerbRemoveDoAfterEvent>(OnVerbRemoveDoAfter);
    }

    public bool IsLayerEnabled()
    {
        return Cfg.GetCVar(CMUMedicalCCVars.Enabled) &&
            Cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled);
    }

    private void OnAfterInteract(Entity<CMUTourniquetItemComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            args.Target is not { } patient ||
            !IsLayerEnabled() ||
            HasComp<SynthComponent>(patient) ||
            !TryComp<HumanMedicalComponent>(patient, out var medical))
        {
            return;
        }

        args.Handled = true;
        if (!TryCreateApplyAttempt(args.User, medical, ent.Comp, out var attempt, out var alreadyApplied))
        {
            var message = alreadyApplied
                ? "cmu-medical-tourniquet-already-on"
                : "cmu-medical-tourniquet-no-target";
            Popup.PopupPredicted(Loc.GetString(message), patient, args.User, PopupType.SmallCaution);
            return;
        }

        var applyEv = new CMUTourniquetApplyDoAfterEvent(attempt);
        var applyDo = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.ApplyDelay,
            applyEv,
            ent.Owner,
            target: patient,
            used: ent.Owner)
        {
            BlockDuplicate = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameTarget,
        };

        if (DoAfter.TryStartDoAfter(applyDo))
            Popup.PopupPredicted(Loc.GetString("cmu-medical-tourniquet-applying"), patient, args.User);
    }

    private void OnApplyDoAfter(Entity<CMUTourniquetItemComponent> ent, ref CMUTourniquetApplyDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Target is not { } patient ||
            !IsLayerEnabled() ||
            HasComp<SynthComponent>(patient) ||
            !TryComp<HumanMedicalComponent>(patient, out var medical))
        {
            return;
        }

        var result = HumanTreatment.TryApplyTreatment(patient, args.Attempt, medical);
        if (!result.Applied)
        {
            Popup.PopupPredicted(Loc.GetString("cmu-medical-tourniquet-no-target"), patient, args.User, PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.ApplySound is not null)
            Audio.PlayPredicted(ent.Comp.ApplySound, patient, args.User);

        if (ent.Comp.ConsumedOnApply && Net.IsServer)
            QueueDel(ent.Owner);

        Popup.PopupPredicted(Loc.GetString("cmu-medical-tourniquet-applied"), patient, args.User);
    }

    private void OnPatientGetAltVerbs(Entity<HumanMedicalComponent> patient, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!IsLayerEnabled() ||
            !args.CanInteract ||
            !args.CanAccess ||
            !TryCreateRemoveAttempt(args.User, patient.Comp, out var attempt))
        {
            return;
        }

        var user = args.User;
        var patientUid = patient.Owner;
        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("cmu-medical-tourniquet-verb-remove"),
            Act = () => StartRemoveDoAfter(user, patientUid, attempt),
            Priority = 1,
        };
        args.Verbs.Add(verb);
    }

    private void StartRemoveDoAfter(
        EntityUid user,
        EntityUid patient,
        TreatmentAttempt attempt)
    {
        var removeEv = new CMUTourniquetVerbRemoveDoAfterEvent(attempt);
        var removeDo = new DoAfterArgs(
            EntityManager,
            user,
            TimeSpan.FromSeconds(1),
            removeEv,
            patient,
            target: patient)
        {
            BlockDuplicate = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DuplicateCondition = DuplicateConditions.SameTarget,
        };

        if (DoAfter.TryStartDoAfter(removeDo))
            Popup.PopupPredicted(Loc.GetString("cmu-medical-tourniquet-removing"), patient, user);
    }

    private void OnVerbRemoveDoAfter(Entity<HumanMedicalComponent> patient, ref CMUTourniquetVerbRemoveDoAfterEvent args)
    {
        if (args.Cancelled ||
            !IsLayerEnabled())
        {
            return;
        }

        var refund = HumanMedicalLedger.GetRegion(patient.Comp, args.Attempt.Region).Tourniquet.RefundOnRemove;
        var result = HumanTreatment.TryApplyTreatment(patient.Owner, args.Attempt, patient.Comp);
        if (!result.Applied)
            return;

        Popup.PopupPredicted(Loc.GetString("cmu-medical-tourniquet-removed"), patient.Owner, args.User);

        if (Net.IsServer && refund is { } proto)
            SpawnRefund(args.User, proto);
    }

    private bool TryCreateApplyAttempt(
        EntityUid user,
        HumanMedicalComponent medical,
        CMUTourniquetItemComponent item,
        out TreatmentAttempt attempt,
        out bool alreadyApplied)
    {
        alreadyApplied = false;
        var duration = GetNecrosisSeconds();

        if (TryGetSelectedRegion(user, out var selected))
        {
            var region = HumanMedicalLedger.GetRegion(medical, selected);
            if (region.Tourniquet.Applied)
            {
                attempt = default;
                alreadyApplied = true;
                return false;
            }

            return TryCreateApplyAttempt(medical, selected, duration, item.RefundOnRemove, out attempt);
        }

        foreach (var region in TourniquetFallbackOrder)
        {
            if (!HasActiveSurfaceBleedInTourniquetRegion(medical, region))
                continue;

            return TryCreateApplyAttempt(medical, region, duration, item.RefundOnRemove, out attempt);
        }

        attempt = default;
        return false;
    }

    private static bool TryCreateApplyAttempt(
        HumanMedicalComponent medical,
        BodyRegion region,
        FixedPoint2 duration,
        EntProtoId? refundOnRemove,
        out TreatmentAttempt attempt)
    {
        if (!TryCreateTreatmentAttempt(MedicalActionKind.ApplyTourniquet, region, out attempt))
            return false;

        attempt = new TreatmentAttempt(
            attempt.Kind,
            region,
            Amount: duration,
            RefundOnRemove: refundOnRemove);
        return TreatmentRules.TryCreateTreatmentPlan(medical, attempt).Applied;
    }

    private bool TryCreateRemoveAttempt(
        EntityUid user,
        HumanMedicalComponent medical,
        out TreatmentAttempt attempt)
    {
        if (TryGetSelectedRegion(user, out var selected) &&
            HumanMedicalLedger.GetRegion(medical, selected).Tourniquet.Applied)
        {
            if (!TryCreateTreatmentAttempt(MedicalActionKind.RemoveTourniquet, selected, out attempt))
                return false;

            return TreatmentRules.TryCreateTreatmentPlan(medical, attempt).Applied;
        }

        foreach (var region in TourniquetFallbackOrder)
        {
            if (!HumanMedicalLedger.GetRegion(medical, region).Tourniquet.Applied)
                continue;

            if (!TryCreateTreatmentAttempt(MedicalActionKind.RemoveTourniquet, region, out attempt))
                return false;

            return TreatmentRules.TryCreateTreatmentPlan(medical, attempt).Applied;
        }

        attempt = default;
        return false;
    }

    private static bool TryCreateTreatmentAttempt(
        MedicalActionKind action,
        BodyRegion region,
        out TreatmentAttempt attempt)
    {
        var request = new MedicalActionRequest(
            default,
            default,
            null,
            action,
            MedicalActionSourceKind.HandItem,
            MedicalActionTargetKind.Region,
            region);

        return MedicalTreatmentActionRules.TryCreateHumanTreatmentAttempt(request, out attempt);
    }

    private bool TryGetSelectedRegion(
        EntityUid user,
        out BodyRegion region)
    {
        if (ZoneTargeting.TryGetFreshSelection(user) is { } zone)
        {
            region = RegionForZone(zone);
            return region != BodyRegion.None;
        }

        region = BodyRegion.None;
        return false;
    }

    private FixedPoint2 GetNecrosisSeconds()
    {
        var minutes = Math.Max(0.1f, Cfg.GetCVar(CMUMedicalCCVars.TourniquetNecrosisMinutes));
        return FixedPoint2.New(minutes * 60f);
    }

    private void SpawnRefund(
        EntityUid user,
        EntProtoId proto)
    {
        var coords = Transform(user).Coordinates;
        var item = Spawn(proto, coords);
        Hands.TryPickupAnyHand(user, item);
    }

    private static bool HasActiveSurfaceBleedInTourniquetRegion(
        HumanMedicalComponent medical,
        BodyRegion tourniquetRegion)
    {
        foreach (var bleed in medical.BleedSources)
        {
            if (!bleed.Active ||
                !IsSurfaceBleed(bleed.Kind) ||
                !IsDistalToTourniquet(tourniquetRegion, bleed.Region))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsSurfaceBleed(BleedKind kind)
    {
        return kind is BleedKind.External or BleedKind.Stump;
    }

    private static bool IsDistalToTourniquet(
        BodyRegion tourniquetRegion,
        BodyRegion sourceRegion)
    {
        return tourniquetRegion switch
        {
            BodyRegion.LeftArm => sourceRegion is BodyRegion.LeftArm or BodyRegion.LeftHand,
            BodyRegion.RightArm => sourceRegion is BodyRegion.RightArm or BodyRegion.RightHand,
            BodyRegion.LeftLeg => sourceRegion is BodyRegion.LeftLeg or BodyRegion.LeftFoot,
            BodyRegion.RightLeg => sourceRegion is BodyRegion.RightLeg or BodyRegion.RightFoot,
            _ => false,
        };
    }

    private static BodyRegion RegionForZone(TargetBodyZone zone)
    {
        return zone switch
        {
            TargetBodyZone.LeftArm => BodyRegion.LeftArm,
            TargetBodyZone.RightArm => BodyRegion.RightArm,
            TargetBodyZone.LeftHand => BodyRegion.LeftArm,
            TargetBodyZone.RightHand => BodyRegion.RightArm,
            TargetBodyZone.LeftLeg => BodyRegion.LeftLeg,
            TargetBodyZone.RightLeg => BodyRegion.RightLeg,
            TargetBodyZone.LeftFoot => BodyRegion.LeftLeg,
            TargetBodyZone.RightFoot => BodyRegion.RightLeg,
            _ => BodyRegion.None,
        };
    }
}

[Serializable, NetSerializable]
public sealed partial class CMUTourniquetApplyDoAfterEvent : DoAfterEvent
{
    [DataField]
    public TreatmentAttempt Attempt;

    public CMUTourniquetApplyDoAfterEvent(TreatmentAttempt attempt)
    {
        Attempt = attempt;
    }

    public CMUTourniquetApplyDoAfterEvent()
    {
    }

    public override DoAfterEvent Clone() => new CMUTourniquetApplyDoAfterEvent(Attempt);
}

[Serializable, NetSerializable]
public sealed partial class CMUTourniquetVerbRemoveDoAfterEvent : DoAfterEvent
{
    [DataField]
    public TreatmentAttempt Attempt;

    public CMUTourniquetVerbRemoveDoAfterEvent(TreatmentAttempt attempt)
    {
        Attempt = attempt;
    }

    public CMUTourniquetVerbRemoveDoAfterEvent()
    {
    }

    public override DoAfterEvent Clone() => new CMUTourniquetVerbRemoveDoAfterEvent(Attempt);
}
