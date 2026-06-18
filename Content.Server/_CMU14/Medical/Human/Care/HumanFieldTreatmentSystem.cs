using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Targeting;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Server._CMU14.Medical.Human.Surgery;
using Content.Shared.Body.Systems;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Synth;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Server.Player;
using Robust.Server.GameObjects;
using BodyPartComponent = Content.Shared.Body.Part.BodyPartComponent;
using BodyPartSymmetry = Content.Shared.Body.Part.BodyPartSymmetry;
using BodyPartType = Content.Shared.Body.Part.BodyPartType;

namespace Content.Server._CMU14.Medical.Human.Care;

public sealed partial class HumanFieldTreatmentSystem : EntitySystem
{
    private static readonly TimeSpan BaseTreatmentDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RepeatedTreatmentDelay = TimeSpan.FromSeconds(0.2);
    private static readonly FixedPoint2 SurgicalLineRepairAmount = FixedPoint2.New(10);
    private static readonly FixedPoint2 SynthGraftRepairAmount = FixedPoint2.New(10);

    private static readonly BodyRegion[] HeadRegions = { BodyRegion.Head };
    private static readonly BodyRegion[] ChestRegions = { BodyRegion.Chest };
    private static readonly BodyRegion[] GroinRegions = { BodyRegion.Groin };
    private static readonly BodyRegion[] LeftArmRegions = { BodyRegion.LeftArm };
    private static readonly BodyRegion[] RightArmRegions = { BodyRegion.RightArm };
    private static readonly BodyRegion[] LeftHandRegions = { BodyRegion.LeftHand };
    private static readonly BodyRegion[] RightHandRegions = { BodyRegion.RightHand };
    private static readonly BodyRegion[] LeftLegRegions = { BodyRegion.LeftLeg };
    private static readonly BodyRegion[] RightLegRegions = { BodyRegion.RightLeg };
    private static readonly BodyRegion[] LeftFootRegions = { BodyRegion.LeftFoot };
    private static readonly BodyRegion[] RightFootRegions = { BodyRegion.RightFoot };

