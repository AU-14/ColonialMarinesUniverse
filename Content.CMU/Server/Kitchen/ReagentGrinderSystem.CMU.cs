using System.Numerics;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Chemistry.SmartFridge;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.Kitchen.EntitySystems;

public sealed partial class ReagentGrinderSystem
{
    [Dependency] private SharedContainerSystem _cmuContainers = default!;
    [Dependency] private ItemSlotsSystem _cmuItemSlots = default!;
    [Dependency] private ServerMetaDataSystem _cmuMetadata = default!;
    [Dependency] private SharedPopupSystem _cmuPopup = default!;
    [Dependency] private RMCReagentSystem _cmuReagents = default!;
    [Dependency] private SharedSolutionContainerSystem _cmuSolutions = default!;
    [Dependency] private TransformSystem _cmuTransform = default!;

    partial void InitializeCMU()
    {
        SubscribeLocalEvent<ReagentGrinderComponent, ReagentGrinderLinkMessage>(OnCMULink);
        SubscribeLocalEvent<ReagentGrinderComponent, ReagentGrinderBottleMessage>(OnCMUBottle);
        SubscribeLocalEvent<ReagentGrinderComponent, ReagentGrinderDisposeMessage>(OnCMUDispose);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ReagentGrinderComponent>();
        while (query.MoveNext(out var uid, out var grinder))
        {
            if (grinder.SmartFridge == null)
                continue;

            if (IsValidLink((uid, grinder), grinder.SmartFridge.Value))
                continue;

            grinder.SmartFridge = null;
            Dirty(uid, grinder);
            _cmuPopup.PopupEntity(Loc.GetString("grinder-lost-link"), uid, PopupType.SmallCaution);
        }
    }

    private void OnCMULink(Entity<ReagentGrinderComponent> ent, ref ReagentGrinderLinkMessage args)
    {
        if (IsActive(ent.AsNullable()) || ent.Comp.SmartFridge != null)
            return;

        EntityUid? closest = null;
        var closestDistance = ent.Comp.LinkDistance;
        var query = EntityQueryEnumerator<RMCSmartFridgeComponent>();
        while (query.MoveNext(out var fridge, out _))
        {
            if (_cmuTransform.GetMapId(ent.Owner) != _cmuTransform.GetMapId(fridge))
                continue;

            var distance = Vector2.Distance(
                _cmuTransform.GetWorldPosition(ent.Owner),
                _cmuTransform.GetWorldPosition(fridge));
            if (distance > closestDistance)
                continue;

            closest = fridge;
            closestDistance = distance;
        }

        if (closest == null)
            return;

        ent.Comp.SmartFridge = closest;
        Dirty(ent);
    }

    private void OnCMUBottle(Entity<ReagentGrinderComponent> ent, ref ReagentGrinderBottleMessage args)
    {
        if (IsActive(ent.AsNullable()) ||
            ent.Comp.SmartFridge is not { } fridge ||
            !IsValidLink(ent, fridge) ||
            !TryComp(fridge, out RMCSmartFridgeComponent? fridgeComp) ||
            !TryGetBeakerSolution(ent, out var solutionEntity, out var solution))
        {
            return;
        }

        ReagentQuantity? quantity = null;
        foreach (var reagent in solution.Contents)
        {
            if (reagent.Reagent != args.Reagent.Reagent)
                continue;

            quantity = reagent;
            break;
        }

        if (quantity == null)
            return;

        solution.RemoveReagent(quantity.Value, preserveOrder: true);
        _cmuSolutions.UpdateChemicals(solutionEntity);

        var remaining = quantity.Value.Quantity;
        var container = _cmuContainers.EnsureContainer<Container>(fridge, fridgeComp.ContainerId);
        while (remaining > FixedPoint2.Zero)
        {
            var bottle = Spawn("CMBottleEmpty");
            if (!_cmuSolutions.TryGetSolution(bottle, "drink", out var bottleSolution))
            {
                QueueDel(bottle);
                break;
            }

            _cmuSolutions.TryAddReagent(
                bottleSolution.Value,
                new ReagentQuantity(quantity.Value.Reagent, remaining),
                out var added);
            remaining -= added;
            _cmuMetadata.SetEntityName(
                bottle,
                $"{_cmuReagents.Index(quantity.Value.Reagent.Prototype).LocalizedName} bottle");
            _cmuContainers.Insert(bottle, container);
        }
    }

    private void OnCMUDispose(Entity<ReagentGrinderComponent> ent, ref ReagentGrinderDisposeMessage args)
    {
        if (IsActive(ent.AsNullable()) ||
            !TryGetBeakerSolution(ent, out var solutionEntity, out var solution))
        {
            return;
        }

        ReagentQuantity? quantity = null;
        foreach (var reagent in solution.Contents)
        {
            if (reagent.Reagent != args.Reagent.Reagent)
                continue;

            quantity = reagent;
            break;
        }

        if (quantity == null)
            return;

        solution.RemoveReagent(quantity.Value, preserveOrder: true);
        _cmuSolutions.UpdateChemicals(solutionEntity);
    }

    private bool TryGetBeakerSolution(
        Entity<ReagentGrinderComponent> ent,
        out Entity<SolutionComponent> solutionEntity,
        out Solution solution)
    {
        solutionEntity = default;
        solution = default!;

        var beaker = _cmuItemSlots.GetItemOrNull(ent.Owner, ReagentGrinderComponent.BeakerSlotId);
        if (beaker is not { } beakerUid ||
            !_cmuSolutions.TryGetFitsInDispenser(beakerUid, out var nullableSolutionEntity, out var nullableSolution) ||
            nullableSolutionEntity is not { } foundSolutionEntity ||
            nullableSolution is not { } foundSolution)
        {
            return false;
        }

        solutionEntity = foundSolutionEntity;
        solution = foundSolution;
        return true;
    }

    private bool IsValidLink(Entity<ReagentGrinderComponent> grinder, EntityUid fridge)
    {
        return Exists(fridge) &&
               HasComp<RMCSmartFridgeComponent>(fridge) &&
               _cmuTransform.GetMapId(grinder.Owner) == _cmuTransform.GetMapId(fridge) &&
               Vector2.Distance(
                   _cmuTransform.GetWorldPosition(grinder.Owner),
                   _cmuTransform.GetWorldPosition(fridge)) <= grinder.Comp.LinkLimit;
    }
}
