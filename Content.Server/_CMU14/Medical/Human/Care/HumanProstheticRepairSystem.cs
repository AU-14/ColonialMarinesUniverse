using Content.Shared._CMU14.Medical.Targeting;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Equipment;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared._RMC14.Repairable;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Configuration;

namespace Content.Server._CMU14.Medical.Human.Care;

public sealed partial class HumanProstheticRepairSystem : EntitySystem
{
    private static readonly TimeSpan BaseRepairDelay = TimeSpan.FromSeconds(2);
    private static readonly FixedPoint2 WelderFuelCost = FixedPoint2.New(5);
    private static readonly FixedPoint2 StandardRepairAmount = FixedPoint2.New(15);

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private HumanTreatmentSystem _humanTreatment = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCRepairableSystem _repairable = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedBodyZoneTargetingSystem _zoneTargeting = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanMedicalComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<HumanMedicalComponent, HumanProstheticRepairDoAfterEvent>(OnRepairDoAfter);
    }

    private bool IsLayerEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled);
    }

    private void OnInteractUsing(Entity<HumanMedicalComponent> patient, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !IsLayerEnabled() ||
            !IsProstheticRepairTool(args.Used) ||
            !HasAnyProstheticRegion(patient.Comp))
        {
            return;
        }

        args.Handled = true;
        if (!TryCreateRepairAttempt(
                args.User,
                args.Used,
                patient.Comp,
                out var attempt,
                out var failure))
        {
            _popup.PopupEntity(failure, patient.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        if (!CanUseToolForAttempt(args.User, args.Used, attempt, patient.Comp, consume: false, out failure))
        {
            if (!string.IsNullOrEmpty(failure))
                _popup.PopupEntity(failure, patient.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        var ev = new HumanProstheticRepairDoAfterEvent(attempt);
        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            BaseRepairDelay,
            ev,
            patient.Owner,
            target: patient.Owner,
            used: args.Used)
        {
            BreakOnMove = true,
            BreakOnHandChange = true,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameTarget,
            MovementThreshold = 0.5f,
            TargetEffect = "RMCEffectHealBusy",
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnRepairDoAfter(Entity<HumanMedicalComponent> patient, ref HumanProstheticRepairDoAfterEvent args)
    {
        if (args.Cancelled ||
            !IsLayerEnabled() ||
            args.Used is not { } used)
        {
            return;
        }

        if (!CanUseToolForAttempt(args.User, used, args.Attempt, patient.Comp, consume: true, out var failure))
        {
            if (!string.IsNullOrEmpty(failure))
                _popup.PopupEntity(failure, patient.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        var result = _humanTreatment.TryApplyTreatment(patient.Owner, args.Attempt, patient.Comp);
        if (!result.Applied)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-prosthetic-repair-failed"),
                patient.Owner,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("cmu-prosthetic-repair-finished", ("target", patient.Owner)),
            patient.Owner,
            args.User);
    }

    private bool TryCreateRepairAttempt(
        EntityUid user,
        EntityUid tool,
        HumanMedicalComponent medical,
        out TreatmentAttempt attempt,
        out string failure)
    {
        attempt = default;
        if (_zoneTargeting.TryGetFreshSelection(user) is not { } zone)
        {
            failure = Loc.GetString("cmu-prosthetic-repair-select-limb");
            return false;
        }

        var region = SharedBodyZoneTargetingSystem.ToBodyRegion(zone);
        var regionState = HumanMedicalLedger.GetRegion(medical, region);
        if (regionState.Presence != LimbPresence.Prosthetic)
        {
            failure = Loc.GetString("cmu-prosthetic-repair-not-prosthetic");
            return false;
        }

        if (HasComp<BlowtorchComponent>(tool))
        {
            if (regionState.BruteDamage <= FixedPoint2.Zero)
            {
                failure = Loc.GetString("cmu-prosthetic-repair-no-brute");
                return false;
            }

            attempt = new TreatmentAttempt(
                TreatmentKind.RepairProstheticBrute,
                region,
                Amount: StandardRepairAmount);
            failure = string.Empty;
            return true;
        }

        if (HasComp<RMCCableCoilComponent>(tool))
        {
            if (regionState.BurnDamage <= FixedPoint2.Zero)
            {
                failure = Loc.GetString("cmu-prosthetic-repair-no-burn");
                return false;
            }

            attempt = new TreatmentAttempt(
                TreatmentKind.RepairProstheticBurn,
                region,
                Amount: StandardRepairAmount);
            failure = string.Empty;
            return true;
        }

        if (TryComp<CMUNanopasteComponent>(tool, out var nanopaste))
        {
            if (regionState.BruteDamage <= FixedPoint2.Zero &&
                regionState.BurnDamage <= FixedPoint2.Zero)
            {
                failure = Loc.GetString("cmu-prosthetic-repair-no-damage");
                return false;
            }

            attempt = new TreatmentAttempt(
                TreatmentKind.RepairProstheticComposite,
                region,
                Amount: nanopaste.RepairAmount);
            failure = string.Empty;
            return true;
        }

        failure = Loc.GetString("cmu-prosthetic-repair-bad-tool");
        return false;
    }

    private bool CanUseToolForAttempt(
        EntityUid user,
        EntityUid tool,
        TreatmentAttempt attempt,
        HumanMedicalComponent medical,
        bool consume,
        out string failure)
    {
        var regionState = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        if (regionState.Presence != LimbPresence.Prosthetic)
        {
            failure = Loc.GetString("cmu-prosthetic-repair-not-prosthetic");
            return false;
        }

        if (attempt.Kind == TreatmentKind.RepairProstheticBrute &&
            HasComp<BlowtorchComponent>(tool))
        {
            if (regionState.BruteDamage <= FixedPoint2.Zero)
            {
                failure = Loc.GetString("cmu-prosthetic-repair-no-brute");
                return false;
            }

            failure = string.Empty;
            return _repairable.UseFuel(tool, user, WelderFuelCost, attempt: !consume);
        }

        if (attempt.Kind == TreatmentKind.RepairProstheticBurn &&
            TryComp<StackComponent>(tool, out var stack) &&
            HasComp<RMCCableCoilComponent>(tool))
        {
            if (regionState.BurnDamage <= FixedPoint2.Zero)
            {
                failure = Loc.GetString("cmu-prosthetic-repair-no-burn");
                return false;
            }

            if (_stacks.GetCount(tool, stack) <= 0)
            {
                failure = Loc.GetString("cmu-prosthetic-repair-no-cable");
                return false;
            }

            if (consume)
                _stacks.Use(tool, 1, stack);

            failure = string.Empty;
            return true;
        }

        if (attempt.Kind == TreatmentKind.RepairProstheticComposite &&
            TryComp<CMUNanopasteComponent>(tool, out var nanopaste))
        {
            if (regionState.BruteDamage <= FixedPoint2.Zero &&
                regionState.BurnDamage <= FixedPoint2.Zero)
            {
                failure = Loc.GetString("cmu-prosthetic-repair-no-damage");
                return false;
            }

            if (nanopaste.Uses <= 0)
            {
                failure = Loc.GetString("cmu-prosthetic-repair-empty-nanopaste");
                return false;
            }

            if (consume)
            {
                nanopaste.Uses--;
                Dirty(tool, nanopaste);
                if (nanopaste.Uses <= 0)
                    QueueDel(tool);
            }

            failure = string.Empty;
            return true;
        }

        failure = Loc.GetString("cmu-prosthetic-repair-bad-tool");
        return false;
    }

    private bool IsProstheticRepairTool(EntityUid tool)
    {
        return HasComp<BlowtorchComponent>(tool) ||
            HasComp<RMCCableCoilComponent>(tool) ||
            HasComp<CMUNanopasteComponent>(tool);
    }

    private static bool HasAnyProstheticRegion(HumanMedicalComponent medical)
    {
        foreach (var region in medical.Regions)
        {
            if (region.Presence == LimbPresence.Prosthetic)
                return true;
        }

        return false;
    }
}
