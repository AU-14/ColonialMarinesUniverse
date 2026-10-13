// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.CMU14.Fluids;
using Content.Server.Power.EntitySystems;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.Fluids;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Electric;

// The machines of the electric age (see RecipeMachineComponent): the wiremill, the bender, the assembler and the rest. A machine
// that is powered looks for a recipe of its kind that its slots and tank can pay for, uses up the inputs, works for as long as
// the recipe says (and stands still while the power is out), and gives the outputs when done.
public sealed class RecipeMachineSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly FluidSystem _fluids = default!;
    [Dependency] private readonly SteamGasSystem _steam = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerStateSystem _powerState = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private static readonly SoundSpecifier DoneSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");

    // How often the progress of a running machine is sent to the people looking at it.
    private const float DirtyInterval = 0.25f;

    private readonly Dictionary<string, List<MachineRecipePrototype>> _recipes = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RecipeMachineComponent, RecipeMachineCircuitMessage>(OnCircuit);
        SubscribeLocalEvent<RecipeMachineComponent, ExaminedEvent>(OnExamined);
        _proto.PrototypesReloaded += _ => _recipes.Clear();
    }

    // The recipes of a kind of machine, in the order they are tried in.
    public IReadOnlyList<MachineRecipePrototype> RecipesOf(string kind)
    {
        if (_recipes.TryGetValue(kind, out var known))
            return known;

        var list = new List<MachineRecipePrototype>();
        foreach (var recipe in _proto.EnumeratePrototypes<MachineRecipePrototype>())
        {
            if (recipe.Machine == kind)
                list.Add(recipe);
        }

        // The ones that name a circuit first, then the ones that ask for more (a ring with a diamond before a plain ring), then by name.
        list.Sort((a, b) =>
        {
            var byCircuit = (a.Circuit == null ? 1 : 0).CompareTo(b.Circuit == null ? 1 : 0);
            if (byCircuit != 0)
                return byCircuit;

            var byAsk = (b.Inputs.Count + b.ItemInputs.Count + b.FluidInputs.Count + b.Catalysts.Count).CompareTo(a.Inputs.Count + a.ItemInputs.Count + a.FluidInputs.Count + a.Catalysts.Count);
            return byAsk != 0 ? byAsk : string.CompareOrdinal(a.ID, b.ID);
        });

        _recipes[kind] = list;
        return list;
    }

    private void OnCircuit(Entity<RecipeMachineComponent> machine, ref RecipeMachineCircuitMessage args)
    {
        machine.Comp.Circuit = Math.Clamp(args.Circuit, 0, machine.Comp.MaxCircuit);
        Dirty(machine);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RecipeMachineComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var machine = new Entity<RecipeMachineComponent>(uid, comp);

            // A steam machine runs on steam, which is used up while it works; the others run on power.
            var steamMode = comp.SteamPerSecond > 0f;
            var powered = steamMode
                ? _steam.GetMoles(uid) > 0.01f &&
                  (comp.ActiveRecipe == null || comp.Blocked || _steam.TryUse(uid, comp.SteamPerSecond * frameTime))
                : this.IsPowered(uid, EntityManager);

            if (!powered)
            {
                if (!steamMode)
                    _powerState.TrySetWorkingState(uid, false);

                _appearance.SetData(uid, RecipeMachineVisuals.Working, false);
                continue;
            }

            if (comp.ActiveRecipe == null)
                TryStart(machine);

            if (comp.ActiveRecipe != null)
                Step(machine, frameTime);

            var working = comp.ActiveRecipe != null && !comp.Blocked;
            if (!steamMode)
                _powerState.TrySetWorkingState(uid, working);

            _appearance.SetData(uid, RecipeMachineVisuals.Working, working);
        }
    }

    private void Step(Entity<RecipeMachineComponent> machine, float frameTime)
    {
        var comp = machine.Comp;
        if (comp.ActiveRecipe == null || !_proto.TryIndex<MachineRecipePrototype>(comp.ActiveRecipe, out var recipe))
        {
            Reset(machine);
            return;
        }

        if (comp.Progress < 1f)
        {
            comp.Progress = Math.Min(1f, comp.Progress + frameTime * comp.Speed / Math.Max(0.1f, recipe.Duration));
            comp.NextDirty -= frameTime;
            if (comp.NextDirty <= 0f)
            {
                comp.NextDirty = DirtyInterval;
                Dirty(machine);
            }
        }

        if (comp.Progress < 1f)
            return;

        if (!HasRoomForOutputs(machine, recipe, 0f))
        {
            if (!comp.Blocked)
            {
                comp.Blocked = true;
                Dirty(machine);
            }

            return;
        }

        Finish(machine, recipe);
    }

    private void Reset(Entity<RecipeMachineComponent> machine)
    {
        var comp = machine.Comp;
        comp.ActiveRecipe = null;
        comp.Active = false;
        comp.Blocked = false;
        comp.Progress = 0f;
        Dirty(machine);
    }

    private void TryStart(Entity<RecipeMachineComponent> machine)
    {
        var comp = machine.Comp;
        var tally = Tally(machine);

        foreach (var recipe in RecipesOf(comp.Kind))
        {
            if (recipe.Circuit is { } circuit && circuit != comp.Circuit)
                continue;

            if (!CanPay(machine, recipe, tally))
                continue;

            if (!HasRoomForOutputs(machine, recipe, FluidTotal(recipe.FluidInputs)))
                continue;

            Pay(machine, recipe);

            comp.ActiveRecipe = recipe.ID;
            comp.Active = true;
            comp.Blocked = false;
            comp.Progress = 0f;
            Dirty(machine);
            return;
        }
    }

    private void Finish(Entity<RecipeMachineComponent> machine, MachineRecipePrototype recipe)
    {
        var coords = Transform(machine).Coordinates;

        foreach (var (stackId, amount) in recipe.Outputs)
            SpawnOutput(machine, coords, stackId, amount);

        foreach (var (proto, amount) in recipe.ItemOutputs)
        {
            for (var i = 0; i < amount; i++)
                SpawnItem(machine, coords, proto);
        }

        foreach (var (kind, amount) in recipe.FluidOutputs)
            _fluids.Add(machine, kind, amount);

        _audio.PlayPvs(DoneSound, machine);
        Reset(machine);
    }

    // ------------------------------------------------------------------ inputs

    private Dictionary<string, int> Tally(Entity<RecipeMachineComponent> machine)
    {
        var tally = new Dictionary<string, int>();
        foreach (var slotId in machine.Comp.InputIds)
        {
            if (_itemSlots.GetItemOrNull(machine.Owner, slotId) is not { } item)
                continue;

            if (TryComp<StackComponent>(item, out var stack))
                tally[stack.StackTypeId] = tally.GetValueOrDefault(stack.StackTypeId) + stack.Count;
            else if (MetaData(item).EntityPrototype?.ID is { } proto)
                tally[proto] = tally.GetValueOrDefault(proto) + 1;
        }

        return tally;
    }

    private bool CanPay(Entity<RecipeMachineComponent> machine, MachineRecipePrototype recipe, Dictionary<string, int> tally)
    {
        if (recipe.Inputs.Count == 0 && recipe.FluidInputs.Count == 0 && recipe.ItemInputs.Count == 0)
            return false;

        foreach (var (proto, amount) in recipe.ItemInputs)
        {
            if (tally.GetValueOrDefault(proto) < amount)
                return false;
        }

        foreach (var (stackId, amount) in recipe.Catalysts)
        {
            if (tally.GetValueOrDefault(stackId) < amount + recipe.Inputs.GetValueOrDefault(stackId))
                return false;
        }

        foreach (var (stackId, amount) in recipe.Inputs)
        {
            if (tally.GetValueOrDefault(stackId) < amount)
                return false;
        }

        foreach (var (kind, amount) in recipe.FluidInputs)
        {
            if (_fluids.GetAmount(machine, kind) + 0.0001f < amount)
                return false;
        }

        return true;
    }

    private void Pay(Entity<RecipeMachineComponent> machine, MachineRecipePrototype recipe)
    {
        foreach (var (stackId, amount) in recipe.Inputs)
        {
            var left = amount;
            foreach (var slotId in machine.Comp.InputIds)
            {
                if (left <= 0)
                    break;

                if (_itemSlots.GetItemOrNull(machine.Owner, slotId) is not { } item ||
                    !TryComp<StackComponent>(item, out var stack) ||
                    stack.StackTypeId != stackId)
                {
                    continue;
                }

                var take = Math.Min(left, stack.Count);
                _stack.SetCount(item, stack.Count - take, stack);
                left -= take;
            }
        }

        foreach (var (proto, amount) in recipe.ItemInputs)
        {
            var left = amount;
            foreach (var slotId in machine.Comp.InputIds)
            {
                if (left <= 0)
                    break;

                if (_itemSlots.GetItemOrNull(machine.Owner, slotId) is not { } item ||
                    HasComp<StackComponent>(item) ||
                    MetaData(item).EntityPrototype?.ID != proto.Id)
                {
                    continue;
                }

                PredictedQueueDel(item);
                left--;
            }
        }

        foreach (var (kind, amount) in recipe.FluidInputs)
            _fluids.Remove(machine, kind, amount);
    }

    // ------------------------------------------------------------------ outputs

    private static float FluidTotal(Dictionary<string, float> fluids)
    {
        var total = 0f;
        foreach (var amount in fluids.Values)
            total += amount;

        return total;
    }

    // Whether everything the recipe gives has somewhere to go: the liquids room in the tank (the liquids it is about to use up
    // make room for them), the stacks a free output slot each or a stack of the same kind with room.
    private bool HasRoomForOutputs(Entity<RecipeMachineComponent> machine, MachineRecipePrototype recipe, float fluidFreed)
    {
        var outFluid = FluidTotal(recipe.FluidOutputs);
        if (outFluid > 0f)
        {
            if (!TryComp<FluidTankComponent>(machine, out var tank) || tank.Capacity - (tank.Total - fluidFreed) + 0.0001f < outFluid)
                return false;
        }

        var free = 0;
        var space = new Dictionary<string, int>();
        foreach (var slotId in machine.Comp.OutputIds)
        {
            if (_itemSlots.GetItemOrNull(machine.Owner, slotId) is not { } held)
                free++;
            else if (TryComp<StackComponent>(held, out var stack))
                space[stack.StackTypeId] = space.GetValueOrDefault(stack.StackTypeId) + _stack.GetAvailableSpace(stack);
        }

        foreach (var amount in recipe.ItemOutputs.Values)
        {
            free -= amount;
            if (free < 0)
                return false;
        }

        foreach (var (stackId, amount) in recipe.Outputs)
        {
            var spaceLeft = space.GetValueOrDefault(stackId);
            if (spaceLeft >= amount)
            {
                space[stackId] = spaceLeft - amount;
            }
            else if (free > 0)
            {
                free--;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private void SpawnItem(Entity<RecipeMachineComponent> machine, EntityCoordinates coords, EntProtoId proto)
    {
        var item = Spawn(proto, coords);
        foreach (var slotId in machine.Comp.OutputIds)
        {
            if (_itemSlots.GetItemOrNull(machine.Owner, slotId) == null && _itemSlots.TryInsert(machine.Owner, slotId, item, null))
                return;
        }

        // The room was counted before the work began; an item that still finds none is left beside the machine.
    }

    private void SpawnOutput(Entity<RecipeMachineComponent> machine, EntityCoordinates coords, ProtoId<StackPrototype> stackId, int amount)
    {
        if (!_proto.TryIndex(stackId, out var stackProto) || string.IsNullOrEmpty(stackProto.Spawn.Id))
            return;

        var result = Spawn(stackProto.Spawn, coords);
        if (TryComp<StackComponent>(result, out var resultStack))
            _stack.SetCount(result, amount, resultStack);

        // A stack of the same kind in an output slot takes what it can first; the rest goes into a free slot.
        if (resultStack != null)
        {
            foreach (var slotId in machine.Comp.OutputIds)
            {
                if (_itemSlots.GetItemOrNull(machine.Owner, slotId) is not { } held ||
                    !TryComp<StackComponent>(held, out var heldStack) ||
                    heldStack.StackTypeId != resultStack.StackTypeId)
                {
                    continue;
                }

                _stack.TryMergeStacks((result, resultStack), (held, heldStack), out _);
                if (TerminatingOrDeleted(result) || resultStack.Count <= 0)
                    return;
            }
        }

        foreach (var slotId in machine.Comp.OutputIds)
        {
            if (_itemSlots.GetItemOrNull(machine.Owner, slotId) == null && _itemSlots.TryInsert(machine.Owner, slotId, result, null))
                return;
        }

        PredictedQueueDel(result);
    }

    // ------------------------------------------------------------------ examine

    private void OnExamined(Entity<RecipeMachineComponent> machine, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using var block = args.PushGroup(nameof(RecipeMachineComponent));

        var powered = machine.Comp.SteamPerSecond > 0f
            ? _steam.GetMoles(machine.Owner) > 0.01f
            : this.IsPowered(machine.Owner, EntityManager);

        if (!powered)
            args.PushMarkup(Loc.GetString("au14-machine-examine-unpowered"));
        else if (machine.Comp.Blocked)
            args.PushMarkup(Loc.GetString("au14-machine-examine-blocked"));
        else if (machine.Comp.ActiveRecipe != null)
            args.PushMarkup(Loc.GetString("au14-machine-examine-working", ("percent", (int) (machine.Comp.Progress * 100))));
        else
            args.PushMarkup(Loc.GetString("au14-machine-examine-idle"));

        if (machine.Comp.Circuit > 0)
            args.PushMarkup(Loc.GetString("au14-machine-examine-circuit", ("circuit", machine.Comp.Circuit)));
    }
}
