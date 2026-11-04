using Content.Shared.Chat;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

/// <summary>Scrollback data that does not require an offscreen UI control.</summary>
public sealed class ChatLogEntry
{
    private readonly ChatMessage _message;
    private readonly Func<FormattedMessage> _format;
    private readonly Color _color;
    private readonly Color? _accent;
    private readonly int? _fontSize;
    private FormattedMessage? _formatted;
    private int _repeatCount = 1;

    internal ChatMessageRow? Row;
    internal float Height = 32;
    internal float Offset;
    internal int Visit;

    internal ChatLogEntry(ChatMessage message, Func<FormattedMessage> format, Color color, Color? accent, int? fontSize)
    {
        _message = message;
        _format = format;
        _color = color;
        _accent = accent;
        _fontSize = fontSize;
    }

    internal ChatMessageRow CreateRow()
    {
        var row = new ChatMessageRow(_message, _formatted ??= _format(), _color, _accent, _fontSize);
        row.SetRepeatCount(_repeatCount);
        return row;
    }

    public void SetRepeatCount(int count)
    {
        _repeatCount = count;
        Row?.SetRepeatCount(count);
    }
}
