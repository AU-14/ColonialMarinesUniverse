// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Grinding;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;

namespace Content.Server.CMU14.Grinding;

// The rules of every machine with a WindowMachine component (see there), kept in one place so that every machine made here
// follows them and none has to repeat them.
public sealed class MachineSlotStackingSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Before the item slots, so that a stack that is added is not also taken out or put in the usual way.
        SubscribeLocalEvent<WindowMachineComponent, ItemSlotButtonPressedEvent>(OnSlotButton, before: new[] { typeof(ItemSlotsSystem) });

        // After them, so that the verbs they add are taken off again.
        SubscribeLocalEvent<WindowMachineComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs, after: new[] { typeof(ItemSlotsSystem) });

        SubscribeLocalEvent<WindowMachineComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
    }

    private void OnSlotButton(Entity<WindowMachineComponent> machine, ref ItemSlotButtonPressedEvent args)
    {
        if (!TryStackHeld(machine.Owner, args.Actor, args.SlotId, machine.Comp.StackSlots))
            return;

        args.TryEject = false;
        args.TryInsert = false;
    }

    private void OnGetVerbs(Entity<WindowMachineComponent> machine, ref GetVerbsEvent<AlternativeVerb> args)
    {
        HideSlotVerbs(ref args);
    }

    private void OnInsertAttempt(Entity<WindowMachineComponent> machine, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.User != null && args.Slot.ID is { } slotId && machine.Comp.OutputSlots.Contains(slotId))
            args.Cancelled = true;
    }

    /// <summary>
    /// A slot button was pressed with a stack in the hand. When the slot holds the same kind of stack, or when no slot of
    /// the list is free any more and another slot holds it, the stack in the hand is added to that one.
    /// Returns true when that was done, so that the slot is neither emptied nor filled the usual way.
    /// </summary>
    public bool TryStackHeld(EntityUid machine, EntityUid user, string pressedSlot, IReadOnlyList<string> stackSlots)
    {
        if (!_hands.TryGetActiveItem(user, out var held) || !TryComp<StackComponent>(held, out var heldStack))
            return false;

        if (_itemSlots.GetItemOrNull(machine, pressedSlot) is { } pressed && TryMerge(machine, user, (held.Value, heldStack), pressed))
            return true;

        // Free slots are used first; stacking starts when they run out.
        foreach (var slotId in stackSlots)
        {
            if (_itemSlots.GetItemOrNull(machine, slotId) == null)
                return false;
        }

        foreach (var slotId in stackSlots)
        {
            if (_itemSlots.GetItemOrNull(machine, slotId) is { } other && TryMerge(machine, user, (held.Value, heldStack), other))
                return true;
        }

        return false;
    }

    private bool TryMerge(EntityUid machine, EntityUid user, Entity<StackComponent> held, EntityUid target)
    {
        if (!TryComp<StackComponent>(target, out var targetStack) || targetStack.StackTypeId != held.Comp.StackTypeId)
            return false;

        if (!_stack.TryMergeStacks((held.Owner, held.Comp), (target, targetStack), out var moved))
            return false;

        _popup.PopupEntity(Loc.GetString("au14-machine-stacked", ("count", moved)), machine, user);
        return true;
    }

    /// <summary>Takes the verbs that name what is inside (the slots' insert and eject verbs) off the machine.</summary>
    public static void HideSlotVerbs(ref GetVerbsEvent<AlternativeVerb> args)
    {
        args.Verbs.RemoveWhere(verb => verb.Category == VerbCategory.Insert || verb.Category == VerbCategory.Eject);
    }
}
