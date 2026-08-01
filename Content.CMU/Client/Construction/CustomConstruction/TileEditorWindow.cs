// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Content.Client._AU14.UI;
using Content.Shared._AU14.Construction.CustomConstruction;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// The "Tiles Editor" (Admin Tools): pick a tile from a searchable list, choose which MAIN CATEGORY (the
/// Z-Level page or the normal Spawnlists page) it lands on, set its material cost, and save. The server turns
/// the chosen tile into a buildable tile-applier entity + recipe. Fires <see cref="OnSubmit"/>.
/// </summary>
public sealed class TileEditorWindow : DefaultWindow
{
    // Friendly label -> material stack id. The CM material stacks the lathe/construction sheets provide.
    private static readonly (string Label, string Stack)[] Materials =
    {
        ("Metal", "CMSteel"),
        ("Plasteel", "CMPlasteel"),
        ("Glass", "CMGlass"),
        ("Wood", "RMCWood"),
        ("Plastic", "RMCPlastic"),
    };

    public event Action<SubmitCustomTileEditorEvent>? OnSubmit;

    private readonly LineEdit _search;
    private readonly BoxContainer _rows;
    private readonly Label _selectedLabel;
    private readonly OptionButton _mainCategory;
    private readonly OptionButton _spawnlist;
    private readonly LineEdit _category;
    private readonly OptionButton _material;
    private readonly LineEdit _amount;

    private readonly List<string> _spawnlistValues = new();
    private List<string> _tiles = new();
    private string _selectedTile = string.Empty;

    private readonly IPrototypeManager _prototype;
    private readonly IResourceCache _resCache;

    public TileEditorWindow()
    {
        _prototype = IoCManager.Resolve<IPrototypeManager>();
        _resCache = IoCManager.Resolve<IResourceCache>();

        Title = Loc.GetString("construction-tile-editor-title");
        MinSize = new Vector2(460, 620);

        _search = new LineEdit { PlaceHolder = Loc.GetString("construction-tile-editor-search"), HorizontalExpand = true };
        _rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, HScrollEnabled = false, MinSize = new Vector2(0, 220) };
        scroll.AddChild(_rows);

        _selectedLabel = new Label { Text = Loc.GetString("construction-tile-editor-none") };

        _mainCategory = new OptionButton { HorizontalExpand = true };
        _mainCategory.AddItem(Loc.GetString("construction-tile-editor-page-zlevel"), 0);
        _mainCategory.AddItem(Loc.GetString("construction-tile-editor-page-spawnlists"), 1);
        _mainCategory.SelectId(0);

        _spawnlist = new OptionButton { HorizontalExpand = true, Disabled = true };
        _category = new LineEdit { HorizontalExpand = true, Text = "Flooring" };

        // Z-Level page is fixed to the Tiles spawnlist, so the spawnlist picker is only enabled on the Spawnlists page.
        _mainCategory.OnItemSelected += a =>
        {
            _mainCategory.SelectId(a.Id);
            _spawnlist.Disabled = a.Id == 0;
        };

        _material = new OptionButton { HorizontalExpand = true };
        for (var i = 0; i < Materials.Length; i++)
            _material.AddItem(Materials[i].Label, i);
        _material.SelectId(0);
        _material.OnItemSelected += a => _material.SelectId(a.Id);

        _amount = new LineEdit { Text = "1", HorizontalExpand = true };

