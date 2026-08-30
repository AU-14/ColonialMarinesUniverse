using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Robust.Client.Console;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.RichText;

/// <summary>
/// Renders command links in structured chat as buttons so they use the standard UI press handling.
/// </summary>
[UsedImplicitly]
public sealed partial class ChatCommandLinkTag : IMarkupTagHandler
{
    public const string TagName = "chatcmdlink";

    [Dependency] private IClientConsoleHost _console = default!;

    public string Name => TagName;

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        if (!node.Value.TryGetString(out var text) ||
            !node.Attributes.TryGetValue("command", out var commandParameter) ||
            !commandParameter.TryGetString(out var command))
        {
            control = null;
            return false;
        }

        var button = new Button
        {
            Text = text,
            DefaultCursorShape = Control.CursorShape.Hand,
            StyleBoxOverride = new StyleBoxEmpty(),
        };
        button.Label.FontColorOverride = Color.LightBlue;
        button.OnMouseEntered += _ => button.Label.FontColorOverride = Color.Blue;
        button.OnMouseExited += _ => button.Label.FontColorOverride = Color.LightBlue;
        button.OnPressed += _ => _console.ExecuteCommand(command);

        if (node.Attributes.TryGetValue("title", out var title))
            button.ToolTip = title.StringValue;

        control = button;
        return true;
    }
}
