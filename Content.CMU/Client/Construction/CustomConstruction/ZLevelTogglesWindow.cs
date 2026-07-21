// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._AU14.ZLevelBuilding;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// The "Z-Level Toggles" admin tool (construction menu > Tools): one row per game-map prototype with a Yes/No
/// toggle for whether z-level building (stairs, support beams, digging) is allowed there. Choices persist on
/// the server across rounds; flipping a toggle for a map loaded THIS round applies immediately.
/// </summary>
public sealed class ZLevelTogglesWindow : DefaultWindow
{
    /// <summary>Fired when the admin flips a map's toggle: (game-map prototype id, allowed).</summary>
    public event Action<string, bool>? OnToggle;

    private readonly LineEdit _search;
    private readonly BoxContainer _rows;
    private List<ZLevelToggleEntry> _maps = new();

    public ZLevelTogglesWindow()
    {
        Title = Loc.GetString("au-zlevel-toggles-title");
        MinSize = new Vector2(460, 520);

        _search = new LineEdit { PlaceHolder = Loc.GetString("au-zlevel-toggles-search"), HorizontalExpand = true, Margin = new Thickness(0, 0, 0, 4) };
        _search.OnTextChanged += _ => Rebuild();

        _rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_rows);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true, Margin = new Thickness(4) };
        root.AddChild(new Label { Text = Loc.GetString("au-zlevel-toggles-hint"), Margin = new Thickness(0, 0, 0, 4) });
        root.AddChild(_search);
        root.AddChild(scroll);
        Contents.AddChild(root);
    }

    public void Populate(OpenZLevelTogglesEvent ev)
    {
        _maps = ev.Maps;
        Rebuild();
    }

    private void Rebuild()
    {
        _rows.RemoveAllChildren();
        var filter = _search.Text.Trim();

        foreach (var entry in _maps)
        {
            if (filter.Length > 0
                && !entry.MapName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                && !entry.MapProtoId.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, Margin = new Thickness(0, 0, 0, 2) };

            var nameText = entry.Loaded
                ? Loc.GetString("au-zlevel-toggles-map-loaded", ("map", entry.MapName))
                : entry.MapName;
            row.AddChild(new Label { Text = nameText, HorizontalExpand = true, ClipText = true });

            var toggle = new Button
            {
                ToggleMode = true,
                Pressed = entry.Enabled,
                MinWidth = 60,
                Text = Loc.GetString(entry.Enabled ? "au-zlevel-toggles-yes" : "au-zlevel-toggles-no"),
                ToolTip = entry.MapProtoId,
            };

            var captured = entry;
            toggle.OnToggled += args =>
            {
                captured.Enabled = args.Pressed;
                toggle.Text = Loc.GetString(args.Pressed ? "au-zlevel-toggles-yes" : "au-zlevel-toggles-no");
                OnToggle?.Invoke(captured.MapProtoId, args.Pressed);
            };

            row.AddChild(toggle);
            _rows.AddChild(row);
        }
    }
}
