using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Lobby;

/// <summary>Reserves chat space before measuring the responsive character editor.</summary>
public sealed class CmuCharacterSetupLayout(Control editor, Control chat) : Container
{
    private const float Gap = 12;

    private static float ChatWidth(float width) => Math.Clamp(width * 0.23f, 300, 420);

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var chatWidth = ChatWidth(availableSize.X);
        chat.Measure(new Vector2(chatWidth, availableSize.Y));
        editor.Measure(new Vector2(Math.Max(0, availableSize.X - chatWidth - Gap), availableSize.Y));
        return new Vector2(editor.DesiredSize.X + chatWidth + Gap,
            Math.Max(editor.DesiredSize.Y, chat.DesiredSize.Y));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var editorWidth = Math.Max(0, finalSize.X - ChatWidth(finalSize.X) - Gap);
        editor.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(editorWidth, finalSize.Y)));
        chat.Arrange(new UIBox2(editorWidth + Gap, 0, finalSize.X, finalSize.Y));
        return finalSize;
    }
}
