// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Server.CMU14.Automation;
using Content.Server.Storage.EntitySystems;
using Content.Shared.CMU14.Automation;
using Content.Shared.CMU14.Electric;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CMU14.Grinding;
using Content.Shared.CMU14.Smelting;
using Content.Shared.CMU14.Steam;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
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
    [Dependency] private readonly ConveyorBeltSystem _belts = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<StorageComponent>> _boxes = new();
    private readonly HashSet<Entity<EntityStorageComponent>> _crates = new();
    private readonly List<EntityUid> _found = new();
    private readonly HashSet<Entity<ConveyorBeltComponent>> _beltSet = new();
    private readonly List<EntityUid> _beltsFound = new();
    private readonly List<EntityUid> _onBelt = new();
    private readonly Dictionary<string, HashSet<string>> _wanted = new();
    private readonly Dictionary<string, HashSet<string>> _structure = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutoPortsComponent, RecipeMachineAutoMessage>(OnSwitch);
        SubscribeLocalEvent<AutoPortsComponent, RecipeMachineFilterMessage>(OnFilter);
        _proto.PrototypesReloaded += _ =>
        {
            _wanted.Clear();
            _structure.Clear();
        };
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

    // A thing ticked in the filter is added to it, and one unticked is taken out.
    private void OnFilter(Entity<AutoPortsComponent> machine, ref RecipeMachineFilterMessage args)
    {
        var list = args.Output ? machine.Comp.OutputFilter : machine.Comp.InputFilter;
        if (args.Value)
        {
            if (!list.Contains(args.Id))
                list.Add(args.Id);
        }
        else
        {
            list.Remove(args.Id);
        }

        Dirty(machine);
    }

    // The id a thing goes by in a filter: the type of a stack, or the prototype of a single thing.
    private string? KeyOf(EntityUid item)
    {
        return TryComp<StackComponent>(item, out var stack) ? stack.StackTypeId.Id : MetaData(item).EntityPrototype?.ID;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AutoPortsComponent>();
        while (query.MoveNext(out var uid, out var ports))
        {
            if (!ports.AutoInput && !ports.AutoOutput)
                continue;

            ports.Next -= frameTime;
            if (ports.Next > 0f)
                continue;

            ports.Next = ports.Interval;

            FindContainers(uid, ports.Range);
            if (_found.Count == 0 && _beltsFound.Count == 0)
                continue;

            if (ports.AutoOutput)
                Output(uid, ports);

            if (ports.AutoInput)
                Input(uid, ports);
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

        // The belts next to it too.
        _beltsFound.Clear();
        _beltSet.Clear();
        _lookup.GetEntitiesInRange(coords, range, _beltSet);
        foreach (var belt in _beltSet)
            _beltsFound.Add(belt.Owner);
    }

    // What the machine has made goes, a slot at a time, into the first container that takes it; or, when no box takes it, onto a belt that leads away from the machine.
    // With a filter set only what is in the filter goes out.
    private void Output(EntityUid machine, AutoPortsComponent ports)
    {
        foreach (var slotId in OutputsOf(machine, ports))
        {
            if (_itemSlots.GetItemOrNull(machine, slotId) is not { } inSlot)
                continue;

            if (ports.OutputFilter.Count > 0 && (KeyOf(inSlot) is not { } wanted || !ports.OutputFilter.Contains(wanted)))
                continue;

            if (!_itemSlots.TryEject(machine, slotId, null, out var ejected) || ejected is not { } taken)
                continue;

            foreach (var target in _found)
            {
                if (_belts.TryPut(target, taken))
                    break;
            }

            // No box had room: onto a belt that carries it away (and then it is no longer the machine's).
            if (!TerminatingOrDeleted(taken) && !_container.IsEntityInContainer(taken) && PutOnBelt(machine, taken))
                continue;

            // Nothing took it, or only a part of a stack: what is left goes back into the slot.
            if (!TerminatingOrDeleted(taken) && !_container.IsEntityInContainer(taken))
                _itemSlots.TryInsert(machine, slotId, taken, null);
        }
    }

    // The nearest belt that leads away from the machine and has room takes the thing, which is set down on it.
    private bool PutOnBelt(EntityUid machine, EntityUid item)
    {
        var from = _transform.GetWorldPosition(machine);
        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        foreach (var belt in _beltsFound)
        {
            var at = _transform.GetWorldPosition(belt);
            var away = at - from;
            if (Vector2.Dot(_belts.DirectionOf(belt), away) < -0.01f)
                continue;

            var distance = away.LengthSquared();
            if (distance >= bestDistance)
                continue;

            _belts.ItemsOn(belt, _onBelt);
            if (_onBelt.Count >= ConveyorBeltSystem.TileCapacity)
                continue;

            best = belt;
            bestDistance = distance;
        }

        if (best == null)
            return false;

        _transform.SetCoordinates(item, Transform(best.Value).Coordinates);
        return true;
    }

    // One stack a turn of what the recipes of the machine use is taken out of the first container that holds one, or off a belt that leads to the machine. With a filter
    // set only what is in the filter is taken.
    private void Input(EntityUid machine, AutoPortsComponent ports)
    {
        // A recipe machine takes what its recipes use; any other what the things it is made of take (fuel, ore, ingots), when it is one of the slots that it has said.
        var recipeMachine = ports.InputSlots.Count == 0 && TryComp<RecipeMachineComponent>(machine, out var comp) ? comp : null;
        var wanted = recipeMachine != null ? WantedBy(recipeMachine.Kind) : StructureWants(machine);
        if (wanted.Count == 0)
            return;

        foreach (var container in _found)
        {
            var contents = ContentsOf(container);
            if (contents == null)
                continue;

            foreach (var item in contents)
            {
                if (Wants(wanted, ports, item, machine, recipeMachine == null) && TryTake(machine, item, loose: false))
                    return;
            }
        }

        var here = _transform.GetWorldPosition(machine);
        foreach (var belt in _beltsFound)
        {
            // Only a belt that carries towards the machine brings it anything.
            if (Vector2.Dot(_belts.DirectionOf(belt), here - _transform.GetWorldPosition(belt)) <= 0.01f)
                continue;

            _belts.ItemsOn(belt, _onBelt);
            foreach (var item in new List<EntityUid>(_onBelt))
            {
                if (Wants(wanted, ports, item, machine, recipeMachine == null) && TryTake(machine, item, loose: true))
                    return;
            }
        }
    }

    private bool Wants(HashSet<string> wanted, AutoPortsComponent ports, EntityUid item, EntityUid machine, bool checkSlots)
    {
        if (KeyOf(item) is not { } key || !wanted.Contains(key))
            return false;

        if (ports.InputFilter.Count > 0 && !ports.InputFilter.Contains(key))
            return false;

        return !checkSlots || SlotsTake(machine, ports, item);
    }

    // Whether one of the input slots of a machine that is not a recipe machine would take the thing: it holds a stack of the same kind with room, or it is free and takes it.
    private bool SlotsTake(EntityUid machine, AutoPortsComponent ports, EntityUid item)
    {
        TryComp<StackComponent>(item, out var stack);
        foreach (var slotId in InputsOf(machine, ports))
        {
            if (!SlotAccepts(machine, slotId, item))
                continue;

            if (_itemSlots.GetItemOrNull(machine, slotId) is { } held)
            {
                if (stack != null && TryComp<StackComponent>(held, out var heldStack) && heldStack.StackTypeId == stack.StackTypeId && _stack.GetAvailableSpace(heldStack) > 0)
                    return true;

                continue;
            }

            if (_itemSlots.TryGetSlot(machine, slotId, out var slot) && _itemSlots.CanInsert(machine, slot, item, null))
                return true;
        }

        return false;
    }

    // The fuel of a machine goes into its fuel slot and nothing else does; what is not fuel does not go there. Only for the machines that are not recipe machines.
    private bool SlotAccepts(EntityUid machine, string slotId, EntityUid item)
    {
        if (!TryComp<AutoPortsComponent>(machine, out var ports) || ports.InputSlots.Count == 0 || KeyOf(item) is not { } key)
            return true;

        var fuel = FuelOf(machine);
        return slotId == FuelSlot ? fuel.Contains(key) : !fuel.Contains(key);
    }

    private const string FuelSlot = "fuel";

    private HashSet<string> FuelOf(EntityUid machine)
    {
        var set = new HashSet<string>();
        if (TryComp<BoilerComponent>(machine, out var boiler))
        {
            foreach (var fuel in boiler.Fuels.Keys)
                set.Add(fuel.Id);
        }

        if (TryComp<SolidFuelPressComponent>(machine, out var press) && _itemSlots.TryGetSlot(machine, FuelSlot, out _))
            set.Add(press.FuelStack.Id);

        return set;
    }

    private IEnumerable<string> InputsOf(EntityUid machine, AutoPortsComponent ports)
    {
        if (ports.InputSlots.Count > 0)
            return ports.InputSlots;

        return TryComp<RecipeMachineComponent>(machine, out var recipeMachine) ? recipeMachine.InputIds : RecipeMachineComponent.InputSlotIds;
    }

    private IEnumerable<string> OutputsOf(EntityUid machine, AutoPortsComponent ports)
    {
        if (ports.OutputSlots.Count > 0)
            return ports.OutputSlots;

        return TryComp<RecipeMachineComponent>(machine, out var recipeMachine) ? recipeMachine.OutputIds : RecipeMachineComponent.OutputSlotIds;
    }

    private List<EntityUid>? ContentsOf(EntityUid container)
    {
        if (TryComp<StorageComponent>(container, out var storage))
            return new List<EntityUid>(storage.Container.ContainedEntities);

        if (TryComp<EntityStorageComponent>(container, out var crate))
            return new List<EntityUid>(crate.Contents.ContainedEntities);

        return null;
    }

    // Puts the item into an input slot that holds the same, or into a free one. A loose thing (one off a belt) is not in a container to be taken out of.
    private bool TryTake(EntityUid machine, EntityUid item, bool loose)
    {
        var inputs = InputsOf(machine, Comp<AutoPortsComponent>(machine));
        if (TryComp<StackComponent>(item, out var stack))
        {
            foreach (var slotId in inputs)
            {
                if (!SlotAccepts(machine, slotId, item))
                    continue;

                if (_itemSlots.GetItemOrNull(machine, slotId) is not { } held ||
                    !TryComp<StackComponent>(held, out var heldStack) ||
                    heldStack.StackTypeId != stack.StackTypeId ||
                    _stack.GetAvailableSpace(heldStack) <= 0)
                {
                    continue;
                }

                if (!loose && !_container.TryRemoveFromContainer(item))
                    return false;

                _stack.TryMergeStacks((item, stack), (held, heldStack), out _);

                // What did not fit goes back where it came from.
                if (!loose && !TerminatingOrDeleted(item))
                    DropBack(item);

                return true;
            }
        }

        foreach (var slotId in inputs)
        {
            if (_itemSlots.GetItemOrNull(machine, slotId) != null || !SlotAccepts(machine, slotId, item))
                continue;

            if (!loose && !_container.TryRemoveFromContainer(item))
                return false;

            if (_itemSlots.TryInsert(machine, slotId, item, null))
                return true;

            if (!loose)
                DropBack(item);

            return false;
        }

        return false;
    }

    private void DropBack(EntityUid item)
    {
        foreach (var target in _found)
        {
            if (_belts.TryPut(target, item))
                return;
        }
    }

    // What a machine that is not a recipe machine uses, by what it is: the fuel of a boiler, the ore of a smelter, the ingots of a press and the planks of its fire, what a grinder
    // grinds and what a condenser sets into pieces. Cached by the kind of the machine.
    private HashSet<string> StructureWants(EntityUid machine)
    {
        var key = MetaData(machine).EntityPrototype?.ID ?? string.Empty;
        if (_structure.TryGetValue(key, out var known))
            return known;

        var set = new HashSet<string>();

        if (TryComp<BoilerComponent>(machine, out var boiler))
        {
            foreach (var fuel in boiler.Fuels.Keys)
                set.Add(fuel.Id);
        }

        if (HasComp<SteamSmelterComponent>(machine))
        {
            foreach (var recipe in _proto.EnumeratePrototypes<SteamSmeltRecipePrototype>())
            {
                foreach (var input in recipe.Inputs.Keys)
                    set.Add(input.Id);
            }
        }

        if (TryComp<SolidFuelPressComponent>(machine, out var press))
        {
            foreach (var recipe in _proto.EnumeratePrototypes<PressRecipePrototype>())
            {
                foreach (var input in recipe.Inputs.Keys)
                    set.Add(input.Id);
            }

            if (_itemSlots.TryGetSlot(machine, SolidFuelPressComponent.FuelSlotId, out _))
                set.Add(press.FuelStack.Id);
        }

        if (HasComp<GrinderComponent>(machine))
        {
            foreach (var recipe in _proto.EnumeratePrototypes<GrinderRecipePrototype>())
                set.Add(recipe.Input.Id);
        }

        if (HasComp<CondenserComponent>(machine))
        {
            foreach (var recipe in _proto.EnumeratePrototypes<CondenserRecipePrototype>())
            {
                if (recipe.Material is { } material)
                    set.Add(material.Id);
            }
        }

        _structure[key] = set;
        return set;
    }

    // A machine that has no window with the switches has them in its menu of the right mouse button. The menu is asked by the system of the disconnect button, which has the verbs of the
    // ports already, and the two cannot both listen to the same thing.
    public void AddSwitchVerbs(Entity<AutoPortsComponent> machine, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || HasComp<RecipeMachineComponent>(machine))
            return;

        var target = machine.Owner;
        var ports = machine.Comp;

        if (ports.InputSlots.Count > 0)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(ports.AutoInput ? "au14-autoports-input-off" : "au14-autoports-input-on"),
                Priority = 8,
                Act = () => Toggle(target, output: false),
            });
        }

        if (ports.OutputSlots.Count > 0)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(ports.AutoOutput ? "au14-autoports-output-off" : "au14-autoports-output-on"),
                Priority = 7,
                Act = () => Toggle(target, output: true),
            });
        }
    }

    private void Toggle(EntityUid machine, bool output)
    {
        if (!TryComp<AutoPortsComponent>(machine, out var ports))
            return;

        if (output)
            ports.AutoOutput = !ports.AutoOutput;
        else
            ports.AutoInput = !ports.AutoInput;

        Dirty(machine, ports);
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
