// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Fluids;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Fluids;

// Client side of the fluid ports window: opens it and gives it the state of the ports.
public sealed class FluidPortsBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private FluidPortsWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FluidPortsWindow>();
        _window.OnPortChosen += port => SendMessage(new FluidPortChosenMessage(port));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FluidPortsBoundUserInterfaceState ports)
            _window?.Update(ports);
    }
}
