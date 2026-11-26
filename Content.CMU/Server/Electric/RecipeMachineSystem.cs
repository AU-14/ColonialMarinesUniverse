// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using Content.Server.CMU14.Fluids;
using Content.Server.Power.EntitySystems;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Fluids;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
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

        Subs.BuiEvents<RecipeMachineComponent>(RecipeMachineUiKey.Key, subs =>
        {
            subs.Event<RecipeMachineCircuitMessage>(OnCircuit);
            subs.Event<CMUSelectMachineRecipeMessage>(OnRecipeSelected);
        });
        SubscribeLocalEvent<RecipeMachineComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RecipeMachineComponent, CMUFluidTankRoomEvent>(OnGetFluidTankRoom);
        SubscribeLocalEvent<RecipeMachineComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<RecipeMachineComponent, EntRemovedFromContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<StackComponent, StackCountChangedEvent>(OnStackCountChanged);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnSlotChanged(EntityUid uid, RecipeMachineComponent comp, ContainerModifiedMessage args)
    {
        comp.NeedsRecipeCheck = true;
    }

    private void OnStackCountChanged(EntityUid uid, StackComponent comp, StackCountChangedEvent args)
    {
        if (TryComp<RecipeMachineComponent>(Transform(uid).ParentUid, out var machine))
            machine.NeedsRecipeCheck = true;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        _recipes.Clear();
        var query = AllEntityQuery<RecipeMachineComponent>();
        while (query.MoveNext(out var uid, out var machine))
        {
            if (machine.SelectedRecipe is { } id &&
                (!_proto.TryIndex<MachineRecipePrototype>(id, out var recipe) || recipe.Machine != machine.Kind))
            {
                machine.SelectedRecipe = null;
                Dirty(uid, machine);
            }
            machine.NeedsRecipeCheck = true;
        }
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

        list.Sort((a, b) =>
        {
            var priority = Priority(b).CompareTo(Priority(a));
            return priority != 0 ? priority : string.CompareOrdinal(a.ID, b.ID);
        });

        _recipes[kind] = list;
        return list;
    }

    private static (bool Circuit, int Ingredients, int Batch) Priority(MachineRecipePrototype recipe)
    {
        var ingredients = recipe.Inputs.Count + recipe.ItemInputs.Count + recipe.FluidInputs.Count + recipe.Catalysts.Count;
        return (recipe.Circuit.HasValue, ingredients, recipe.AutoPriority);
    }

    private void OnCircuit(Entity<RecipeMachineComponent> machine, ref RecipeMachineCircuitMessage args)
    {
        machine.Comp.Circuit = Math.Clamp(args.Circuit, 0, machine.Comp.MaxCircuit);
        if (machine.Comp.SelectedRecipe is { } selected && _proto.TryIndex<MachineRecipePrototype>(selected, out var recipe)
            && recipe.Circuit is { } required && required != machine.Comp.Circuit)
            machine.Comp.SelectedRecipe = null;

        machine.Comp.NeedsRecipeCheck = true;
        Dirty(machine);
    }

    private void OnRecipeSelected(Entity<RecipeMachineComponent> machine, ref CMUSelectMachineRecipeMessage args)
    {
        if (args.Recipe is { } id)
        {
            if (!_proto.TryIndex<MachineRecipePrototype>(id, out var recipe) || recipe.Machine != machine.Comp.Kind)
                return;

            if (recipe.Circuit is { } circuit)
            {
                if (circuit < 0 || circuit > machine.Comp.MaxCircuit)
                    return;

                machine.Comp.Circuit = circuit;
            }
        }

        machine.Comp.SelectedRecipe = args.Recipe;
        machine.Comp.RecipeChoices.Clear();
        machine.Comp.NeedsRecipeCheck = true;
        Dirty(machine);
    }

    private void OnGetFluidTankRoom(Entity<RecipeMachineComponent> machine, ref CMUFluidTankRoomEvent args)
    {
        if (machine.Comp.ActiveRecipe is { } active &&
            _proto.TryIndex<MachineRecipePrototype>(active, out var recipe))
        {
            // Inputs are paid at the start. Keep their newly freed room for the batch's products.
            args.Available -= FluidTotal(recipe.FluidOutputs);
        }
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
                    _powerState.TrySetWorkingState(uid, comp.ActiveRecipe != null && !comp.Blocked);

                _appearance.SetData(uid, RecipeMachineVisuals.Working, false);
                continue;
            }

            if (comp.ActiveRecipe == null || comp.Blocked)
            {
                var revision = CompOrNull<FluidTankComponent>(uid)?.ContentsRevision ?? 0;
                if (comp.NeedsRecipeCheck || comp.LastTankRevision != revision)
                {
                    comp.LastTankRevision = revision;
                    comp.NeedsRecipeCheck = false;
                    if (comp.ActiveRecipe == null)
                        TryStart(machine);
                    else
                        Step(machine, frameTime);
                }
            }

            if (comp.ActiveRecipe != null && !comp.Blocked)
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

        if (!HasRoomForOutputs(machine, recipe, 0f, machine.Comp.UnmarkedOutput))
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
        comp.NeedsRecipeCheck = true;
        comp.ActiveRecipe = null;
        comp.Active = false;
        comp.Blocked = false;
        comp.Progress = 0f;
        comp.UnmarkedOutput = false;
        Dirty(machine);
    }

    private void TryStart(Entity<RecipeMachineComponent> machine)
    {
        var comp = machine.Comp;
        var tally = Tally(machine);

        var recipe = SelectRecipe(RecipesOf(comp.Kind), comp.SelectedRecipe, comp.Circuit,
            candidate => CanPay(machine, candidate, tally) &&
                         HasRoomForOutputs(machine, candidate, FluidTotal(candidate.FluidInputs), UsesUnmarkedStock(machine, candidate)),
            out var choices);

        if (choices != null && !comp.RecipeChoices.SequenceEqual(choices))
        {
            comp.RecipeChoices = choices;
            Dirty(machine);
        }
        else if (choices == null && comp.RecipeChoices.Count > 0)
        {
            comp.RecipeChoices.Clear();
            Dirty(machine);
        }

        if (recipe == null)
            return;

        comp.UnmarkedOutput = UsesUnmarkedStock(machine, recipe);
        Pay(machine, recipe);
        comp.ActiveRecipe = recipe.ID;
        comp.Active = true;
        comp.Blocked = false;
        comp.Progress = 0f;
        Dirty(machine);
    }

    // The caller supplies recipes in priority order and checks the available inputs and output room.
    // A tie leaves all inputs in place until the operator picks a recipe.
    internal static MachineRecipePrototype? SelectRecipe(IReadOnlyList<MachineRecipePrototype> recipes, string? selected,
        int circuit, Func<MachineRecipePrototype, bool> canStart, out List<string>? choices)
    {
        MachineRecipePrototype? first = null;
        choices = null;
        foreach (var recipe in recipes)
        {
            if (selected != null && recipe.ID != selected)
                continue;
            if (recipe.Circuit != null && recipe.Circuit != circuit)
                continue;
            if (first != null && Priority(recipe) != Priority(first))
                break;
            if (!canStart(recipe))
                continue;

            if (first == null)
            {
                first = recipe;
                continue;
            }

            choices ??= new List<string> { first.ID };
            choices.Add(recipe.ID);
        }

        return choices == null ? first : null;
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

        // Release the reservation before adding the products it was protecting.
        machine.Comp.ActiveRecipe = null;
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

    // Inspect the same stacks, in the same order, that this batch will consume.
    private bool UsesUnmarkedStock(Entity<RecipeMachineComponent> machine, MachineRecipePrototype recipe)
        => recipe.InheritStockOrigin && StackPayments(machine, recipe).Any(input => !HasComp<ASRSSellableComponent>(input.Item));

    private IEnumerable<(EntityUid Item, StackComponent Stack, int Count)> StackPayments(
        Entity<RecipeMachineComponent> machine, MachineRecipePrototype recipe)
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
                    stack.StackTypeId != stackId || stack.Count <= 0)
                {
                    continue;
                }

                var take = Math.Min(left, stack.Count);
                left -= take;
                yield return (item, stack, take);
            }
        }
    }

    private void Pay(Entity<RecipeMachineComponent> machine, MachineRecipePrototype recipe)
    {
        foreach (var (item, stack, count) in StackPayments(machine, recipe))
            _stack.SetCount(item, stack.Count - count, stack);

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
    private bool HasRoomForOutputs(Entity<RecipeMachineComponent> machine, MachineRecipePrototype recipe, float fluidFreed, bool unmarkedOutput)
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
            {
                var origin = CompOrNull<ASRSSellableComponent>(held);
                if (unmarkedOutput ? origin == null : origin is { FromOreDeposit: false })
                    space[stack.StackTypeId] = space.GetValueOrDefault(stack.StackTypeId) + _stack.GetAvailableSpace(stack);
            }
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
        if (!machine.Comp.UnmarkedOutput)
            EnsureComp<ASRSSellableComponent>(item);
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
        if (!machine.Comp.UnmarkedOutput)
            EnsureComp<ASRSSellableComponent>(result);
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
        else if (machine.Comp.RecipeChoices.Count > 0)
            args.PushMarkup(Loc.GetString("cmu-machine-recipe-choose-output"));
        else
            args.PushMarkup(Loc.GetString("au14-machine-examine-idle"));

        if (machine.Comp.Circuit > 0)
            args.PushMarkup(Loc.GetString("au14-machine-examine-circuit", ("circuit", machine.Comp.Circuit)));
    }
}
