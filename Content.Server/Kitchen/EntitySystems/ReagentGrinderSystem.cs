using System.Linq;
using Content.Server._CMU14.Botany;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Chemistry.SmartFridge;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Kitchen.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.Kitchen.EntitySystems;

/// <inheritdoc />
public sealed partial class ReagentGrinderSystem : SharedReagentGrinderSystem
{
    private const string BottlePrototype = "CMBottleEmpty";
    private const string BottleSolution = "drink";

    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private ServerMetaDataSystem _metaData = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private RMCReagentSystem _reagents = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReagentGrinderComponent, ReagentGrinderLinkMessage>(OnLink);
        SubscribeLocalEvent<ReagentGrinderComponent, ReagentGrinderBottleMessage>(OnBottle);
        SubscribeLocalEvent<ReagentGrinderComponent, ReagentGrinderDisposeMessage>(OnDispose);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ReagentGrinderComponent>();
        while (query.MoveNext(out var uid, out var grinder))
        {
            if (grinder.SmartFridge is not { } fridge)
                continue;

            if (TryComp(fridge, out RMCSmartFridgeComponent? _) &&
                _transform.GetMapCoordinates(uid).InRange(_transform.GetMapCoordinates(fridge), grinder.LinkLimit))
            {
                continue;
            }

            grinder.SmartFridge = null;
            Dirty(uid, grinder);
            UpdateUi(uid);
            _popup.PopupEntity(Loc.GetString("grinder-lost-link"), uid, PopupType.SmallCaution);
        }
    }

    protected override void OnInteractUsing(Entity<ReagentGrinderComponent> ent, ref InteractUsingEvent args)
    {
        if (HasComp<CMUPlantBagComponent>(args.Used) &&
            TryComp(args.Used, out StorageComponent? plantBag))
        {
            args.Handled = true;
            TransferPlantBag(ent, args.Used, plantBag, args.User);
            return;
        }

        base.OnInteractUsing(ent, ref args);
    }

    private void TransferPlantBag(
        Entity<ReagentGrinderComponent> grinder,
        EntityUid plantBagUid,
        StorageComponent plantBag,
        EntityUid user)
    {
        if (IsActive(grinder.AsNullable()))
            return;

        var availableSpace = grinder.Comp.StorageMaxEntities - grinder.Comp.InputContainer.ContainedEntities.Count;
        if (availableSpace <= 0)
        {
            _popup.PopupEntity(Loc.GetString("reagent-grinder-component-chamber-full"), grinder, user);
            return;
        }

        var transferred = 0;
        foreach (var item in plantBag.Container.ContainedEntities.ToList())
        {
            if (transferred >= availableSpace)
                break;

            if (!HasComp<ExtractableComponent>(item) ||
                !_container.Remove(item, plantBag.Container))
            {
                continue;
            }

            if (_container.Insert(item, grinder.Comp.InputContainer))
            {
                transferred++;
                continue;
            }

            _container.Insert(item, plantBag.Container);
        }

        if (transferred == 0)
        {
            _popup.PopupEntity(
                Loc.GetString("reagent-grinder-component-plant-bag-empty", ("bag", plantBagUid)),
                grinder,
                user);
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("reagent-grinder-component-plant-bag-loaded", ("count", transferred)),
            grinder,
            user);
        UpdateUi(grinder);
    }

    private void OnLink(Entity<ReagentGrinderComponent> ent, ref ReagentGrinderLinkMessage args)
    {
        if (ent.Comp.SmartFridge != null ||
            IsActive(ent.AsNullable()) ||
            !_power.IsPowered(ent.Owner))
        {
            return;
        }

        var grinderCoordinates = _transform.GetMapCoordinates(ent.Owner);
        EntityUid? closest = null;
        var closestDistance = float.MaxValue;
        var query = EntityQueryEnumerator<RMCSmartFridgeComponent>();
        while (query.MoveNext(out var fridge, out _))
        {
            var fridgeCoordinates = _transform.GetMapCoordinates(fridge);
            if (fridgeCoordinates.MapId != grinderCoordinates.MapId)
                continue;

            var distance = (fridgeCoordinates.Position - grinderCoordinates.Position).Length();
            if (distance > ent.Comp.LinkDistance || distance >= closestDistance)
                continue;

            closest = fridge;
            closestDistance = distance;
        }

        if (closest == null)
            return;

        ent.Comp.SmartFridge = closest;
        Dirty(ent);
        UpdateUi(ent);
    }

    private void OnBottle(Entity<ReagentGrinderComponent> ent, ref ReagentGrinderBottleMessage args)
    {
        if (!TryGetValidFridge(ent, out var fridge) ||
            IsActive(ent.AsNullable()) ||
            _itemSlots.GetItemOrNull(ent.Owner, ReagentGrinderComponent.BeakerSlotId) is not { } beaker ||
            !_solution.TryGetFitsInDispenser(beaker, out var beakerSolution, out var contents))
        {
            return;
        }

        var quantity = contents.GetReagentQuantity(args.Reagent.Reagent);
        if (quantity <= 0)
            return;

        var fridgeContainer = _container.EnsureContainer<Container>(fridge.Owner, fridge.Comp.ContainerId);
        while (quantity > 0)
        {
            var bottle = Spawn(BottlePrototype);
            _solution.EnsureSolution(bottle, BottleSolution, out var bottleSolution);
            if (!_solution.TryAddReagent(
                    bottleSolution,
                    args.Reagent.Reagent,
                    quantity,
                    out var accepted) ||
                accepted <= 0 ||
                !_container.Insert(bottle, fridgeContainer))
            {
                QueueDel(bottle);
                break;
            }

            _solution.RemoveReagent(beakerSolution.Value, args.Reagent.Reagent, accepted);
            quantity -= accepted;

            if (_reagents.TryIndex(args.Reagent.Reagent, out var reagent))
                _metaData.SetEntityName(bottle, $"{reagent.LocalizedName} bottle");
        }

        UpdateUi(ent);
    }

    private void OnDispose(Entity<ReagentGrinderComponent> ent, ref ReagentGrinderDisposeMessage args)
    {
        if (!TryGetValidFridge(ent, out _) ||
            IsActive(ent.AsNullable()) ||
            _itemSlots.GetItemOrNull(ent.Owner, ReagentGrinderComponent.BeakerSlotId) is not { } beaker ||
            !_solution.TryGetFitsInDispenser(beaker, out var beakerSolution, out var contents))
        {
            return;
        }

        var quantity = contents.GetReagentQuantity(args.Reagent.Reagent);
        if (quantity <= 0)
            return;

        _solution.RemoveReagent(beakerSolution.Value, args.Reagent.Reagent, quantity);
        UpdateUi(ent);
    }

    private bool TryGetValidFridge(
        Entity<ReagentGrinderComponent> grinder,
        out Entity<RMCSmartFridgeComponent> fridge)
    {
        fridge = default;
        if (grinder.Comp.SmartFridge is not { } fridgeUid ||
            !TryComp(fridgeUid, out RMCSmartFridgeComponent? fridgeComp) ||
            !_transform.GetMapCoordinates(grinder.Owner)
                .InRange(_transform.GetMapCoordinates(fridgeUid), grinder.Comp.LinkLimit))
        {
            return false;
        }

        fridge = (fridgeUid, fridgeComp);
        return true;
    }
}
