using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Surgery;

public sealed class CMULimbPrinterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly RMCReagentSystem _reagents = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

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

    private void OnPrint(Entity<CMULimbPrinterComponent> ent, ref CMULimbPrinterPrintMessage msg)
    {
        if (!TryGetLimbPrototype(ent.Comp, msg.Type, msg.Symmetry, out var limbPrototype, out var limbName))
            return;

        if (!TryCanPrint(ent.Owner, ent.Comp, out var reason))
        {
            _popup.PopupEntity(reason, ent.Owner, msg.Actor, PopupType.SmallCaution);
            RefreshUi(ent.Owner, ent.Comp);
            return;
        }

        if (!TryGetSynthesisSolution(ent.Owner, out var synthesisSolution, out var synthesis)
            || !TryGetSyringeSolution(ent.Owner, out var syringeSolution, out var blood))
        {
            RefreshUi(ent.Owner, ent.Comp);
            return;
        }

        ConsumeReagent(synthesisSolution, synthesis, ent.Comp.SynthesisReagent, ent.Comp.SynthesisCost);
        ConsumeReagent(syringeSolution, blood, BloodReagent, ent.Comp.BloodCost);

        var limb = Spawn(limbPrototype, Transform(ent.Owner).Coordinates);
        _transform.PlaceNextTo(limb, ent.Owner);

        ent.Comp.WorkingUntil = _timing.CurTime + TimeSpan.FromSeconds(1.2);
        _appearance.SetData(ent.Owner, CMULimbPrinterVisuals.Working, true);
        _audio.PlayPvs(PrintSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-limb-printer-printed", ("limb", limbName)), ent.Owner, msg.Actor);
        RefreshUi(ent.Owner, ent.Comp);
    }

    private bool TryCanPrint(EntityUid uid, CMULimbPrinterComponent comp, out string reason)
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

    private void RefreshUi(EntityUid uid, CMULimbPrinterComponent comp)
    {
        var canPrint = TryCanPrint(uid, comp, out var reason);
        var status = canPrint
            ? Loc.GetString("cmu-limb-printer-status-ready")
            : reason;

        var beaker = _slots.GetItemOrNull(uid, CMULimbPrinterComponent.BeakerSlotId);
        var syringe = _slots.GetItemOrNull(uid, CMULimbPrinterComponent.SyringeSlotId);
        var synthesisUnits = 0f;
        var synthesisMax = 0f;
        var bloodUnits = 0f;
        var bloodMax = 0f;

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
            comp.SynthesisCost.Float(),
            comp.BloodCost.Float(),
            comp.WorkingUntil > _timing.CurTime ? comp.WorkingUntil : null,
            BuildOptions(comp, canPrint, reason));

        _ui.SetUiState(uid, CMULimbPrinterUIKey.Key, state);
    }

    private List<CMULimbPrinterOption> BuildOptions(CMULimbPrinterComponent comp, bool canPrint, string disabledReason)
    {
        return
        [
            MakeOption(comp, BodyPartType.Arm, BodyPartSymmetry.Left, canPrint, disabledReason),
            MakeOption(comp, BodyPartType.Leg, BodyPartSymmetry.Left, canPrint, disabledReason),
            MakeOption(comp, BodyPartType.Arm, BodyPartSymmetry.Right, canPrint, disabledReason),
            MakeOption(comp, BodyPartType.Leg, BodyPartSymmetry.Right, canPrint, disabledReason),
        ];
    }

    private CMULimbPrinterOption MakeOption(
        CMULimbPrinterComponent comp,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        bool canPrint,
        string disabledReason)
    {
        TryGetLimbPrototype(comp, type, symmetry, out var prototype, out var name);
        return new CMULimbPrinterOption(type, symmetry, name, prototype, canPrint, canPrint ? string.Empty : disabledReason);
    }

    private bool TryGetLimbPrototype(
        CMULimbPrinterComponent comp,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        out EntProtoId prototype,
        out string name)
    {
        prototype = default;
        name = string.Empty;

        if (type == BodyPartType.Arm && symmetry == BodyPartSymmetry.Left)
        {
            prototype = comp.LeftArmPrototype;
            name = Loc.GetString("cmu-limb-printer-left-arm");
            return true;
        }

        if (type == BodyPartType.Leg && symmetry == BodyPartSymmetry.Left)
        {
            prototype = comp.LeftLegPrototype;
            name = Loc.GetString("cmu-limb-printer-left-leg");
            return true;
        }

        if (type == BodyPartType.Arm && symmetry == BodyPartSymmetry.Right)
        {
            prototype = comp.RightArmPrototype;
            name = Loc.GetString("cmu-limb-printer-right-arm");
            return true;
        }

        if (type == BodyPartType.Leg && symmetry == BodyPartSymmetry.Right)
        {
            prototype = comp.RightLegPrototype;
            name = Loc.GetString("cmu-limb-printer-right-leg");
            return true;
        }

        return false;
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
