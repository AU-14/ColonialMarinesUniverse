using System.Numerics;
using Content.Client.CMU14.Yautja.ClanHall;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Yautja;

// the private line between a hunter and their thrall
public sealed class YautjaThrallMessageWindow : YautjaHudWindow
{
    private const int MaxLength = 160;

    private readonly Label _limitLabel;
    private readonly TextEdit _messageEdit;
    private readonly HudButton _sendButton;

    public event Action<string>? OnSend;

    public YautjaThrallMessageWindow()
        : base(Loc.GetString("cmu-yautja-thrall-message-title"), new Vector2(420, 240), new Vector2(360, 210))
    {
        AddClose();

        _messageEdit = new TextEdit
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MinHeight = 72,
            Placeholder = new Rope.Leaf(Loc.GetString("cmu-yautja-thrall-message-placeholder")),
        };
        _messageEdit.OnTextChanged += args => UpdateLimit(Rope.Collapse(args.TextRope));
        Body.AddChild(Field(_messageEdit));

        var footer = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _limitLabel = Reading(Hud.Muted);
        _limitLabel.HorizontalExpand = true;
        footer.AddChild(_limitLabel);
        _sendButton = new HudButton(Loc.GetString("cmu-yautja-thrall-message-send").ToUpperInvariant(), LabelFont)
        {
            MinWidth = 140,
        };
        _sendButton.Activated += Send;
        footer.AddChild(_sendButton);
        Body.AddChild(footer);

        UpdateLimit(string.Empty);
    }

    private void UpdateLimit(string text)
    {
        var length = text.Length;
        _limitLabel.Text = Loc.GetString("cmu-yautja-translator-limit", ("count", length), ("max", MaxLength));
        _limitLabel.FontColorOverride = length > MaxLength ? Hud.Hot : Hud.Muted;
        _sendButton.Disabled = string.IsNullOrWhiteSpace(text);
    }

    private void Send()
    {
        var text = Collapse(_messageEdit).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text.Length > MaxLength)
            text = text[..MaxLength];

        OnSend?.Invoke(text);
        _messageEdit.TextRope = Rope.Leaf.Empty;
        UpdateLimit(string.Empty);
    }
}
