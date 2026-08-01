// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._AU14.UI;
using Content.Shared._AU14.Administration;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._AU14.Administration;

/// <summary>
/// The Host-only "Tool Permissions" manager: grant editor tools (Construction Item Editor, Tile Editor,
/// Lathe Editor, Z-Level tools, INSFOR Editor) to any ckey, online or not. The top row grants; below it,
/// every ckey with grants is listed - clicking one expands it to show that user's tools with per-tool
/// Remove buttons. Replaces the old jobwhitelistadd JModEditor/InsforEditor gating, which lower admin
/// ranks could reach.
/// </summary>
public sealed class ToolPermissionsWindow : DefaultWindow
{
    public event Action<ModifyToolPermissionEvent>? OnModify;

    private readonly LineEdit _ckeyEdit;
    private readonly OptionButton _toolPicker;
    private readonly BoxContainer _userList;
    private readonly List<string> _toolOptionIds = new();

    // Which ckeys are expanded, preserved across server refreshes.
    private readonly HashSet<string> _expanded = new(StringComparer.OrdinalIgnoreCase);

    private List<ToolPermissionUser> _users = new();

    public ToolPermissionsWindow()
    {
        Title = Loc.GetString("au14-toolperm-title");
        MinSize = new Vector2(560, 520);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true, Margin = new Thickness(8) };

        // Grant row: ckey + tool + Grant.
        _ckeyEdit = new LineEdit { PlaceHolder = Loc.GetString("au14-toolperm-ckey-placeholder"), HorizontalExpand = true };
        _toolPicker = new OptionButton();
        foreach (var (id, nameLoc) in AU14ToolPermissions.AllTools)
        {
            _toolPicker.AddItem(Loc.GetString(nameLoc), _toolOptionIds.Count);
            _toolOptionIds.Add(id);
        }
        _toolPicker.OnItemSelected += a => _toolPicker.Select(a.Id);
        GmodStyle.Modernize(_toolPicker);

        var grantButton = new Button { Text = Loc.GetString("au14-toolperm-grant") };
        GmodStyle.Modernize(grantButton);
        grantButton.OnPressed += _ =>
        {
            var ckey = _ckeyEdit.Text.Trim();
            if (ckey.Length == 0)
                return;

            OnModify?.Invoke(new ModifyToolPermissionEvent
            {
                Ckey = ckey,
                Tool = _toolOptionIds[_toolPicker.SelectedId],
                Grant = true,
            });
            _expanded.Add(ckey);
        };

        root.AddChild(new Label { Text = Loc.GetString("au14-toolperm-grant-header"), StyleClasses = { "LabelKeyText" } });
        root.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Children = { _ckeyEdit, _toolPicker, grantButton },
        });

        // Users with grants.
        root.AddChild(new Label { Text = Loc.GetString("au14-toolperm-users-header"), StyleClasses = { "LabelKeyText" }, Margin = new Thickness(0, 10, 0, 2) });
        _userList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true };
        scroll.AddChild(_userList);
        root.AddChild(scroll);

        var panel = new PanelContainer
        {
            PanelOverride = GmodStyle.Panel(GmodStyle.PanelBg),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        panel.AddChild(root);
        Contents.AddChild(panel);
        GmodStyle.RecolorKeyLabels(panel);
    }

    /// <summary>Called by the client system whenever the server sends fresh grants.</summary>
    public void Populate(OpenToolPermissionsEvent ev)
    {
        _users = ev.Users;
        RebuildUserList();
    }

    private void RebuildUserList()
    {
        _userList.RemoveAllChildren();

        if (_users.Count == 0)
        {
            _userList.AddChild(new Label { Text = Loc.GetString("au14-toolperm-none") });
            return;
        }

        foreach (var user in _users)
        {
            var ckey = user.Ckey;

            // Clicking the ckey expands/collapses its permissions.
            var header = new Button
            {
                Text = $"{ckey} ({user.Tools.Count})",
                HorizontalExpand = true,
                ToggleMode = true,
                Pressed = _expanded.Contains(ckey),
            };
            GmodStyle.Modernize(header);

            var detail = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                Visible = _expanded.Contains(ckey),
                Margin = new Thickness(16, 2, 0, 6),
            };

            foreach (var tool in user.Tools)
            {
                var nameLoc = AU14ToolPermissions.AllTools.FirstOrDefault(t => t.Id == tool).NameLoc;
                var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
                row.AddChild(new Label
                {
                    Text = nameLoc != null ? Loc.GetString(nameLoc) : tool,
                    HorizontalExpand = true,
                    VerticalAlignment = VAlignment.Center,
                });

                var remove = new Button { Text = Loc.GetString("au14-toolperm-remove") };
                GmodStyle.Modernize(remove);
                remove.OnPressed += _ => OnModify?.Invoke(new ModifyToolPermissionEvent
                {
                    Ckey = ckey,
                    Tool = tool,
                    Grant = false,
                });
                row.AddChild(remove);
                detail.AddChild(row);
            }

            header.OnToggled += args =>
            {
                detail.Visible = args.Pressed;
                if (args.Pressed) _expanded.Add(ckey);
                else _expanded.Remove(ckey);
            };

            _userList.AddChild(header);
            _userList.AddChild(detail);
        }
    }
}
