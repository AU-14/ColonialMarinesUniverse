using Content.Client.CMU14.Interface;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.UserInterface.Guidebook;

/// <summary>
///     One line in the guidebook's search results: where the match is, and the sentence it sits in.
/// </summary>
public sealed class CmuGuideResult : ContainerButton
{
    public CmuGuideResult(CmuGuideIndex.Hit hit, Action onPressed)
    {
        StyleClasses.Add(StyleNano.StyleClassCmuGuideResult);
        HorizontalExpand = true;
        OnPressed += _ => onPressed();

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(6, 4),
            MouseFilter = MouseFilterMode.Ignore,
        };

        var title = new RichTextLabel
        {
            HorizontalExpand = true,
            StyleClasses = { StyleNano.StyleClassCmuGuideResult },
            MouseFilter = MouseFilterMode.Ignore,
        };
        title.SetMessage(TitleMessage(hit));
        box.AddChild(title);

        if (!hit.IsTitle)
        {
            var snippet = new RichTextLabel
            {
                HorizontalExpand = true,
                StyleClasses = { StyleNano.StyleClassCmuGuideSnippet },
                MouseFilter = MouseFilterMode.Ignore,
            };
            snippet.SetMessage(SnippetMessage(hit));
            box.AddChild(snippet);
        }

        AddChild(box);
    }

    private static FormattedMessage TitleMessage(CmuGuideIndex.Hit hit)
    {
        var msg = new FormattedMessage();

        if (hit.IsTitle)
        {
            msg.AddText(hit.Before);
            msg.PushColor(CrtTerminalPalette.Caution);
            msg.AddText(hit.Match);
            msg.Pop();
            msg.AddText(hit.After);
            return msg;
        }

        msg.AddText(hit.Title);

        if (hit.Heading != null)
        {
            msg.PushColor(CrtTerminalPalette.TextDim);
            msg.AddText(" > " + hit.Heading);
            msg.Pop();
        }

        return msg;
    }

    private static FormattedMessage SnippetMessage(CmuGuideIndex.Hit hit)
    {
        var msg = new FormattedMessage();
        msg.PushColor(CrtTerminalPalette.TextDim);
        msg.AddText(hit.Before);
        msg.Pop();
        msg.PushColor(CrtTerminalPalette.Caution);
        msg.AddText(hit.Match);
        msg.Pop();
        msg.PushColor(CrtTerminalPalette.TextDim);
        msg.AddText(hit.After);
        msg.Pop();
        return msg;
    }
}
