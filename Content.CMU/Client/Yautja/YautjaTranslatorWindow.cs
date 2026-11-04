using System.Numerics;
using Content.Client.CMU14.Yautja.ClanHall;
using Content.Shared.CMU14.Yautja;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Yautja;

// what you type comes out of the bracer's speaker in a human voice. the power bar is what each broadcast costs
public sealed class YautjaTranslatorWindow : YautjaHudWindow
{
    private readonly HudBar _power;
    private readonly Label _powerLabel;
    private readonly Label _limitLabel;
    private readonly TextEdit _messageEdit;
    private readonly HudButton _sendButton;

    private int _maxLength = 160;
    private bool _affordable = true;

    public event Action<string>? OnSend;
    public event Action? OnTyping;

    public YautjaTranslatorWindow()
        : base(Loc.GetString("cmu-yautja-translator-window-title"), new Vector2(440, 250), new Vector2(380, 220))
    {
        _power = new HudBar(0) { MinSize = new Vector2(90, 8) };
        Header.AddChild(_power);
        _powerLabel = Reading(Hud.Text);
        Header.AddChild(_powerLabel);
        AddClose();

        _messageEdit = new TextEdit
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MinHeight = 72,
            Placeholder = new Rope.Leaf(Loc.GetString("cmu-yautja-translator-placeholder")),
        };
        _messageEdit.OnTextChanged += args =>
        {
            UpdateLimit(Rope.Collapse(args.TextRope));
            OnTyping?.Invoke();
        };
        Body.AddChild(Field(_messageEdit));

        var footer = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _limitLabel = Reading(Hud.Muted);
        _limitLabel.HorizontalExpand = true;
        footer.AddChild(_limitLabel);
        _sendButton = new HudButton(Loc.GetString("cmu-yautja-translator-send").ToUpperInvariant(), LabelFont)
        {
            MinWidth = 140,
        };
        _sendButton.Activated += Send;
        footer.AddChild(_sendButton);
        Body.AddChild(footer);

        UpdateLimit(string.Empty);
    }

    public void UpdateState(YautjaTranslatorBuiState state)
    {
        _maxLength = state.MaxLength;
        _affordable = state.Charge >= state.Cost;
        _power.Fill = state.MaxCharge <= 0 ? 0 : state.Charge / (float) state.MaxCharge;
        _powerLabel.Text = Loc.GetString("cmu-yautja-translator-power", ("charge", state.Charge), ("max", state.MaxCharge), ("cost", state.Cost));
        _powerLabel.FontColorOverride = _affordable ? Hud.Text : Hud.Hot;
        UpdateLimit(Collapse(_messageEdit));
    }

    private void UpdateLimit(string text)
    {
        var length = text.Length;
        _limitLabel.Text = Loc.GetString("cmu-yautja-translator-limit", ("count", length), ("max", _maxLength));
        _limitLabel.FontColorOverride = length > _maxLength ? Hud.Hot : Hud.Muted;
        _sendButton.Disabled = string.IsNullOrWhiteSpace(text) || !_affordable;
    }

    private void Send()
    {
        var text = Collapse(_messageEdit).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text.Length > _maxLength)
            text = text[.._maxLength];

        OnSend?.Invoke(text);
        _messageEdit.TextRope = Rope.Leaf.Empty;
        UpdateLimit(string.Empty);
    }
}
