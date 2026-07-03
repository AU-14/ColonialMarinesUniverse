// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._AU14.SavedBuilds;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Network;
using Robust.Shared.Maths;

namespace Content.Client._AU14.SavedBuilds;

/// <summary>
/// Build-partner management window (opened from the construction menu's "Partners" button). Lists the other
/// online players; for each you can grant or revoke permission for THEM to include YOUR built entities in
/// their saved builds. Fires <see cref="OnSetPartner"/>; the owning client system relays it to the server.
/// </summary>
public sealed class BuildPartnerWindow : DefaultWindow
{
    /// <summary>Raised when the local player toggles a partner: (the other player, true = grant / false = revoke).</summary>
    public event Action<NetUserId, bool>? OnSetPartner;

    /// <summary>Raised when the local player clicks "Clear all" to revoke every partner at once.</summary>
    public event Action? OnClearAll;

    private readonly BoxContainer _list;

    public BuildPartnerWindow()
    {
        Title = Loc.GetString("build-partner-window-title");
        MinSize = new Vector2(340, 300);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };

        root.AddChild(new Label
        {
            Text = Loc.GetString("build-partner-window-desc"),
            Margin = new Thickness(0, 0, 0, 6),
        });

        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true };
        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        scroll.AddChild(_list);
        root.AddChild(scroll);

        var clearAll = new Button
        {
            Text = Loc.GetString("build-partner-window-clear-all"),
            Margin = new Thickness(0, 6, 0, 0),
        };
        clearAll.OnPressed += _ => OnClearAll?.Invoke();
        root.AddChild(clearAll);

        Contents.AddChild(root);
    }

    /// <summary>Rebuilds the player rows from a fresh server list.</summary>
    public void Populate(List<BuildPartnerInfo> players)
    {
        _list.RemoveAllChildren();

        if (players.Count == 0)
        {
            _list.AddChild(new Label { Text = Loc.GetString("build-partner-window-empty"), Margin = new Thickness(4) });
            return;
        }

        // Current partners first (so you can see who can save your builds at a glance), then by name.
        players.Sort((a, b) =>
            a.IsPartner != b.IsPartner
                ? (a.IsPartner ? -1 : 1)
                : string.Compare(a.Name, b.Name, StringComparison.InvariantCultureIgnoreCase));

        foreach (var player in players)
        {
            var info = player; // capture for the lambda
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 2),
            };

            row.AddChild(new Label
            {
                Text = info.Name,
                HorizontalExpand = true,
                VerticalAlignment = Control.VAlignment.Center,
            });

            var button = new Button
            {
                Text = Loc.GetString(info.IsPartner
                    ? "build-partner-window-remove"
                    : "build-partner-window-add"),
                MinWidth = 90,
            };
            button.OnPressed += _ => OnSetPartner?.Invoke(info.User, !info.IsPartner);
            row.AddChild(button);

            _list.AddChild(row);
        }
    }
}
