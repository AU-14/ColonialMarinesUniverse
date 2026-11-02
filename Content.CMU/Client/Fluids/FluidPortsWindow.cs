// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Numerics;
using Content.Client.CMU14.Machines;
using Content.Shared.CMU14.Fluids;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Fluids;

// Asks which port of a machine the pipe in hand is to be joined to, as the configurator does with the links of devices:
// every port of the machine is a button, and one that the pipe is joined to already lets it go.
public sealed class FluidPortsWindow : DefaultWindow
{
    private readonly BoxContainer _list = MachineWindowKit.Column(4);

    public event Action<string>? OnPortChosen;

    public FluidPortsWindow()
    {
        Title = Loc.GetString("au14-fluid-ports-title");
        MinSize = new Vector2(280, 120);
        Contents.AddChild(_list);
    }

    public void Update(FluidPortsBoundUserInterfaceState state)
    {
        _list.RemoveAllChildren();
        _list.AddChild(new Label { Text = Loc.GetString("au14-fluid-ports-hint"), FontColorOverride = MachineWindowKit.SectionTitle });

        foreach (var entry in state.Ports)
        {
            var name = Loc.GetString($"au14-fluid-port-{entry.Id}");
            var way = Loc.GetString(entry.Output ? "au14-fluid-ports-out" : "au14-fluid-ports-in");
            var text = entry.Kind.Length > 0 ? $"{name} ({way}, {entry.Kind})" : $"{name} ({way})";

            if (entry.Joined)
                text += " - " + Loc.GetString("au14-fluid-ports-joined");
            else if (entry.Links > 0)
                text += " - " + Loc.GetString("au14-fluid-ports-taken");

            var button = new Button { Text = text, HorizontalExpand = true };
            var id = entry.Id;
            button.OnPressed += _ => OnPortChosen?.Invoke(id);
            _list.AddChild(button);
        }
    }
}
