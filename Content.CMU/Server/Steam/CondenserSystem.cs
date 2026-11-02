// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.CMU14.Fluids;
using Content.Server.Power.EntitySystems;
using Content.Shared.CMU14.Steam;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Steam;

// The condenser: it makes a piece of a recipe when it has the liquid, the coolant and the material for it, and room for the
// result; the ordinary one only while it has power, the brass one only while the coolant is steady.
public sealed class CondenserSystem : EntitySystem
{
    [Dependency] private readonly FluidSystem _fluids = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    private const float DirtyInterval = 0.2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CondenserComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CondenserComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
    }

    // Only what a recipe takes goes into the material slot.
    private void OnInsertAttempt(Entity<CondenserComponent> condenser, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != CondenserComponent.MaterialSlotId)
            return;

        if (!TryComp<StackComponent>(args.Item, out var stack))
        {
            args.Cancelled = true;
            return;
        }

        foreach (var recipe in _proto.EnumeratePrototypes<CondenserRecipePrototype>())
        {
            if (recipe.Material == stack.StackTypeId)
                return;
        }

        args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CondenserComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateCondenser((uid, comp), frameTime);
        }
    }

    private void UpdateCondenser(Entity<CondenserComponent> condenser, float frameTime)
    {
        var comp = condenser.Comp;

        if (comp.NeedsPower && !this.IsPowered(condenser.Owner, EntityManager))
        {
            Stop(condenser, false);
            return;
        }

        var recipe = FindRecipe(condenser);
        if (recipe == null)
        {
            Stop(condenser, true);
            return;
        }

        // The brass one wants a steady supply: with less of the coolant than that it loses its work.
        if (comp.SteadyCoolant > 0f && _fluids.GetAmount(condenser.Owner, recipe.Coolant) < comp.SteadyCoolant)
        {
            Stop(condenser, true);
            return;
        }

        if (comp.ActiveRecipe != recipe.ID)
        {
            comp.ActiveRecipe = recipe.ID;
            comp.Progress = 0f;
        }

        comp.Active = true;
        comp.Progress += frameTime / Math.Max(recipe.Duration, 0.1f);

        if (comp.Progress >= 1f)
        {
            Complete(condenser, recipe);
            return;
        }

        comp.NextDirty -= frameTime;
        if (comp.NextDirty <= 0f)
        {
            comp.NextDirty = DirtyInterval;
            Dirty(condenser);
        }
    }

    private void Stop(Entity<CondenserComponent> condenser, bool forget)
    {
        var comp = condenser.Comp;
        if (!comp.Active && comp.Progress == 0f && comp.ActiveRecipe == null)
            return;

        comp.Active = false;
        if (forget)
        {
            comp.Progress = 0f;
            comp.ActiveRecipe = null;
        }

        Dirty(condenser);
    }

    // The recipe the contents allow: the liquids in the tank, the material in its slot, room for the result.
    private CondenserRecipePrototype? FindRecipe(Entity<CondenserComponent> condenser)
    {
        StackComponent? stack = null;
        if (_itemSlots.GetItemOrNull(condenser.Owner, CondenserComponent.MaterialSlotId) is { } material)
            TryComp(material, out stack);

        foreach (var recipe in _proto.EnumeratePrototypes<CondenserRecipePrototype>())
        {
            // A recipe with a material needs it in the slot, enough of it; one without asks nothing of the slot.
            if (recipe.Material != null && (stack == null || recipe.Material != stack.StackTypeId || stack.Count < recipe.MaterialAmount))
                continue;

            if (_fluids.GetAmount(condenser.Owner, recipe.Liquid) < recipe.LiquidAmount ||
                _fluids.GetAmount(condenser.Owner, recipe.Coolant) < recipe.CoolantAmount ||
                !CanTakeOutput(condenser, recipe))
            {
                continue;
            }

            return recipe;
        }

        return null;
    }

    // The output slot is empty, or holds the same pieces and has room for more of them.
    private bool CanTakeOutput(Entity<CondenserComponent> condenser, CondenserRecipePrototype recipe)
    {
        if (_itemSlots.GetItemOrNull(condenser.Owner, CondenserComponent.OutputSlotId) is not { } held)
            return true;

        return TryComp<StackComponent>(held, out var stack) && stack.StackTypeId == recipe.Result &&
               _stack.GetAvailableSpace(stack) >= recipe.ResultAmount;
    }

    private void Complete(Entity<CondenserComponent> condenser, CondenserRecipePrototype recipe)
    {
        var comp = condenser.Comp;
        comp.Progress = 0f;
        comp.ActiveRecipe = null;
        comp.Active = false;

        _fluids.Remove(condenser.Owner, recipe.Liquid, recipe.LiquidAmount);
        _fluids.Remove(condenser.Owner, recipe.Coolant, recipe.CoolantAmount);

        if (recipe.Material != null && _itemSlots.GetItemOrNull(condenser.Owner, CondenserComponent.MaterialSlotId) is { } material &&
            TryComp<StackComponent>(material, out var materialStack))
        {
            if (materialStack.Count <= recipe.MaterialAmount)
                PredictedQueueDel(material);
            else
                _stack.SetCount((material, materialStack), materialStack.Count - recipe.MaterialAmount);
        }

        if (_proto.TryIndex(recipe.Result, out var resultProto) && !string.IsNullOrEmpty(resultProto.Spawn.Id))
        {
            var made = Spawn(resultProto.Spawn, Transform(condenser).Coordinates);
            if (TryComp<StackComponent>(made, out var madeStack))
                _stack.SetCount((made, madeStack), recipe.ResultAmount);

            if (_itemSlots.GetItemOrNull(condenser.Owner, CondenserComponent.OutputSlotId) is { } held &&
                TryComp<StackComponent>(held, out var heldStack))
            {
                _stack.TryMergeStacks(made, (held, heldStack), out _);
                if (!TerminatingOrDeleted(made))
                    Del(made);
            }
            else
            {
                _itemSlots.TryInsert(condenser.Owner, CondenserComponent.OutputSlotId, made, null);
            }
        }

        Dirty(condenser);
    }

    private void OnExamined(Entity<CondenserComponent> condenser, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(condenser.Comp.NeedsPower ? "au14-condenser-examine-electric" : "au14-condenser-examine-brass",
            ("percent", (int) (condenser.Comp.Progress * 100))));
    }
}
