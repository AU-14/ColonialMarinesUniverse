// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.Storage.EntitySystems;
using Content.Shared.CMU14.Electric;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Electric;

// Moves things between a machine and the boxes and crates next to it, once a second, for the machines that have it turned on.
public sealed class AutoPortsSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly StorageSystem _storage = default!;
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly RecipeMachineSystem _recipes = default!;

    private readonly HashSet<Entity<StorageComponent>> _boxes = new();
    private readonly HashSet<Entity<EntityStorageComponent>> _crates = new();
    private readonly List<EntityUid> _found = new();
    private readonly Dictionary<string, HashSet<string>> _wanted = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutoPortsComponent, RecipeMachineAutoMessage>(OnSwitch);
        _proto.PrototypesReloaded += _ => _wanted.Clear();
    }

    [Dependency] private readonly Robust.Shared.Prototypes.IPrototypeManager _proto = default!;

    private void OnSwitch(Entity<AutoPortsComponent> machine, ref RecipeMachineAutoMessage args)
    {
        if (args.Output)
            machine.Comp.AutoOutput = args.Value;
        else
            machine.Comp.AutoInput = args.Value;

        Dirty(machine);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AutoPortsComponent, RecipeMachineComponent>();
        while (query.MoveNext(out var uid, out var ports, out var machine))
        {
            if (!ports.AutoInput && !ports.AutoOutput)
                continue;

            ports.Next -= frameTime;
            if (ports.Next > 0f)
                continue;

            ports.Next = ports.Interval;

            FindContainers(uid, ports.Range);
            if (_found.Count == 0)
                continue;

            if (ports.AutoOutput)
                Output(uid);

            if (ports.AutoInput)
                Input(uid, machine);
        }
    }

    // The boxes and crates next to the machine that nobody holds (the lookup leaves out what is held or worn).
    private void FindContainers(EntityUid machine, float range)
    {
        _found.Clear();
        _boxes.Clear();
        _crates.Clear();

        var coords = Transform(machine).Coordinates;
        _lookup.GetEntitiesInRange(coords, range, _boxes);
        _lookup.GetEntitiesInRange(coords, range, _crates);

        foreach (var box in _boxes)
        {
            if (box.Owner != machine)
                _found.Add(box.Owner);
        }

        foreach (var crate in _crates)
        {
            if (crate.Owner != machine && !_found.Contains(crate.Owner))
                _found.Add(crate.Owner);
        }
    }

    // What the machine has made goes, a slot at a time, into the first container that takes it.
    private void Output(EntityUid machine)
    {
        var outputs = TryComp<RecipeMachineComponent>(machine, out var recipeMachine) ? recipeMachine.OutputIds : RecipeMachineComponent.OutputSlotIds;
        foreach (var slotId in outputs)
        {
            if (_itemSlots.GetItemOrNull(machine, slotId) == null)
                continue;

            if (!_itemSlots.TryEject(machine, slotId, null, out var ejected) || ejected is not { } taken)
                continue;

            foreach (var target in _found)
            {
                if (TryPut(target, taken))
                    break;
            }

            // Nothing took it, or only a part of a stack: what is left goes back into the slot.
            if (!TerminatingOrDeleted(taken) && !_container.IsEntityInContainer(taken))
                _itemSlots.TryInsert(machine, slotId, taken, null);
        }
    }

    private bool TryPut(EntityUid container, EntityUid item)
    {
        if (TryComp<StorageComponent>(container, out var storage))
            return _storage.Insert(container, item, out _, user: null, storageComp: storage, playSound: false);

        if (TryComp<EntityStorageComponent>(container, out var crate) && _entityStorage.CanInsert(item, container, crate))
            return _entityStorage.Insert(item, container, crate);

        return false;
    }

    // One stack a turn of what the recipes of the machine use is taken out of the first container that holds one.
    private void Input(EntityUid machine, RecipeMachineComponent comp)
    {
        var wanted = WantedBy(comp.Kind);
        if (wanted.Count == 0)
            return;

        foreach (var container in _found)
        {
            var contents = ContentsOf(container);
            if (contents == null)
                continue;

            foreach (var item in contents)
            {
                string? key = TryComp<StackComponent>(item, out var stack) ? stack.StackTypeId.Id : MetaData(item).EntityPrototype?.ID;
                if (key == null || !wanted.Contains(key))
                    continue;

                if (TryTake(machine, item))
                    return;
            }
        }
    }

    private List<EntityUid>? ContentsOf(EntityUid container)
    {
        if (TryComp<StorageComponent>(container, out var storage))
            return new List<EntityUid>(storage.Container.ContainedEntities);

        if (TryComp<EntityStorageComponent>(container, out var crate))
            return new List<EntityUid>(crate.Contents.ContainedEntities);

        return null;
    }

    // Puts the item into an input slot that holds the same, or into a free one.
    private bool TryTake(EntityUid machine, EntityUid item)
    {
        var inputs = TryComp<RecipeMachineComponent>(machine, out var recipeMachine) ? recipeMachine.InputIds : RecipeMachineComponent.InputSlotIds;
        if (TryComp<StackComponent>(item, out var stack))
        {
            foreach (var slotId in inputs)
            {
                if (_itemSlots.GetItemOrNull(machine, slotId) is not { } held ||
                    !TryComp<StackComponent>(held, out var heldStack) ||
                    heldStack.StackTypeId != stack.StackTypeId ||
                    _stack.GetAvailableSpace(heldStack) <= 0)
                {
                    continue;
                }

                if (!_container.TryRemoveFromContainer(item))
                    return false;

                _stack.TryMergeStacks((item, stack), (held, heldStack), out _);

                // What did not fit goes back where it came from.
                if (!TerminatingOrDeleted(item))
                    DropBack(item);

                return true;
            }
        }

        foreach (var slotId in inputs)
        {
            if (_itemSlots.GetItemOrNull(machine, slotId) != null)
                continue;

            if (!_container.TryRemoveFromContainer(item))
                return false;

            if (_itemSlots.TryInsert(machine, slotId, item, null))
                return true;

            DropBack(item);
            return false;
        }

        return false;
    }

    private void DropBack(EntityUid item)
    {
        foreach (var target in _found)
        {
            if (TryPut(target, item))
                return;
        }
    }

    private HashSet<string> WantedBy(string kind)
    {
        if (_wanted.TryGetValue(kind, out var known))
            return known;

        var set = new HashSet<string>();
        foreach (var recipe in _recipes.RecipesOf(kind))
        {
            foreach (var id in recipe.Inputs.Keys)
                set.Add(id.Id);

            foreach (var id in recipe.Catalysts.Keys)
                set.Add(id.Id);

            foreach (var id in recipe.ItemInputs.Keys)
                set.Add(id.Id);
        }

        _wanted[kind] = set;
        return set;
    }
}