    private static readonly BodyRegion[] PickerRegionOrder =
    {
        BodyRegion.Head,
        BodyRegion.Chest,
        BodyRegion.Groin,
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
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private HumanSurgeryToolSystem _humanSurgeryTools = default!;
    [Dependency] private HumanTreatmentSystem _humanTreatment = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private INetConfigurationManager _netConfig = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedBodyZoneTargetingSystem _zoneTargeting = default!;

    private readonly Dictionary<EntityUid, PendingFieldTreatment> _pendingTreatments = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<HumanMedicalComponent>(BodyPartPickerUIKey.Key, subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnBodyPartPickerClosed);
            subs.Event<BodyPartPickerSelectMessage>(OnBodyPartPickerSelect);
        });

        SubscribeLocalEvent<CMUWoundTreaterInterceptEvent>(OnWoundTreaterIntercept);
        SubscribeLocalEvent<HumanMedicalComponent, HumanFieldTreatmentDoAfterEvent>(OnFieldTreatmentDoAfter);
    }

    private bool IsLayerEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled) &&
            _cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled);
    }

    private void OnWoundTreaterIntercept(ref CMUWoundTreaterInterceptEvent args)
    {
        if (args.Handled ||
            !IsLayerEnabled() ||
            HasComp<SynthComponent>(args.Patient) ||
            !TryComp<WoundTreaterComponent>(args.Treater, out var treater) ||
            !TryComp<HumanMedicalComponent>(args.Patient, out var medical))
        {
            return;
        }

        if (_humanSurgeryTools.TryHandleSurgeryToolInteraction(
                args.User,
                args.Patient,
                args.Treater,
                medical,
                popupNoProcedure: false))
        {
            args.Handled = true;
            return;
        }

        args.Handled = true;
        if (!TryGetMedicalActionKind(treater, out var action))
        {
            PopupNoTreatment(args.User, args.Patient, treater);
            return;
        }

        if (!HasEnoughUses(args.Treater, treater))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-human-medical-treatment-empty"),
                args.Patient,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        var recoveryAmount = ResolveTreatmentRecoveryAmount(args.User, treater);
        if (TryCreateTargetedTreatmentAttempt(
                args.User,
                medical,
                action,
                treater,
                recoveryAmount,
                out var attempt,
                out var targetZone))
        {
            StartFieldTreatment(
                args.User,
                args.Patient,
                args.Treater,
                treater,
                action,
                recoveryAmount,
                attempt,
                medical,
                chainByZone: true,
                targetZone);
            return;
        }

        if (IsTreatmentPickerDisabled(args.User))
        {
            PopupNoTreatment(args.User, args.Patient, treater);
            return;
        }

        OpenTreatmentPicker(args.User, args.Patient, args.Treater, treater, medical, action);
    }

    private void OnBodyPartPickerClosed(Entity<HumanMedicalComponent> patient, ref BoundUIClosedEvent args)
    {
        if (_pendingTreatments.TryGetValue(args.Actor, out var pending) &&
            pending.Patient == patient.Owner)
        {
            _pendingTreatments.Remove(args.Actor);
        }
    }

    private void OnBodyPartPickerSelect(Entity<HumanMedicalComponent> patient, ref BodyPartPickerSelectMessage args)
    {
        var user = args.Actor;
        if (!_pendingTreatments.TryGetValue(user, out var pending) ||
            pending.Patient != patient.Owner)
        {
            return;
        }

        _pendingTreatments.Remove(user);
        _ui.CloseUi(patient.Owner, BodyPartPickerUIKey.Key, user);

        if (!IsLayerEnabled() ||
            HasComp<SynthComponent>(patient.Owner) ||
            !TryComp<WoundTreaterComponent>(pending.Treater, out var treater) ||
            !_hands.IsHolding((user, null), pending.Treater) ||
            !TryGetEntity(args.Part, out var partUid) ||
            partUid is not { } selectedPart ||
            !TryComp<BodyPartComponent>(selectedPart, out var part) ||
            !_body.BodyHasChild(patient.Owner, selectedPart, part: part) ||
            !PartCanRepresentRegion(selectedPart, part, args.Region) ||
            !CanTargetRegionForFieldTreatment(patient.Comp, args.Region))
        {
            return;
        }

        if (!HasEnoughUses(pending.Treater, treater))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-human-medical-treatment-empty"),
                patient.Owner,
                user,
                PopupType.SmallCaution);
            return;
        }

        if (!TryCreateTreatmentAttempt(
                patient.Comp,
                pending.Action,
                args.Region,
                treater,
                pending.RecoveryAmount,
                out var attempt))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-human-medical-treatment-failed"),
                patient.Owner,
                user,
                PopupType.SmallCaution);
            return;
        }

        StartFieldTreatment(
            user,
            patient.Owner,
            pending.Treater,
            treater,
            pending.Action,
            pending.RecoveryAmount,
            attempt,
            patient.Comp,
            chainByZone: false,
            default);
    }

    private void StartFieldTreatment(
        EntityUid user,
        EntityUid patient,
        EntityUid treaterUid,
        WoundTreaterComponent treater,
        MedicalActionKind action,
        FixedPoint2 recoveryAmount,
        TreatmentAttempt attempt,
        HumanMedicalComponent medical,
        bool chainByZone,
        TargetBodyZone targetZone,
        TimeSpan minimumDelay = default)
    {
        var delay = ResolveTreatmentDelay(user, patient, treaterUid, treater);
        if (delay < minimumDelay)
            delay = minimumDelay;

        if (delay <= TimeSpan.Zero)
        {
            if (TryApplyTreatment(user, patient, treaterUid, treater, attempt, medical))
            {
                TryContinueFieldTreatment(
                    user,
                    patient,
                    treaterUid,
                    treater,
                    medical,
                    action,
                    recoveryAmount,
                    attempt.Region,
                    chainByZone,
                    targetZone);
            }

            return;
        }

        var ev = new HumanFieldTreatmentDoAfterEvent(
            attempt,
            action,
            recoveryAmount,
            chainByZone,
            targetZone);
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            delay,
            ev,
            patient,
            target: patient,
            used: treaterUid)
        {
            BreakOnMove = true,
            BreakOnHandChange = true,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameTarget,
            MovementThreshold = 0.5f,
            TargetEffect = "RMCEffectHealBusy",
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _audio.PlayPvs(treater.TreatBeginSound, user);
        if (user != patient && treater.TargetStartPopup is { } startPopup)
        {
            _popup.PopupEntity(
                Loc.GetString(startPopup, ("user", user)),
                patient,
                patient,
                PopupType.Medium);
        }
    }

    private void OnFieldTreatmentDoAfter(Entity<HumanMedicalComponent> patient, ref HumanFieldTreatmentDoAfterEvent args)
    {
        var user = args.User;
        if (args.Cancelled ||
            !IsLayerEnabled() ||
            args.Used is not { } used ||
            HasComp<SynthComponent>(patient.Owner) ||
            !TryComp<WoundTreaterComponent>(used, out var treater))
        {
            return;
        }

        if (!TryApplyTreatment(user, patient.Owner, used, treater, args.Attempt, patient.Comp))
            return;

        TryContinueFieldTreatment(
            user,
            patient.Owner,
            used,
            treater,
            patient.Comp,
            args.Action,
            args.RecoveryAmount,
            args.Attempt.Region,
            args.ChainByZone,
            args.TargetZone);
    }

    private bool TryApplyTreatment(
        EntityUid user,
        EntityUid patient,
        EntityUid treaterUid,
        WoundTreaterComponent treater,
        TreatmentAttempt attempt,
        HumanMedicalComponent medical)
    {
        if (!HasEnoughUses(treaterUid, treater))
            return false;

        var result = _humanTreatment.TryApplyTreatment(patient, attempt, medical);
        if (!result.Applied)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-human-medical-treatment-failed"),
                patient,
                user,
                PopupType.SmallCaution);
            return false;
        }

        _audio.PlayPvs(treater.TreatEndSound, user);
        ConsumeTreater(treaterUid, treater);
        PopupTreatmentFinished(user, patient, treater);
        return true;
    }

    private void OpenTreatmentPicker(
        EntityUid user,
        EntityUid patient,
        EntityUid treaterUid,
        WoundTreaterComponent treater,
        HumanMedicalComponent medical,
        MedicalActionKind action)
    {
        var entries = BuildBodyPartPickerEntries(patient, medical);
        if (entries.Count == 0)
        {
            PopupNoTreatment(user, patient, treater);
            return;
        }

        var recoveryAmount = ResolveTreatmentRecoveryAmount(user, treater);
        _pendingTreatments[user] = new PendingFieldTreatment(
            patient,
            treaterUid,
            action,
            recoveryAmount);

        _ui.SetUiState(
            patient,
            BodyPartPickerUIKey.Key,
            new BodyPartPickerBuiState(GetNetEntity(patient), entries));
        _ui.OpenUi(patient, BodyPartPickerUIKey.Key, user);
    }

    private bool IsTreatmentPickerDisabled(EntityUid user)
    {
        return _players.TryGetSessionByEntity(user, out var session) &&
            _netConfig.GetClientCVar(session.Channel, CMUMedicalCCVars.DisableWoundTreatmentRadial);
    }

    private List<BodyPartPickerEntry> BuildBodyPartPickerEntries(
        EntityUid patient,
        HumanMedicalComponent medical)
    {
        var entries = new List<BodyPartPickerEntry>(PickerRegionOrder.Length);
        foreach (var region in PickerRegionOrder)
        {
            if (!CanTargetRegionForFieldTreatment(medical, region))
                continue;
            if (!TryFindPartForRegion(patient, region, out var partUid, out var part))
                continue;

            entries.Add(new BodyPartPickerEntry(
                GetNetEntity(partUid),
                part.PartType,
                part.Symmetry,
                CountOpenTreatmentTargets(medical, region),
                region.ToString(),
                region,
                ResolveRadialStatus(medical, region)));
        }

        entries.Sort(static (left, right) =>
            RegionSortIndex(left.Region).CompareTo(RegionSortIndex(right.Region)));
        return entries;
    }

    private bool TryFindPartForRegion(
        EntityUid patient,
        BodyRegion region,
        out EntityUid partUid,
        out BodyPartComponent part)
    {
        foreach (var (candidateUid, candidatePart) in _body.GetBodyChildren(patient))
        {
            if (!PartCanRepresentRegion(candidateUid, candidatePart, region))
                continue;

            partUid = candidateUid;
            part = candidatePart;
            return true;
        }

        partUid = default;
        part = default!;
        return false;
    }

    private bool PartCanRepresentRegion(EntityUid partUid, BodyPartComponent part, BodyRegion region)
    {
        if (region == BodyRegion.Groin)
            return part.PartType == BodyPartType.Torso;

        return ResolvePartRegion(partUid, part) == region;
    }

    private BodyRegion ResolvePartRegion(EntityUid partUid, BodyPartComponent part)
    {
        if (TryComp<AnatomyRegionComponent>(partUid, out var anatomy) &&
            anatomy.Region != BodyRegion.None)
        {
            return anatomy.Region;
        }

        return RegionForPart(part.PartType, part.Symmetry);
    }

    private static BodyRegion RegionForPart(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return (type, symmetry) switch
        {
            (BodyPartType.Head, _) => BodyRegion.Head,
            (BodyPartType.Torso, _) => BodyRegion.Chest,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => BodyRegion.LeftArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => BodyRegion.RightArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => BodyRegion.LeftHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => BodyRegion.RightHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => BodyRegion.LeftLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => BodyRegion.RightLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => BodyRegion.LeftFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => BodyRegion.RightFoot,
            _ => BodyRegion.None,
        };
    }

    private static bool CanTargetRegionForFieldTreatment(HumanMedicalComponent medical, BodyRegion region)
    {
        if (region == BodyRegion.None)
            return false;

        var regionState = HumanMedicalLedger.GetRegion(medical, region);
        if (regionState.Presence == LimbPresence.Present)
            return true;

        return CountOpenTreatmentTargets(medical, region) > 0;
    }

    private static int CountOpenTreatmentTargets(HumanMedicalComponent medical, BodyRegion region)
    {
        var count = 0;
        foreach (var bleed in medical.BleedSources)
        {
            if (bleed.Region == region && bleed.Active)
                count++;
        }

        foreach (var injury in medical.Injuries)
        {
            if (injury.Region != region ||
                injury.Flags.HasFlag(InjuryFlags.Closed) ||
                injury.Damage <= FixedPoint2.Zero)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static BodyPartPickerRadialStatus ResolveRadialStatus(
        HumanMedicalComponent medical,
        BodyRegion region)
    {
        var regionState = HumanMedicalLedger.GetRegion(medical, region);
        if (regionState.Incision != IncisionDepth.Closed || HasOpenStump(medical, region))
            return BodyPartPickerRadialStatus.Surgery;

        var hasBrute = regionState.BruteDamage > FixedPoint2.Zero || HasActiveBleed(medical, region);
        var hasBurn = regionState.BurnDamage > FixedPoint2.Zero;

        foreach (var injury in medical.Injuries)
        {
            if (injury.Region != region ||
                injury.Flags.HasFlag(InjuryFlags.Closed) ||
                injury.Damage <= FixedPoint2.Zero)
            {
                continue;
            }

            if (injury.Kind == InjuryKind.Burn)
                hasBurn = true;
            else
                hasBrute = true;
        }

        return (hasBrute, hasBurn) switch
        {
            (true, true) => BodyPartPickerRadialStatus.Both,
            (true, false) => BodyPartPickerRadialStatus.Brute,
            (false, true) => BodyPartPickerRadialStatus.Burn,
            _ => BodyPartPickerRadialStatus.Uninjured,
        };
    }

    private static bool HasOpenStump(HumanMedicalComponent medical, BodyRegion region)
    {
        foreach (var injury in medical.Injuries)
        {
            if (injury.Region == region &&
                injury.IsOpenStump &&
                !injury.Flags.HasFlag(InjuryFlags.Closed) &&
                injury.Damage > FixedPoint2.Zero)
            {
                return true;
            }
        }

        foreach (var bleed in medical.BleedSources)
        {
            if (bleed.Region == region &&
                bleed.Kind == BleedKind.Stump &&
                bleed.Active)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasActiveBleed(HumanMedicalComponent medical, BodyRegion region)
    {
        foreach (var bleed in medical.BleedSources)
        {
            if (bleed.Region == region && bleed.Active)
                return true;
        }

        return false;
    }

    private static int RegionSortIndex(BodyRegion region)
    {
        for (var i = 0; i < PickerRegionOrder.Length; i++)
        {
            if (PickerRegionOrder[i] == region)
                return i;
        }

        return PickerRegionOrder.Length;
    }

    private static bool TryCreateTreatmentAttempt(
        HumanMedicalComponent medical,
        MedicalActionKind action,
        BodyRegion region,
        WoundTreaterComponent treater,
        FixedPoint2 recoveryAmount,
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

        if (!MedicalTreatmentActionRules.TryCreateHumanTreatmentAttempt(request, out attempt))
            return false;

        attempt = action switch
        {
            MedicalActionKind.ApplyGauze => CreateGauzeAttempt(
                medical,
                region,
                treater.CMUStopsArterialBleeding,
                recoveryAmount),
            MedicalActionKind.ApplySalve => attempt with { Amount = recoveryAmount },
            MedicalActionKind.ApplySuture => CreateSutureAttempt(medical, region),
            MedicalActionKind.ApplySurgicalLine => attempt with { Amount = SurgicalLineRepairAmount },
            MedicalActionKind.ApplySyntheticGraft => attempt with { Amount = SynthGraftRepairAmount },
            _ => attempt,
        };

        if (attempt.Region == BodyRegion.None)
            return false;

        return TreatmentRules.TryCreateTreatmentPlan(medical, attempt).Applied;
    }

    private bool TryCreateTargetedTreatmentAttempt(
        EntityUid user,
        HumanMedicalComponent medical,
        MedicalActionKind action,
        WoundTreaterComponent treater,
        FixedPoint2 recoveryAmount,
        out TreatmentAttempt attempt,
        out TargetBodyZone targetZone)
    {
        if (_zoneTargeting.TryGetFreshSelection(user) is not { } zone)
        {
            attempt = default;
            targetZone = default;
            return false;
        }

        targetZone = zone;
        return TryCreateTreatmentAttemptForZone(
            medical,
            action,
            treater,
            recoveryAmount,
            zone,
            out attempt);
    }

    private static bool TryCreateTreatmentAttemptForZone(
        HumanMedicalComponent medical,
        MedicalActionKind action,
        WoundTreaterComponent treater,
        FixedPoint2 recoveryAmount,
        TargetBodyZone zone,
        out TreatmentAttempt attempt)
    {
        foreach (var region in RegionsForZone(zone))
        {
            if (TryCreateTreatmentAttemptForRegion(
                    medical,
                    action,
                    region,
                    treater,
                    recoveryAmount,
                    out attempt))
            {
                return true;
            }

            var stumpAnchor = LimbLossRules.GetStumpAnchorRegion(region);
            if (stumpAnchor == BodyRegion.None ||
                stumpAnchor == region ||
                !HasOpenStump(medical, stumpAnchor))
            {
                continue;
            }

            if (TryCreateTreatmentAttemptForRegion(
                    medical,
                    action,
                    stumpAnchor,
                    treater,
                    recoveryAmount,
                    out attempt))
            {
                return true;
            }
        }

        attempt = default;
        return false;
    }

    private static bool TryCreateTreatmentAttemptForRegion(
        HumanMedicalComponent medical,
        MedicalActionKind action,
        BodyRegion region,
        WoundTreaterComponent treater,
        FixedPoint2 recoveryAmount,
        out TreatmentAttempt attempt)
    {
        if (!CanTargetRegionForFieldTreatment(medical, region))
        {
            attempt = default;
            return false;
        }

        return TryCreateTreatmentAttempt(
            medical,
            action,
            region,
            treater,
            recoveryAmount,
            out attempt);
    }

    private void TryContinueFieldTreatment(
        EntityUid user,
        EntityUid patient,
        EntityUid treaterUid,
        WoundTreaterComponent treater,
        HumanMedicalComponent medical,
        MedicalActionKind action,
        FixedPoint2 recoveryAmount,
        BodyRegion region,
        bool chainByZone,
        TargetBodyZone targetZone)
    {
        if (!IsLayerEnabled() ||
            HasComp<SynthComponent>(patient) ||
            !HasEnoughUses(treaterUid, treater) ||
            !_hands.IsHolding((user, null), treaterUid))
        {
            return;
        }

        var hasNext = chainByZone
            ? TryCreateTreatmentAttemptForZone(
                medical,
                action,
                treater,
                recoveryAmount,
                targetZone,
                out var nextAttempt)
            : TryCreateTreatmentAttempt(
                medical,
                action,
                region,
                treater,
                recoveryAmount,
                out nextAttempt);

        if (!hasNext)
            return;

        StartFieldTreatment(
            user,
            patient,
            treaterUid,
            treater,
            action,
            recoveryAmount,
            nextAttempt,
            medical,
            chainByZone,
            targetZone,
            RepeatedTreatmentDelay);
    }

    private static TreatmentAttempt CreateGauzeAttempt(
        HumanMedicalComponent medical,
        BodyRegion region,
        bool stopsArterialBleeding,
        FixedPoint2 recoveryAmount)
    {
        if (MedicalBleedControlRules.TryCreateBleedControlAttempt(
                medical,
                region,
                stopsArterialBleeding,
                out var attempt,
                out _))
        {
            return attempt with { Amount = recoveryAmount };
        }

        return default;
    }

    private static TreatmentAttempt CreateSutureAttempt(HumanMedicalComponent medical, BodyRegion region)
    {
        foreach (var injury in medical.Injuries)
        {
            if (injury.Region != region ||
                injury.Flags.HasFlag(InjuryFlags.Sutured) ||
                injury.Flags.HasFlag(InjuryFlags.Closed) ||
                !IsSuturable(injury.Kind))
            {
                continue;
            }

            var bleedSourceId = FindLinkedBleedSourceId(medical, injury);
            return new TreatmentAttempt(
                TreatmentKind.Suture,
                region,
                InjuryId: injury.Id,
                BleedSourceId: bleedSourceId);
        }

        return new TreatmentAttempt(TreatmentKind.Suture, region);
    }

    private static int FindLinkedBleedSourceId(HumanMedicalComponent medical, InjuryRecord injury)
    {
        foreach (var bleed in medical.BleedSources)
        {
            if (!bleed.Active)
                continue;
            if (bleed.SourceInjuryId == injury.Id || bleed.Region == injury.Region)
                return bleed.Id;
        }

        return 0;
    }

    private static bool TryGetMedicalActionKind(WoundTreaterComponent treater, out MedicalActionKind action)
    {
        if (treater.CMUMedicalAction is { } actionOverride &&
            actionOverride != MedicalActionKind.None)
        {
            action = actionOverride;
            return true;
        }

        if (!treater.CMUTreatsWounds)
        {
            action = treater.Wound switch
            {
                WoundType.Burn => MedicalActionKind.ApplySyntheticGraft,
                WoundType.Brute => MedicalActionKind.ApplySurgicalLine,
                _ => MedicalActionKind.None,
            };

            return action != MedicalActionKind.None;
        }

        action = treater.Wound switch
        {
            WoundType.Burn => MedicalActionKind.ApplySalve,
            WoundType.Brute => MedicalActionKind.ApplyGauze,
            _ => MedicalActionKind.None,
        };

        return action != MedicalActionKind.None;
    }

    private static bool IsSuturable(InjuryKind kind)
    {
        return kind is InjuryKind.Cut or InjuryKind.Puncture or InjuryKind.Stump or InjuryKind.SurgicalIncision;
    }

    private static BodyRegion[] RegionsForZone(TargetBodyZone zone)
    {
        return zone switch
        {
            TargetBodyZone.Head => HeadRegions,
            TargetBodyZone.Chest => ChestRegions,
            TargetBodyZone.GroinPelvis => GroinRegions,
            TargetBodyZone.LeftArm => LeftArmRegions,
            TargetBodyZone.RightArm => RightArmRegions,
            TargetBodyZone.LeftHand => LeftHandRegions,
            TargetBodyZone.RightHand => RightHandRegions,
            TargetBodyZone.LeftLeg => LeftLegRegions,
            TargetBodyZone.RightLeg => RightLegRegions,
            TargetBodyZone.LeftFoot => LeftFootRegions,
            TargetBodyZone.RightFoot => RightFootRegions,
            _ => ChestRegions,
        };
    }

    private FixedPoint2 ResolveTreatmentRecoveryAmount(
        EntityUid user,
        WoundTreaterComponent treater)
    {
        var hasSkills = _skills.HasAllSkills(user, treater.Skills);
        var damage = hasSkills
            ? treater.Damage
            : treater.UnskilledDamage ?? treater.Damage;

        if (damage is { } value && value != FixedPoint2.Zero)
            return FixedPoint2.Abs(value);

        return FixedPoint2.Zero;
    }

    private TimeSpan ResolveTreatmentDelay(
        EntityUid user,
        EntityUid patient,
        EntityUid treaterUid,
        WoundTreaterComponent treater)
    {
        if (treater.InstantWoundTreatment ||
            (treater.InstantWoundTreatmentSkills.Count > 0 &&
             _skills.HasAllSkills(user, treater.InstantWoundTreatmentSkills)))
        {
            return TimeSpan.Zero;
        }

        var delay = BaseTreatmentDelay + _skills.GetDelay(user, treaterUid);
        var multiplier = _skills.GetSkillDelayMultiplier(user, treater.DoAfterSkill, treater.DoAfterSkillMultipliers);
        if (user == patient)
            multiplier *= treater.SelfTargetDoAfterMultiplier;

        return delay * multiplier;
    }

    private bool HasEnoughUses(EntityUid treaterUid, WoundTreaterComponent treater)
    {
        return !treater.Consumable ||
            !TryComp<StackComponent>(treaterUid, out var stack) ||
            _stacks.GetCount(treaterUid, stack) > 0;
    }

    private void ConsumeTreater(EntityUid treaterUid, WoundTreaterComponent treater)
    {
        if (!treater.Consumable || !_net.IsServer)
            return;

        if (TryComp<StackComponent>(treaterUid, out var stack))
        {
            _stacks.Use(treaterUid, 1, stack);
            return;
        }

        QueueDel(treaterUid);
    }

    private void PopupNoTreatment(EntityUid user, EntityUid patient, WoundTreaterComponent treater)
    {
        if (user == patient)
        {
            if (treater.NoneSelfPopup is { } selfPopup)
                _popup.PopupEntity(Loc.GetString(selfPopup), patient, user, PopupType.SmallCaution);

            return;
        }

        if (treater.NoneOtherPopup is { } otherPopup)
            _popup.PopupEntity(Loc.GetString(otherPopup, ("target", patient)), patient, user, PopupType.SmallCaution);
    }

    private void PopupTreatmentFinished(EntityUid user, EntityUid patient, WoundTreaterComponent treater)
    {
        var userPopup = treater.UserFinishPopup ?? treater.UserPopup;
        var targetPopup = treater.TargetFinishPopup ?? treater.TargetPopup;

        if (userPopup != null)
            _popup.PopupEntity(Loc.GetString(userPopup, ("target", patient)), patient, user);

        if (user != patient && targetPopup != null)
            _popup.PopupEntity(Loc.GetString(targetPopup, ("user", user)), patient, patient);
    }

    private readonly record struct PendingFieldTreatment(
        EntityUid Patient,
        EntityUid Treater,
        MedicalActionKind Action,
        FixedPoint2 RecoveryAmount);
}
