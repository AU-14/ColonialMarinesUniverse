// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Smelting;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Smelting;

// Client side of the electric smelter: opens its window, sends the pressed slot and the temperature the player sets.
public sealed class ElectricSmelterBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private ElectricSmelterWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ElectricSmelterWindow>();
        _window.SetOwner(Owner);
        _window.OnSlotPressed += OnSlotPressed;
        _window.OnTemperatureChanged += OnTemperatureChanged;
    }

    private void OnSlotPressed(string slotId)
    {
        SendPredictedMessage(new ItemSlotButtonPressedEvent(slotId));
    }

    private void OnTemperatureChanged(float temperature)
    {
        SendMessage(new ElectricSmelterSetTemperatureMessage(temperature));
    }
}
