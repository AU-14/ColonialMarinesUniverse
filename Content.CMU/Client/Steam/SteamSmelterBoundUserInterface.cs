// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Steam;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Steam;

// Client side of the steam smelter: opens its window, sends the pressed slot and the setting of the regulators.
public sealed class SteamSmelterBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private SteamSmelterWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SteamSmelterWindow>();
        _window.SetOwner(Owner);
        _window.OnSlotPressed += OnSlotPressed;
        _window.OnRegulatorChanged += OnRegulatorChanged;
    }

    private void OnSlotPressed(string slotId)
    {
        SendPredictedMessage(new ItemSlotButtonPressedEvent(slotId));
    }

    private void OnRegulatorChanged(bool slag, float value)
    {
        SendMessage(new SteamSmelterRegulatorMessage(slag, value));
    }
}
