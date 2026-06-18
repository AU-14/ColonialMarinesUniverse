using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Targeting;
using Content.Shared._CMU14.Medical.Equipment;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Server._CMU14.Medical.Human.Surgery;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared._RMC14.Synth;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Configuration;

namespace Content.Server._CMU14.Medical.Human.Care;

public sealed partial class HumanBleedControlTreatmentSystem : EntitySystem
{
    private static readonly TimeSpan ClampDelay = TimeSpan.FromSeconds(2);

    private static readonly BodyRegion[] ClampFallbackOrder =
    {
        BodyRegion.Chest,
        BodyRegion.Groin,
        BodyRegion.Head,
        BodyRegion.LeftArm,
        BodyRegion.RightArm,
        BodyRegion.LeftHand,
        BodyRegion.RightHand,
        BodyRegion.LeftLeg,
        BodyRegion.RightLeg,
        BodyRegion.LeftFoot,
        BodyRegion.RightFoot,
    };

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private HumanSurgeryToolSystem _humanSurgeryTools = default!;
    [Dependency] private HumanTreatmentSystem _humanTreatment = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedBodyZoneTargetingSystem _zoneTargeting = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUOrganClampComponent, AfterInteractEvent>(OnBleedControlAfterInteract);
        SubscribeLocalEvent<CMUOrganClampComponent, HumanBleedClampTreatmentDoAfterEvent>(OnClampDoAfter);
    }

    private bool IsLayerEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled) &&
            _cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled);
    }

    private void OnBleedControlAfterInteract(Entity<CMUOrganClampComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            args.Target is not { } patient ||
            HasComp<SynthComponent>(patient) ||
            !TryComp<HumanMedicalComponent>(patient, out var medical))
        {
            return;
        }

        if (_humanSurgeryTools.TryHandleSurgeryToolInteraction(
                args.User,
                patient,
                ent.Owner,
                medical,
                popupNoProcedure: false))
        {
            args.Handled = true;
            return;
        }

        if (!IsLayerEnabled())
            return;

        args.Handled = true;
        if (!TryCreateClampAttempt(args.User, medical, out var attempt))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-human-medical-clamp-no-bleed"),
                patient,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        var ev = new HumanBleedClampTreatmentDoAfterEvent(attempt);
        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ClampDelay,
            ev,
            ent.Owner,
            target: patient,
            used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameTarget,
            TargetEffect = "RMCEffectHealBusy",
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnClampDoAfter(Entity<CMUOrganClampComponent> ent, ref HumanBleedClampTreatmentDoAfterEvent args)
    {
        if (args.Cancelled ||
            !IsLayerEnabled() ||
            args.Target is not { } patient ||
            HasComp<SynthComponent>(patient) ||
            !TryComp<HumanMedicalComponent>(patient, out var medical))
        {
            return;
        }

        var result = _humanTreatment.TryApplyTreatment(patient, args.Attempt, medical);
        if (result.Applied)
            return;

        _popup.PopupEntity(
            Loc.GetString("cmu-human-medical-clamp-failed"),
            patient,
            args.User,
            PopupType.SmallCaution);
    }

    private bool TryCreateClampAttempt(
        EntityUid user,
        HumanMedicalComponent medical,
        out TreatmentAttempt attempt)
    {
        if (_zoneTargeting.TryGetFreshSelection(user) is { } zone)
        {
            var aimed = RegionForZone(zone);
            if (TryCreateClampAttempt(medical, aimed, out attempt))
                return true;
        }

        foreach (var region in ClampFallbackOrder)
        {
            if (TryCreateClampAttempt(medical, region, out attempt))
                return true;
        }

        attempt = default;
        return false;
    }

    private static bool TryCreateClampAttempt(
        HumanMedicalComponent medical,
        BodyRegion region,
        out TreatmentAttempt attempt)
    {
        foreach (var bleed in medical.BleedSources)
        {
            if (bleed.Region != region ||
                bleed.Kind != BleedKind.Internal ||
                !bleed.Active)
            {
                continue;
            }

            var request = new MedicalActionRequest(
                default,
                default,
                null,
                MedicalActionKind.ApplyClamp,
                MedicalActionSourceKind.HandItem,
                MedicalActionTargetKind.Region,
                region);

            if (!MedicalTreatmentActionRules.TryCreateHumanTreatmentAttempt(request, out attempt))
                return false;

            attempt = new TreatmentAttempt(
                attempt.Kind,
                region,
                BleedSourceId: bleed.Id);
            return TreatmentRules.TryCreateTreatmentPlan(medical, attempt).Applied;
        }

        attempt = default;
        return false;
    }

    private static BodyRegion RegionForZone(TargetBodyZone zone)
    {
        return zone switch
        {
            TargetBodyZone.Head => BodyRegion.Head,
            TargetBodyZone.Chest => BodyRegion.Chest,
            TargetBodyZone.GroinPelvis => BodyRegion.Groin,
            TargetBodyZone.LeftArm => BodyRegion.LeftArm,
            TargetBodyZone.RightArm => BodyRegion.RightArm,
            TargetBodyZone.LeftHand => BodyRegion.LeftHand,
            TargetBodyZone.RightHand => BodyRegion.RightHand,
            TargetBodyZone.LeftLeg => BodyRegion.LeftLeg,
            TargetBodyZone.RightLeg => BodyRegion.RightLeg,
            TargetBodyZone.LeftFoot => BodyRegion.LeftFoot,
            TargetBodyZone.RightFoot => BodyRegion.RightFoot,
            _ => BodyRegion.None,
        };
    }
}
