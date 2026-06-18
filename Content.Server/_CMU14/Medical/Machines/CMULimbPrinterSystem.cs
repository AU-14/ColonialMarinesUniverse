using Content.Shared._CMU14.Medical.Machines;
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

namespace Content.Server._CMU14.Medical.Machines;

public sealed partial class CMULimbPrinterSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RMCReagentSystem _reagents = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const string BloodReagent = "Blood";
    private const string SyringeSolutionName = "injector";
    private const float UiRefreshInterval = 1f;
    private static readonly SoundSpecifier PrintSound = new SoundCollectionSpecifier("Welder");

    private float _uiAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<CMULimbPrinterComponent>(CMULimbPrinterUIKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<CMULimbPrinterPrintMessage>(OnPrint);
            subs.Event<CMULimbPrinterEjectBeakerMessage>(OnEjectBeaker);
            subs.Event<CMULimbPrinterEjectSyringeMessage>(OnEjectSyringe);
            subs.Event<CMULimbPrinterEjectMetalMessage>(OnEjectMetal);
            subs.Event<CMULimbPrinterEjectCableMessage>(OnEjectCable);
        });

        SubscribeLocalEvent<CMULimbPrinterComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<CMULimbPrinterComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMULimbPrinterComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var working = comp.WorkingUntil > now;
            _appearance.SetData(uid, CMULimbPrinterVisuals.Working, working);
        }

        _uiAccumulator += frameTime;
        if (_uiAccumulator < UiRefreshInterval)
            return;

        _uiAccumulator = 0f;
        query = EntityQueryEnumerator<CMULimbPrinterComponent>();
        while (query.MoveNext(out var uid, out var comp))
            RefreshUi(uid, comp);
    }

    private void OnUiOpened(Entity<CMULimbPrinterComponent> ent, ref BoundUIOpenedEvent args)
    {
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnContainerChanged<T>(Entity<CMULimbPrinterComponent> ent, ref T args)
    {
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

    private void OnEjectMetal(Entity<CMULimbPrinterComponent> ent, ref CMULimbPrinterEjectMetalMessage msg)
    {
        EjectSlot(ent.Owner, CMULimbPrinterComponent.MetalSlotId, msg.Actor);
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnEjectCable(Entity<CMULimbPrinterComponent> ent, ref CMULimbPrinterEjectCableMessage msg)
    {
        EjectSlot(ent.Owner, CMULimbPrinterComponent.CableSlotId, msg.Actor);
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnPrint(Entity<CMULimbPrinterComponent> ent, ref CMULimbPrinterPrintMessage msg)
    {
        if (!TryGetLimbPrototype(ent.Comp, msg.Recipe, msg.Type, msg.Symmetry, out var limbPrototype, out var limbName))
            return;

        string reason;
        var canPrint = msg.Recipe == CMULimbPrinterRecipeKind.Robotic
            ? TryCanPrintRobotic(ent.Owner, ent.Comp, out reason)
            : TryCanPrintOrganic(ent.Owner, ent.Comp, out reason);

        if (!canPrint)
        {
            _popup.PopupEntity(reason, ent.Owner, msg.Actor, PopupType.SmallCaution);
            RefreshUi(ent.Owner, ent.Comp);
            return;
        }

        if (msg.Recipe == CMULimbPrinterRecipeKind.Robotic)
        {
            ConsumeStackResource(ent.Owner, CMULimbPrinterComponent.MetalSlotId, ent.Comp.MetalStack, ent.Comp.MetalCost);
            ConsumeStackResource(ent.Owner, CMULimbPrinterComponent.CableSlotId, ent.Comp.CableStack, ent.Comp.CableCost);
        }
        else
        {
            if (!TryGetSynthesisSolution(ent.Owner, out var synthesisSolution, out var synthesis)
                || !TryGetSyringeSolution(ent.Owner, out var syringeSolution, out var blood))
            {
                RefreshUi(ent.Owner, ent.Comp);
                return;
            }

            ConsumeReagent(synthesisSolution, synthesis, ent.Comp.SynthesisReagent, ent.Comp.SynthesisCost);
            ConsumeReagent(syringeSolution, blood, BloodReagent, ent.Comp.BloodCost);
        }

        var limb = Spawn(limbPrototype, Transform(ent.Owner).Coordinates);
        AttachPrintedExtremity(limb, ent.Comp, msg.Recipe, msg.Type, msg.Symmetry);
        _transform.PlaceNextTo(limb, ent.Owner);

        ent.Comp.WorkingUntil = _timing.CurTime + TimeSpan.FromSeconds(1.2);
        _appearance.SetData(ent.Owner, CMULimbPrinterVisuals.Working, true);
        _audio.PlayPvs(PrintSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-limb-printer-printed", ("limb", limbName)), ent.Owner, msg.Actor);
        RefreshUi(ent.Owner, ent.Comp);
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
        if (!TryGetStackResource(uid, CMULimbPrinterComponent.MetalSlotId, comp.MetalStack, out var metal, out var metalStack))
        {
            reason = Loc.GetString("cmu-limb-printer-missing-metal");
            return false;
        }

        if (_stacks.GetCount(metal, metalStack) < comp.MetalCost)
        {
            reason = Loc.GetString("cmu-limb-printer-low-metal");
            return false;
        }

        if (!TryGetStackResource(uid, CMULimbPrinterComponent.CableSlotId, comp.CableStack, out var cable, out var cableStack))
        {
            reason = Loc.GetString("cmu-limb-printer-missing-cable");
            return false;
        }

        if (_stacks.GetCount(cable, cableStack) < comp.CableCost)
        {
            reason = Loc.GetString("cmu-limb-printer-low-cable");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void RefreshUi(EntityUid uid, CMULimbPrinterComponent comp)
    {
        var canPrintOrganic = TryCanPrintOrganic(uid, comp, out var organicReason);
        var canPrintRobotic = TryCanPrintRobotic(uid, comp, out var roboticReason);
        var status = GetStatus(canPrintOrganic, organicReason, canPrintRobotic, roboticReason);

        var beaker = _slots.GetItemOrNull(uid, CMULimbPrinterComponent.BeakerSlotId);
        var syringe = _slots.GetItemOrNull(uid, CMULimbPrinterComponent.SyringeSlotId);
        var metal = _slots.GetItemOrNull(uid, CMULimbPrinterComponent.MetalSlotId);
        var cable = _slots.GetItemOrNull(uid, CMULimbPrinterComponent.CableSlotId);
        var synthesisUnits = 0f;
        var synthesisMax = 0f;
        var bloodUnits = 0f;
        var bloodMax = 0f;
        var metalCount = GetStackCount(metal);
        var cableCount = GetStackCount(cable);

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

        var reagentName = _reagents.TryIndex(comp.SynthesisReagent, out var reagent)
            ? reagent.LocalizedName
            : comp.SynthesisReagent.ToString();

        var state = new CMULimbPrinterBuiState(
            status,
            reagentName,
            beaker is { } beakerUid ? Name(beakerUid) : null,
            syringe is { } syringeUid ? Name(syringeUid) : null,
            synthesisUnits,
            synthesisMax,
            bloodUnits,
            bloodMax,
            metal is { } metalUid ? Name(metalUid) : null,
            cable is { } cableUid ? Name(cableUid) : null,
            metalCount,
            cableCount,
            comp.SynthesisCost.Float(),
            comp.BloodCost.Float(),
            comp.MetalCost,
            comp.CableCost,
            comp.WorkingUntil > _timing.CurTime ? comp.WorkingUntil : null,
            BuildOptions(comp, canPrintOrganic, organicReason, canPrintRobotic, roboticReason));

        _ui.SetUiState(uid, CMULimbPrinterUIKey.Key, state);
    }

    private string GetStatus(
        bool canPrintOrganic,
        string organicReason,
        bool canPrintRobotic,
        string roboticReason)
    {
        if (canPrintOrganic && canPrintRobotic)
            return Loc.GetString("cmu-limb-printer-status-ready");

        if (canPrintOrganic)
            return Loc.GetString("cmu-limb-printer-status-organic-ready");

        if (canPrintRobotic)
            return Loc.GetString("cmu-limb-printer-status-robotic-ready");

        return Loc.GetString(
            "cmu-limb-printer-status-not-ready",
            ("organic", organicReason),
            ("robotic", roboticReason));
    }

    private List<CMULimbPrinterOption> BuildOptions(
        CMULimbPrinterComponent comp,
        bool canPrintOrganic,
        string organicDisabledReason,
        bool canPrintRobotic,
        string roboticDisabledReason)
    {
        return
        [
            MakeOption(comp, CMULimbPrinterRecipeKind.Organic, BodyPartType.Arm, BodyPartSymmetry.Left, canPrintOrganic, organicDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Organic, BodyPartType.Leg, BodyPartSymmetry.Left, canPrintOrganic, organicDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Robotic, BodyPartType.Arm, BodyPartSymmetry.Left, canPrintRobotic, roboticDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Robotic, BodyPartType.Leg, BodyPartSymmetry.Left, canPrintRobotic, roboticDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Organic, BodyPartType.Hand, BodyPartSymmetry.Left, canPrintOrganic, organicDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Organic, BodyPartType.Foot, BodyPartSymmetry.Left, canPrintOrganic, organicDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Robotic, BodyPartType.Hand, BodyPartSymmetry.Left, canPrintRobotic, roboticDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Robotic, BodyPartType.Foot, BodyPartSymmetry.Left, canPrintRobotic, roboticDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Organic, BodyPartType.Arm, BodyPartSymmetry.Right, canPrintOrganic, organicDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Organic, BodyPartType.Leg, BodyPartSymmetry.Right, canPrintOrganic, organicDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Robotic, BodyPartType.Arm, BodyPartSymmetry.Right, canPrintRobotic, roboticDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Robotic, BodyPartType.Leg, BodyPartSymmetry.Right, canPrintRobotic, roboticDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Organic, BodyPartType.Hand, BodyPartSymmetry.Right, canPrintOrganic, organicDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Organic, BodyPartType.Foot, BodyPartSymmetry.Right, canPrintOrganic, organicDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Robotic, BodyPartType.Hand, BodyPartSymmetry.Right, canPrintRobotic, roboticDisabledReason),
            MakeOption(comp, CMULimbPrinterRecipeKind.Robotic, BodyPartType.Foot, BodyPartSymmetry.Right, canPrintRobotic, roboticDisabledReason),
        ];
    }

    private CMULimbPrinterOption MakeOption(
        CMULimbPrinterComponent comp,
        CMULimbPrinterRecipeKind recipe,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        bool canPrint,
        string disabledReason)
    {
        TryGetLimbPrototype(comp, recipe, type, symmetry, out var prototype, out var name);
        return new CMULimbPrinterOption(recipe, type, symmetry, name, prototype, canPrint, canPrint ? string.Empty : disabledReason);
    }

    private bool TryGetLimbPrototype(
        CMULimbPrinterComponent comp,
        CMULimbPrinterRecipeKind recipe,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        out EntProtoId prototype,
        out string name)
    {
        prototype = default;
        name = string.Empty;

        if (type == BodyPartType.Arm && symmetry == BodyPartSymmetry.Left)
        {
            prototype = recipe == CMULimbPrinterRecipeKind.Robotic
                ? comp.RoboticLeftArmPrototype
                : comp.LeftArmPrototype;
            name = Loc.GetString(recipe == CMULimbPrinterRecipeKind.Robotic
                ? "cmu-limb-printer-robotic-left-arm"
                : "cmu-limb-printer-left-arm");
            return true;
        }

        if (type == BodyPartType.Leg && symmetry == BodyPartSymmetry.Left)
        {
            prototype = recipe == CMULimbPrinterRecipeKind.Robotic
                ? comp.RoboticLeftLegPrototype
                : comp.LeftLegPrototype;
            name = Loc.GetString(recipe == CMULimbPrinterRecipeKind.Robotic
                ? "cmu-limb-printer-robotic-left-leg"
                : "cmu-limb-printer-left-leg");
            return true;
        }

        if (type == BodyPartType.Hand && symmetry == BodyPartSymmetry.Left)
        {
            prototype = recipe == CMULimbPrinterRecipeKind.Robotic
                ? comp.RoboticLeftHandPrototype
                : comp.LeftHandPrototype;
            name = Loc.GetString(recipe == CMULimbPrinterRecipeKind.Robotic
                ? "cmu-limb-printer-robotic-left-hand"
                : "cmu-limb-printer-left-hand");
            return true;
        }

        if (type == BodyPartType.Foot && symmetry == BodyPartSymmetry.Left)
        {
            prototype = recipe == CMULimbPrinterRecipeKind.Robotic
                ? comp.RoboticLeftFootPrototype
                : comp.LeftFootPrototype;
            name = Loc.GetString(recipe == CMULimbPrinterRecipeKind.Robotic
                ? "cmu-limb-printer-robotic-left-foot"
                : "cmu-limb-printer-left-foot");
            return true;
        }

        if (type == BodyPartType.Arm && symmetry == BodyPartSymmetry.Right)
        {
            prototype = recipe == CMULimbPrinterRecipeKind.Robotic
                ? comp.RoboticRightArmPrototype
                : comp.RightArmPrototype;
            name = Loc.GetString(recipe == CMULimbPrinterRecipeKind.Robotic
                ? "cmu-limb-printer-robotic-right-arm"
                : "cmu-limb-printer-right-arm");
            return true;
        }

        if (type == BodyPartType.Leg && symmetry == BodyPartSymmetry.Right)
        {
            prototype = recipe == CMULimbPrinterRecipeKind.Robotic
                ? comp.RoboticRightLegPrototype
                : comp.RightLegPrototype;
            name = Loc.GetString(recipe == CMULimbPrinterRecipeKind.Robotic
                ? "cmu-limb-printer-robotic-right-leg"
                : "cmu-limb-printer-right-leg");
            return true;
        }

        if (type == BodyPartType.Hand && symmetry == BodyPartSymmetry.Right)
        {
            prototype = recipe == CMULimbPrinterRecipeKind.Robotic
                ? comp.RoboticRightHandPrototype
                : comp.RightHandPrototype;
            name = Loc.GetString(recipe == CMULimbPrinterRecipeKind.Robotic
                ? "cmu-limb-printer-robotic-right-hand"
                : "cmu-limb-printer-right-hand");
            return true;
        }

        if (type == BodyPartType.Foot && symmetry == BodyPartSymmetry.Right)
        {
            prototype = recipe == CMULimbPrinterRecipeKind.Robotic
                ? comp.RoboticRightFootPrototype
                : comp.RightFootPrototype;
            name = Loc.GetString(recipe == CMULimbPrinterRecipeKind.Robotic
                ? "cmu-limb-printer-robotic-right-foot"
                : "cmu-limb-printer-right-foot");
            return true;
        }

        return false;
    }

    private void AttachPrintedExtremity(
        EntityUid limb,
        CMULimbPrinterComponent comp,
        CMULimbPrinterRecipeKind recipe,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        (string Slot, BodyPartType Type, EntProtoId Prototype)? child = type switch
        {
            BodyPartType.Arm when symmetry == BodyPartSymmetry.Left =>
                (Slot: "left_hand", Type: BodyPartType.Hand, Prototype: recipe == CMULimbPrinterRecipeKind.Robotic ? comp.RoboticLeftHandPrototype : comp.LeftHandPrototype),
            BodyPartType.Arm when symmetry == BodyPartSymmetry.Right =>
                (Slot: "right_hand", Type: BodyPartType.Hand, Prototype: recipe == CMULimbPrinterRecipeKind.Robotic ? comp.RoboticRightHandPrototype : comp.RightHandPrototype),
            BodyPartType.Leg when symmetry == BodyPartSymmetry.Left =>
                (Slot: "left_foot", Type: BodyPartType.Foot, Prototype: recipe == CMULimbPrinterRecipeKind.Robotic ? comp.RoboticLeftFootPrototype : comp.LeftFootPrototype),
            BodyPartType.Leg when symmetry == BodyPartSymmetry.Right =>
                (Slot: "right_foot", Type: BodyPartType.Foot, Prototype: recipe == CMULimbPrinterRecipeKind.Robotic ? comp.RoboticRightFootPrototype : comp.RightFootPrototype),
            _ => null
        };

        if (child is not { } childInfo)
            return;

        var childUid = Spawn(childInfo.Prototype, Transform(limb).Coordinates);
        var attached = TryComp<BodyPartComponent>(limb, out var limbPart)
            && (_body.AttachPart(limb, childInfo.Slot, childUid, limbPart)
                || _body.TryCreatePartSlotAndAttach(limb, childInfo.Slot, childUid, childInfo.Type, limbPart));

        if (!attached)
            QueueDel(childUid);
    }

    private int GetStackCount(EntityUid? uid)
    {
        if (uid is not { } stackUid || !TryComp<StackComponent>(stackUid, out var stack))
            return 0;

        return _stacks.GetCount(stackUid, stack);
    }

    private bool TryGetStackResource(
        EntityUid uid,
        string slotId,
        ProtoId<StackPrototype> stackType,
        out EntityUid stackUid,
        out StackComponent stack)
    {
        stackUid = default;
        stack = default!;

        var item = _slots.GetItemOrNull(uid, slotId);
        if (item is not { } itemUid ||
            !TryComp<StackComponent>(itemUid, out var foundStack) ||
            foundStack.StackTypeId != stackType)
        {
            return false;
        }

        stackUid = itemUid;
        stack = foundStack;
        return true;
    }

    private void ConsumeStackResource(
        EntityUid uid,
        string slotId,
        ProtoId<StackPrototype> stackType,
        int amount)
    {
        if (amount <= 0 ||
            !TryGetStackResource(uid, slotId, stackType, out var stackUid, out var stack))
        {
            return;
        }

        _stacks.Use(stackUid, amount, stack);
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
