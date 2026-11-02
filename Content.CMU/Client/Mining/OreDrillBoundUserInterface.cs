// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Mining;

// Client side of the ore drill: opens its window and sends the server the slot that was pressed.
public sealed class OreDrillBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private OreDrillWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<OreDrillWindow>();
        _window.SetOwner(Owner);
        _window.OnSlotPressed += OnSlotPressed;
    }

    private void OnSlotPressed(string slotId)
    {
        SendPredictedMessage(new ItemSlotButtonPressedEvent(slotId));
    }
}
