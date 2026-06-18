using System;
using System.Collections.Generic;
using Content.Server.StatusEffectNew;
using Content.Shared._CMU14.Medical.Targeting;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Surgery;
using Content.Shared._CMU14.Medical.Synthetic;
using Content.Shared._RMC14.Repairable;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using Content.Shared.Standing;
using Content.Shared.Tools.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Synthetic;

public sealed partial class CMUSynthLimbSurgerySystem : EntitySystem
{
    private static readonly TimeSpan CutDelay = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan WireDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DetachDelay = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan AttachDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan WeldDelay = TimeSpan.FromSeconds(2.5);
    private static readonly FixedPoint2 WelderFuelCost = FixedPoint2.New(5);
    private const int CableCost = 1;
    private const string PryingQuality = "Prying";

    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedBodyZoneTargetingSystem _bodyZone = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCRepairableSystem _repairable = default!;
    [Dependency] private HumanSurgeryModeSystem _surgeryMode = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedSynthSystem _synth = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SynthComponent, RMCSynthRepairToolUseAttemptEvent>(OnSynthRepairToolUseAttempt);
        SubscribeLocalEvent<SynthComponent, CMUSynthLimbSurgeryDoAfterEvent>(OnSurgeryDoAfter);
    }

    private void OnSynthRepairToolUseAttempt(Entity<SynthComponent> synth, ref RMCSynthRepairToolUseAttemptEvent args)
    {
        if (args.Handled)
            return;

        var canReach = _interaction.InRangeUnobstructed(args.User, synth.Owner);
        if (TryStartSurgeryStep(synth, args.User, args.Used, canReach))
            args.Handled = true;
    }

    private bool TryStartSurgeryStep(
        Entity<SynthComponent> synth,
        EntityUid user,
        EntityUid used,
        bool canReach)
    {
        if (!canReach)
        {
            return false;
        }

        if (!HasActiveSynthSurgery(synth.Owner) &&
            !_surgeryMode.IsSurgeryModeEnabled(user) &&
            ShouldLetDirectSynthRepairHandle(synth, used))
        {
            return false;
        }

        if (!TryCreateStep(synth, user, used, out var step, out var slotId, out var failure))
        {
            if (failure != null)
            {
                _popup.PopupEntity(failure, synth.Owner, user, PopupType.SmallCaution);
                return true;
            }

            return false;
        }

        if (!IsLyingDownForSurgery(synth.Owner))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-synth-surgery-needs-down"),
                synth.Owner,
                user,
                PopupType.SmallCaution);
            return true;
        }

        if (!CanStartResourceStep(user, used, step))
            return true;

        var ev = new CMUSynthLimbSurgeryDoAfterEvent
        {
            Step = step,
            SlotId = slotId,
        };

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            DelayForStep(step),
            ev,
            synth.Owner,
            target: synth.Owner,
            used: used)
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
            return true;

        _popup.PopupEntity(
            Loc.GetString(StartLoc(step), ("part", PartName(slotId))),
            synth.Owner,
            user);
        return true;
    }

    private void OnSurgeryDoAfter(Entity<SynthComponent> synth, ref CMUSynthLimbSurgeryDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (args.Used is not { } used)
        {
            return;
        }

        var user = args.User;

        var applied = args.Step switch
        {
            CMUSynthLimbSurgeryStep.CutChassis => TryApplyCutChassis(synth.Owner, user, used, args.SlotId),
            CMUSynthLimbSurgeryStep.StripWiring => TryApplyStripWiring(synth.Owner, user, used, args.SlotId),
            CMUSynthLimbSurgeryStep.DetachLimb => TryApplyDetachLimb(synth.Owner, user, args.SlotId),
            CMUSynthLimbSurgeryStep.AttachLimb => TryApplyAttachLimb(synth.Owner, user, used, args.SlotId),
            CMUSynthLimbSurgeryStep.WeldChassis => TryApplyWeldChassis(synth.Owner, user, used, args.SlotId),
            _ => false,
        };

        if (!applied)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-synth-surgery-step-failed"),
                synth.Owner,
                user,
                PopupType.SmallCaution);
        }
    }

    private bool TryCreateStep(
        Entity<SynthComponent> synth,
        EntityUid user,
        EntityUid used,
        out CMUSynthLimbSurgeryStep step,
        out string slotId,
        out string? failure)
    {
        step = default;
        slotId = string.Empty;
        failure = null;

        if (TryComp<BodyPartComponent>(used, out var usedPart))
        {
            if (!TryFindTargetForHeldLimb(synth.Owner, used, usedPart, out var target))
            {
                failure = Loc.GetString("cmu-synth-surgery-limb-no-slot");
                return false;
            }

            slotId = target.SlotId;
            if (!TryGetSurgeryState(synth.Owner, slotId, out var state) ||
                state.Stage != CMUSynthLimbSurgeryStage.WiringPrepped)
            {
                failure = Loc.GetString(
                    "cmu-synth-surgery-limb-needs-prep",
                    ("part", PartName(slotId)));
                return false;
            }

            step = CMUSynthLimbSurgeryStep.AttachLimb;
            return true;
        }

        if (IsCableCoil(used))
        {
            if (TryFindPreparedState(synth.Owner, user, CMUSynthLimbSurgeryStage.ChassisOpen, out var state))
            {
                slotId = state.SlotId;
                step = CMUSynthLimbSurgeryStep.StripWiring;
                return true;
            }

            if (HasMissingSynthSlot(synth.Owner))
                failure = Loc.GetString("cmu-synth-surgery-needs-cut-first");

            return false;
        }

        if (IsPryingTool(used))
        {
            if (TryFindPreparedState(synth.Owner, user, CMUSynthLimbSurgeryStage.WiringPrepped, out var prepped) &&
                TryFindAttachedSlot(synth.Owner, prepped.SlotId, out _))
            {
                slotId = prepped.SlotId;
                step = CMUSynthLimbSurgeryStep.DetachLimb;
                return true;
            }

            if (HasActiveSynthSurgery(synth.Owner))
                failure = Loc.GetString("cmu-synth-surgery-next-pry");

            return false;
        }

        if (IsWelder(used, synth.Comp))
        {
            if (TryFindPreparedState(synth.Owner, user, CMUSynthLimbSurgeryStage.LimbAttached, out var attached))
            {
                slotId = attached.SlotId;
                step = CMUSynthLimbSurgeryStep.WeldChassis;
                return true;
            }

            if (TryFindPreparedState(synth.Owner, user, CMUSynthLimbSurgeryStage.ChassisOpen, out var open))
            {
                failure = Loc.GetString(
                    "cmu-synth-surgery-next-cable",
                    ("part", PartName(open.SlotId)));
                return false;
            }

            if (TryFindPreparedState(synth.Owner, user, CMUSynthLimbSurgeryStage.WiringPrepped, out var prepped))
            {
                if (TryFindAttachedSlot(synth.Owner, prepped.SlotId, out _))
                {
                    failure = Loc.GetString(
                        "cmu-synth-surgery-next-pry-part",
                        ("part", PartName(prepped.SlotId)));
                    return false;
                }

                if (TryFindMissingSlot(synth.Owner, prepped.SlotId, out _))
                {
                    slotId = prepped.SlotId;
                    step = CMUSynthLimbSurgeryStep.WeldChassis;
                    return true;
                }
            }

            if (TryFindMissingSlotForTool(synth.Owner, user, out var target))
            {
                slotId = target.SlotId;
                step = CMUSynthLimbSurgeryStep.CutChassis;
                return true;
            }

            if (_surgeryMode.IsSurgeryModeEnabled(user) &&
                TryFindAttachedSlotForTool(synth.Owner, user, out var attachedTarget))
            {
                slotId = attachedTarget.SlotId;
                step = CMUSynthLimbSurgeryStep.CutChassis;
                return true;
            }

            if (_surgeryMode.IsSurgeryModeEnabled(user))
            {
                failure = Loc.GetString("cmu-synth-surgery-target-limb");
                return false;
            }
        }

        return false;
    }

    private bool TryApplyCutChassis(
        EntityUid patient,
        EntityUid user,
        EntityUid tool,
        string slotId)
    {
        if (TryGetSurgeryState(patient, slotId, out _))
        {
            return false;
        }

        if (TryFindMissingSlot(patient, slotId, out var missingTarget))
        {
            if (!UseWelderFuel(tool, user))
                return false;

            var comp = EnsureComp<CMUSynthLimbSurgeryComponent>(patient);
            comp.Slots.Add(new CMUSynthLimbSurgeryState
            {
                SlotId = missingTarget.SlotId,
                Parent = missingTarget.Parent,
                Type = missingTarget.Type,
                Symmetry = missingTarget.Symmetry,
                Stage = CMUSynthLimbSurgeryStage.ChassisOpen,
            });
        }
        else if (TryFindAttachedSlot(patient, slotId, out var attachedTarget))
        {
            if (!UseWelderFuel(tool, user))
                return false;

            var comp = EnsureComp<CMUSynthLimbSurgeryComponent>(patient);
            comp.Slots.Add(new CMUSynthLimbSurgeryState
            {
                SlotId = attachedTarget.SlotId,
                Parent = attachedTarget.Parent,
                Type = attachedTarget.Type,
                Symmetry = attachedTarget.Symmetry,
                Stage = CMUSynthLimbSurgeryStage.ChassisOpen,
            });
        }
        else
        {
            return false;
        }

        _popup.PopupEntity(
            Loc.GetString("cmu-synth-surgery-cut-finished", ("part", PartName(slotId))),
            patient,
            user);
        return true;
    }

    private bool TryApplyStripWiring(
        EntityUid patient,
        EntityUid user,
        EntityUid tool,
        string slotId)
    {
        if (!TryGetSurgeryState(patient, slotId, out var state) ||
            state.Stage != CMUSynthLimbSurgeryStage.ChassisOpen ||
            !UseCable(tool))
        {
            return false;
        }

        state.Stage = CMUSynthLimbSurgeryStage.WiringPrepped;
        _popup.PopupEntity(
            Loc.GetString("cmu-synth-surgery-wire-finished", ("part", PartName(slotId))),
            patient,
            user);
        return true;
    }

    private bool TryApplyDetachLimb(
        EntityUid patient,
        EntityUid user,
        string slotId)
    {
        if (!TryGetSurgeryState(patient, slotId, out var state) ||
            state.Stage != CMUSynthLimbSurgeryStage.WiringPrepped ||
            !TryFindAttachedSlot(patient, slotId, out var target) ||
            !DetachPart(target.Part))
        {
            return false;
        }

        RefreshDetachedPart(patient, target.Part, target.PartComp);

        _popup.PopupEntity(
            Loc.GetString("cmu-synth-surgery-detach-finished", ("part", PartName(slotId))),
            patient,
            user);
        return true;
    }

    private bool TryApplyAttachLimb(
        EntityUid patient,
        EntityUid user,
        EntityUid limb,
        string slotId)
    {
        if (!TryGetSurgeryState(patient, slotId, out var state) ||
            state.Stage != CMUSynthLimbSurgeryStage.WiringPrepped ||
            !TryComp<BodyPartComponent>(limb, out var limbPart) ||
            limbPart.Body != null ||
            !TryFindMissingSlot(patient, slotId, out var target) ||
            target.Type != limbPart.PartType ||
            target.Symmetry != limbPart.Symmetry ||
            !_body.AttachPart(target.Parent, target.SlotId, limb, target.ParentPart, limbPart))
        {
            return false;
        }

        state.Parent = target.Parent;
        state.Stage = CMUSynthLimbSurgeryStage.LimbAttached;
        RefreshAttachedPart(patient, limb, limbPart);

        _popup.PopupEntity(
            Loc.GetString("cmu-synth-surgery-attach-finished", ("part", PartName(slotId))),
            patient,
            user);
        return true;
    }

    private bool TryApplyWeldChassis(
        EntityUid patient,
        EntityUid user,
        EntityUid tool,
        string slotId)
    {
        if (!TryComp<CMUSynthLimbSurgeryComponent>(patient, out var comp))
        {
            return false;
        }

        for (var i = 0; i < comp.Slots.Count; i++)
        {
            var state = comp.Slots[i];
            if (state.SlotId != slotId)
            {
                continue;
            }

            if (state.Stage != CMUSynthLimbSurgeryStage.LimbAttached &&
                (state.Stage != CMUSynthLimbSurgeryStage.WiringPrepped ||
                 !TryFindMissingSlot(patient, slotId, out _)))
            {
                continue;
            }

            if (!UseWelderFuel(tool, user))
                return false;

            comp.Slots.RemoveAt(i);
            if (comp.Slots.Count == 0)
                RemComp<CMUSynthLimbSurgeryComponent>(patient);

            _popup.PopupEntity(
                Loc.GetString("cmu-synth-surgery-weld-finished", ("part", PartName(slotId))),
                patient,
                user);
            return true;
        }

        return false;
    }

    private bool TryFindTargetForHeldLimb(
        EntityUid patient,
        EntityUid limb,
        BodyPartComponent limbPart,
        out SynthMissingSlot target)
    {
        target = default;
        if (limbPart.Body != null)
            return false;

        foreach (var candidate in EnumerateMissingSlots(patient))
        {
            if (candidate.Type != limbPart.PartType ||
                candidate.Symmetry != limbPart.Symmetry ||
                !_body.CanAttachPart(candidate.Parent, candidate.SlotId, limb, candidate.ParentPart, limbPart))
            {
                continue;
            }

            target = candidate;
            return true;
        }

        return false;
    }

    private bool TryFindMissingSlotForTool(
        EntityUid patient,
        EntityUid user,
        out SynthMissingSlot target)
    {
        if (_bodyZone.TryGetSelectedZone(user) is { } zone)
        {
            var (type, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(zone);
            if (type != BodyPartType.Torso &&
                TryFindMissingSlot(patient, type, symmetry, out target))
            {
                return true;
            }
        }

        foreach (var candidate in EnumerateMissingSlots(patient))
        {
            target = candidate;
            return true;
        }

        target = default;
        return false;
    }

    private bool TryFindAttachedSlotForTool(
        EntityUid patient,
        EntityUid user,
        out SynthAttachedSlot target)
    {
        target = default;
        if (_bodyZone.TryGetSelectedZone(user) is not { } zone)
            return false;

        var slotId = SlotForZone(zone);
        return slotId != null && TryFindAttachedSlot(patient, slotId, out target);
    }

    private bool TryFindPreparedState(
        EntityUid patient,
        EntityUid user,
        CMUSynthLimbSurgeryStage stage,
        out CMUSynthLimbSurgeryState state)
    {
        state = default!;
        if (!TryComp<CMUSynthLimbSurgeryComponent>(patient, out var comp))
            return false;

        if (_bodyZone.TryGetSelectedZone(user) is { } zone)
        {
            var selectedSlot = SlotForZone(zone);
            if (selectedSlot != null)
            {
                foreach (var candidate in comp.Slots)
                {
                    if (candidate.Stage == stage &&
                        candidate.SlotId == selectedSlot)
                    {
                        state = candidate;
                        return true;
                    }
                }
            }
        }

        foreach (var candidate in comp.Slots)
        {
            if (candidate.Stage != stage)
                continue;

            state = candidate;
            return true;
        }

        return false;
    }

    private bool TryGetSurgeryState(
        EntityUid patient,
        string slotId,
        out CMUSynthLimbSurgeryState state)
    {
        state = default!;
        if (!TryComp<CMUSynthLimbSurgeryComponent>(patient, out var comp))
            return false;

        foreach (var candidate in comp.Slots)
        {
            if (candidate.SlotId != slotId)
                continue;

            state = candidate;
            return true;
        }

        return false;
    }

    private bool TryFindMissingSlot(
        EntityUid patient,
        string slotId,
        out SynthMissingSlot target)
    {
        foreach (var candidate in EnumerateMissingSlots(patient))
        {
            if (candidate.SlotId != slotId)
                continue;

            target = candidate;
            return true;
        }

        target = default;
        return false;
    }

    private bool TryFindAttachedSlot(
        EntityUid patient,
        string slotId,
        out SynthAttachedSlot target)
    {
        target = default;
        if (!TryComp<BodyComponent>(patient, out var body))
            return false;

        foreach (var (parentUid, parentPart) in _body.GetBodyChildren(patient, body))
        {
            foreach (var (candidateSlotId, slot) in parentPart.Children)
            {
                if (candidateSlotId != slotId ||
                    !IsReattachableSlot(candidateSlotId, slot.Type) ||
                    !_containers.TryGetContainer(parentUid, SharedBodySystem.GetPartSlotContainerId(candidateSlotId), out var container))
                {
                    continue;
                }

                foreach (var partUid in container.ContainedEntities)
                {
                    if (!TryComp<BodyPartComponent>(partUid, out var part))
                        continue;

                    target = new SynthAttachedSlot(
                        parentUid,
                        parentPart,
                        partUid,
                        part,
                        candidateSlotId,
                        slot.Type,
                        SymmetryForSlot(candidateSlotId));
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryFindMissingSlot(
        EntityUid patient,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        out SynthMissingSlot target)
    {
        foreach (var candidate in EnumerateMissingSlots(patient))
        {
            if (candidate.Type != type ||
                candidate.Symmetry != symmetry)
            {
                continue;
            }

            target = candidate;
            return true;
        }

        target = default;
        return false;
    }

    private bool HasMissingSynthSlot(EntityUid patient)
    {
        foreach (var _ in EnumerateMissingSlots(patient))
            return true;

        return false;
    }

    private bool HasActiveSynthSurgery(EntityUid patient)
    {
        return TryComp<CMUSynthLimbSurgeryComponent>(patient, out var comp) &&
            comp.Slots.Count > 0;
    }

    private IEnumerable<SynthMissingSlot> EnumerateMissingSlots(EntityUid patient)
    {
        if (!TryComp<BodyComponent>(patient, out var body))
            yield break;

        foreach (var (parentUid, parentPart) in _body.GetBodyChildren(patient, body))
        {
            foreach (var (slotId, slot) in parentPart.Children)
            {
                if (!IsReattachableSlot(slotId, slot.Type) ||
                    (_containers.TryGetContainer(parentUid, SharedBodySystem.GetPartSlotContainerId(slotId), out var container) &&
                     container.ContainedEntities.Count > 0))
                {
                    continue;
                }

                yield return new SynthMissingSlot(
                    parentUid,
                    parentPart,
                    slotId,
                    slot.Type,
                    SymmetryForSlot(slotId));
            }
        }
    }

    private bool ShouldLetDirectSynthRepairHandle(Entity<SynthComponent> synth, EntityUid used)
    {
        if (IsCableCoil(used) &&
            _synth.HasDamage(synth.Owner, synth.Comp.CableCoilDamageGroup))
        {
            return true;
        }

        return IsWelder(used, synth.Comp) &&
            _synth.HasDamage(synth.Owner, synth.Comp.WelderDamageGroup);
    }

    private bool CanStartResourceStep(
        EntityUid user,
        EntityUid used,
        CMUSynthLimbSurgeryStep step)
    {
        return step switch
        {
            CMUSynthLimbSurgeryStep.CutChassis or CMUSynthLimbSurgeryStep.WeldChassis =>
                _repairable.UseFuel(used, user, WelderFuelCost, attempt: true),
            CMUSynthLimbSurgeryStep.StripWiring => HasCable(used),
            _ => true,
        };
    }

    private bool UseWelderFuel(EntityUid tool, EntityUid user)
    {
        return _repairable.UseFuel(tool, user, WelderFuelCost);
    }

    private bool UseCable(EntityUid tool)
    {
        return IsCableCoil(tool) && _stacks.Use(tool, CableCost);
    }

    private bool HasCable(EntityUid tool)
    {
        return TryComp<StackComponent>(tool, out var stack) &&
            _stacks.GetCount(tool, stack) >= CableCost;
    }

    private bool IsWelder(EntityUid used, SynthComponent synth)
    {
        return HasComp<BlowtorchComponent>(used) &&
            _tool.HasQuality(used, synth.RepairQuality);
    }

    private bool IsCableCoil(EntityUid used)
    {
        return HasComp<RMCCableCoilComponent>(used);
    }

    private bool IsPryingTool(EntityUid used)
    {
        return _tool.HasQuality(used, PryingQuality);
    }

    private bool DetachPart(EntityUid part)
    {
        if (!_containers.TryGetContainingContainer((part, null, null), out var container))
            return false;

        return _containers.Remove(part, container);
    }

    private bool IsLyingDownForSurgery(EntityUid patient)
    {
        if (_standing.IsDown(patient))
            return true;

        return TryComp<BuckleComponent>(patient, out var buckle) &&
            buckle.BuckledTo is { } buckledTo &&
            TryComp<StrapComponent>(buckledTo, out var strap) &&
            strap.Position == StrapPosition.Down;
    }

    private void RefreshAttachedPart(
        EntityUid patient,
        EntityUid limb,
        BodyPartComponent limbPart)
    {
        foreach (var (partUid, part) in _body.GetBodyPartChildren(limb, limbPart))
        {
            RemoveMissingStatus(patient, part.PartType, part.Symmetry);
            SetHumanoidLayer(patient, part.PartType, part.Symmetry, visible: true);
        }

        RestoreUsableHands(patient);
    }

    private void RefreshDetachedPart(
        EntityUid patient,
        EntityUid limb,
        BodyPartComponent limbPart)
    {
        foreach (var (partUid, part) in _body.GetBodyPartChildren(limb, limbPart))
        {
            _ = partUid;
            SetHumanoidLayer(patient, part.PartType, part.Symmetry, visible: false);
        }
    }

    private void SetHumanoidLayer(
        EntityUid body,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        bool visible)
    {
        if (!HasComp<HumanoidAppearanceComponent>(body))
            return;

        if (LayerForPart(type, symmetry) is not { } layer)
            return;

        _humanoid.SetLayerVisibility(body, layer, visible);
        _appearance.SetData(body, layer, !visible);
    }

    private void RestoreUsableHands(EntityUid body)
    {
        if (!TryComp<HandsComponent>(body, out var hands))
            return;

        foreach (var (partUid, part) in _body.GetBodyChildren(body))
        {
            if (part.PartType != BodyPartType.Hand)
                continue;

            var location = part.Symmetry switch
            {
                BodyPartSymmetry.Left => HandLocation.Left,
                BodyPartSymmetry.Right => HandLocation.Right,
                _ => HandLocation.Middle,
            };

            var slotId = part.Symmetry == BodyPartSymmetry.Left
                ? "left_hand"
                : part.Symmetry == BodyPartSymmetry.Right
                    ? "right_hand"
                    : null;
            if (slotId == null)
                continue;

            var handId = SharedBodySystem.GetPartSlotContainerId(slotId);
            if (!_hands.TrySetHandLocation((body, hands), handId, location))
                _hands.AddHand((body, hands), handId, location);
        }

        if (hands.ActiveHandId == null && hands.SortedHands.Count > 0)
            _hands.SetActiveHand((body, hands), hands.SortedHands[0]);
    }

    private void RemoveMissingStatus(
        EntityUid body,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        if (StatusForPart(type, symmetry) is { } status)
            _status.TryRemoveStatusEffect(body, status);
    }

    private static EntProtoId? StatusForPart(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return (type, symmetry) switch
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
    }

    private static HumanoidVisualLayers? LayerForPart(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return (type, symmetry) switch
        {
            (BodyPartType.Head, BodyPartSymmetry.None) => HumanoidVisualLayers.Head,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => HumanoidVisualLayers.LArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => HumanoidVisualLayers.RArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => HumanoidVisualLayers.LHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => HumanoidVisualLayers.RHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => HumanoidVisualLayers.LLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => HumanoidVisualLayers.RLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => HumanoidVisualLayers.LFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => HumanoidVisualLayers.RFoot,
            _ => null,
        };
    }

    private static TimeSpan DelayForStep(CMUSynthLimbSurgeryStep step)
    {
        return step switch
        {
            CMUSynthLimbSurgeryStep.CutChassis => CutDelay,
            CMUSynthLimbSurgeryStep.StripWiring => WireDelay,
            CMUSynthLimbSurgeryStep.DetachLimb => DetachDelay,
            CMUSynthLimbSurgeryStep.AttachLimb => AttachDelay,
            CMUSynthLimbSurgeryStep.WeldChassis => WeldDelay,
            _ => TimeSpan.FromSeconds(2),
        };
    }

    private static string StartLoc(CMUSynthLimbSurgeryStep step)
    {
        return step switch
        {
            CMUSynthLimbSurgeryStep.CutChassis => "cmu-synth-surgery-cut-start",
            CMUSynthLimbSurgeryStep.StripWiring => "cmu-synth-surgery-wire-start",
            CMUSynthLimbSurgeryStep.DetachLimb => "cmu-synth-surgery-detach-start",
            CMUSynthLimbSurgeryStep.AttachLimb => "cmu-synth-surgery-attach-start",
            CMUSynthLimbSurgeryStep.WeldChassis => "cmu-synth-surgery-weld-start",
            _ => "cmu-synth-surgery-cut-start",
        };
    }

    private static bool IsReattachableSlot(string slotId, BodyPartType type)
    {
        return slotId == "head" ||
            (type is BodyPartType.Arm or BodyPartType.Hand or BodyPartType.Leg or BodyPartType.Foot &&
             SymmetryForSlot(slotId) != BodyPartSymmetry.None);
    }

    private static BodyPartSymmetry SymmetryForSlot(string slotId)
    {
        return slotId.StartsWith("left_", StringComparison.Ordinal)
            ? BodyPartSymmetry.Left
            : slotId.StartsWith("right_", StringComparison.Ordinal)
                ? BodyPartSymmetry.Right
                : BodyPartSymmetry.None;
    }

    private static string? SlotForZone(TargetBodyZone zone)
    {
        return zone switch
        {
            TargetBodyZone.Head => "head",
            TargetBodyZone.LeftArm => "left_arm",
            TargetBodyZone.RightArm => "right_arm",
            TargetBodyZone.LeftHand => "left_hand",
            TargetBodyZone.RightHand => "right_hand",
            TargetBodyZone.LeftLeg => "left_leg",
            TargetBodyZone.RightLeg => "right_leg",
            TargetBodyZone.LeftFoot => "left_foot",
            TargetBodyZone.RightFoot => "right_foot",
            _ => null,
        };
    }

    private static string PartName(string slotId)
    {
        return slotId.Replace('_', ' ');
    }

    private readonly record struct SynthMissingSlot(
        EntityUid Parent,
        BodyPartComponent ParentPart,
        string SlotId,
        BodyPartType Type,
        BodyPartSymmetry Symmetry);

    private readonly record struct SynthAttachedSlot(
        EntityUid Parent,
        BodyPartComponent ParentPart,
        EntityUid Part,
        BodyPartComponent PartComp,
        string SlotId,
        BodyPartType Type,
        BodyPartSymmetry Symmetry);
}