        var save = new Button { Text = Loc.GetString("construction-tile-editor-save"), HorizontalExpand = true, Margin = new Thickness(0, 0, 2, 0) };
        var cancel = new Button { Text = Loc.GetString("construction-tile-editor-cancel"), HorizontalExpand = true };
        save.OnPressed += _ => Submit();
        cancel.OnPressed += _ => Close();

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        root.AddChild(LabelFor("construction-tile-editor-tile"));
        root.AddChild(_search);
        root.AddChild(scroll);
        root.AddChild(_selectedLabel);
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
            Margin = new Thickness(0, 8, 0, 0),
            Children = { save, cancel },
        });

        var panel = new PanelContainer { PanelOverride = GmodStyle.Panel(GmodStyle.PanelBg), HorizontalExpand = true, VerticalExpand = true };
        panel.AddChild(root);
        Contents.AddChild(panel);

        GmodStyle.Modernize(save, baseBg: GmodStyle.RowSelected, selectedBg: GmodStyle.RowSelected);
        GmodStyle.Modernize(cancel);
        GmodStyle.Modernize(_mainCategory);
        GmodStyle.Modernize(_spawnlist);
        GmodStyle.Modernize(_material);
        GmodStyle.RecolorKeyLabels(panel);

        _search.OnTextChanged += a => Refresh(a.Text);
    }

    public void Populate(OpenCustomTileEditorEvent ev)
    {
        _tiles = ev.AvailableTiles;

        _spawnlist.Clear();
        _spawnlistValues.Clear();
        for (var i = 0; i < ev.AvailableSpawnlists.Count; i++)
        {
            _spawnlist.AddItem(ev.AvailableSpawnlists[i], i);
            _spawnlistValues.Add(ev.AvailableSpawnlists[i]);
        }
        if (_spawnlistValues.Count > 0)
            _spawnlist.SelectId(0);

        Refresh(string.Empty);
    }

    private static Label LabelFor(string loc) => new()
    {
        Text = Loc.GetString(loc),
        StyleClasses = { "LabelKeyText" },
        Margin = new Thickness(0, 6, 0, 2),
    };

    private void Refresh(string filter)
    {
        _rows.RemoveAllChildren();

        var needle = filter.Trim().ToLowerInvariant();
        // Uncapped: the list must be scrollable through every tile (see EntitySelectorWindow).
        foreach (var tile in _tiles)
        {
            if (needle.Length > 0 && !tile.ToLowerInvariant().Contains(needle))
                continue;

            _rows.AddChild(MakeTileRow(tile));
        }
    }

    private Control MakeTileRow(string id)
    {
        // Show the tile's actual sprite next to its id so it can be picked visually.
        var preview = new TextureRect
        {
            MinSize = new Vector2(32, 32),
            SetSize = new Vector2(32, 32),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VAlignment.Center,
        };
        if (_prototype.TryIndex<ContentTileDefinition>(id, out var def) && def.Sprite is { } sprite)
        {
            try { preview.Texture = _resCache.GetResource<TextureResource>(sprite).Texture; }
            catch { /* missing texture - just show the label */ }
        }

        var row = new ContainerButton { HorizontalExpand = true, Margin = new Thickness(0, 0, 0, 2) };
        row.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(4, 2, 4, 2),
            Children = { preview, new Label { Text = id, VerticalAlignment = VAlignment.Center } },
        });
        GmodStyle.Modernize(row);
        row.OnPressed += _ =>
        {
            _selectedTile = id;
            _selectedLabel.Text = Loc.GetString("construction-tile-editor-selected", ("tile", id));
        };
        return row;
    }

    private void Submit()
    {
        if (string.IsNullOrEmpty(_selectedTile))
            return;

        var zLevelPage = _mainCategory.SelectedId == 0;
        var spawnlist = zLevelPage || _spawnlistValues.Count == 0
            ? "Tiles"
            : _spawnlistValues[Math.Clamp(_spawnlist.SelectedId, 0, _spawnlistValues.Count - 1)];

        if (!int.TryParse(_amount.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount < 1)
            amount = 1;

        var mat = _material.SelectedId >= 0 && _material.SelectedId < Materials.Length ? Materials[_material.SelectedId].Stack : "CMSteel";

        OnSubmit?.Invoke(new SubmitCustomTileEditorEvent
        {
            TileId = _selectedTile,
            Material = mat,
            Amount = amount,
            Category = string.IsNullOrWhiteSpace(_category.Text) ? "Flooring" : _category.Text.Trim(),
            Spawnlist = spawnlist,
            ZLevelPage = zLevelPage,
        });

        Close();
    }
}
