// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.CMU14.Fluids;
using Content.Server.Power.EntitySystems;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Grinding;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Grinding;

// Grinders: take what is in the slots and grind it by recipe, with power or steam, making items or fluids.
public sealed class GrinderSystem : EntitySystem
{
    [Dependency] private readonly FluidSystem _fluids = default!;
    [Dependency] private readonly SteamGasSystem _steam = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerStateSystem _powerState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly SoundSpecifier GrindDoneSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");

    // The sound of the vanilla reagent grinder, played with its own volume and rolloff.
    private static readonly SoundSpecifier GrindSound = new SoundPathSpecifier("/Audio/Machines/blender.ogg", AudioParams.Default.WithVolume(-2.5f).WithRolloffFactor(2f));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrinderComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<GrinderComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var grinder = new Entity<GrinderComponent>(uid, comp);

            // A steam grinder runs on steam, which is used up while it grinds; the others run on power.
            var steamMode = comp.SteamPerSecond > 0f;
            var powered = steamMode
                ? _steam.GetMoles(uid) > 0.01f &&
                  (comp.GrindEndsAt == null || _steam.TryUse(uid, comp.SteamPerSecond * frameTime))
                : this.IsPowered(uid, EntityManager);

            if (!powered)
            {
                if (comp.GrindEndsAt != null)
                    PauseBatch(grinder);

                if (!steamMode)
                    _powerState.TrySetWorkingState(uid, false);

                continue;
            }

            if (comp.GrindEndsAt == null)
                TryStartBatch(grinder);

            if (comp.GrindEndsAt is { } endsAt && now >= endsAt)
                CompleteBatch(grinder);

