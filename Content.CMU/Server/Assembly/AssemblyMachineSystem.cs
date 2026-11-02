// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Assembly;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.PowerCell;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Assembly;

// Assembly machines: takes the parts from the slots, makes the batch of the recipe over time (needs power), and plays a sound when it is done.
public sealed class AssemblyMachineSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly SoundSpecifier AssembleDoneSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AssemblyMachineComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AssemblyMachineComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var machine = new Entity<AssemblyMachineComponent>(uid, comp);

            if (comp.AssembleEndsAt is { } endsAt)
            {
                if (now >= endsAt)
                    CompleteBatch(machine);

                continue;
            }

            TryStartBatch(machine);
        }
    }

    private void TryStartBatch(Entity<AssemblyMachineComponent> machine)
    {
        if (_itemSlots.GetItemOrNull(machine.Owner, AssemblyMachineComponent.OutputSlotId) != null)
            return;

        if (GetActiveRecipe(machine) is not { } recipe)
            return;

        if (!LayoutSatisfied(machine, recipe))
            return;

        var energyCost = recipe.EnergyCost * GetEnergyMultiplier(machine);
        if (!_powerCell.HasCharge(machine.Owner, energyCost))
            return;

        ConsumeLayout(machine, recipe);

        _powerCell.TryUseCharge(machine.Owner, energyCost);

        var duration = TimeSpan.FromSeconds(recipe.AssembleTime.TotalSeconds * GetSpeedMultiplier(machine));
        machine.Comp.AssembleDuration = duration;
        machine.Comp.AssembleEndsAt = _timing.CurTime + duration;
        machine.Comp.PendingRecipe = recipe.ID;
        machine.Comp.Active = true;
        Dirty(machine);
        _appearance.SetData(machine.Owner, AssemblyMachineVisuals.Working, true);
    }

    private void CompleteBatch(Entity<AssemblyMachineComponent> machine)
    {
        var comp = machine.Comp;
        comp.AssembleEndsAt = null;
        comp.AssembleDuration = null;
        comp.Active = false;
        _appearance.SetData(machine.Owner, AssemblyMachineVisuals.Working, false);

        if (comp.PendingRecipe is not { } recipeId || !_proto.TryIndex<AssemblyRecipePrototype>(recipeId, out var recipe))
        {
            comp.PendingRecipe = null;
            Dirty(machine);
            return;
        }

        comp.PendingRecipe = null;

        var coords = Transform(machine).Coordinates;
        var result = Spawn(recipe.Result, coords);
        if (recipe.ResultAmount != 1 && TryComp<StackComponent>(result, out var resultStack))
            _stack.SetCount(result, recipe.ResultAmount, resultStack);

        if (!_itemSlots.TryInsert(machine.Owner, AssemblyMachineComponent.OutputSlotId, result, null))
            PredictedQueueDel(result);

        _audio.PlayPvs(AssembleDoneSound, machine);
        Dirty(machine);
    }

    private AssemblyRecipePrototype? GetActiveRecipe(Entity<AssemblyMachineComponent> machine)
    {
        if (_itemSlots.GetItemOrNull(machine.Owner, AssemblyMachineComponent.BlueprintSlotId) is not { } blueprint ||
            !TryComp<AssemblyBlueprintComponent>(blueprint, out var bp) || bp.ProgrammedRecipe is not { } recipeId)
        {
            return null;
        }

        return _proto.TryIndex<AssemblyRecipePrototype>(recipeId, out var recipe) ? recipe : null;
    }

    private bool LayoutSatisfied(Entity<AssemblyMachineComponent> machine, AssemblyRecipePrototype recipe)
    {
        for (var i = 0; i < AssemblyMachineComponent.MaterialSlotIds.Length; i++)
        {
            var item = _itemSlots.GetItemOrNull(machine.Owner, AssemblyMachineComponent.MaterialSlotIds[i]);

            if (!recipe.Layout.TryGetValue(i, out var requirement))
            {
                if (item != null)
                    return false;

                continue;
            }

            if (item is not { } material || !TryComp<StackComponent>(material, out var stack) ||
                stack.StackTypeId != requirement.Material || stack.Count < requirement.Amount)
            {
                return false;
            }
        }

        return true;
    }

    private void ConsumeLayout(Entity<AssemblyMachineComponent> machine, AssemblyRecipePrototype recipe)
    {
        for (var i = 0; i < AssemblyMachineComponent.MaterialSlotIds.Length; i++)
        {
            if (!recipe.Layout.TryGetValue(i, out var requirement))
                continue;

            if (_itemSlots.GetItemOrNull(machine.Owner, AssemblyMachineComponent.MaterialSlotIds[i]) is not { } item ||
                !TryComp<StackComponent>(item, out var stack))
            {
                continue;
            }

            _stack.SetCount(item, stack.Count - requirement.Amount, stack);
        }
    }

    private float GetSpeedMultiplier(Entity<AssemblyMachineComponent> machine)
    {
        var multiplier = 1f;
        foreach (var slotId in AssemblyMachineComponent.UpgradeSlotIds)
        {
            if (_itemSlots.GetItemOrNull(machine.Owner, slotId) is { } upgrade && TryComp<AssemblyUpgradeComponent>(upgrade, out var comp))
                multiplier *= comp.SpeedMultiplier;
        }

        return multiplier;
    }

    private float GetEnergyMultiplier(Entity<AssemblyMachineComponent> machine)
    {
        var multiplier = 1f;
        foreach (var slotId in AssemblyMachineComponent.UpgradeSlotIds)
        {
            if (_itemSlots.GetItemOrNull(machine.Owner, slotId) is { } upgrade && TryComp<AssemblyUpgradeComponent>(upgrade, out var comp))
                multiplier *= comp.EnergyMultiplier;
        }

        return multiplier;
    }

    private void OnExamined(Entity<AssemblyMachineComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var comp = ent.Comp;
        using var block = args.PushGroup(nameof(AssemblyMachineComponent));

        if (comp.Active && comp.AssembleEndsAt is { } endsAt && comp.AssembleDuration is { } duration && duration > TimeSpan.Zero)
        {
            var left = endsAt - _timing.CurTime;
            var percent = (int) Math.Clamp((1 - left.TotalSeconds / duration.TotalSeconds) * 100, 0, 100);
            args.PushMarkup(Loc.GetString("au14-assembly-examine-progress", ("percent", percent)));
        }
        else if (GetActiveRecipe(ent) == null)
        {
            args.PushMarkup(Loc.GetString("au14-assembly-examine-no-blueprint"));
        }
        else
        {
            args.PushMarkup(Loc.GetString("au14-assembly-examine-idle"));
        }
    }
}
