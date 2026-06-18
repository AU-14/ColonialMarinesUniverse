using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Targeting;
using Content.Shared._CMU14.Medical.Equipment;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared._RMC14.Synth;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server._CMU14.Medical.Human.Care;

public sealed partial class HumanOrthopedicTreatmentSystem : EntitySystem
{
    private const float DefaultCastKnittingMinutes = 5f;

    private static readonly BodyRegion[] SplintFallbackOrder =
    {
        BodyRegion.LeftArm,
        BodyRegion.RightArm,
        BodyRegion.LeftHand,
        BodyRegion.RightHand,
        BodyRegion.LeftLeg,
        BodyRegion.RightLeg,
        BodyRegion.LeftFoot,
        BodyRegion.RightFoot,
    };

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private HumanTreatmentSystem _humanTreatment = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedBodyZoneTargetingSystem _zoneTargeting = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUSplintItemComponent, AfterInteractEvent>(OnSplintAfterInteract);
        SubscribeLocalEvent<CMUSplintItemComponent, HumanSplintTreatmentDoAfterEvent>(OnSplintDoAfter);
        SubscribeLocalEvent<CMUCastItemComponent, AfterInteractEvent>(OnCastAfterInteract);
        SubscribeLocalEvent<CMUCastItemComponent, HumanCastTreatmentDoAfterEvent>(OnCastDoAfter);
    }

    private bool IsLayerEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled) &&
            _cfg.GetCVar(CMUMedicalCCVars.BoneEnabled);
    }

    public void HandleAfterInteract(Entity<HumanMedicalComponent> ent, ref AfterInteractEvent args)
    {
        if (!TryGetHumanPatient(args, out var patient, out var medical))
            return;

        if (TryComp<CMUSplintItemComponent>(args.Used, out var splint))
        {
            HandleSplintAfterInteract(args.User, patient, args.Used, splint, medical, ref args);
            return;
        }

        if (TryComp<CMUCastItemComponent>(args.Used, out var cast))
            HandleCastAfterInteract(args.User, patient, args.Used, cast, medical, ref args);
    }

    private void OnSplintAfterInteract(Entity<CMUSplintItemComponent> ent, ref AfterInteractEvent args)
    {
        if (!TryGetHumanPatient(args, out var patient, out var medical))
            return;

        HandleSplintAfterInteract(args.User, patient, ent.Owner, ent.Comp, medical, ref args);
    }

    private void OnCastAfterInteract(Entity<CMUCastItemComponent> ent, ref AfterInteractEvent args)
    {
        if (!TryGetHumanPatient(args, out var patient, out var medical))
            return;

        HandleCastAfterInteract(args.User, patient, ent.Owner, ent.Comp, medical, ref args);
    }

    private bool TryGetHumanPatient(
        AfterInteractEvent args,
        out EntityUid patient,
        out HumanMedicalComponent medical)
    {
        patient = default;
        medical = default!;

        if (args.Handled ||
            !args.CanReach ||
            args.Target is not { } target ||
            !IsLayerEnabled() ||
            HasComp<SynthComponent>(target) ||
            !TryComp<HumanMedicalComponent>(target, out var resolvedMedical))
        {
            return false;
        }

        patient = target;
        medical = resolvedMedical;
        return true;
    }

    private void HandleSplintAfterInteract(
        EntityUid user,
        EntityUid patient,
        EntityUid item,
        CMUSplintItemComponent splint,
        HumanMedicalComponent medical,
        ref AfterInteractEvent args)
    {
        args.Handled = true;
        if (!TryCreateOrthopedicAttempt(user, medical, MedicalActionKind.ApplySplint, FixedPoint2.Zero, out var attempt))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-human-medical-splint-no-break"),
                patient,
                user,
                PopupType.SmallCaution);
            return;
        }

        var ev = new HumanSplintTreatmentDoAfterEvent(attempt);
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            splint.ApplyDelay,
            ev,
            item,
            target: patient,
            used: item)
        {
            BreakOnMove = true,
            BreakOnDamage = splint.BreakOnDamage,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void HandleCastAfterInteract(
        EntityUid user,
        EntityUid patient,
        EntityUid item,
        CMUCastItemComponent cast,
        HumanMedicalComponent medical,
        ref AfterInteractEvent args)
    {
        args.Handled = true;
        var duration = FixedPoint2.New(Math.Max(DefaultCastKnittingMinutes, cast.PostOpHealMinutes) * 60f);
        if (!TryCreateOrthopedicAttempt(user, medical, MedicalActionKind.ApplyCast, duration, out var attempt))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-human-medical-cast-no-break"),
                patient,
                user,
                PopupType.SmallCaution);
            return;
        }

        var ev = new HumanCastTreatmentDoAfterEvent(attempt);
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            cast.ApplyDelay,
            ev,
            item,
            target: patient,
            used: item)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnSplintDoAfter(Entity<CMUSplintItemComponent> ent, ref HumanSplintTreatmentDoAfterEvent args)
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
        if (!result.Applied)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-human-medical-splint-failed"),
                patient,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.ApplySound is not null)
            _audio.PlayPvs(ent.Comp.ApplySound, patient);

        ConsumeSplintUse(ent);
    }

    private void OnCastDoAfter(Entity<CMUCastItemComponent> ent, ref HumanCastTreatmentDoAfterEvent args)
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
        if (!result.Applied)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-human-medical-cast-failed"),
                patient,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.ApplySound is not null)
            _audio.PlayPvs(ent.Comp.ApplySound, patient);

        ConsumeCastUse(ent);
    }

    private bool TryCreateOrthopedicAttempt(
        EntityUid user,
        HumanMedicalComponent medical,
        MedicalActionKind action,
        FixedPoint2 amount,
        out TreatmentAttempt attempt)
    {
        if (_zoneTargeting.TryGetFreshSelection(user) is { } zone)
        {
            var aimed = RegionForZone(zone);
            if (TryCreateOrthopedicAttempt(medical, action, aimed, amount, out attempt))
                return true;
        }

        foreach (var region in SplintFallbackOrder)
        {
            if (TryCreateOrthopedicAttempt(medical, action, region, amount, out attempt))
                return true;
        }

        attempt = default;
        return false;
    }

    private static bool TryCreateOrthopedicAttempt(
        HumanMedicalComponent medical,
        MedicalActionKind action,
        BodyRegion region,
        FixedPoint2 amount,
        out TreatmentAttempt attempt)
    {
        if (region == BodyRegion.None)
        {
            attempt = default;
            return false;
        }

        var request = new MedicalActionRequest(
            default,
            default,
            null,
            action,
            MedicalActionSourceKind.HandItem,
            MedicalActionTargetKind.Region,
            region);

        if (!MedicalTreatmentActionRules.TryCreateHumanTreatmentAttempt(request, out attempt))
            return false;

        attempt = new TreatmentAttempt(attempt.Kind, region, Amount: amount);
        return TreatmentRules.TryCreateTreatmentPlan(medical, attempt).Applied;
    }

    private static BodyRegion RegionForZone(TargetBodyZone zone)
    {
        return zone switch
        {
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

    private void ConsumeSplintUse(Entity<CMUSplintItemComponent> ent)
    {
        if (!ent.Comp.ConsumedOnApply || !_net.IsServer)
            return;

        ent.Comp.Uses--;
        if (ent.Comp.Uses <= 0)
            QueueDel(ent.Owner);
    }

    private void ConsumeCastUse(Entity<CMUCastItemComponent> ent)
    {
        if (!ent.Comp.ConsumedOnApply || !_net.IsServer)
            return;

        ent.Comp.Uses--;
        if (ent.Comp.Uses <= 0)
            QueueDel(ent.Owner);
    }
}
