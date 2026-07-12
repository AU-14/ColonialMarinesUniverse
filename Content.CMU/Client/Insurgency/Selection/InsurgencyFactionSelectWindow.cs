using System;
using System.Numerics;
using Content.Shared._AU14.Insurgency;
using Content.Shared._AU14.Insurgency.Selection;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._AU14.Insurgency.Selection;

/// <summary>
///     The CLF-leader faction selection popup. A single scrolling list of the round's Default factions:
///     each row shows just the title, and expands on click to reveal its description, playstyle, and a
///     sprite preview of what its cell kit deploys. Factions that do not oppose the round's GOVFOR are
///     shown but cannot be chosen. The server has the final say on every pick.
/// </summary>
public sealed class InsurgencyFactionSelectWindow : DefaultWindow
{
    private readonly IPrototypeManager _prototype;

    private readonly RichTextLabel _govforLabel;
    private readonly BoxContainer _list;

    public event Action<int>? OnSelectDefault;

    public InsurgencyFactionSelectWindow()
    {
        _prototype = IoCManager.Resolve<IPrototypeManager>();

        Title = Loc.GetString("insfor-select-title");
        MinSize = new Vector2(760, 620);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true };

        _govforLabel = new RichTextLabel { Margin = new Thickness(6, 4) };
        root.AddChild(_govforLabel);

        root.AddChild(new Label { Text = Loc.GetString("insfor-select-default-header"), StyleClasses = { "LabelHeading" }, Margin = new Thickness(6, 2) });

        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        // HScrollEnabled false so the row content wraps to the viewport width instead of running off it.
        root.AddChild(new ScrollContainer
        {
            Children = { _list },
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = false,
        });

        Contents.AddChild(root);
    }

    public void SetState(InsurgencyFactionSelectEuiState state)
    {
        _govforLabel.SetMessage(state.GovforPlatoonName is { } name
            ? Loc.GetString("insfor-select-govfor", ("name", name))
            : Loc.GetString("insfor-select-govfor-unknown"));

        RebuildDefault(state);

        // Matches the improved construction menu; safe to re-run per state push.
        InsforUiStyle.Apply(this);
    }

    private void RebuildDefault(InsurgencyFactionSelectEuiState state)
    {
        _list.RemoveAllChildren();

        if (state.Defaults.Count == 0)
        {
            _list.AddChild(new Label { Text = Loc.GetString("insfor-select-empty") });
            return;
        }

        foreach (var option in state.Defaults)
            _list.AddChild(BuildRow(option));
    }

    // One faction row: a title header that toggles a detail panel (description, playstyle, and a sprite
    // preview of the cell kit). Non-opposing factions expand the same way but cannot be chosen.
    private Control BuildRow(DefaultFactionOption option)
    {
        var panel = new PanelContainer { Margin = new Thickness(0, 0, 0, 6), HorizontalExpand = true };
        var outer = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(6), HorizontalExpand = true };

        // Header: optional flag sprite + a full-width toggle button showing the title.
        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
        if (option.FlagEntity != null && _prototype.HasIndex<EntityPrototype>(option.FlagEntity))
        {
            var flag = new EntityPrototypeView { MinSize = new Vector2(32, 32), VerticalAlignment = VAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            flag.SetPrototype(new EntProtoId(option.FlagEntity));
            header.AddChild(flag);
        }

        var title = string.IsNullOrWhiteSpace(option.Title) ? Loc.GetString("insfor-select-untitled") : option.Title;
        if (!option.Opposes)
            title += "  " + Loc.GetString("insfor-select-unavailable-tag");

        var toggle = new Button
        {
            Text = title,
            HorizontalExpand = true,
            StyleClasses = { "OpenRight" },
            ClipText = true,
        };
        header.AddChild(toggle);
        outer.AddChild(header);

        // Detail, hidden until the header is clicked.
        var detail = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Visible = false,
            Margin = new Thickness(4, 6, 0, 2),
        };

        // Left: wrapped prose + the Choose button.
        var textCol = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, Margin = new Thickness(0, 0, 8, 0) };

        if (!string.IsNullOrWhiteSpace(option.Description))
            textCol.AddChild(Wrapped(option.Description));

        if (!string.IsNullOrWhiteSpace(option.Roleplay))
        {
            textCol.AddChild(new Label { Text = Loc.GetString("insfor-select-playstyle-header"), StyleClasses = { "LabelHeading" }, Margin = new Thickness(0, 6, 0, 2) });
            textCol.AddChild(Wrapped(option.Roleplay));
        }

        if (!option.Opposes)
            textCol.AddChild(new Label { Text = Loc.GetString("insfor-select-not-opposed"), StyleClasses = { "LabelSubText" }, Margin = new Thickness(0, 6, 0, 0) });

        var id = option.Id;
        var choose = new Button
        {
            Text = Loc.GetString("insfor-select-choose"),
            Disabled = !option.Opposes,
            HorizontalAlignment = HAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0),
        };
        choose.OnPressed += _ => OnSelectDefault?.Invoke(id);
        textCol.AddChild(choose);

        detail.AddChild(textCol);

        // Right: cell-kit sprite preview.
        detail.AddChild(BuildCellKit(option.CellKitEntities));

        outer.AddChild(detail);

        toggle.OnPressed += _ => detail.Visible = !detail.Visible;

        panel.AddChild(outer);
        return panel;
    }

    // A fixed-width column previewing the cell kit's contents as a wrapping grid of entity sprites.
    private Control BuildCellKit(System.Collections.Generic.List<string> entities)
    {
        var col = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinSize = new Vector2(280, 0),
            HorizontalExpand = false,
        };
        col.AddChild(new Label { Text = Loc.GetString("insfor-select-cellkit-header"), StyleClasses = { "LabelHeading" } });

        var valid = 0;
        var grid = new GridContainer { Columns = 6 };
        foreach (var proto in entities)
        {
            if (!_prototype.HasIndex<EntityPrototype>(proto))
                continue;
            var view = new EntityPrototypeView { MinSize = new Vector2(40, 40), Margin = new Thickness(1) };
            view.SetPrototype(new EntProtoId(proto));
            grid.AddChild(view);
            valid++;
        }

        if (valid == 0)
            col.AddChild(new Label { Text = Loc.GetString("insfor-select-cellkit-empty"), StyleClasses = { "LabelSubText" } });
        else
            col.AddChild(grid);

        return col;
    }

    // A word-wrapping prose label. RichTextLabel wraps to its parent width, and the list's ScrollContainer
    // has horizontal scroll disabled, so text stays inside the window instead of running off it.
    private static RichTextLabel Wrapped(string text)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(text);
        return label;
    }
}
