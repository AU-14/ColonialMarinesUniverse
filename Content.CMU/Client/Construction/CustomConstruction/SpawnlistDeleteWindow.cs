// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._AU14.UI;
using Content.Shared._AU14.Construction.CustomConstruction;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// The Spawnlist Delete tool window (Admin Tools > Delete Spawnlist): pick a spawnlist from a dropdown
/// (never typed by hand - see the editor UI QoL rules) showing how many generated recipes it holds, then
/// confirm through an armed two-step delete with a delay, since this wipes every recipe in the spawnlist.
/// </summary>
public sealed class SpawnlistDeleteWindow : DefaultWindow
{
    // 🔧 TUNABLE: seconds the confirm button stays locked after arming, so a double-click can't delete.
    private const float ConfirmDelaySeconds = 3f;

    public event Action<string>? OnDeleteSpawnlist;

    private readonly OptionButton _spawnlistDropdown;
    private readonly List<string> _spawnlistValues = new();
    private readonly Label _warningLabel;
    private readonly Button _deleteButton;
    private readonly Button _confirmButton;

    public SpawnlistDeleteWindow()
    {
        Title = Loc.GetString("construction-spawnlist-delete-title");
        MinSize = new Vector2(380, 220);

        _spawnlistDropdown = new OptionButton { HorizontalExpand = true };
        _spawnlistDropdown.OnItemSelected += a =>
        {
            _spawnlistDropdown.SelectId(a.Id);
            ResetArming();
        };

        _warningLabel = new Label { Text = string.Empty, Margin = new Thickness(0, 6, 0, 2) };

        _deleteButton = new Button { Text = Loc.GetString("construction-spawnlist-delete-arm"), HorizontalExpand = true, Margin = new Thickness(0, 0, 2, 0) };
        _confirmButton = new Button { Text = Loc.GetString("construction-spawnlist-delete-confirm"), HorizontalExpand = true, Visible = false, Disabled = true };
        GmodStyle.Modernize(_spawnlistDropdown);
        GmodStyle.Modernize(_deleteButton);
        GmodStyle.Modernize(_confirmButton);
        _deleteButton.OnPressed += _ => Arm();
        _confirmButton.OnPressed += _ => Confirm();

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        root.AddChild(new Label { Text = Loc.GetString("construction-spawnlist-delete-pick"), Margin = new Thickness(0, 0, 0, 2) });
        root.AddChild(_spawnlistDropdown);
        root.AddChild(_warningLabel);
        root.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { _deleteButton, _confirmButton },
        });

        var panel = new PanelContainer
        {
            PanelOverride = GmodStyle.Panel(GmodStyle.PanelBg),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        panel.AddChild(root);
        Contents.AddChild(panel);
    }

    public void Populate(OpenSpawnlistDeleteEvent ev)
    {
        _spawnlistDropdown.Clear();
        _spawnlistValues.Clear();

        foreach (var (spawnlist, count) in ev.SpawnlistCounts.OrderBy(kv => kv.Key, StringComparer.InvariantCulture))
        {
            _spawnlistDropdown.AddItem(
                Loc.GetString("construction-spawnlist-delete-option", ("spawnlist", spawnlist), ("count", count)),
                _spawnlistValues.Count);
            _spawnlistValues.Add(spawnlist);
        }

        if (_spawnlistValues.Count == 0)
        {
            _spawnlistDropdown.AddItem(Loc.GetString("construction-spawnlist-delete-none"), 0);
            _spawnlistDropdown.Disabled = true;
            _deleteButton.Disabled = true;
        }

        _spawnlistDropdown.SelectId(0);
        ResetArming();
    }

    private string? Selected =>
        _spawnlistValues.Count > _spawnlistDropdown.SelectedId ? _spawnlistValues[_spawnlistDropdown.SelectedId] : null;

    private void ResetArming()
    {
        _deleteButton.Disabled = _spawnlistValues.Count == 0;
        _confirmButton.Visible = false;
        _confirmButton.Disabled = true;
        _warningLabel.Text = string.Empty;
    }

    private void Arm()
    {
        if (Selected is not { } spawnlist)
            return;

        _deleteButton.Disabled = true;
        _confirmButton.Visible = true;
        _confirmButton.Disabled = true;
        _warningLabel.Text = Loc.GetString("construction-spawnlist-delete-warning", ("spawnlist", spawnlist));

        var armed = spawnlist;
        Timer.Spawn(TimeSpan.FromSeconds(ConfirmDelaySeconds), () =>
        {
            // The admin may have switched spawnlists (which resets arming) or closed the window meanwhile.
            if (Disposed || !_confirmButton.Visible || Selected != armed)
                return;

            _confirmButton.Disabled = false;
            _warningLabel.Text = Loc.GetString("construction-spawnlist-delete-ready", ("spawnlist", armed));
        });
    }

    private void Confirm()
    {
        if (Selected is not { } spawnlist)
            return;

        OnDeleteSpawnlist?.Invoke(spawnlist);
        Close();
    }
}
