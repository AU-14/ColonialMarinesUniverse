// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Globalization;
using System.Numerics;
using Content.Client._AU14.UI;
using Content.Shared._AU14.Construction.CustomConstruction;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// The one shared cost/placement form for the Mass Entity Editor's TILES mode: pick the top-bar page
/// (Z-Level or a spawnlist), category, material and amount, and the server applies it to every selected
/// tile as its own independent recipe. Fires <see cref="OnSubmit"/> (tile ids are filled in by the caller).
/// </summary>
public sealed class MassTileConfigWindow : DefaultWindow
{
    // Friendly label -> material stack id (same set as the single Tiles Editor).
    private static readonly (string Label, string Stack)[] Materials =
    {
        ("Metal", "CMSteel"),
        ("Plasteel", "CMPlasteel"),
        ("Glass", "CMGlass"),
        ("Wood", "RMCWood"),
        ("Plastic", "RMCPlastic"),
    };

    public event Action<SubmitMassTileEditorEvent>? OnSubmit;

    private readonly OptionButton _mainCategory;
    private readonly LineEdit _spawnlist;
    private readonly LineEdit _category;
    private readonly OptionButton _material;
    private readonly LineEdit _amount;

    public MassTileConfigWindow(int tileCount)
    {
        Title = Loc.GetString("construction-mass-tiles-title", ("count", tileCount));
        MinSize = new Vector2(380, 340);

        _mainCategory = new OptionButton { HorizontalExpand = true };
        _mainCategory.AddItem(Loc.GetString("construction-tile-editor-page-zlevel"), 0);
        _mainCategory.AddItem(Loc.GetString("construction-tile-editor-page-spawnlists"), 1);
        _mainCategory.SelectId(0);

        _spawnlist = new LineEdit { HorizontalExpand = true, Text = "AU14", Editable = false };
        _mainCategory.OnItemSelected += a =>
        {
            _mainCategory.SelectId(a.Id);
            _spawnlist.Editable = a.Id == 1;
        };

        _category = new LineEdit { HorizontalExpand = true, Text = "Flooring" };

        _material = new OptionButton { HorizontalExpand = true };
        for (var i = 0; i < Materials.Length; i++)
            _material.AddItem(Materials[i].Label, i);
        _material.SelectId(0);
        _material.OnItemSelected += a => _material.SelectId(a.Id);

        _amount = new LineEdit { Text = "1", HorizontalExpand = true };

        var save = new Button { Text = Loc.GetString("construction-tile-editor-save"), HorizontalExpand = true, Margin = new Thickness(0, 0, 2, 0) };
        var cancel = new Button { Text = Loc.GetString("construction-tile-editor-cancel"), HorizontalExpand = true };
        GmodStyle.Modernize(save);
        GmodStyle.Modernize(cancel);
        save.OnPressed += _ => Submit();
        cancel.OnPressed += _ => Close();

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        root.AddChild(LabelFor("construction-tile-editor-main-category"));
        root.AddChild(_mainCategory);
        root.AddChild(LabelFor("construction-tile-editor-spawnlist"));
        root.AddChild(_spawnlist);
        root.AddChild(LabelFor("construction-tile-editor-category"));
        root.AddChild(_category);
        root.AddChild(LabelFor("construction-tile-editor-material"));
        root.AddChild(_material);
        root.AddChild(LabelFor("construction-tile-editor-amount"));
        root.AddChild(_amount);
        root.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { save, cancel },
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

    private static Label LabelFor(string loc) => new() { Text = Loc.GetString(loc), Margin = new Thickness(0, 6, 0, 2) };

    private void Submit()
    {
        if (!int.TryParse(_amount.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount < 1)
            amount = 1;

        var zLevelPage = _mainCategory.SelectedId == 0;
        OnSubmit?.Invoke(new SubmitMassTileEditorEvent
        {
            Material = Materials[Math.Clamp(_material.SelectedId, 0, Materials.Length - 1)].Stack,
            Amount = amount,
            Category = string.IsNullOrWhiteSpace(_category.Text) ? "Flooring" : _category.Text.Trim(),
            Spawnlist = string.IsNullOrWhiteSpace(_spawnlist.Text) ? "AU14" : _spawnlist.Text.Trim(),
            ZLevelPage = zLevelPage,
        });

        Close();
    }
}
