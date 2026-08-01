// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Numerics;
using Content.Client._AU14.UI;
using Content.Shared._AU14.Construction.CustomConstruction;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// Human-in-the-loop save confirmation: before a Save is committed, the server sends back the exact list
/// of file writes and database rows the save would produce (from the same validation pass the real save
/// runs), and this window shows it as a scrollable list with one final Confirm. Cancel drops the save
/// entirely - nothing has been written at that point.
/// </summary>
public sealed class DbSavePreviewWindow : DefaultWindow
{
    /// <summary>Fired once if the admin confirms; the owning system re-sends the stashed submit for real.</summary>
    public event Action? OnConfirm;

    private readonly Label _summary;
    private readonly ItemList _lines;
    private readonly Button _confirm;

    public DbSavePreviewWindow()
    {
        Title = Loc.GetString("construction-db-preview-title");
        MinSize = new Vector2(560, 420);

        _summary = new Label { Text = string.Empty, Margin = new Thickness(0, 0, 0, 4) };
        _lines = new ItemList { VerticalExpand = true, SelectMode = ItemList.ItemListSelectMode.None };

        _confirm = new Button { Text = Loc.GetString("construction-db-preview-confirm"), HorizontalExpand = true, Margin = new Thickness(0, 0, 2, 0) };
        var cancel = new Button { Text = Loc.GetString("construction-db-preview-cancel"), HorizontalExpand = true };
        GmodStyle.Modernize(_confirm);
        GmodStyle.Modernize(cancel);
        _confirm.OnPressed += _ =>
        {
            OnConfirm?.Invoke();
            OnConfirm = null;
            Close();
        };
        cancel.OnPressed += _ => Close();

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        root.AddChild(_summary);
        root.AddChild(_lines);
        root.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { _confirm, cancel },
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

    public void Populate(OpenDbSavePreviewEvent ev)
    {
        _summary.Text = Loc.GetString("construction-db-preview-summary",
            ("kind", Loc.GetString(ev.Kind)), ("planned", ev.Planned), ("rejected", ev.Rejected));

        _lines.Clear();
        foreach (var line in ev.Lines)
            _lines.AddItem(line, selectable: false);

        // Nothing would be written: only Cancel makes sense.
        _confirm.Disabled = ev.Planned == 0;
    }
}
