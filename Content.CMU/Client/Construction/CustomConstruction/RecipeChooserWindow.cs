// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Numerics;
using Content.Client._AU14.UI;
using Content.Shared._AU14.Construction.CustomConstruction;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// Shown when an admin opens the Construction Items Editor for an entity that ALREADY has recipe entries:
/// lists each existing recipe with Change / Remove buttons, plus an "Add new recipe" button. Picking Change or
/// Add opens the full editor; Remove deletes that entry. Fires the corresponding events.
/// </summary>
public sealed class RecipeChooserWindow : DefaultWindow
{
    public event Action<string>? OnChange;
    public event Action<string>? OnRemove;
    public event Action? OnAddNew;

    private readonly BoxContainer _rows;

    public RecipeChooserWindow()
    {
        Title = Loc.GetString("construction-chooser-title");
        MinSize = new Vector2(420, 320);

        _rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_rows);

        var addNew = new Button { Text = Loc.GetString("construction-chooser-add-new"), HorizontalExpand = true, Margin = new Thickness(0, 8, 0, 0) };
        addNew.OnPressed += _ => { OnAddNew?.Invoke(); Close(); };

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        root.AddChild(scroll);
        root.AddChild(addNew);

        var panel = new PanelContainer { PanelOverride = GmodStyle.Panel(GmodStyle.PanelBg), HorizontalExpand = true, VerticalExpand = true };
        panel.AddChild(root);
        Contents.AddChild(panel);

        GmodStyle.Modernize(addNew, baseBg: GmodStyle.RowSelected, selectedBg: GmodStyle.RowSelected);
    }

    public void Populate(OpenCustomConstructionChooserEvent ev)
    {
        _rows.RemoveAllChildren();
        foreach (var entry in ev.Entries)
        {
            var key = entry.EntryKey;

            var change = new Button { Text = Loc.GetString("construction-chooser-change"), Margin = new Thickness(0, 0, 2, 0) };
            change.OnPressed += _ => { OnChange?.Invoke(key); Close(); };
            GmodStyle.Modernize(change);

            var remove = new Button { Text = Loc.GetString("construction-chooser-remove") };
            GmodStyle.Modernize(remove);
            remove.OnPressed += _ =>
            {
                OnRemove?.Invoke(key);
                remove.Disabled = true;
                change.Disabled = true;
            };

            _rows.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 3),
                Children =
                {
                    new Label
                    {
                        Text = Loc.GetString("construction-chooser-entry", ("spawnlist", entry.Spawnlist), ("category", entry.Category)),
                        HorizontalExpand = true,
                        VerticalAlignment = VAlignment.Center,
                    },
                    change,
                    remove,
                },
            });
        }
    }
}
