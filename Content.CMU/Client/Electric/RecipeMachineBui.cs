// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Electric;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Electric;

public sealed class RecipeMachineBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private RecipeMachineWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<RecipeMachineWindow>();
        _window.SetOwner(Owner);
        _window.OnSlotPressed += slotId => SendPredictedMessage(new ItemSlotButtonPressedEvent(slotId));
        _window.OnCircuitChosen += circuit => SendMessage(new RecipeMachineCircuitMessage(circuit));
        _window.OnAutoChosen += (output, value) => SendMessage(new RecipeMachineAutoMessage(output, value));
        _window.OnFilterChosen += (output, id, value) => SendMessage(new RecipeMachineFilterMessage(output, id, value));
    }
}
