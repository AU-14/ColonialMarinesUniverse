using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._CMU14.CharacterDescription.UI;

public static class CharacterDescriptionRowBuilder
{
    public static void AddHeading(BoxContainer container, string text)
    {
        var label = new Label { Text = text };
        label.AddStyleClass(StyleBase.StyleClassLabelHeading);
        container.AddChild(label);
    }

    public static void AddSeparator(BoxContainer container)
    {
        container.AddChild(new Control { MinHeight = 6 });
    }

    public static void AddRow(BoxContainer container, string label, string? value, string? tooltip = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 3),
            ToolTip = tooltip,
        };

        row.AddChild(new Label { Text = label, ToolTip = tooltip });
        row.AddChild(new Control { HorizontalExpand = true });
        row.AddChild(new Label { Text = value, HorizontalAlignment = Control.HAlignment.Right, ClipText = false });

        container.AddChild(row);
    }

    public static void AddParagraph(BoxContainer container, string label, string? text, string? tooltip = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var heading = new Label { Text = label, ToolTip = tooltip };
        heading.AddStyleClass(StyleBase.StyleClassLabelSubText);
        container.AddChild(heading);

        var body = new RichTextLabel { Text = text, Margin = new Thickness(0, 0, 0, 8), ToolTip = tooltip };
        container.AddChild(body);
    }
}
