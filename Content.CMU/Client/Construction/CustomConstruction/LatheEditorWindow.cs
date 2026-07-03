// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Globalization;
using System.Numerics;
using Content.Client._AU14.UI;
using Content.Shared._AU14.Construction.CustomConstruction;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// The "Lathe Editor" (Admin Tools): add a print recipe to the CM autolathe or armylathe. Pick which lathe,
/// pick the item to print (via the shared entity selector), set its steel/glass/plastic cost and print time,
/// and save. Fires <see cref="OnSubmit"/>; the server writes the recipe and rebuilds the lathe's pack.
/// </summary>
public sealed class LatheEditorWindow : DefaultWindow
{
    public event Action<SubmitCustomLatheEditorEvent>? OnSubmit;
    public event Action<string>? OnRemove;

    private readonly BoxContainer _existing;
    private readonly OptionButton _lathe;
    private readonly Label _selectedLabel;
    private readonly LineEdit _steel;
    private readonly LineEdit _glass;
    private readonly LineEdit _plastic;
    private readonly LineEdit _time;

    private string _resultId = string.Empty;
    private EntitySelectorWindow? _selector;

    public LatheEditorWindow()
    {
        Title = Loc.GetString("construction-lathe-editor-title");
        MinSize = new Vector2(380, 360);

        _lathe = new OptionButton { HorizontalExpand = true };
        _lathe.AddItem(Loc.GetString("construction-lathe-editor-autolathe"), 0);
        _lathe.AddItem(Loc.GetString("construction-lathe-editor-armylathe"), 1);
        _lathe.SelectId(0);
        _lathe.OnItemSelected += a => _lathe.SelectId(a.Id);

        _selectedLabel = new Label { Text = Loc.GetString("construction-lathe-editor-none") };
        var pick = new Button { Text = Loc.GetString("construction-lathe-editor-pick-item"), HorizontalExpand = true };
        pick.OnPressed += _ => OpenSelector();

        _steel = new LineEdit { Text = "0", HorizontalExpand = true };
        _glass = new LineEdit { Text = "0", HorizontalExpand = true };
        _plastic = new LineEdit { Text = "0", HorizontalExpand = true };
        _time = new LineEdit { Text = "4", HorizontalExpand = true };

        var save = new Button { Text = Loc.GetString("construction-lathe-editor-save"), HorizontalExpand = true, Margin = new Thickness(0, 0, 2, 0) };
        var cancel = new Button { Text = Loc.GetString("construction-lathe-editor-cancel"), HorizontalExpand = true };
        save.OnPressed += _ => Submit();
        cancel.OnPressed += _ => Close();

        _existing = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var existingScroll = new ScrollContainer { HorizontalExpand = true, HScrollEnabled = false, MinSize = new Vector2(0, 110) };
        existingScroll.AddChild(_existing);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        root.AddChild(LabelFor("construction-lathe-editor-existing"));
        root.AddChild(existingScroll);
        root.AddChild(LabelFor("construction-lathe-editor-lathe"));
        root.AddChild(_lathe);
        root.AddChild(pick);
        root.AddChild(_selectedLabel);
        root.AddChild(LabelFor("construction-lathe-editor-steel"));
        root.AddChild(_steel);
        root.AddChild(LabelFor("construction-lathe-editor-glass"));
        root.AddChild(_glass);
        root.AddChild(LabelFor("construction-lathe-editor-plastic"));
        root.AddChild(_plastic);
        root.AddChild(LabelFor("construction-lathe-editor-time"));
        root.AddChild(_time);
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
        GmodStyle.Modernize(pick);
        GmodStyle.Modernize(_lathe);
        GmodStyle.RecolorKeyLabels(panel);
    }

    /// <summary>Fills the existing-recipes list (each row removes that recipe when clicked).</summary>
    public void Populate(OpenCustomLatheEditorEvent ev)
    {
        _existing.RemoveAllChildren();
        foreach (var recipe in ev.ExistingRecipes)
        {
            var id = recipe.RecipeId;
            var row = new Button
            {
                Text = $"{recipe.Lathe}: {recipe.Result}  [{Loc.GetString("construction-lathe-editor-remove")}]",
                HorizontalExpand = true,
                Margin = new Thickness(0, 0, 0, 2),
            };
            GmodStyle.Modernize(row);
            row.OnPressed += _ =>
            {
                OnRemove?.Invoke(id);
                row.Disabled = true;
                row.Text = $"{recipe.Lathe}: {recipe.Result}  [x]";
            };
            _existing.AddChild(row);
        }
    }

    private static Label LabelFor(string loc) => new()
    {
        Text = Loc.GetString(loc),
        StyleClasses = { "LabelKeyText" },
        Margin = new Thickness(0, 6, 0, 2),
    };

    private void OpenSelector()
    {
        _selector?.Close();
        _selector = new EntitySelectorWindow();
        _selector.OnEntitySelected += id =>
        {
            if (string.IsNullOrEmpty(id))
                return;
            _resultId = id;
            _selectedLabel.Text = Loc.GetString("construction-lathe-editor-selected", ("item", id));
        };
        _selector.OnClose += () => _selector = null;
        _selector.OpenCentered();
    }

    private static int ParseCost(string text) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : 0;

    private void Submit()
    {
        if (string.IsNullOrEmpty(_resultId))
            return;

        if (!float.TryParse(_time.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var time) || time <= 0)
            time = 4f;

        OnSubmit?.Invoke(new SubmitCustomLatheEditorEvent
        {
            Lathe = _lathe.SelectedId == 1 ? CustomLatheTarget.Armylathe : CustomLatheTarget.Autolathe,
            ResultId = _resultId,
            SteelCost = ParseCost(_steel.Text),
            GlassCost = ParseCost(_glass.Text),
            PlasticCost = ParseCost(_plastic.Text),
            CompleteTime = time,
        });

        Close();
    }
}
