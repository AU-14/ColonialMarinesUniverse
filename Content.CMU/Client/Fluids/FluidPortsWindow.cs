// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Numerics;
using Content.Client.CMU14.Machines;
using Content.Shared.CMU14.Fluids;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Fluids;

// Shows machine flow directions and individual links independently of each viewer's selected pipe.
public sealed class FluidPortsWindow : DefaultWindow
{
    private readonly BoxContainer _list = MachineWindowKit.Column(4);

    public event Action<string>? OnPortChosen;
    public event Action<string, NetEntity>? OnDisconnect;

    public FluidPortsWindow()
    {
        Title = Loc.GetString("au14-fluid-ports-title");
        MinSize = new Vector2(460, 260);
        Contents.AddChild(new ScrollContainer { Children = { _list } });
    }

    public void Update(FluidPortsBoundUserInterfaceState state)
    {
        _list.RemoveAllChildren();
        var hint = new RichTextLabel();
        hint.SetMessage(Loc.GetString("au14-fluid-ports-hint"));
        _list.AddChild(hint);

        foreach (var entry in state.Ports)
        {
            var name = Loc.GetString($"au14-fluid-port-{entry.Id}");
            var way = Loc.GetString(entry.Output ? "au14-fluid-ports-out" : "au14-fluid-ports-in");
            _list.AddChild(new Label
            {
                Text = Loc.GetString("cmu-fluid-port-heading", ("port", name), ("direction", way), ("fluid", entry.Kind)),
                FontColorOverride = entry.Output ? MachineWindowKit.OutputBorder : MachineWindowKit.InputBorder,
            });
            var id = entry.Id;
            foreach (var link in entry.Links)
            {
                var row = MachineWindowKit.Row(4);
                row.AddChild(new Label
                {
                    Text = Loc.GetString("cmu-fluid-port-link", ("pipe", link.Name), ("position", link.Position)),
                    HorizontalExpand = true,
                });
                var disconnect = new Button { Text = Loc.GetString("cmu-fluid-disconnect") };
                disconnect.OnPressed += _ => OnDisconnect?.Invoke(id, link.Pipe);
                row.AddChild(disconnect);
                _list.AddChild(row);
            }
            if (entry.Links.Count == 0)
                _list.AddChild(new Label { Text = Loc.GetString("cmu-fluid-port-unconnected") });
            var button = new Button
            {
                Text = Loc.GetString("cmu-fluid-connect-selected"),
                Disabled = entry.Links.Count >= entry.MaxLinks,
                HorizontalExpand = true,
            };
            button.OnPressed += _ => OnPortChosen?.Invoke(id);
            _list.AddChild(button);
        }
    }
}