            if (!steamMode)
                _powerState.TrySetWorkingState(uid, comp.GrindEndsAt != null);
        }
    }

    private void TryStartBatch(Entity<GrinderComponent> grinder)
    {
        if (!HasProcessableMaterial(grinder))
            return;

        if (!HasRoomForLoaded(grinder))
            return;

        var duration = grinder.Comp.GrindRemaining ?? grinder.Comp.GrindTime;
        grinder.Comp.GrindRemaining = null;
        grinder.Comp.GrindDuration = grinder.Comp.GrindTime;
        grinder.Comp.GrindEndsAt = _timing.CurTime + duration;
        grinder.Comp.Active = true;
        Dirty(grinder);

        _audio.PlayPvs(GrindSound, grinder);
    }

    private void PauseBatch(Entity<GrinderComponent> grinder)
    {
        var comp = grinder.Comp;
        if (comp.GrindEndsAt is not { } endsAt)
            return;

        var left = endsAt - _timing.CurTime;
        comp.GrindRemaining = left > TimeSpan.Zero ? left : TimeSpan.Zero;
        comp.GrindEndsAt = null;
        Dirty(grinder);
    }

    private void CompleteBatch(Entity<GrinderComponent> grinder)
    {
        var comp = grinder.Comp;
        comp.GrindEndsAt = null;
        comp.GrindDuration = null;
        comp.Active = false;

        var coords = Transform(grinder).Coordinates;
        var processed = 0;

        foreach (var slotId in GrinderComponent.MaterialSlotIds)
        {
            if (_itemSlots.GetItemOrNull(grinder.Owner, slotId) is not { } item ||
                !TryComp<StackComponent>(item, out var stack) ||
                FindRecipe(stack.StackTypeId) is not { } recipe ||
                stack.Count < recipe.InputAmount)
            {
                continue;
            }

            if (!HasRoomFor(grinder, recipe))
                break;

            _stack.SetCount(item, stack.Count - recipe.InputAmount, stack);

            SpawnOutput(grinder, coords, recipe.Result, recipe.ResultAmount);
            foreach (var extra in recipe.Extras)
            {
                SpawnOutput(grinder, coords, extra.Result, extra.Amount);
            }

            processed++;
        }

        if (processed > 0)
        {
            _audio.PlayPvs(GrindDoneSound, grinder);
        }

        Dirty(grinder);
    }

    private void SpawnOutput(Entity<GrinderComponent> grinder, EntityCoordinates coords, ProtoId<StackPrototype> stackId, int amount)
    {
        if (!_proto.TryIndex(stackId, out var stackProto) || string.IsNullOrEmpty(stackProto.Spawn.Id))
            return;

        var result = Spawn(stackProto.Spawn, coords);
        if (TryComp<StackComponent>(result, out var resultStack))
            _stack.SetCount(result, amount, resultStack);

        if (TryInsertOutput(grinder, result))
            return;

        // No slot is free any more, so the new pieces are added to a stack of the same kind in one that is taken.
        if (!TryStackIntoOutput(grinder, result))
            PredictedQueueDel(result);
    }

    // Puts as much of the stack as fits on top of the same kind of stack lying in an output slot.
    private bool TryStackIntoOutput(Entity<GrinderComponent> grinder, EntityUid result)
    {
        if (!TryComp<StackComponent>(result, out var resultStack))
            return false;

        foreach (var slotId in GrinderComponent.OutputSlotIds)
        {
            if (_itemSlots.GetItemOrNull(grinder.Owner, slotId) is not { } held ||
                !TryComp<StackComponent>(held, out var heldStack) ||
                heldStack.StackTypeId != resultStack.StackTypeId)
            {
                continue;
            }

            _stack.TryMergeStacks((result, resultStack), (held, heldStack), out _);
            if (TerminatingOrDeleted(result) || resultStack.Count <= 0)
                return true;
        }

        return false;
    }

    // Whether everything the recipe makes has somewhere to go: a free output slot each, or a stack of the same kind with room.
    private bool HasRoomFor(Entity<GrinderComponent> grinder, GrinderRecipePrototype recipe)
    {
        var free = 0;
        var space = new Dictionary<string, int>();

        foreach (var slotId in GrinderComponent.OutputSlotIds)
        {
            if (_itemSlots.GetItemOrNull(grinder.Owner, slotId) is not { } held)
            {
                free++;
            }
            else if (TryComp<StackComponent>(held, out var stack))
            {
                space[stack.StackTypeId] = space.GetValueOrDefault(stack.StackTypeId) + _stack.GetAvailableSpace(stack);
            }
        }

        var wanted = new List<(string Stack, int Amount)> { (recipe.Result, recipe.ResultAmount) };
        foreach (var extra in recipe.Extras)
        {
            wanted.Add((extra.Result, extra.Amount));
        }

        foreach (var (stackId, amount) in wanted)
        {
            if (free > 0)
            {
                free--;
            }
            else if (space.GetValueOrDefault(stackId) >= amount)
            {
                space[stackId] -= amount;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private bool TryInsertOutput(Entity<GrinderComponent> grinder, EntityUid item)
    {
        foreach (var slotId in GrinderComponent.OutputSlotIds)
        {
            if (_itemSlots.GetItemOrNull(grinder.Owner, slotId) != null)
                continue;

            if (_itemSlots.TryInsert(grinder.Owner, slotId, item, null))
                return true;
        }

        return false;
    }

    // Every recipe among the loaded materials has to have room for its pieces, so that a batch never runs out halfway.
    private bool HasRoomForLoaded(Entity<GrinderComponent> grinder)
    {
        var any = false;
        foreach (var slotId in GrinderComponent.MaterialSlotIds)
        {
            if (_itemSlots.GetItemOrNull(grinder.Owner, slotId) is not { } item || !TryComp<StackComponent>(item, out var stack))
                continue;

            if (FindRecipe(stack.StackTypeId) is not { } recipe || stack.Count < recipe.InputAmount)
                continue;

            any = true;
            if (HasRoomFor(grinder, recipe))
                return true;
        }

        return !any;
    }

    private bool HasProcessableMaterial(Entity<GrinderComponent> grinder)
    {
        foreach (var slotId in GrinderComponent.MaterialSlotIds)
        {
            if (_itemSlots.GetItemOrNull(grinder.Owner, slotId) is not { } item || !TryComp<StackComponent>(item, out var stack))
                continue;

            if (FindRecipe(stack.StackTypeId) is { } recipe && stack.Count >= recipe.InputAmount)
                return true;
        }

        return false;
    }

    private GrinderRecipePrototype? FindRecipe(ProtoId<StackPrototype> input)
    {
        foreach (var recipe in _proto.EnumeratePrototypes<GrinderRecipePrototype>())
        {
            if (recipe.Input == input)
                return recipe;
        }

        return null;
    }

    private void OnExamined(Entity<GrinderComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var comp = ent.Comp;
        using var block = args.PushGroup(nameof(GrinderComponent));

        if (ent.Comp.SteamPerSecond > 0f ? _steam.GetMoles(ent.Owner) <= 0.01f : !this.IsPowered(ent.Owner, EntityManager))
        {
            args.PushMarkup(Loc.GetString("au14-grinder-examine-unpowered"));
        }
        else if (comp.Active && comp.GrindEndsAt is { } endsAt && comp.GrindDuration is { } duration && duration > TimeSpan.Zero)
        {
            var left = endsAt - _timing.CurTime;
            var percent = (int) Math.Clamp((1 - left.TotalSeconds / duration.TotalSeconds) * 100, 0, 100);
            args.PushMarkup(Loc.GetString("au14-grinder-examine-progress", ("percent", percent)));
        }
        else
        {
            args.PushMarkup(Loc.GetString("au14-grinder-examine-idle"));
        }
    }
}
