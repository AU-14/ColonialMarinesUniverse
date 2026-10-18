// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.CMU14.Fluids;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Smelting;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Smelting;

// The solid fuel press: takes stacks of fuel stock, presses them with steam into fuel, and lights up while it works.
public sealed class SolidFuelPressSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly FluidSystem _fluids = default!;
    [Dependency] private readonly SteamGasSystem _steam = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    // The ingots in the input slots: the item and the stack on it.
    private readonly List<(EntityUid Item, StackComponent Stack)> _inputs = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SolidFuelPressComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<SolidFuelPressComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
    }

    // Only planks burn in it.
    private void OnInsertAttempt(Entity<SolidFuelPressComponent> press, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != SolidFuelPressComponent.FuelSlotId)
            return;

        if (!TryComp<StackComponent>(args.Item, out var stack) || stack.StackTypeId != press.Comp.FuelStack)
            args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SolidFuelPressComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdatePress((uid, comp), frameTime);
        }
    }

    private void UpdatePress(Entity<SolidFuelPressComponent> press, float frameTime)
    {
        var comp = press.Comp;
        var now = _timing.CurTime;

        var recipe = FindRecipe(press, out var consumed);

        // Nothing to press, or nowhere to put the result: whatever was begun is stopped where it is.
        if (recipe == null || !CanTakeOutput(press, recipe) || !HasFluids(press, recipe))
        {
            if (recipe == null || comp.ActiveRecipe != recipe.ID)
                Cancel(press);
            else
                Pause(press);

            return;
        }

        if (comp.ActiveRecipe != null && comp.ActiveRecipe != recipe.ID)
            Cancel(press);

        // A steam press runs on steam, a solid-fuel one on a burning plank; either only when there is work for it to do.
        if (comp.SteamPerSecond > 0f)
        {
            if (!_steam.TryUse(press.Owner, comp.SteamPerSecond * frameTime))
            {
                Pause(press);
                return;
            }
        }
        else if (comp.FuelLeft <= 0f && !BurnPlank(press))
        {
            Pause(press);
            return;
        }

        if (comp.WorkEndsAt == null)
        {
            var duration = comp.WorkRemaining ?? TimeSpan.FromSeconds(recipe.Duration);
            comp.WorkDuration = TimeSpan.FromSeconds(recipe.Duration);
            comp.WorkEndsAt = now + duration;
            comp.WorkRemaining = null;
            comp.ActiveRecipe = recipe.ID;
            comp.Working = true;
            Dirty(press);
            SetLight(press, true);
        }

        if (comp.SteamPerSecond <= 0f)
            comp.FuelLeft -= frameTime;

        if (now >= comp.WorkEndsAt)
            Complete(press, recipe, consumed);
    }

    // Burns the next plank of the fuel slot. Returns false when there is none.
    private bool BurnPlank(Entity<SolidFuelPressComponent> press)
    {
        if (_itemSlots.GetItemOrNull(press.Owner, SolidFuelPressComponent.FuelSlotId) is not { } fuel ||
            !TryComp<StackComponent>(fuel, out var stack) || stack.StackTypeId != press.Comp.FuelStack || stack.Count <= 0)
        {
            return false;
        }

        _stack.SetCount((fuel, stack), stack.Count - 1);
        press.Comp.FuelLeft = press.Comp.PlankBurnSeconds;
        return true;
    }

    // Stops the work with what is left of it kept, to go on from where it stopped.
    private void Pause(Entity<SolidFuelPressComponent> press)
    {
        var comp = press.Comp;
        if (comp.WorkEndsAt is not { } endsAt)
            return;

        var left = endsAt - _timing.CurTime;
        comp.WorkRemaining = left > TimeSpan.Zero ? left : TimeSpan.Zero;
        comp.WorkEndsAt = null;
        comp.Working = false;
        Dirty(press);
        SetLight(press, false);
    }

    // Stops the work and forgets it: what was in the press is no longer what it was begun with.
    private void Cancel(Entity<SolidFuelPressComponent> press)
    {
        var comp = press.Comp;
        if (comp.WorkEndsAt == null && comp.WorkRemaining == null && !comp.Working && comp.ActiveRecipe == null)
            return;

        comp.WorkEndsAt = null;
        comp.WorkDuration = null;
        comp.WorkRemaining = null;
        comp.ActiveRecipe = null;
        comp.Working = false;
        Dirty(press);
        SetLight(press, false);
    }

    private void SetLight(EntityUid uid, bool on)
    {
        if (_light.TryGetLight(uid, out var light))
            _light.SetEnabled(uid, on, light);
    }

    // The recipe of the mold and the ingots in the press, and what it uses up. Two ingots are combined when there is a recipe
    // for the pair; otherwise the one that has a recipe of its own is pressed alone, the first slot's before the second's.
    private PressRecipePrototype? FindRecipe(Entity<SolidFuelPressComponent> press, out List<(EntityUid Item, int Amount)> consumed)
    {
        consumed = new List<(EntityUid, int)>();

        // With a mold in the press only the recipes of its shape are looked at; with none, the recipes that need no mold.
        var moldShape = string.Empty;
        if (_itemSlots.GetItemOrNull(press.Owner, SolidFuelPressComponent.MoldSlotId) is { } mold)
        {
            if (!TryComp<PressMoldComponent>(mold, out var shape))
                return null;

            moldShape = shape.Shape;
        }

        _inputs.Clear();
        foreach (var slotId in SolidFuelPressComponent.InputSlotIds)
        {
            if (_itemSlots.GetItemOrNull(press.Owner, slotId) is { } item && TryComp<StackComponent>(item, out var stack))
                _inputs.Add((item, stack));
        }

        if (_inputs.Count == 0)
            return null;

        PressRecipePrototype? best = null;
        List<(EntityUid, int)>? bestUse = null;
        var bestSlot = int.MaxValue;

        foreach (var recipe in _proto.EnumeratePrototypes<PressRecipePrototype>())
        {
            if (recipe.Shape != moldShape || recipe.Inputs.Count == 0 || recipe.Inputs.Count > _inputs.Count)
                continue;

            if (!TryMatch(recipe, out var use))
                continue;

            // The recipe of two ingots beats the recipe of one; of two with the same count the one that takes the earlier slot wins.
            var slot = int.MaxValue;
            foreach (var (item, _) in use)
                slot = Math.Min(slot, _inputs.FindIndex(input => input.Item == item));

            if (best == null || recipe.Inputs.Count > best.Inputs.Count || recipe.Inputs.Count == best.Inputs.Count && slot < bestSlot)
            {
                best = recipe;
                bestUse = use;
                bestSlot = slot;
            }
        }

        if (best != null && bestUse != null)
            consumed = bestUse;

        return best;
    }

    // Each of the recipe's inputs needs an ingot of its own, with enough of it.
    private bool TryMatch(PressRecipePrototype recipe, out List<(EntityUid, int)> use)
    {
        use = new List<(EntityUid, int)>();
        var taken = new HashSet<EntityUid>();

        foreach (var (stackId, amount) in recipe.Inputs)
        {
            var found = false;
            foreach (var (item, stack) in _inputs)
            {
                if (taken.Contains(item) || stack.StackTypeId != stackId || stack.Count < amount)
                    continue;

                taken.Add(item);
                use.Add((item, amount));
                found = true;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }

    // The storage holds the liquids the piece needs.
    private bool HasFluids(Entity<SolidFuelPressComponent> press, PressRecipePrototype recipe)
    {
        foreach (var (kind, amount) in recipe.Fluids)
        {
            if (_fluids.GetAmount(press.Owner, kind) < amount)
                return false;
        }

        return true;
    }

    // Some output slot is empty, or holds the same pieces and has room for more of them.
    private bool CanTakeOutput(Entity<SolidFuelPressComponent> press, PressRecipePrototype recipe)
    {
        foreach (var slotId in SolidFuelPressComponent.OutputSlotIds)
        {
            if (_itemSlots.GetItemOrNull(press.Owner, slotId) is not { } held)
                return true;

            if (TryComp<StackComponent>(held, out var stack) && stack.StackTypeId == recipe.Result &&
                _stack.GetAvailableSpace(stack) >= recipe.ResultAmount)
            {
                return true;
            }
        }

        return false;
    }

    private void Complete(Entity<SolidFuelPressComponent> press, PressRecipePrototype recipe, List<(EntityUid Item, int Amount)> consumed)
    {
        Cancel(press);

        if (!_proto.TryIndex(recipe.Result, out var resultProto) || string.IsNullOrEmpty(resultProto.Spawn.Id))
            return;

        foreach (var (item, amount) in consumed)
        {
            if (TryComp<StackComponent>(item, out var stack))
                _stack.SetCount((item, stack), stack.Count - amount);
        }

        foreach (var (kind, amount) in recipe.Fluids)
        {
            _fluids.TryUse(press.Owner, kind, amount);
        }

        var made = Spawn(resultProto.Spawn, Transform(press).Coordinates);
        if (TryComp<StackComponent>(made, out var madeStack))
            _stack.SetCount((made, madeStack), recipe.ResultAmount);

        // Added to the pieces already lying in an output slot, or into a slot that is free.
        foreach (var slotId in SolidFuelPressComponent.OutputSlotIds)
        {
            if (_itemSlots.GetItemOrNull(press.Owner, slotId) is not { } held || !TryComp<StackComponent>(held, out var heldStack) ||
                heldStack.StackTypeId != recipe.Result || _stack.GetAvailableSpace(heldStack) < recipe.ResultAmount)
            {
                continue;
            }

            _stack.TryMergeStacks(made, (held, heldStack), out _);
            if (!TerminatingOrDeleted(made))
                Del(made);

            return;
        }

        foreach (var slotId in SolidFuelPressComponent.OutputSlotIds)
        {
            if (_itemSlots.GetItemOrNull(press.Owner, slotId) == null && _itemSlots.TryInsert(press.Owner, slotId, made, null))
                return;
        }

        // No slot took it: it lies in front of the press, which is no loss.
    }

    private void OnExamined(Entity<SolidFuelPressComponent> press, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using var block = args.PushGroup(nameof(SolidFuelPressComponent));
        args.PushMarkup(Loc.GetString(press.Comp.Working ? "au14-press-examine-working" : "au14-press-examine-idle"));
    }
}
