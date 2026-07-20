// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client._AU14.SavedBuilds;

/// <summary>
/// Small control panel for the saved-build selection: a range slider (1x1 .. 11x11), append/clear of
/// range boxes, a live count of highlighted (whitelisted) entities, and a name + save. Drives
/// <see cref="BuildSaveModeSystem"/>; the in-world overlay shows the actual selection.
/// </summary>
public sealed class BuildSaveWindow : DefaultWindow
{
    private readonly BuildSaveModeSystem _mode;

    private readonly Label _sizeLabel;
    private readonly Label _countLabel;
    private readonly LineEdit _nameEdit;

    public BuildSaveWindow(BuildSaveModeSystem mode)
    {
        _mode = mode;

        Title = Loc.GetString("saved-build-window-title");
        MinSize = new Vector2(280, 0);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };

        // Range row.
        root.AddChild(new Label { Text = Loc.GetString("saved-build-window-range"), StyleClasses = { "LabelKeyText" } });

        var slider = new Slider
        {
            MinValue = 0,
            MaxValue = BuildSaveModeSystem.MaxRadius,
            Value = mode.Radius,
            Rounded = true,
            HorizontalExpand = true,
        };
        root.AddChild(slider);

        _sizeLabel = new Label();
        root.AddChild(_sizeLabel);

        // Append / clear.
        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        var append = new Button { Text = Loc.GetString("saved-build-window-append"), HorizontalExpand = true, Margin = new Thickness(0, 0, 2, 0) };
        var clear = new Button { Text = Loc.GetString("saved-build-window-clear"), HorizontalExpand = true };
        buttons.AddChild(append);
        buttons.AddChild(clear);
        root.AddChild(buttons);

        _countLabel = new Label { Margin = new Thickness(0, 6, 0, 6) };
        root.AddChild(_countLabel);

        var multiZHelp = new RichTextLabel
        {
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 8),
        };
        multiZHelp.SetMessage(Loc.GetString("saved-build-window-multiz-help"), Color.FromHex("#E5C07B"));
        root.AddChild(multiZHelp);

        // Mapper-mode option: also grab unanchored loose items (default is anchored structures only). Only shown
        // when the build-mode dropdown is on Mapper; for other modes it's irrelevant.
        if (mode.Mode == Content.Shared._AU14.SavedBuilds.BuildSaveMode.Mapper)
        {
            var includeLoose = new CheckBox
            {
                Text = Loc.GetString("saved-build-window-include-loose"),
                Pressed = mode.IncludeLoose,
            };
            includeLoose.OnToggled += args =>
            {
                _mode.IncludeLoose = args.Pressed;
                _mode.RefreshSelection();
            };
            root.AddChild(includeLoose);
        }

        var includeTiles = new CheckBox
        {
            Text = Loc.GetString("saved-build-window-include-tiles"),
            Pressed = mode.IncludeTiles,
            Modulate = Color.FromHex("#64D66A"),
        };
        includeTiles.OnToggled += args =>
        {
            _mode.IncludeTiles = args.Pressed;
            _mode.RefreshSelection();
        };
        root.AddChild(includeTiles);

        // Multi-z capture is opt-in: appending a range used to reach onto the levels above and below
        // unconditionally, which swallowed whatever happened to sit over or under the selection.
        var includeMultiZ = new CheckBox
        {
            Text = Loc.GetString("saved-build-window-include-multiz"),
            Pressed = mode.IncludeMultiZ,
            Modulate = Color.FromHex("#E5C07B"),
        };
        includeMultiZ.OnToggled += args =>
        {
            _mode.IncludeMultiZ = args.Pressed;
            _mode.RefreshSelection();
        };
        root.AddChild(includeMultiZ);

        // Name + save.
        _nameEdit = new LineEdit { PlaceHolder = Loc.GetString("saved-build-window-name"), HorizontalExpand = true };
        root.AddChild(_nameEdit);

        var save = new Button { Text = Loc.GetString("saved-build-window-save"), Margin = new Thickness(0, 6, 0, 0) };
        root.AddChild(save);

        // Open the saved-builds folder in the OS file explorer (host machine; admin only server-side).
        var openFolder = new Button { Text = Loc.GetString("saved-build-window-open-folder"), Margin = new Thickness(0, 6, 0, 0) };
        root.AddChild(openFolder);
        openFolder.OnPressed += _ =>
            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SavedBuildListSystem>().OpenFolder();

        Contents.AddChild(root);

        slider.OnValueChanged += s =>
        {
            _mode.SetRadius((int) Math.Round(s.Value));
            UpdateSizeLabel();
        };
        append.OnPressed += _ => _mode.AppendCurrentBox();
        clear.OnPressed += _ => _mode.ClearSelection();
        save.OnPressed += _ => _mode.Save(_nameEdit.Text);

        _mode.SelectionChanged += UpdateCount;
        OnClose += () => _mode.SelectionChanged -= UpdateCount;

        UpdateSizeLabel();
        UpdateCount();
    }

    private void UpdateSizeLabel()
    {
        var size = (_mode.Radius * 2) + 1;
        _sizeLabel.Text = Loc.GetString("saved-build-window-size", ("size", size));
    }

    private void UpdateCount()
    {
        _countLabel.Text = Loc.GetString("saved-build-window-selected",
            ("count", _mode.HighlightCount),
            ("tiles", _mode.HighlightedTiles.Count));
    }
}
