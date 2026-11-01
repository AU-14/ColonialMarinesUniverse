using System.Numerics;
using Content.Shared.CMU14.CharacterDescription;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.CharacterDescription.UI;

/// <summary>
/// In-round editor for the character's short examine text and full description.
/// </summary>
public sealed class EditCharacterDescriptionWindow : DefaultWindow
{
    private readonly LineEdit _shortExamine;
    private readonly TextEdit _fullDescription;
    private readonly Label _shortLimit;
    private readonly Label _fullLimit;

    private int _maxShort;
    private int _maxFull;

    public event Action<string, string>? OnSave;

    public EditCharacterDescriptionWindow()
    {
        Title = Loc.GetString("cmu-edit-description-title");
        SetSize = new Vector2(480, 420);
        MinSize = new Vector2(360, 300);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        Contents.AddChild(root);

        root.AddChild(new Label
        {
            Text = Loc.GetString("cmu-edit-description-hint"),
            StyleClasses = { "LabelSubText" },
        });

        _shortLimit = new Label { StyleClasses = { "LabelSubText" } };
        root.AddChild(Header(Loc.GetString("cmu-edit-description-short"), _shortLimit));
        _shortExamine = new LineEdit { HorizontalExpand = true };
        _shortExamine.OnTextChanged += _ => UpdateLimits();
        root.AddChild(_shortExamine);

        _fullLimit = new Label { StyleClasses = { "LabelSubText" } };
        root.AddChild(Header(Loc.GetString("cmu-edit-description-full"), _fullLimit));
        _fullDescription = new TextEdit
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MinHeight = 120,
        };
        _fullDescription.OnTextChanged += _ => UpdateLimits();
        root.AddChild(new PanelContainer
        {
            StyleClasses = { "BackgroundDark" },
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = { _fullDescription },
        });

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        var save = new Button { Text = Loc.GetString("cmu-edit-description-save"), HorizontalExpand = true };
        save.OnPressed += _ => OnSave?.Invoke(_shortExamine.Text, Rope.Collapse(_fullDescription.TextRope));
        var cancel = new Button { Text = Loc.GetString("cmu-edit-description-cancel"), HorizontalExpand = true };
        cancel.OnPressed += _ => Close();
        buttons.AddChild(save);
        buttons.AddChild(cancel);
        root.AddChild(buttons);
    }

    public void UpdateState(EditCharacterDescriptionEuiState state)
    {
        _maxShort = state.MaxShortExamine;
        _maxFull = state.MaxFullDescription;
        _shortExamine.Text = state.ShortExamine;
        _fullDescription.TextRope = new Rope.Leaf(state.FullDescription);
        UpdateLimits();
    }

    private static BoxContainer Header(string title, Label limit)
    {
        var label = new Label { Text = title, HorizontalExpand = true };
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Children = { label, limit },
        };
    }

    private void UpdateLimits()
    {
        SetLimit(_shortLimit, _shortExamine.Text.Length, _maxShort);
        SetLimit(_fullLimit, Rope.Collapse(_fullDescription.TextRope).Length, _maxFull);
    }

    private static void SetLimit(Label label, int count, int max)
    {
        label.Text = Loc.GetString("cmu-edit-description-limit", ("count", count), ("max", max));
        label.FontColorOverride = count > max ? Color.Red : null;
    }
}
