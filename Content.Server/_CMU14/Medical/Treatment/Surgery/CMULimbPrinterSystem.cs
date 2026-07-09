using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

public sealed partial class CMULimbPrinterSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RMCReagentSystem _reagents = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUMedicalSchedulerSystem _scheduler = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const string BloodReagent = "Blood";
    private const string SyringeSolutionName = "injector";
    private const float UiRefreshInterval = 1f;
    private static readonly CMUMedicalWorkKey WorkingExpiryWork = new("limb-printer-working-expiry");
    private static readonly SoundSpecifier PrintSound = new SoundCollectionSpecifier("Welder");

    private readonly HashSet<EntityUid> _openPrinters = new();
    private readonly List<EntityUid> _stalePrinters = new();
    private float _uiAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<CMULimbPrinterComponent>(CMULimbPrinterUIKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
            subs.Event<CMULimbPrinterPrintMessage>(OnPrint);
            subs.Event<CMULimbPrinterEjectBeakerMessage>(OnEjectBeaker);
            subs.Event<CMULimbPrinterEjectSyringeMessage>(OnEjectSyringe);
            subs.Event<CMULimbPrinterEjectMaterialMessage>(OnEjectMaterial);
        });

        SubscribeLocalEvent<CMULimbPrinterComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<CMULimbPrinterComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<CMULimbPrinterComponent, ComponentShutdown>(OnPrinterShutdown);
        SubscribeLocalEvent<CMULimbPrinterComponent, CMUMedicalWorkDueEvent>(OnScheduledWorkDue);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _uiAccumulator += frameTime;
        if (_uiAccumulator < UiRefreshInterval)
            return;

        _uiAccumulator = 0f;
        _stalePrinters.Clear();
        foreach (var printer in _openPrinters)
        {
            if (!TryComp<CMULimbPrinterComponent>(printer, out var comp) ||
                !_ui.IsUiOpen(printer, CMULimbPrinterUIKey.Key))
            {
                _stalePrinters.Add(printer);
                continue;
            }

            RefreshUi(printer, comp);
        }

        foreach (var printer in _stalePrinters)
            _openPrinters.Remove(printer);
    }

    private void OnUiOpened(Entity<CMULimbPrinterComponent> ent, ref BoundUIOpenedEvent args)
    {
        _openPrinters.Add(ent.Owner);
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnUiClosed(Entity<CMULimbPrinterComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!_ui.IsUiOpen(ent.Owner, CMULimbPrinterUIKey.Key))
            _openPrinters.Remove(ent.Owner);
    }

    private void OnContainerChanged<T>(Entity<CMULimbPrinterComponent> ent, ref T args)
    {
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnPrinterShutdown(Entity<CMULimbPrinterComponent> ent, ref ComponentShutdown args)
    {
        _openPrinters.Remove(ent.Owner);
        _scheduler.Cancel(ent.Owner, WorkingExpiryWork);
    }

    private void OnScheduledWorkDue(Entity<CMULimbPrinterComponent> ent, ref CMUMedicalWorkDueEvent args)
    {
        if (args.Key != WorkingExpiryWork)
            return;

        if (ent.Comp.WorkingUntil > _timing.CurTime)
        {
            _scheduler.Schedule(ent.Owner, WorkingExpiryWork, ent.Comp.WorkingUntil);
            return;
        }

        ent.Comp.WorkingUntil = TimeSpan.Zero;
        _appearance.SetData(ent.Owner, CMULimbPrinterVisuals.Working, false);
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnEjectBeaker(Entity<CMULimbPrinterComponent> ent, ref CMULimbPrinterEjectBeakerMessage msg)
    {
        EjectSlot(ent.Owner, CMULimbPrinterComponent.BeakerSlotId, msg.Actor);
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnEjectSyringe(Entity<CMULimbPrinterComponent> ent, ref CMULimbPrinterEjectSyringeMessage msg)
    {
        EjectSlot(ent.Owner, CMULimbPrinterComponent.SyringeSlotId, msg.Actor);
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnEjectMaterial(Entity<CMULimbPrinterComponent> ent, ref CMULimbPrinterEjectMaterialMessage msg)
    {
        EjectSlot(ent.Owner, CMULimbPrinterComponent.MaterialSlotId, msg.Actor);
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnPrint(Entity<CMULimbPrinterComponent> ent, ref CMULimbPrinterPrintMessage msg)
    {
        if (!TryGetLimbPrototype(ent.Comp, msg.Kind, msg.Type, msg.Symmetry, out var limbPrototype, out var limbName))
            return;

        if (!TryCanPrint(ent.Owner, ent.Comp, msg.Kind, out var reason))
        {
            _popup.PopupEntity(reason, ent.Owner, msg.Actor, PopupType.SmallCaution);
            RefreshUi(ent.Owner, ent.Comp);
            return;
        }

        if (!TryConsumePrintResources(ent.Owner, ent.Comp, msg.Kind, out reason))
        {
            _popup.PopupEntity(reason, ent.Owner, msg.Actor, PopupType.SmallCaution);
            RefreshUi(ent.Owner, ent.Comp);
            return;
        }

        var limb = Spawn(limbPrototype, Transform(ent.Owner).Coordinates);
        AttachPrintedExtremity(limb, msg.Kind, msg.Type, msg.Symmetry);
        _transform.PlaceNextTo(limb, ent.Owner);

        StartWorking(ent, TimeSpan.FromSeconds(1.2));
        _audio.PlayPvs(PrintSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-limb-printer-printed", ("limb", limbName)), ent.Owner, msg.Actor);
        RefreshUi(ent.Owner, ent.Comp);
    }

    /// <summary>
    ///     Starts or extends the printer's working presentation and replaces its scheduled expiry.
    /// </summary>
    public void StartWorking(Entity<CMULimbPrinterComponent> ent, TimeSpan duration)
    {
        ent.Comp.WorkingUntil = _timing.CurTime + duration;
        _scheduler.Schedule(ent.Owner, WorkingExpiryWork, ent.Comp.WorkingUntil);
        _appearance.SetData(ent.Owner, CMULimbPrinterVisuals.Working, true);
    }

    private bool TryCanPrint(
        EntityUid uid,
        CMULimbPrinterComponent comp,
        CMULimbPrinterPrintKind kind,
        out string reason)
    {
        return kind switch
        {
            CMULimbPrinterPrintKind.Organic => TryCanPrintOrganic(uid, comp, out reason),
            CMULimbPrinterPrintKind.Robotic => TryCanPrintRobotic(uid, comp, out reason),
            _ => TryCanPrintOrganic(uid, comp, out reason),
        };
    }

    private bool TryCanPrintOrganic(EntityUid uid, CMULimbPrinterComponent comp, out string reason)
    {
        if (!TryGetSynthesisSolution(uid, out _, out var synthesis))
        {
            reason = Loc.GetString("cmu-limb-printer-missing-beaker");
            return false;
        }

        if (GetReagentVolume(synthesis, comp.SynthesisReagent) < comp.SynthesisCost)
        {
            reason = Loc.GetString("cmu-limb-printer-missing-matrix");
            return false;
        }

        if (!TryGetSyringeSolution(uid, out _, out var blood))
        {
            reason = Loc.GetString("cmu-limb-printer-missing-syringe");
            return false;
        }

        if (GetReagentVolume(blood, BloodReagent) < comp.BloodCost)
        {
            reason = Loc.GetString("cmu-limb-printer-missing-blood");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private bool TryCanPrintRobotic(EntityUid uid, CMULimbPrinterComponent comp, out string reason)
    {
        if (!TryGetRoboticMetalStack(uid, comp, out _, out var materialStack, out reason))
            return false;

        if (materialStack.Count < GetRoboticMetalCost(comp))
        {
            reason = Loc.GetString("cmu-limb-printer-missing-metal");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private bool TryConsumePrintResources(
        EntityUid uid,
        CMULimbPrinterComponent comp,
        CMULimbPrinterPrintKind kind,
        out string reason)
    {
        switch (kind)
        {
            case CMULimbPrinterPrintKind.Robotic:
                if (!TryGetRoboticMetalStack(uid, comp, out var material, out var stack, out reason))
                    return false;

                if (!_stack.Use(material, GetRoboticMetalCost(comp), stack))
                {
                    reason = Loc.GetString("cmu-limb-printer-missing-metal");
                    return false;
                }

                reason = string.Empty;
                return true;

            case CMULimbPrinterPrintKind.Organic:
            default:
                if (!TryGetSynthesisSolution(uid, out var synthesisSolution, out var synthesis))
                {
                    reason = Loc.GetString("cmu-limb-printer-missing-beaker");
                    return false;
                }

                if (!TryGetSyringeSolution(uid, out var syringeSolution, out var blood))
                {
                    reason = Loc.GetString("cmu-limb-printer-missing-syringe");
                    return false;
                }

                if (GetReagentVolume(synthesis, comp.SynthesisReagent) < comp.SynthesisCost)
                {
                    reason = Loc.GetString("cmu-limb-printer-missing-matrix");
                    return false;
                }

                if (GetReagentVolume(blood, BloodReagent) < comp.BloodCost)
                {
                    reason = Loc.GetString("cmu-limb-printer-missing-blood");
                    return false;
                }

                ConsumeReagent(synthesisSolution, synthesis, comp.SynthesisReagent, comp.SynthesisCost);
                ConsumeReagent(syringeSolution, blood, BloodReagent, comp.BloodCost);
                reason = string.Empty;
                return true;
        }
    }

    private void RefreshUi(EntityUid uid, CMULimbPrinterComponent comp)
    {
        if (!_ui.IsUiOpen(uid, CMULimbPrinterUIKey.Key))
            return;

        var organicCanPrint = TryCanPrint(uid, comp, CMULimbPrinterPrintKind.Organic, out var organicReason);
        var roboticCanPrint = TryCanPrint(uid, comp, CMULimbPrinterPrintKind.Robotic, out var roboticReason);
        var status = organicCanPrint || roboticCanPrint
            ? Loc.GetString("cmu-limb-printer-status-ready")
            : organicReason;

        var beaker = _slots.GetItemOrNull(uid, CMULimbPrinterComponent.BeakerSlotId);
        var syringe = _slots.GetItemOrNull(uid, CMULimbPrinterComponent.SyringeSlotId);
        var material = _slots.GetItemOrNull(uid, CMULimbPrinterComponent.MaterialSlotId);
        var synthesisUnits = 0f;
        var synthesisMax = 0f;
        var bloodUnits = 0f;
        var bloodMax = 0f;
        var materialUnits = 0;
        var materialMax = 0;

        if (TryGetSynthesisSolution(uid, out _, out var synthesis))
        {
            synthesisUnits = GetReagentVolume(synthesis, comp.SynthesisReagent).Float();
            synthesisMax = synthesis.MaxVolume.Float();
        }

        if (TryGetSyringeSolution(uid, out _, out var blood))
        {
            bloodUnits = GetReagentVolume(blood, BloodReagent).Float();
            bloodMax = blood.MaxVolume.Float();
        }

        if (material is { } materialUid && TryComp<StackComponent>(materialUid, out var materialStack))
        {
            materialUnits = materialStack.Count;
            materialMax = _stack.GetMaxCount(materialStack);
        }

        var reagentName = _reagents.TryIndex(comp.SynthesisReagent, out var reagent)
            ? reagent.LocalizedName
            : comp.SynthesisReagent.ToString();

        var state = new CMULimbPrinterBuiState(
            status,
            reagentName,
            Loc.GetString("cmu-limb-printer-metal-type"),
            beaker is { } beakerUid ? Name(beakerUid) : null,
            syringe is { } syringeUid ? Name(syringeUid) : null,
            material is { } materialNameUid ? Name(materialNameUid) : null,
            synthesisUnits,
            synthesisMax,
            bloodUnits,
            bloodMax,
            comp.SynthesisCost.Float(),
            comp.BloodCost.Float(),
            materialUnits,
            materialMax,
            GetRoboticMetalCost(comp),
            comp.WorkingUntil > _timing.CurTime ? comp.WorkingUntil : null,
            BuildOptions(comp, organicCanPrint, organicReason, roboticCanPrint, roboticReason));

        _ui.SetUiState(uid, CMULimbPrinterUIKey.Key, state);
    }

    private List<CMULimbPrinterOption> BuildOptions(
        CMULimbPrinterComponent comp,
        bool organicCanPrint,
        string organicDisabledReason,
        bool roboticCanPrint,
        string roboticDisabledReason)
    {
        return
        [
            MakeOption(comp, CMULimbPrinterPrintKind.Organic, BodyPartType.Arm, BodyPartSymmetry.Left, organicCanPrint, organicDisabledReason),
            MakeOption(comp, CMULimbPrinterPrintKind.Robotic, BodyPartType.Arm, BodyPartSymmetry.Left, roboticCanPrint, roboticDisabledReason),
            MakeOption(comp, CMULimbPrinterPrintKind.Organic, BodyPartType.Leg, BodyPartSymmetry.Left, organicCanPrint, organicDisabledReason),
            MakeOption(comp, CMULimbPrinterPrintKind.Robotic, BodyPartType.Leg, BodyPartSymmetry.Left, roboticCanPrint, roboticDisabledReason),
            MakeOption(comp, CMULimbPrinterPrintKind.Organic, BodyPartType.Arm, BodyPartSymmetry.Right, organicCanPrint, organicDisabledReason),
            MakeOption(comp, CMULimbPrinterPrintKind.Robotic, BodyPartType.Arm, BodyPartSymmetry.Right, roboticCanPrint, roboticDisabledReason),
            MakeOption(comp, CMULimbPrinterPrintKind.Organic, BodyPartType.Leg, BodyPartSymmetry.Right, organicCanPrint, organicDisabledReason),
            MakeOption(comp, CMULimbPrinterPrintKind.Robotic, BodyPartType.Leg, BodyPartSymmetry.Right, roboticCanPrint, roboticDisabledReason),
        ];
    }

    private CMULimbPrinterOption MakeOption(
        CMULimbPrinterComponent comp,
        CMULimbPrinterPrintKind kind,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        bool canPrint,
        string disabledReason)
    {
        TryGetLimbPrototype(comp, kind, type, symmetry, out var prototype, out var name);
        return new CMULimbPrinterOption(kind, type, symmetry, name, prototype, canPrint, canPrint ? string.Empty : disabledReason);
    }

    private bool TryGetLimbPrototype(
        CMULimbPrinterComponent comp,
        CMULimbPrinterPrintKind kind,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        out EntProtoId prototype,
        out string name)
    {
        prototype = default;
        name = string.Empty;

        if (kind == CMULimbPrinterPrintKind.Organic && type == BodyPartType.Arm && symmetry == BodyPartSymmetry.Left)
        {
            prototype = comp.LeftArmPrototype;
            name = Loc.GetString("cmu-limb-printer-left-arm");
            return true;
        }

        if (kind == CMULimbPrinterPrintKind.Organic && type == BodyPartType.Leg && symmetry == BodyPartSymmetry.Left)
        {
            prototype = comp.LeftLegPrototype;
            name = Loc.GetString("cmu-limb-printer-left-leg");
            return true;
        }

        if (kind == CMULimbPrinterPrintKind.Organic && type == BodyPartType.Arm && symmetry == BodyPartSymmetry.Right)
        {
            prototype = comp.RightArmPrototype;
            name = Loc.GetString("cmu-limb-printer-right-arm");
            return true;
        }

        if (kind == CMULimbPrinterPrintKind.Organic && type == BodyPartType.Leg && symmetry == BodyPartSymmetry.Right)
        {
            prototype = comp.RightLegPrototype;
            name = Loc.GetString("cmu-limb-printer-right-leg");
            return true;
        }

        if (kind == CMULimbPrinterPrintKind.Robotic && type == BodyPartType.Arm && symmetry == BodyPartSymmetry.Left)
        {
            prototype = comp.RoboticLeftArmPrototype;
            name = Loc.GetString("cmu-limb-printer-left-robotic-arm");
            return true;
        }

        if (kind == CMULimbPrinterPrintKind.Robotic && type == BodyPartType.Leg && symmetry == BodyPartSymmetry.Left)
        {
            prototype = comp.RoboticLeftLegPrototype;
            name = Loc.GetString("cmu-limb-printer-left-robotic-leg");
            return true;
        }

        if (kind == CMULimbPrinterPrintKind.Robotic && type == BodyPartType.Arm && symmetry == BodyPartSymmetry.Right)
        {
            prototype = comp.RoboticRightArmPrototype;
            name = Loc.GetString("cmu-limb-printer-right-robotic-arm");
            return true;
        }

        if (kind == CMULimbPrinterPrintKind.Robotic && type == BodyPartType.Leg && symmetry == BodyPartSymmetry.Right)
        {
            prototype = comp.RoboticRightLegPrototype;
            name = Loc.GetString("cmu-limb-printer-right-robotic-leg");
            return true;
        }

        return false;
    }

    private void AttachPrintedExtremity(
        EntityUid limb,
        CMULimbPrinterPrintKind kind,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        if (kind == CMULimbPrinterPrintKind.Robotic)
        {
            if (TryComp<CMURoboticLimbComponent>(limb, out var robotic))
                AttachPrintedChild(limb, robotic.ChildSlot, robotic.ChildPrototype);

            return;
        }

        (string Slot, EntProtoId Prototype)? child = type switch
        {
            BodyPartType.Arm when symmetry == BodyPartSymmetry.Left =>
                (Slot: "left_hand", Prototype: "CMUPartHumanLeftHand"),
            BodyPartType.Arm when symmetry == BodyPartSymmetry.Right =>
                (Slot: "right_hand", Prototype: "CMUPartHumanRightHand"),
            BodyPartType.Leg when symmetry == BodyPartSymmetry.Left =>
                (Slot: "left_foot", Prototype: "CMUPartHumanLeftFoot"),
            BodyPartType.Leg when symmetry == BodyPartSymmetry.Right =>
                (Slot: "right_foot", Prototype: "CMUPartHumanRightFoot"),
            _ => null
        };

        if (child is not { } childInfo)
            return;

        AttachPrintedChild(limb, childInfo.Slot, childInfo.Prototype);
    }

    private void AttachPrintedChild(EntityUid limb, string? slot, EntProtoId? prototype)
    {
        if (string.IsNullOrWhiteSpace(slot) || prototype is not { } childPrototype)
            return;

        var childUid = Spawn(childPrototype, Transform(limb).Coordinates);
        var attached = TryComp<BodyPartComponent>(limb, out var limbPart)
            && TryComp<BodyPartComponent>(childUid, out var childPart)
            && (_body.AttachPart(limb, slot, childUid, limbPart, childPart)
                || _body.TryCreatePartSlotAndAttach(limb, slot, childUid, childPart.PartType, limbPart, childPart));

        if (!attached)
            QueueDel(childUid);
    }

    private bool TryGetSynthesisSolution(EntityUid uid, out Entity<SolutionComponent> solutionEnt, out Solution solution)
    {
        solutionEnt = default;
        solution = default!;
        var beaker = _slots.GetItemOrNull(uid, CMULimbPrinterComponent.BeakerSlotId);
        if (beaker is not { } beakerUid
            || !_solutions.TryGetFitsInDispenser(beakerUid, out var nullableSolutionEnt, out var nullableSolution)
            || nullableSolutionEnt is not { } foundSolutionEnt
            || nullableSolution is not { } foundSolution)
        {
            return false;
        }

        solutionEnt = foundSolutionEnt;
        solution = foundSolution;
        return true;
    }

    private bool TryGetSyringeSolution(EntityUid uid, out Entity<SolutionComponent> solutionEnt, out Solution solution)
    {
        solutionEnt = default;
        solution = default!;
        var syringe = _slots.GetItemOrNull(uid, CMULimbPrinterComponent.SyringeSlotId);
        if (syringe is not { } syringeUid
            || !_solutions.TryGetSolution(syringeUid, SyringeSolutionName, out var nullableSolutionEnt, out var nullableSolution)
            || nullableSolutionEnt is not { } foundSolutionEnt
            || nullableSolution is not { } foundSolution)
        {
            return false;
        }

        solutionEnt = foundSolutionEnt;
        solution = foundSolution;
        return true;
    }

    private FixedPoint2 GetReagentVolume(Solution solution, string reagent)
    {
        var total = FixedPoint2.Zero;
        foreach (var quantity in solution.Contents)
        {
            if (quantity.Reagent.Prototype == reagent)
                total += quantity.Quantity;
        }

        return total;
    }

    private bool TryGetRoboticMetalStack(
        EntityUid uid,
        CMULimbPrinterComponent comp,
        out EntityUid material,
        out StackComponent stack,
        out string reason)
    {
        material = default;
        stack = default!;

        var item = _slots.GetItemOrNull(uid, CMULimbPrinterComponent.MaterialSlotId);
        if (item is not { } materialUid)
        {
            reason = Loc.GetString("cmu-limb-printer-missing-metal-slot");
            return false;
        }

        if (!TryComp<StackComponent>(materialUid, out var foundStack) ||
            !foundStack.StackTypeId.Equals(comp.RoboticMetalStack.ToString(), StringComparison.Ordinal))
        {
            reason = Loc.GetString("cmu-limb-printer-wrong-metal");
            return false;
        }

        material = materialUid;
        stack = foundStack;
        reason = string.Empty;
        return true;
    }

    private static int GetRoboticMetalCost(CMULimbPrinterComponent comp)
    {
        return Math.Max(0, comp.RoboticMetalCost);
    }

    private void ConsumeReagent(Entity<SolutionComponent> solutionEnt, Solution solution, string reagent, FixedPoint2 amount)
    {
        var remaining = amount;
        for (var i = solution.Contents.Count - 1; i >= 0 && remaining > FixedPoint2.Zero; i--)
        {
            var quantity = solution.Contents[i];
            if (quantity.Reagent.Prototype != reagent)
                continue;

            var remove = FixedPoint2.Min(quantity.Quantity, remaining);
            _solutions.RemoveReagent(solutionEnt, quantity.Reagent, remove);
            remaining -= remove;
        }
    }

    private void EjectSlot(EntityUid uid, string slotId, EntityUid user)
    {
        if (_slots.TryGetSlot(uid, slotId, out var slot))
            _slots.TryEjectToHands(uid, slot, user, true);
    }
}
